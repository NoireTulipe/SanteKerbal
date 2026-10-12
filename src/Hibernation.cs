using System.Collections.Generic;

namespace SanteKerbal
{
    /// <summary>
    /// Hibernation : un Kerbal endormi dans un caisson ne mange ni ne boit, sa santé est figée,
    /// mais il consomme de l'électricité et ne peut pas piloter. Sans électricité, il se dégrade vite.
    /// Le réveil prend quelques heures (toujours incapable de piloter) et coûte un peu de santé.
    /// </summary>
    internal static class Hibernation
    {
        public static bool Available => Settings.Enabled && Settings.Hibernation;

        /// <summary>Le Kerbal est-il assis dans une pièce équipée d'un caisson ?</summary>
        public static Part CryoPartOf(Vessel vessel, ProtoCrewMember pcm)
        {
            if (vessel == null || !vessel.loaded)
                return null;
            foreach (Part p in vessel.parts)
                if (p.protoModuleCrew.Contains(pcm))
                    return p.FindModuleImplementing<ModuleSKCryoPod>() != null ? p : null;
            return null;
        }

        /// <summary>Ce que le bouton d'hibernation propose pour ce Kerbal.</summary>
        public enum Action { None, Sleep, MoveAndSleep, Wake }

        public static Action ActionFor(KerbalHealth k, ProtoCrewMember pcm, Vessel vessel)
        {
            if (!Available || k.Dead || k.Waking || vessel == null || !vessel.loaded)
                return Action.None;
            if (k.Asleep)
                return Action.Wake;
            if (CryoPartOf(vessel, pcm) != null)
                return Action.Sleep;
            return FreeCryoPart(vessel) != null ? Action.MoveAndSleep : Action.None;
        }

        public static string Label(Action a)
        {
            switch (a)
            {
                case Action.Sleep:
                case Action.MoveAndSleep: return "Hiberner";
                case Action.Wake: return "Réveiller";
                default: return null;
            }
        }

        /// <summary>Exécute l'action proposée par ActionFor.</summary>
        public static void Do(Action a, KerbalHealth k, ProtoCrewMember pcm, Vessel vessel)
        {
            switch (a)
            {
                case Action.Sleep:
                    Sleep(k, pcm, vessel);
                    break;
                case Action.Wake:
                    Wake(k, pcm, vessel);
                    break;
                case Action.MoveAndSleep:
                    MoveToCryoAndSleep(k, pcm, vessel);
                    break;
            }
        }

        /// <summary>Le vaisseau a-t-il au moins un caisson (plein ou non) ?</summary>
        public static bool HasCryoPod(Vessel vessel)
        {
            foreach (Part p in vessel.parts)
                if (p.FindModuleImplementing<ModuleSKCryoPod>() != null)
                    return true;
            return false;
        }

        /// <summary>
        /// Un mort assis dans un caisson le bloque : on peut déplacer son corps vers une place libre
        /// hors caisson. Renvoie la pièce de destination possible, ou null.
        /// </summary>
        public static Part BodyDestination(Vessel vessel, ProtoCrewMember pcm)
        {
            if (vessel == null || !vessel.loaded || CryoPartOf(vessel, pcm) == null)
                return null;
            foreach (Part p in vessel.parts)
                if (p.FindModuleImplementing<ModuleSKCryoPod>() == null && p.CrewCapacity > 0 && p.protoModuleCrew.Count < p.CrewCapacity)
                    return p;
            return null;
        }

        public static void MoveBody(KerbalHealth k, ProtoCrewMember pcm, Vessel vessel)
        {
            Part src = PartOf(vessel, pcm);
            Part dst = BodyDestination(vessel, pcm);
            if (src == null || dst == null || !k.Dead)
                return;
            k.ReturnPartId = 0;
            string title = dst.partInfo.title;
            MoveCrew(pcm, src, dst, () => HealthEvents.Announce(vessel, $"Le corps de {k.Name} a été déplacé ({title})."));
        }

        /// <summary>Un caisson du vaisseau avec une place libre.</summary>
        public static Part FreeCryoPart(Vessel vessel)
        {
            foreach (Part p in vessel.parts)
                if (p.FindModuleImplementing<ModuleSKCryoPod>() != null && p.protoModuleCrew.Count < p.CrewCapacity)
                    return p;
            return null;
        }

