using System.Collections.Generic;
using UnityEngine;

namespace SanteKerbal
{
    /// <summary>Applications Temps (accélération du temps) et Journal de bord de la tablette.</summary>
    public partial class Tablet
    {
        #region Temps

        // Accélération en cours lancée par la tablette (UT visé, 0 = aucune)
        private static double warpTarget;
        private static string warpLabel;
        private static float warpStartReal;

        // Durée libre saisie par le joueur
        private static string customDays = "1", customHours = "0";

        private static readonly float[] SpeedShortcuts = { 10f, 100f, 1000f, 100000f };

        /// <summary>Fin de l'accélération lancée par la tablette : heure atteinte ou arrêt par le joueur / une alerte.</summary>
        private static void UpdateWarp()
        {
            if (warpTarget <= 0)
                return;
            double now = Planetarium.GetUniversalTime();
            bool stopped = TimeWarp.CurrentRateIndex == 0 && Time.realtimeSinceStartup > warpStartReal + 2f;
            if (now >= warpTarget - 0.5 || stopped)
                warpTarget = 0;
        }

        private static void WarpTo(double ut, string label)
        {
            double now = Planetarium.GetUniversalTime();
            if (ut <= now + 1 || TimeWarp.fetch == null)
                return;
            warpTarget = ut;
            warpLabel = label;
            warpStartReal = Time.realtimeSinceStartup;
            TimeWarp.fetch.WarpTo(ut);
            Log.Info($"Tablette : accélération jusqu'à {KSPUtil.PrintDateCompact(ut, true)} ({label})");
        }

        private static void StopWarp()
        {
            warpTarget = 0;
            TimeWarp.SetRate(0, false);
        }

