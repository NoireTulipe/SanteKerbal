using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SanteKerbal
{
    /// <summary>
    /// Santé de l'équipage d'un vaisseau chargé (à portée physique) :
    /// eau et nourriture (avec recycleurs et serres), accélérations et chaleur.
    /// Les vaisseaux non chargés sont gérés par HealthScenario (BackgroundSim).
    /// </summary>
    public class HealthVesselModule : VesselModule
    {
        // Intervalle minimal entre deux calculs de besoins, en secondes de jeu
        private const double TickSeconds = 1.0;

        // Après un chargement ou une sortie de l'accélération du temps, la physique
        // se stabilise et produit des pics de G parasites : on les ignore pendant ce délai.
        private const float PhysicsSettleSeconds = 3f;

        private float nextDebugLog;

        private float ignoreGUntil;
        private double highGTime;    // temps continu passé au-dessus de HighG
        private double lethalGTime;  // temps continu passé au-dessus de LethalG

        public override Activation GetActivation()
        {
            return Activation.FlightScene | Activation.LoadedVessels;
        }

        public override void OnLoadVessel() => SettlePhysics();
        public override void OnGoOffRails() => SettlePhysics();

        private void SettlePhysics()
        {
            ignoreGUntil = Time.time + PhysicsSettleSeconds;
            highGTime = 0;
            lethalGTime = 0;
        }

        public void FixedUpdate()
        {
            HealthScenario scenario = HealthScenario.Instance;
            if (vessel == null || !vessel.loaded || vessel.isEVA || scenario == null)
                return;

            // G et chaleur : à chaque pas de physique, seulement quand la physique tourne vraiment
            if (!vessel.packed)
                PhysicalEffects(TimeWarp.fixedDeltaTime);

            // Horloge partagée avec la simulation en arrière-plan : pas de double comptage
            // quand le vaisseau passe de non chargé à chargé
            double now = Planetarium.GetUniversalTime();
            double dt = now - scenario.GetLastUpdate(vessel.id, now);
            if (dt < 0)
            {
                scenario.SetLastUpdate(vessel.id, now);   // retour en arrière (chargement d'une sauvegarde)
                return;
            }
            if (dt < TickSeconds)
                return;
            scenario.SetLastUpdate(vessel.id, now);

            NeedsTick(dt);
        }

        #region Accélérations et chaleur

        private void PhysicalEffects(double dt)
        {
            if (Time.time >= ignoreGUntil)
                GForces(dt);
            else
                highGTime = lethalGTime = 0;
            Heat(dt);
        }

        private void GForces(double dt)
        {
            double g = vessel.geeForce;

            highGTime = g > Settings.HighG ? highGTime + dt : 0;
            lethalGTime = g > Settings.LethalG ? lethalGTime + dt : 0;

            bool lethal = lethalGTime >= Settings.LethalGSeconds;
            bool hurting = highGTime > Settings.HighGGraceSeconds;
            if (!lethal && !hurting)
                return;

            foreach (KerbalHealth k in AliveCrew())
            {
                double before = k.Health;
                if (lethal)
                    k.Damage(k.Health);
                else
                    k.Damage(Settings.HighGDamagePerSecond * dt);
                HealthEvents.HealthChanged(vessel, k, before, lethal ? $"écrasé par {g:F1} G" : $"accélération de {g:F1} G");
            }
        }

        private void Heat(double dt)
        {
            foreach (Part part in vessel.parts)
            {
                if (part.protoModuleCrew.Count == 0)
                    continue;
                double excess = part.temperature - Settings.HeatThreshold;
                if (excess <= 0)
                    continue;

                double damage = excess * Settings.HeatDamagePerSecondPerKelvin * dt;
                foreach (ProtoCrewMember pcm in part.protoModuleCrew)
                {
                    KerbalHealth k = HealthScenario.Instance.Get(pcm);
                    if (k.Dead)
                        continue;
                    double before = k.Health;
                    k.Damage(damage);
                    HealthEvents.HealthChanged(vessel, k, before, $"chaleur ({part.temperature:F0} K)");
                }
            }
        }

        #endregion

        #region Eau et nourriture

        private void NeedsTick(double dt)
        {
            List<KerbalHealth> alive = AliveCrew();
            Equipment eq = Equipment.FromParts(vessel.parts);
            if (alive.Count == 0 && !eq.Any)
                return;

            NeedsResult r = LifeSupportSim.Consume(new LoadedStore(vessel), alive.Count, eq, dt);
            UpdateEquipmentStatus(eq, r, alive.Count, dt);

            foreach (KerbalHealth k in alive)
            {
                LifeSupportSim.ApplyHealth(k, r, dt,
                    (kh, w, f) => HealthEvents.NeedsChanged(vessel, kh, w, f),
                    (kh, before, cause) => HealthEvents.HealthChanged(vessel, kh, before, cause));
            }

            if (Settings.DebugLogInterval > 0 && Time.realtimeSinceStartup >= nextDebugLog)
            {
                nextDebugLog = Time.realtimeSinceStartup + Settings.DebugLogInterval;
                Log.Info($"[{vessel.vesselName}] dt={dt:F1}s eau={Amount(LifeResources.Water):F2} ({r.WaterFrac:P0}, recyclé {r.Recycled:F3}) " +
                         $"nourriture={Amount(LifeResources.Food):F2} ({r.FoodFrac:P0}, récolté {r.Produced:F3}) " +
                         $"élec={r.EcFrac:P0} G={vessel.geeForce:F2} temp. max habitacle={MaxCrewedTemperature():F0} K | " +
                         string.Join(", ", alive.Select(k => k.ToString())));
            }
        }

        /// <summary>Texte d'état affiché dans le menu clic droit des recycleurs et des serres.</summary>
        private static void UpdateEquipmentStatus(Equipment eq, NeedsResult r, int crew, double dt)
        {
            double perDay = KSPUtil.dateTimeFormatter.Day / dt;
            foreach (ModuleSKRecycler m in eq.Recyclers)
            {
                if (r.EcFrac < 0.99) m.status = "Manque d'électricité";
                else if (crew == 0) m.status = "En veille (pas d'équipage)";
                else m.status = $"Actif ({r.Recycled * perDay:F1} eau/j)";
            }
            foreach (ModuleSKGreenhouse m in eq.Greenhouses)
            {
                if (r.EcFrac < 0.99) m.status = "Manque d'électricité";
                else if (r.GreenhouseLacksWater) m.status = "Manque d'eau";
                else m.status = $"Actif ({r.Produced * perDay:F1} nourriture/j)";
            }
        }

        private double Amount(string resource)
        {
            LifeResources.GetTotals(vessel, resource, out double amount, out double _);
            return amount;
        }

        #endregion

        private double MaxCrewedTemperature()
        {
            double max = 0;
            foreach (Part part in vessel.parts)
                if (part.protoModuleCrew.Count > 0 && part.temperature > max)
                    max = part.temperature;
            return max;
        }

        private List<KerbalHealth> AliveCrew()
        {
            List<ProtoCrewMember> crew = vessel.GetVesselCrew();
            if (crew == null || crew.Count == 0)
                return new List<KerbalHealth>();
            return crew.Select(HealthScenario.Instance.Get).Where(k => !k.Dead).ToList();
        }
    }
}
