using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SanteKerbal
{
    /// <summary>
    /// Conserve la santé de tous les Kerbals dans la sauvegarde (nœud SCENARIO du fichier .sfs),
    /// l'heure du dernier calcul de chaque vaisseau, et fait évoluer en arrière-plan les vaisseaux
    /// non chargés ainsi que les Kerbals au repos à la KSC.
    /// </summary>
    [KSPScenario(ScenarioCreationOptions.AddToAllGames,
        GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.EDITOR)]
    public class HealthScenario : ScenarioModule
    {
        // Intervalle minimal entre deux calculs d'un vaisseau non chargé, en secondes de jeu
        private const double BackgroundTickSeconds = 60.0;

        public static HealthScenario Instance { get; private set; }

        private readonly Dictionary<string, KerbalHealth> kerbals = new Dictionary<string, KerbalHealth>();

        // Heure (UT) du dernier calcul de chaque vaisseau ; Guid.Empty = Kerbals au repos à la KSC
        private readonly Dictionary<Guid, double> lastUpdate = new Dictionary<Guid, double>();

        private float nextDebugLog;

        public override void OnAwake()
        {
            base.OnAwake();
            Instance = this;
            // Déclaration officielle des morts à la récupération (après la remise « disponible » par KSP)
            GameEvents.onVesselRecoveryProcessing.Add(OnRecoveryProcessing);
            GameEvents.onVesselRecoveryProcessingComplete.Add(OnRecoveryProcessing);
            GameEvents.OnGameSettingsApplied.Add(Settings.ApplyGame);
            Settings.ApplyGame();
        }

        public void OnDestroy()
        {
            GameEvents.onVesselRecoveryProcessing.Remove(OnRecoveryProcessing);
            GameEvents.onVesselRecoveryProcessingComplete.Remove(OnRecoveryProcessing);
            GameEvents.OnGameSettingsApplied.Remove(Settings.ApplyGame);
            if (Instance == this)
                Instance = null;
        }

        private void OnRecoveryProcessing(ProtoVessel pv, KSP.UI.Screens.MissionRecoveryDialog dialog, float factor)
        {
            Mortality.DeclareDeadOnRecovery(pv);
        }

        #region Données

        public bool IsDead(string name)
        {
            return kerbals.TryGetValue(name, out KerbalHealth k) && k.Dead;
        }

        /// <summary>Renvoie l'état d'un Kerbal, en le créant en pleine santé s'il est inconnu.</summary>
        public KerbalHealth Get(ProtoCrewMember pcm)
        {
            if (!kerbals.TryGetValue(pcm.name, out KerbalHealth k))
            {
                k = new KerbalHealth(pcm.name);
                kerbals.Add(pcm.name, k);
                Log.Info("Nouveau Kerbal suivi : " + pcm.name);
            }
            return k;
        }

        /// <summary>Heure du dernier calcul ; un vaisseau inconnu commence maintenant.</summary>
        public double GetLastUpdate(Guid id, double now)
        {
            if (!lastUpdate.TryGetValue(id, out double ut))
            {
                lastUpdate[id] = now;
                return now;
            }
            return ut;
        }

        public void SetLastUpdate(Guid id, double ut) => lastUpdate[id] = ut;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            kerbals.Clear();
            foreach (ConfigNode kn in node.GetNodes("KERBAL"))
            {
                KerbalHealth k = KerbalHealth.Load(kn);
                if (k != null)
                    kerbals[k.Name] = k;
            }

            lastUpdate.Clear();
            foreach (ConfigNode vn in node.GetNodes("VESSEL_TIME"))
            {
                string id = vn.GetValue("id");
                double ut = 0;
                if (id != null && vn.TryGetValue("ut", ref ut))
                {
                    try { lastUpdate[new Guid(id)] = ut; }
                    catch (FormatException) { }
                }
            }
            Log.Info($"Sauvegarde lue : {kerbals.Count} Kerbal(s) suivi(s), {lastUpdate.Count} horloge(s) de vaisseau");
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            foreach (KerbalHealth k in kerbals.Values)
                k.Save(node.AddNode("KERBAL"));

            // On oublie les vaisseaux disparus (récupérés, détruits), quand la liste des vaisseaux est disponible
            HashSet<Guid> existing = null;
            if (FlightGlobals.Vessels != null && FlightGlobals.Vessels.Count > 0)
                existing = new HashSet<Guid>(FlightGlobals.Vessels.Where(v => v != null).Select(v => v.id));
            foreach (KeyValuePair<Guid, double> e in lastUpdate)
            {
                if (existing != null && e.Key != Guid.Empty && !existing.Contains(e.Key))
                    continue;
                ConfigNode vn = node.AddNode("VESSEL_TIME");
                vn.AddValue("id", e.Key.ToString());
                vn.AddValue("ut", e.Value.ToString("R"));
            }
        }

        #endregion

        #region Arrière-plan

        public void Update()
        {
            GameScenes scene = HighLogic.LoadedScene;
            if (scene != GameScenes.FLIGHT && scene != GameScenes.SPACECENTER && scene != GameScenes.TRACKSTATION)
                return;
            if (FlightGlobals.Vessels == null)
                return;

            double now = Planetarium.GetUniversalTime();
            if (!Settings.Enabled)
            {
                foreach (Guid id in lastUpdate.Keys.ToList())
                    lastUpdate[id] = now;
                return;
            }
            bool log = Settings.DebugLogInterval > 0 && Time.realtimeSinceStartup >= nextDebugLog;
            if (log)
                nextDebugLog = Time.realtimeSinceStartup + Settings.DebugLogInterval;

            foreach (Vessel v in FlightGlobals.Vessels)
            {
                // Les vaisseaux chargés sont gérés par HealthVesselModule ; l'EVA est hors périmètre
                if (v == null || v.loaded || v.protoVessel == null || IgnoredType(v.vesselType))
                    continue;
                if (!TryTick(v.id, now, BackgroundTickSeconds, out double dt))
                    continue;
                BackgroundTick(v, dt, log);
            }

            if (TryTick(Guid.Empty, now, BackgroundTickSeconds, out double restDt) && Settings.KscRest)
                RestAtKSC(restDt);
        }

        // L'EVA est hors périmètre ; débris, astéroïdes et drapeaux n'ont ni équipage ni équipement
        private static bool IgnoredType(VesselType t)
        {
            return t == VesselType.EVA || t == VesselType.Debris || t == VesselType.SpaceObject || t == VesselType.Flag;
        }

        /// <summary>Renvoie true (et avance l'horloge) si assez de temps de jeu s'est écoulé.</summary>
        private bool TryTick(Guid id, double now, double minDt, out double dt)
        {
            dt = now - GetLastUpdate(id, now);
            if (dt < 0)
            {
                lastUpdate[id] = now;   // retour en arrière (chargement d'une sauvegarde)
                return false;
            }
            if (dt < minDt)
                return false;
            lastUpdate[id] = now;
            return true;
        }

        private void BackgroundTick(Vessel v, double dt, bool log)
        {
            ProtoVessel pv = v.protoVessel;
            List<ProtoCrewMember> crew = pv.GetVesselCrew();
            List<KerbalHealth> alive = crew.Select(Get).Where(k => !k.Dead).ToList();
            Equipment eq = Equipment.FromProto(pv);
            if (alive.Count == 0 && !eq.Any)
                return;

            var store = new ProtoStore(pv);
            NeedsResult r = LifeSupportSim.Consume(store, alive.Count, eq, dt);
            foreach (KerbalHealth k in alive)
            {
                LifeSupportSim.ApplyHealth(k, r, dt,
                    (kh, w, f) => HealthEvents.NeedsChanged(v, kh, w, f),
                    (kh, before, cause) => HealthEvents.HealthChanged(v, kh, before, cause));
            }

            if (log)
            {
                Log.Info($"[{v.vesselName}] (non chargé) dt={dt:F0}s eau={store.Amount(LifeResources.Water):F2} ({r.WaterFrac:P0}) " +
                         $"nourriture={store.Amount(LifeResources.Food):F2} ({r.FoodFrac:P0}) élec={r.EcFrac:P0} | " +
                         string.Join(", ", alive.Select(k => k.ToString())));
            }
        }

        /// <summary>Les Kerbals disponibles à la KSC récupèrent au rythme de régénération normal.</summary>
        private void RestAtKSC(double dt)
        {
            KerbalRoster roster = HighLogic.CurrentGame?.CrewRoster;
            if (roster == null)
                return;
            double heal = Settings.RegenPerDay * dt / KSPUtil.dateTimeFormatter.Day;
            foreach (ProtoCrewMember pcm in roster.Kerbals(ProtoCrewMember.RosterStatus.Available))
            {
                if (kerbals.TryGetValue(pcm.name, out KerbalHealth k) && !k.Dead && k.Health < KerbalHealth.Max)
                {
                    k.Heal(heal);
                    k.LackWater = k.LackFood = false;
                }
            }
        }

        #endregion
    }
}
