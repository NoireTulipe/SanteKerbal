using System.Collections.Generic;
using KSP.UI.Screens;
using UnityEngine;

namespace SanteKerbal
{
    /// <summary>
    /// Bouton de barre d'outils et fenêtre de santé pour le vaisseau actif.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class HealthWindow : MonoBehaviour
    {
        private const string IconUrl = "SanteKerbal/Icons/toolbar";
        private const int WindowId = 0x5A17E;

        // Conservés d'une scène de vol à l'autre pendant la session
        private static Rect windowRect = new Rect(200, 120, 500, 0);
        private const float WindowWidth = 500f;
        private static bool visible;

        private ApplicationLauncherButton button;
        private bool uiHidden;

        private GUIStyle barLabelStyle;
        private GUIStyle richLabelStyle;

        // Moniteurs cardiaques des Kerbals du vaisseau actif, par nom
        private readonly Dictionary<string, HeartMonitor> monitors = new Dictionary<string, HeartMonitor>();

        public void Start()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(AddButton);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(RemoveButton);
            GameEvents.onHideUI.Add(OnHideUI);
            GameEvents.onShowUI.Add(OnShowUI);
            if (ApplicationLauncher.Ready)
                AddButton();
        }

        public void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(AddButton);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(RemoveButton);
            GameEvents.onHideUI.Remove(OnHideUI);
            GameEvents.onShowUI.Remove(OnShowUI);
            RemoveButton();
        }

        private void OnHideUI() => uiHidden = true;
        private void OnShowUI() => uiHidden = false;

        private void AddButton()
        {
            if (button != null || ApplicationLauncher.Instance == null)
                return;
            Texture2D icon = GameDatabase.Instance.GetTexture(IconUrl, false);
            if (icon == null)
            {
                Log.Warning("Icône " + IconUrl + " introuvable, bouton sans image.");
                icon = Texture2D.whiteTexture;
            }
            button = ApplicationLauncher.Instance.AddModApplication(
                () => visible = true, () => visible = false,
                null, null, null, null,
                ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
                icon);
            if (visible)
                button.SetTrue(false);
            Log.Info("Bouton de barre d'outils ajouté");
        }

        private void RemoveButton()
        {
            if (button == null)
                return;
            if (ApplicationLauncher.Instance != null)
                ApplicationLauncher.Instance.RemoveModApplication(button);
            button = null;
        }

        /// <summary>Fait battre les moniteurs du vaisseau actif (fenêtre ouverte seulement).</summary>
        public void Update()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (!visible || !Settings.Enabled || !Settings.Monitor || vessel == null || vessel.isEVA
                || HealthScenario.Instance == null)
                return;

            float dt = Time.deltaTime;   // nul en pause : le tracé s'arrête aussi
            double g = vessel.packed ? 1.0 : vessel.geeForce;
            HeartMonitor loudest = null;
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
                    loudest = beat ? m : null;
                    loudestPitch = 0.9f + (Mathf.Abs(k.Name.GetHashCode()) % 5) * 0.05f;
                }
            }
            if (loudest != null)
                MonitorSounds.Beep(loudestPitch);
        }

        private static double CabinTemperature(Vessel vessel, ProtoCrewMember pcm)
        {
            foreach (Part p in vessel.parts)
                if (p.protoModuleCrew.Contains(pcm))
                    return p.temperature;
            return 0;
        }

        public void OnGUI()
        {
            if (!visible || uiHidden || HealthScenario.Instance == null)
                return;

            GUI.skin = HighLogic.Skin;
            if (barLabelStyle == null)
            {
                barLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
                richLabelStyle = new GUIStyle(GUI.skin.label) { richText = true };
            }
            windowRect = GUILayout.Window(WindowId, windowRect, DrawWindow, "SantéKerbal", GUILayout.Width(WindowWidth));
        }

        private void DrawWindow(int id)
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (!Settings.Enabled)
            {
                GUILayout.Label("SantéKerbal est désactivé pour cette partie.");
                GUILayout.Label("(Échap > Paramètres de difficulté > onglet SantéKerbal)");
            }
            else if (vessel == null)
            {
                GUILayout.Label("Aucun vaisseau actif.");
            }
            else if (vessel.isEVA)
            {
                GUILayout.Label(vessel.vesselName);
                GUILayout.Label("Kerbal en EVA : non suivi pour l'instant.");
            }
            else
            {
                DrawVessel(vessel);
            }

            if (GUILayout.Button("Fermer"))
            {
                visible = false;
                if (button != null)
                    button.SetFalse(false);
            }
            GUI.DragWindow();
        }

        private void DrawVessel(Vessel vessel)
        {
            GUILayout.Label("<b>" + vessel.vesselName + "</b>", richLabelStyle);

            List<ProtoCrewMember> crew = vessel.GetVesselCrew();
            int alive = 0;
            if (crew.Count == 0)
            {
                GUILayout.Label("Pas d'équipage à bord.");
            }
            else
            {
                foreach (ProtoCrewMember pcm in crew)
                {
                    KerbalHealth k = HealthScenario.Instance.Get(pcm);
                    if (!k.Dead)
                        alive++;
                    DrawKerbal(k, vessel);
                }
            }

            GUILayout.Space(6);
            LifeResources.GetTotals(vessel, LifeResources.Water, out double water, out double waterMax);
            LifeResources.GetTotals(vessel, LifeResources.Food, out double food, out double foodMax);
            SupplyPanel.Draw(water, waterMax, food, foodMax, alive, Equipment.FromParts(vessel.parts));
        }

        private void DrawKerbal(KerbalHealth k, Vessel vessel)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(k.Name, GUILayout.Width(150));

            Rect bar = GUILayoutUtility.GetRect(110, 18, GUILayout.Width(110));
            DrawBar(bar, k.Dead ? 0f : (float)(k.Health / KerbalHealth.Max), HealthColor(k));
            GUI.Label(bar, k.Dead ? "MORT" : k.Health.ToString("F0"), barLabelStyle);

            if (Settings.Monitor && monitors.TryGetValue(k.Name, out HeartMonitor m))
            {
                GUILayout.Space(6);
                Rect trace = GUILayoutUtility.GetRect(HeartMonitor.Width, HeartMonitor.Height,
                    GUILayout.Width(HeartMonitor.Width), GUILayout.Height(HeartMonitor.Height));
                m.Draw(trace, k.Dead ? new Color(0.6f, 0.6f, 0.6f) : MonitorColor(k));
                GUILayout.Label(k.Dead ? "0" : m.Bpm.ToString("F0"), GUILayout.Width(32));
                GUILayout.Label("bpm", GUILayout.Width(30));
            }
            GUILayout.EndHorizontal();

            if (Settings.DebugTools && !k.Dead)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("   test :", GUILayout.Width(60));
                if (GUILayout.Button("-20"))
                    ChangeHealth(k, -20, vessel);
                if (GUILayout.Button("+20"))
                    ChangeHealth(k, +20, vessel);
                if (GUILayout.Button("Tuer"))
                    Mortality.Kill(k, "outil de test", vessel);
                GUILayout.EndHorizontal();
            }

            if (!k.Dead && (k.LackWater || k.LackFood))
            {
                string lack = k.LackWater && k.LackFood ? "soif et faim" : k.LackWater ? "soif" : "faim";
                GUILayout.Label("<color=#ff8060>   ! " + lack + "</color>", richLabelStyle);
            }
        }

        private static void ChangeHealth(KerbalHealth k, double delta, Vessel vessel)
        {
            if (delta < 0)
                k.Damage(-delta);
            else
                k.Heal(delta);
            Log.Info($"[{vessel.vesselName}] Test : santé de {k.Name} -> {k.Health:F0}");
            if (k.Health <= 0)
                Mortality.Kill(k, "outil de test", vessel);
        }

        /// <summary>Vert phosphore d'un vrai moniteur, puis jaune et rouge quand ça va mal.</summary>
        private static Color MonitorColor(KerbalHealth k)
        {
            if (k.Asleep) return new Color(0.45f, 0.75f, 1f);
            if (k.Health > 50) return new Color(0.3f, 1f, 0.45f);
            if (k.Health > 25) return new Color(1f, 0.85f, 0.2f);
            return new Color(1f, 0.3f, 0.25f);
        }

        private static Color HealthColor(KerbalHealth k)
        {
            if (k.Dead) return Color.gray;
            if (k.Health > 50) return new Color(0.2f, 0.8f, 0.2f);
            if (k.Health > 25) return new Color(0.9f, 0.75f, 0.1f);
            return new Color(0.9f, 0.2f, 0.15f);
        }

        private static void DrawBar(Rect rect, float fraction, Color color)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