        private static Part PartOf(Vessel vessel, ProtoCrewMember pcm)
        {
            foreach (Part p in vessel.parts)
                if (p.protoModuleCrew.Contains(pcm))
                    return p;
            return null;
        }

        /// <summary>
        /// Transfère le Kerbal dans une place libre d'un caisson puis l'endort, comme le transfert
        /// d'équipage de KSP (CrewTransfer.MoveCrewTo) : l'intérieur est recréé à l'image suivante.
        /// </summary>
        private static void MoveToCryoAndSleep(KerbalHealth k, ProtoCrewMember pcm, Vessel vessel)
        {
            Part src = PartOf(vessel, pcm);
            Part dst = FreeCryoPart(vessel);
            if (src == null || dst == null)
                return;
            if (MoveCrew(pcm, src, dst, () => Sleep(k, pcm, vessel)))
                k.ReturnPartId = src.persistentId;   // poste à regagner après le réveil
        }

        /// <summary>
        /// Déplace un Kerbal d'une pièce à l'autre du même vaisseau, comme le transfert d'équipage de
        /// KSP (CrewTransfer.MoveCrewTo) : l'intérieur est recréé à l'image suivante, puis then() est appelé.
        /// </summary>
        private static bool MoveCrew(ProtoCrewMember pcm, Part src, Part dst, System.Action then)
        {
            if (CrewController.Instance == null)
                return false;
            Vessel vessel = src.vessel;
            src.RemoveCrewmember(pcm);
            dst.AddCrewmember(pcm);
            GameEvents.onCrewTransferred.Fire(new GameEvents.HostedFromToAction<ProtoCrewMember, Part>(pcm, src, dst));
            Vessel.CrewWasModified(vessel);
            Log.Info($"{pcm.name} transféré de {src.partInfo.title} vers {dst.partInfo.title}");

            bool active = vessel == FlightGlobals.ActiveVessel;
            if (active)
                vessel.DespawnCrew();
            CrewController.Instance.NextFrame(() =>
            {
                if (active && vessel != null)
                    vessel.SpawnCrew();
                then?.Invoke();
            });
            return true;
        }

        /// <summary>
        /// Après le réveil, renvoie le Kerbal à la pièce d'où il était venu. Renvoie false si le
        /// vaisseau n'est pas chargé (on réessaiera plus tard) ; sinon le retour est fait ou abandonné.
        /// </summary>
        public static bool TryReturnToPost(KerbalHealth k, ProtoCrewMember pcm)
        {
            Vessel vessel = null;
            Part src = null;
            foreach (Vessel v in FlightGlobals.VesselsLoaded)
            {
                src = v.isEVA ? null : PartOf(v, pcm);
                if (src != null) { vessel = v; break; }
            }
            if (vessel == null)
                return false;   // pas chargé (ou en EVA) : plus tard

            uint id = k.ReturnPartId;
            k.ReturnPartId = 0;
            Part dst = vessel.parts.Find(p => p.persistentId == id);
            if (dst == null)
            {
                HealthEvents.Announce(vessel, $"{k.Name} ne retrouve pas son poste (pièce disparue) : il reste dans le caisson.");
                return true;
            }
            if (dst == src)
                return true;
            if (dst.protoModuleCrew.Count >= dst.CrewCapacity)
            {
                HealthEvents.Announce(vessel, $"Le poste de {k.Name} ({dst.partInfo.title}) est occupé : il reste dans le caisson.");
                return true;
            }
            string title = dst.partInfo.title;
            MoveCrew(pcm, src, dst, () => HealthEvents.Announce(vessel, $"{k.Name} a regagné son poste ({title})."));
            return true;
        }

        public static void Sleep(KerbalHealth k, ProtoCrewMember pcm, Vessel vessel)
        {
            if (!Available || k.Dead || k.Asleep)
                return;
            k.Asleep = true;
            k.WakeUntil = 0;
            k.LackWater = k.LackFood = false;
            Mortality.Incapacitate(pcm);
            HealthEvents.Announce(vessel, $"{k.Name} est plongé en hibernation.");
        }

