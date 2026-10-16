namespace KerTech
{
    /// <summary>Messages de santé (log + écran), communs aux vaisseaux chargés et non chargés.</summary>
    internal static class HealthEvents
    {
        private static readonly double[] Thresholds = { 50.0, 25.0, 10.0 };

        public static void Announce(Vessel vessel, string message) => Announce(vessel, message, false);

        /// <summary>Message à l'écran, dans KSP.log et dans le journal de bord.</summary>
        public static void Announce(Vessel vessel, string message, bool alert)
        {
            string name = vessel != null ? vessel.vesselName : "?";
            Log.Info("[" + name + "] " + message);
            Journal.Add(name, message, alert);
            // Pour un autre vaisseau que l'actif, on précise lequel
            string shown = vessel != null && vessel == FlightGlobals.ActiveVessel ? message : name + " : " + message;
            ScreenMessages.PostScreenMessage(shown, 5f, ScreenMessageStyle.UPPER_CENTER);
        }

        /// <summary>Annonce importante : arrête aussi l'accélération du temps (réglage stopWarpOnAlert).</summary>
        public static void Alert(Vessel vessel, string message)
        {
            Announce(vessel, message, true);
            if (Settings.StopWarpOnAlert && TimeWarp.CurrentRateIndex > 0)
            {
                TimeWarp.SetRate(0, false);
                Log.Info("Accélération du temps arrêtée : " + message);
            }
        }

        public static void NeedsChanged(Vessel vessel, KerbalHealth k, bool lackWater, bool lackFood)
        {
            if (lackWater != k.LackWater)
            {
                if (lackWater) Alert(vessel, $"{k.Name} n'a plus d'eau !");
                else Announce(vessel, $"{k.Name} a de nouveau de l'eau.");
            }
            if (lackFood != k.LackFood)
            {
                if (lackFood) Alert(vessel, $"{k.Name} n'a plus de nourriture !");
                else Announce(vessel, $"{k.Name} a de nouveau à manger.");
            }
        }

        /// <summary>Annonce le passage des seuils de santé, et tue le Kerbal à 0.</summary>
        public static void HealthChanged(Vessel vessel, KerbalHealth k, double before, string cause)
        {
            if (k.Dead)
                return;
            if (k.Health <= 0)
            {
                Mortality.Kill(k, cause, vessel);
                Alert(vessel, $"{k.Name} est mort ({cause}).");
                return;
            }
            foreach (double threshold in Thresholds)
            {
                if (before > threshold && k.Health <= threshold)
                {
                    // Seuils critiques : on arrête aussi l'accélération du temps
                    if (threshold <= 25) Alert(vessel, $"Santé de {k.Name} : {threshold:F0} % ({cause})");
                    else Announce(vessel, $"Santé de {k.Name} : {threshold:F0} % ({cause})");
                    break;
                }
            }
        }
    }
}
