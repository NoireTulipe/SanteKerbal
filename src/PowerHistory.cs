using UnityEngine;

namespace KerTech
{
    /// <summary>
    /// Historique de la charge électrique du vaisseau actif, pour la courbe de la fenêtre de santé.
    /// Les points sont relevés en temps de jeu (la courbe reste juste en accélération du temps),
    /// sur trois échelles. Non sauvegardé : repart de zéro au changement de vaisseau ou de scène.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class PowerHistory : MonoBehaviour
    {
        public const int Points = 120;

        /// <summary>Échelles de la courbe : libellé et durée totale affichée (secondes de jeu, -1 = un jour).</summary>
        public static readonly string[] ScaleLabels = { "10 min", "6 h", "1 jour" };
        private static readonly double[] ScaleSpans = { 600, 6 * 3600, -1 };

        private class Series
        {
            public readonly float[] Values = new float[Points];   // charge en fraction de la capacité
            public int Count;
            public int Head;
            public double LastUT = double.NegativeInfinity;

            public void Push(float v)
            {
                Values[Head] = v;
                Head = (Head + 1) % Points;
                if (Count < Points) Count++;
            }
        }

        private static readonly Series[] series = { new Series(), new Series(), new Series() };
        private static System.Guid vesselId;

        // Bilan instantané (lissé), par seconde de jeu
        public static double Amount, Capacity, NetRate;
        public static bool HasData;
        private double lastAmount = -1, lastUT;

        public static double Span(int scale)
        {
            return ScaleSpans[scale] > 0 ? ScaleSpans[scale] : KSPUtil.dateTimeFormatter.Day;
        }

        public void Update()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v.isEVA || !Settings.Enabled || !Settings.PowerGraph)
            {
                HasData = false;
                return;
            }
            if (v.id != vesselId)
                Reset(v.id);

            double ut = Planetarium.GetUniversalTime();
            LifeResources.GetTotals(v, LifeSupportSim.ElectricCharge, out double amount, out double capacity);
            Amount = amount;
            Capacity = capacity;
            HasData = capacity > 0;

            // Bilan net, mesuré sur au moins 2 s de jeu puis lissé
            if (lastAmount < 0)
            {
                lastAmount = amount;
                lastUT = ut;
            }
            else if (ut - lastUT >= 2.0)
            {
                double rate = (amount - lastAmount) / (ut - lastUT);
                NetRate = Mathf.Abs((float)NetRate) < 1e-9 ? rate : NetRate + (rate - NetRate) * 0.5;
                lastAmount = amount;
                lastUT = ut;
            }
            else if (ut < lastUT)
            {
                lastAmount = -1;   // retour en arrière (chargement)
            }

            float frac = capacity > 0 ? (float)(amount / capacity) : 0f;
            for (int i = 0; i < series.Length; i++)
            {
                Series s = series[i];
                double interval = Span(i) / Points;
                if (ut < s.LastUT)
                    s.LastUT = double.NegativeInfinity;
                if (ut - s.LastUT >= interval)
                {
                    s.Push(frac);
                    s.LastUT = ut;
                }
            }
        }

        private void Reset(System.Guid id)
        {
            vesselId = id;
            for (int i = 0; i < series.Length; i++)
                series[i] = new Series();
            lastAmount = -1;
            NetRate = 0;
        }

        /// <summary>Dessine la courbe de l'échelle choisie dans rect (OnGUI).</summary>
        public static void Draw(Rect rect, int scale)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.03f, 0.04f, 0.07f, 0.95f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            // Lignes de repère à 25, 50 et 75 %
            GUI.color = new Color(1f, 1f, 1f, 0.08f);
            for (int q = 1; q < 4; q++)
                GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height * q / 4f, rect.width, 1f), Texture2D.whiteTexture);

            Series s = series[scale];
            if (s.Count >= 1)
            {
                float step = rect.width / (Points - 1);
                // Les points les plus récents sont à droite ; la courbe se remplit depuis la droite
                int start = (s.Head - s.Count + Points) % Points;
                float x0 = rect.xMax - (s.Count - 1) * step;
                for (int i = 0; i < s.Count; i++)
                {
                    float v = s.Values[(start + i) % Points];
                    float x = x0 + i * step;
                    float y = rect.yMax - v * rect.height;
                    // Aire sous la courbe, puis le trait
                    GUI.color = ChargeColor(v) * new Color(1f, 1f, 1f, 0.25f);
                    GUI.DrawTexture(new Rect(x, y, Mathf.Max(1f, step), rect.yMax - y), Texture2D.whiteTexture);
                    GUI.color = ChargeColor(v);
                    GUI.DrawTexture(new Rect(x, y - 1f, Mathf.Max(1f, step), 2f), Texture2D.whiteTexture);
                }
            }
            GUI.color = old;
        }

        public static Color ChargeColor(float frac)
        {
            if (frac > 0.5f) return new Color(0.35f, 0.85f, 1f);
            if (frac > 0.2f) return new Color(1f, 0.85f, 0.2f);
            return new Color(1f, 0.35f, 0.25f);
        }
    }
}