        public static void Wake(KerbalHealth k, ProtoCrewMember pcm, Vessel vessel)
        {
            if (!k.Asleep || k.Dead)
                return;
            k.Asleep = false;
            k.LackWater = false;   // efface la mémoire « caisson sans courant »
            double before = k.Health;
            k.Damage(Settings.CryoWakeDamage);
            k.WakeUntil = Planetarium.GetUniversalTime() + Settings.CryoWakeHours * 3600.0;
            HealthEvents.Announce(vessel, $"Réveil de {k.Name} en cours ({Settings.CryoWakeHours:F0} h).");
            HealthEvents.HealthChanged(vessel, k, before, "réveil difficile");
        }

        public static void SleepAll(Part part)
        {
            if (HealthScenario.Instance == null)
                return;
            foreach (ProtoCrewMember pcm in new List<ProtoCrewMember>(part.protoModuleCrew))
                Sleep(HealthScenario.Instance.Get(pcm), pcm, part.vessel);
        }

        public static void WakeAll(Part part)
        {
            if (HealthScenario.Instance == null)
                return;
            foreach (ProtoCrewMember pcm in new List<ProtoCrewMember>(part.protoModuleCrew))
                Wake(HealthScenario.Instance.Get(pcm), pcm, part.vessel);
        }

        /// <summary>
        /// Électricité des dormeurs pendant dt ; sans courant, ils perdent de la santé.
        /// Hors chargement, l'électricité a déjà été résolue par PowerSim pour tout le vaisseau.
        /// </summary>
        public static void Tick(IVesselStore store, List<KerbalHealth> sleepers, double dt, Vessel vessel)
        {
            if (sleepers.Count == 0)
                return;
            // Électricité déjà résolue par PowerSim pour tout le vaisseau (caissons et équipements)
            double frac = store.EcFraction;

            double days = dt / KSPUtil.dateTimeFormatter.Day;
            double damage = (1.0 - frac) * Settings.CryoUnpoweredDamagePerDay * days;
            bool unpowered = frac < 0.999;
            foreach (KerbalHealth k in sleepers)
            {
                // LackWater sert ici de mémoire « caisson sans courant » pour n'annoncer qu'au changement
                if (unpowered != k.LackWater)
                {
                    if (unpowered)
                        HealthEvents.Alert(vessel, $"Caisson de {k.Name} : panne d'électricité !");
                    else
                        HealthEvents.Announce(vessel, $"Caisson de {k.Name} : électricité rétablie.");
                }
                k.LackWater = unpowered;

                double before = k.Health;
                k.Damage(damage);
                HealthEvents.HealthChanged(vessel, k, before, "caisson sans électricité");
            }
        }

        /// <summary>Termine les réveils arrivés à échéance (tous les Kerbals, chargés ou non).</summary>
        public static void FinishWakeUps(IEnumerable<KerbalHealth> all, double now)
        {
            foreach (KerbalHealth k in all)
            {
                if (!k.Waking || now < k.WakeUntil)
                    continue;
                k.WakeUntil = 0;
                ProtoCrewMember pcm = HighLogic.CurrentGame?.CrewRoster?[k.Name];
                if (pcm != null && !k.Dead)
                    Mortality.Revive(pcm);
                Log.Info($"{k.Name} est réveillé");
                ScreenMessages.PostScreenMessage($"{k.Name} est réveillé.", 5f, ScreenMessageStyle.UPPER_CENTER);
            }

            // Retour au poste : dès que le Kerbal est opérationnel et son vaisseau chargé
            foreach (KerbalHealth k in all)
            {
                if (k.ReturnPartId == 0 || k.Dead || k.Asleep || k.Waking)
                    continue;
                ProtoCrewMember pcm = HighLogic.CurrentGame?.CrewRoster?[k.Name];
                if (pcm == null || pcm.rosterStatus != ProtoCrewMember.RosterStatus.Assigned)
                    k.ReturnPartId = 0;   // récupéré, mort, disparu : plus de poste à regagner
                else
                    TryReturnToPost(k, pcm);
            }
        }

        /// <summary>À la récupération ou si l'hibernation est désactivée : réveil immédiat, sans délai.</summary>
        public static void WakeInstantly(KerbalHealth k, ProtoCrewMember pcm)
        {
            if (k.Dead || (!k.Asleep && !k.Waking))
                return;
            k.Asleep = false;
            k.WakeUntil = 0;
            k.LackWater = false;
            k.ReturnPartId = 0;
            if (pcm != null)
                Mortality.Revive(pcm);
        }
    }
}
