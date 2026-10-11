namespace SanteKerbal
{
    /// <summary>Messages de santé (log + écran), communs aux vaisseaux chargés et non chargés.</summary>
    internal static class HealthEvents
    {
        private static readonly double[] Thresholds = { 50.0, 25.0, 10.0 };

        public static void Announce(Vessel vessel, string message)
        {
            string name = vessel != null ? vessel.vesselName : "?";
            Log.Info("[" + name + "] " + message);
            // Pour un autre vaisseau que l'actif, on précise lequel
            string shown = vessel != null && vessel == FlightGlobals.ActiveVessel ? message : name + " : " + message;
            ScreenMessages.PostScreenMessage(shown, 5f, ScreenMessageStyle.UPPER_CENTER);
        }

        public static void NeedsChanged(Vessel vessel, KerbalHealth k, bool lackWater, bool lackFood)
        {
            if (lackWater != k.LackWater)
                Announce(vessel, lackWater ? $"{k.Name} n'a plus d'eau !" : $"{k.Name} a de nouveau de l'eau.");
            if (lackFood != k.LackFood)
                Announce(vessel, lackFood ? $"{k.Name} n'a plus de nourriture !" : $"{k.Name} a de nouveau à manger.");
        }

        /// <summary>Annonce le passage des seuils de santé, et tue le Kerbal à 0.</summary>
        public static void HealthChanged(Vessel vessel, KerbalHealth k, double before, string cause)
        {
            if (k.Dead)
                return;
            if (k.Health <= 0)
            {
                Mortality.Kill(k, cause, vessel != null ? vessel.vesselName : "?");
                Announce(vessel, $"{k.Name} est mort ({cause}).");
                return;
            }
            foreach (double threshold in Thresholds)
            {
                if (before > threshold && k.Health <= threshold)
                {
                    Announce(vessel, $"Santé de {k.Name} : {threshold:F0} % ({cause})");
                    break;
                }
            }
        }
    }
}
