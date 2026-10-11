namespace SanteKerbal
{
    /// <summary>
    /// Mort d'un Kerbal : il reste à bord, ne peut plus piloter, et n'est déclaré
    /// officiellement mort (statut Dead de KSP) qu'à la récupération du vaisseau.
    /// </summary>
    internal static class Mortality
    {
        // Durée d'inactivité « infinie » (secondes de jeu) : environ 30 000 ans
        private const double Forever = 1e12;

        public static void Kill(KerbalHealth k, string cause, string vesselName)
        {
            if (k.Dead)
                return;
            k.Health = 0;
            k.Dead = true;
            k.LackWater = k.LackFood = false;
            Log.Info($"[{vesselName}] {k.Name} est mort ({cause})");

            ProtoCrewMember pcm = FindCrew(k.Name);
            if (pcm != null)
                Incapacitate(pcm);
        }

        /// <summary>
        /// Rend le Kerbal inactif : ModuleCommand ne le compte plus pour le contrôle.
        /// À réappliquer régulièrement, car l'évanouissement dû aux G de KSP peut raccourcir la durée.
        /// </summary>
        public static void Incapacitate(ProtoCrewMember pcm)
        {
            if (!pcm.inactive || pcm.inactiveTimeEnd < Planetarium.GetUniversalTime() + Forever / 2)
                pcm.SetInactive(Forever, false);
        }

        public static bool IsDead(ProtoCrewMember pcm)
        {
            return pcm != null && HealthScenario.Instance != null && HealthScenario.Instance.IsDead(pcm.name);
        }

        /// <summary>Appelé après que KSP a remis l'équipage récupéré « disponible ».</summary>
        public static void DeclareDeadOnRecovery(ProtoVessel pv)
        {
            if (pv == null)
                return;
            foreach (ProtoCrewMember pcm in pv.GetVesselCrew())
            {
                if (!IsDead(pcm) || pcm.rosterStatus == ProtoCrewMember.RosterStatus.Dead)
                    continue;
                pcm.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                pcm.flightLog.AddEntry(FlightLog.EntryType.Die);
                Log.Info($"{pcm.name} déclaré officiellement mort à la récupération de {pv.vesselName}");
                ScreenMessages.PostScreenMessage($"{pcm.name} a été déclaré officiellement mort.", 8f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        private static ProtoCrewMember FindCrew(string name)
        {
            KerbalRoster roster = HighLogic.CurrentGame?.CrewRoster;
            return roster?[name];
        }
    }
}