        private void DrawTimePage(Vessel v)
        {
            double now = Planetarium.GetUniversalTime();
            double day = KSPUtil.dateTimeFormatter.Day;
            GUILayout.Label($"<b>Heure de bord</b> : {KSPUtil.PrintDate(now, true)}", richLabel);

            if (warpTarget > 0)
            {
                GUILayout.Label($"<color=#9fd0ff>Accélération : {warpLabel}, encore {KSPUtil.PrintTimeCompact(warpTarget - now, false)}</color>", richLabel);
                if (GUILayout.Button("Arrêter l'accélération", smallButton, GUILayout.Width(U(200))))
                    StopWarp();
                GUILayout.Space(U(6));
            }

            // Avancer d'une durée
            GUILayout.Label("<b>Avancer de</b>", sectionStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1 h", smallButton)) WarpTo(now + 3600, "+1 h");
            if (GUILayout.Button("6 h", smallButton)) WarpTo(now + 6 * 3600, "+6 h");
            if (GUILayout.Button("1 jour", smallButton)) WarpTo(now + day, "+1 jour");
            if (GUILayout.Button("1 semaine", smallButton)) WarpTo(now + 7 * day, "+1 semaine");
            if (GUILayout.Button("30 jours", smallButton)) WarpTo(now + 30 * day, "+30 jours");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Durée :", GUILayout.Width(U(60)));
            customDays = GUILayout.TextField(customDays, GUILayout.Width(U(50)));
            GUILayout.Label("j", GUILayout.Width(U(16)));
            customHours = GUILayout.TextField(customHours, GUILayout.Width(U(50)));
            GUILayout.Label("h", GUILayout.Width(U(16)));
            if (GUILayout.Button("Avancer", smallButton, GUILayout.Width(U(90))))
            {
                double.TryParse(customDays.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d);
                double.TryParse(customHours.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double h);
                double span = System.Math.Max(0, d) * day + System.Math.Max(0, h) * 3600;
                if (span > 0)
                    WarpTo(now + span, $"+{customDays} j {customHours} h");
            }
            GUILayout.EndHorizontal();
            GUILayout.Label($"<color=#a0a0a0>Un jour de jeu = {day / 3600:F0} h.</color>", richLabel);
            GUILayout.Space(U(6));

            // Jusqu'à un événement
            GUILayout.Label("<b>Jusqu'à</b>", sectionStyle);
            int targets = 0;
            foreach (var t in EventTargets(v, now))
            {
                targets++;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{t.label} <color=#a0a0a0>(dans {KSPUtil.PrintTimeCompact(t.ut - now, false)})</color>", richLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Aller", smallButton, GUILayout.Width(U(70))))
                    WarpTo(t.ut, t.label);
                GUILayout.EndHorizontal();
            }
            if (targets == 0)
                GUILayout.Label("<color=#a0a0a0>Aucun événement à venir.</color>", richLabel);
            GUILayout.Space(U(6));

            // Vitesse directe
            GUILayout.Label("<b>Vitesse</b>", sectionStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(TimeWarp.CurrentRateIndex == 0, "x1", smallButton))
                if (TimeWarp.CurrentRateIndex != 0) StopWarp();
            foreach (float rate in SpeedShortcuts)
            {
                int index = RateIndex(rate);
                if (index < 0) continue;
                bool on = TimeWarp.CurrentRateIndex == index && TimeWarp.WarpMode == TimeWarp.Modes.HIGH;
                if (GUILayout.Toggle(on, "x" + rate.ToString("N0"), smallButton) && !on)
                {
                    warpTarget = 0;
                    TimeWarp.fetch.Mode = TimeWarp.Modes.HIGH;
                    TimeWarp.SetRate(index, false);
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("<color=#a0a0a0>Près d'une planète, KSP limite la vitesse. L'accélération s'arrête toute seule en cas d'alerte.</color>", richLabel);
        }

        private static int RateIndex(float rate)
        {
            if (TimeWarp.fetch == null)
                return -1;
            float[] rates = TimeWarp.fetch.warpRates;
            for (int i = 0; i < rates.Length; i++)
                if (Mathf.Approximately(rates[i], rate))
                    return i;
            return -1;
        }

        /// <summary>Événements à venir vers lesquels on peut accélérer.</summary>
        private static List<(string label, double ut)> EventTargets(Vessel v, double now)
        {
            var list = new List<(string, double)>();
            double day = KSPUtil.dateTimeFormatter.Day;

            if (v != null && !v.isEVA && HealthScenario.Instance != null)
            {
                // Fin du réveil le plus proche
                KerbalHealth first = null;
                foreach (ProtoCrewMember pcm in v.GetVesselCrew())
                {
                    KerbalHealth k = HealthScenario.Instance.Get(pcm);
                    if (k.Waking && k.WakeUntil > now && (first == null || k.WakeUntil < first.WakeUntil))
                        first = k;
                }
                if (first != null)
                    list.Add(($"Réveil de {first.Name}", first.WakeUntil));

                // Batteries pleines (d'après le bilan électrique mesuré)
                if (PowerHistory.HasData && PowerHistory.NetRate > 0.005 && PowerHistory.Capacity - PowerHistory.Amount > 1)
                    list.Add(("Batteries pleines", now + (PowerHistory.Capacity - PowerHistory.Amount) / PowerHistory.NetRate));

                // Prochaine pénurie, avec une marge pour avoir le temps de réagir
                int awake = 0;
                foreach (ProtoCrewMember pcm in v.GetVesselCrew())
                {
                    KerbalHealth k = HealthScenario.Instance.Get(pcm);
                    if (!k.Dead && !k.Asleep) awake++;
                }
                if (awake > 0)
                {
                    LifeResources.GetTotals(v, LifeResources.Water, out double water, out double _);
                    LifeResources.GetTotals(v, LifeResources.Food, out double food, out double _);
                    LifeSupportSim.Autonomy(water, food, awake, Equipment.FromParts(v.parts), out double wd, out double fd);
                    double daysLeft = System.Math.Min(wd, fd);
                    if (!double.IsInfinity(daysLeft) && daysLeft > 0)
                    {
                        double margin = System.Math.Min(day, daysLeft * day * 0.1);
                        string what = wd <= fd ? "eau" : "nourriture";
                        if (daysLeft * day - margin > 60)
                            list.Add(($"Juste avant la pénurie ({what})", now + daysLeft * day - margin));
                    }
                }
            }

            // Prochaine alarme de l'horloge de KSP
            AlarmTypeBase alarm = AlarmClockScenario.GetNextAlarm(now);
            if (alarm != null && alarm.ut > now)
                list.Add(($"Alarme : {alarm.title}", alarm.ut));

            list.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            return list;
        }

        #endregion

        #region Journal

        private void DrawJournalPage()
        {
            if (Journal.Entries.Count == 0)
            {
                GUILayout.Label("<color=#a0a0a0>Rien à signaler pour l'instant.</color>", richLabel);
                return;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{Journal.Entries.Count} événement(s), du plus récent au plus ancien.");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Effacer", smallButton, GUILayout.Width(U(80))))
                Journal.Entries.Clear();
            GUILayout.EndHorizontal();
            GUILayout.Space(U(4));

            for (int i = Journal.Entries.Count - 1; i >= 0; i--)
            {
                JournalEntry e = Journal.Entries[i];
                string color = e.Alert ? "#ff8060" : "#d8d8d8";
                string where = string.IsNullOrEmpty(e.Vessel) || e.Vessel == "?" ? "" : $" <color=#9fd0ff>{e.Vessel}</color>";
                GUILayout.Label($"<color=#a0a0a0>{KSPUtil.PrintDateCompact(e.UT, true)}</color>{where}\n   <color={color}>{e.Text}</color>", richLabel);
            }
        }

        #endregion
    }
}
