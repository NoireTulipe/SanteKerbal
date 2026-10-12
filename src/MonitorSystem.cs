using System.Collections.Generic;
using UnityEngine;

namespace SanteKerbal
{
    /// <summary>
    /// Fait battre les moniteurs cardiaques de l'équipage du vaisseau actif, à chaque image.
    /// Partagé par la fenêtre de vol et par les tracés au-dessus des portraits.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class MonitorSystem : MonoBehaviour
    {
        private static readonly Dictionary<string, HeartMonitor> monitors = new Dictionary<string, HeartMonitor>();

        /// <summary>Le bip n'est joué que si la tablette est ouverte (positionné par la tablette, page Santé).</summary>
        public static bool BeepEnabled;

        internal static HeartMonitor Get(string kerbalName)
        {
            monitors.TryGetValue(kerbalName, out HeartMonitor m);
            return m;
        }

        public static bool Active => Settings.Enabled && Settings.Monitor && HealthScenario.Instance != null;

        public void OnDestroy()
        {
            monitors.Clear();
        }

        public void Update()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (!Active || vessel == null || vessel.isEVA)
                return;

            float dt = Time.deltaTime;   // nul en pause : les tracés s'arrêtent aussi
            double g = vessel.packed ? 1.0 : vessel.geeForce;
            bool loudestBeat = false;
            float loudestPitch = 1f;
            double worstHealth = double.MaxValue;

            foreach (ProtoCrewMember pcm in vessel.GetVesselCrew())
            {
                KerbalHealth k = HealthScenario.Instance.Get(pcm);
                if (!monitors.TryGetValue(k.Name, out HeartMonitor m))
                    monitors[k.Name] = m = new HeartMonitor();

                float target = HeartMonitor.TargetBpm(k, g, CabinTemperature(vessel, pcm), out bool irregular);
                bool beat = m.Advance(dt, target, irregular, k.Dead);

                // Un seul bip à la fois : celui du Kerbal le plus mal en point (évite la cacophonie)
                if (!k.Dead && k.Health < worstHealth)
                {
                    worstHealth = k.Health;
                    loudestBeat = beat;
                    loudestPitch = 0.9f + (Mathf.Abs(k.Name.GetHashCode()) % 5) * 0.05f;
                }
            }
            if (loudestBeat && BeepEnabled)
                MonitorSounds.Beep(loudestPitch);
        }

        private static double CabinTemperature(Vessel vessel, ProtoCrewMember pcm)
        {
            foreach (Part p in vessel.parts)
                if (p.protoModuleCrew.Contains(pcm))
                    return p.temperature;
            return 0;
        }

        /// <summary>Vert phosphore d'un vrai moniteur, puis jaune et rouge quand ça va mal ; gris si mort.</summary>
        public static Color TraceColor(KerbalHealth k)
        {
            if (k.Dead) return new Color(0.6f, 0.6f, 0.6f);
            if (k.Asleep) return new Color(0.45f, 0.75f, 1f);
            if (k.Health > 50) return new Color(0.3f, 1f, 0.45f);
            if (k.Health > 25) return new Color(1f, 0.85f, 0.2f);
            return new Color(1f, 0.3f, 0.25f);
        }
    }
}
