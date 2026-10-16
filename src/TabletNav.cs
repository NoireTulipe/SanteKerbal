using UnityEngine;

namespace SanteKerbal
{
    /// <summary>Application Navigation de la tablette : SAS, gaz, assistant de mise en orbite.</summary>
    public partial class Tablet
    {
        private static readonly (VesselAutopilot.AutopilotMode mode, string label)[] SasModes =
        {
            (VesselAutopilot.AutopilotMode.StabilityAssist, "Stabiliser"),
            (VesselAutopilot.AutopilotMode.Prograde, "Prograde"),
            (VesselAutopilot.AutopilotMode.Retrograde, "Rétrograde"),
            (VesselAutopilot.AutopilotMode.Normal, "Normal"),
            (VesselAutopilot.AutopilotMode.Antinormal, "Anti-normal"),
            (VesselAutopilot.AutopilotMode.RadialOut, "Radial ext."),
            (VesselAutopilot.AutopilotMode.RadialIn, "Radial int."),
            (VesselAutopilot.AutopilotMode.Target, "Cible"),
            (VesselAutopilot.AutopilotMode.AntiTarget, "Anti-cible"),
            (VesselAutopilot.AutopilotMode.Maneuver, "Manœuvre"),
        };

        private static string altitudeText = "";

        private void DrawNavigationPage(Vessel v)
        {
            Orbit o = v.orbit;
            GUILayout.Label($"<b>{v.mainBody.displayName.LocalizeRemoveGender()}</b>  altitude {v.altitude / 1000:F1} km, " +
                            $"vitesse {v.obt_speed:F0} m/s", richLabel);
            GUILayout.Label($"Apoapside {o.ApA / 1000:F1} km   Périapside {o.PeA / 1000:F1} km", richLabel);
            GUILayout.Space(U(4));

            DrawAssistant(v);
            GUILayout.Space(U(6));
            DrawThrottle();
            GUILayout.Space(U(6));
            DrawSas(v);
        }

        /// <summary>Assistant de mise en orbite : altitude visée, démarrage, circularisation, annulation.</summary>
        private void DrawAssistant(Vessel v)
        {
            GUILayout.Label("<b>Assistant de mise en orbite</b>", sectionStyle);
            if (Navigator.Current != Navigator.Phase.Off)
            {
                GUILayout.Label($"<color=#9fd0ff>{Navigator.Status}</color>", richLabel);
                if (GUILayout.Button("Annuler l'assistant", smallButton, GUILayout.Width(U(200))))
                    Navigator.Cancel();
                return;
            }

            if (Navigator.TargetAltitude <= 0)
                Navigator.TargetAltitude = Navigator.DefaultAltitude(v);
            if (altitudeText == "")
                altitudeText = (Navigator.TargetAltitude / 1000).ToString("F0");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Orbite visée :", GUILayout.Width(U(110)));
            string edited = GUILayout.TextField(altitudeText, GUILayout.Width(U(60)));
            if (edited != altitudeText)
            {
                altitudeText = edited;
                if (double.TryParse(edited.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double km) && km > 0)
                    Navigator.TargetAltitude = km * 1000;
            }
            GUILayout.Label("km");
            GUILayout.EndHorizontal();

            // Au sol ou en montée : virage gravitationnel ; dans l'espace : circularisation à l'apoapside
            CelestialBody b = v.mainBody;
            double edge = b.atmosphere ? b.atmosphereDepth : 0;
            bool suborbital = v.orbit.PeA < edge;
            GUILayout.BeginHorizontal();
            if (suborbital && v.altitude < edge * 0.9 + 1
                && GUILayout.Button("Mise en orbite", smallButton, GUILayout.Width(U(160))))
                Navigator.StartAscent(v);
            if (!v.Landed && !v.Splashed && v.orbit.ApA > edge && v.orbit.eccentricity < 1
                && GUILayout.Button("Circulariser à l'apoapside", smallButton, GUILayout.Width(U(220))))
                Navigator.StartCircularize(v);
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(Navigator.Status))
                GUILayout.Label($"<color=#a0a0a0>{Navigator.Status}</color>", richLabel);
            GUILayout.Label("<color=#a0a0a0>L'assistant oriente et gère les gaz ; à toi de séparer les étages.</color>", richLabel);
        }

        private void DrawThrottle()
        {
            if (FlightInputHandler.state == null)
                return;
            float t = FlightInputHandler.state.mainThrottle;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>Gaz</b> {t * 100:F0} %", richLabel, GUILayout.Width(U(90)));
            float edited = GUILayout.HorizontalSlider(t, 0f, 1f, GUILayout.ExpandWidth(true));
            if (Mathf.Abs(edited - t) > 0.001f)
                FlightInputHandler.state.mainThrottle = edited;
            if (GUILayout.Button("Couper", smallButton, GUILayout.Width(U(70))))
                FlightInputHandler.state.mainThrottle = 0f;
            if (GUILayout.Button("Plein gaz", smallButton, GUILayout.Width(U(90))))
                FlightInputHandler.state.mainThrottle = 1f;
            GUILayout.EndHorizontal();
        }

        /// <summary>Modes du SAS, disponibles selon le niveau du pilote ou de la sonde (règle de KSP).</summary>
        private void DrawSas(Vessel v)
        {
            VesselAutopilot ap = v.Autopilot;
            if (ap == null)
                return;
            bool sasOn = v.ActionGroups[KSPActionGroup.SAS];
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Orientation (SAS)</b>", sectionStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Toggle(sasOn, sasOn ? "SAS activé" : "SAS coupé", smallButton, GUILayout.Width(U(120))) != sasOn)
                v.ActionGroups.SetGroup(KSPActionGroup.SAS, !sasOn);
            GUILayout.EndHorizontal();

            for (int row = 0; row < 2; row++)
            {
                GUILayout.BeginHorizontal();
                for (int i = row * 5; i < row * 5 + 5; i++)
                {
                    var (mode, label) = SasModes[i];
                    bool available = ap.CanSetMode(mode);
                    bool on = sasOn && ap.Mode == mode;
                    GUI.enabled = available;
                    if (GUILayout.Toggle(on, label, smallButton) && !on && available)
                    {
                        if (Navigator.Current != Navigator.Phase.Off)
                            Navigator.Cancel("Assistant arrêté : orientation reprise à la main");
                        if (!sasOn)
                            v.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                        ap.SetMode(mode);
                    }
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("<color=#a0a0a0>Les modes grisés dépendent du niveau du pilote ou de la sonde.</color>", richLabel);
        }
    }
}
