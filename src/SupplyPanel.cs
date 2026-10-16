using UnityEngine;

namespace KerTech
{
    /// <summary>Bloc « stocks, équipements, autonomie », commun à la fenêtre de vol et à celle de l'éditeur.</summary>
    internal static class SupplyPanel
    {
        public static void Draw(double water, double waterMax, double food, double foodMax, int crew, Equipment eq, float scale = 1f)
        {
            LifeSupportSim.Autonomy(water, food, crew, eq, out double waterDays, out double foodDays);
            DrawLine("Eau", water, waterMax, crew > 0 || eq.Any ? LifeSupportSim.FormatDays(waterDays) : "—", scale);
            DrawLine("Nourriture", food, foodMax, crew > 0 || eq.Any ? LifeSupportSim.FormatDays(foodDays) : "—", scale);

            if (eq.Any)
            {
                if (eq.RecyclerKerbals > 0)
                    GUILayout.Label($"Recycleurs : {eq.RecyclerRatio:P0} de l'eau de {eq.RecyclerKerbals:F0} Kerbal(s)");
                if (eq.GreenhouseFood > 0)
                    GUILayout.Label($"Serres : +{eq.GreenhouseFood:F1} nourriture/j, -{eq.GreenhouseWater:F1} eau/j");
                GUILayout.Label($"Électricité requise : {eq.EcPerSecond:F2}/s");
            }
        }

        private static void DrawLine(string label, double amount, double max, string autonomy, float scale)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(90 * scale));
            GUILayout.Label($"{amount:F1} / {max:F1}", GUILayout.Width(120 * scale));
            GUILayout.Label("autonomie : " + autonomy);
            GUILayout.EndHorizontal();
        }
    }
}
