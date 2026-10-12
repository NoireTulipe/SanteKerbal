using UnityEngine;

namespace SanteKerbal
{
    /// <summary>
    /// Moniteur cardiaque d'un Kerbal : pouls calculé à partir de son état (G, chaleur, manques, santé)
    /// et tracé d'électrocardiogramme qui défile. Purement visuel, rien n'est sauvegardé.
    /// </summary>
    internal class HeartMonitor
    {
        public const int Width = 140;          // largeur du tracé, en pixels (= nombre d'échantillons)
        public const int Height = 26;
        private const float ScrollSpeed = 70f; // pixels par seconde

        private readonly float[] trace = new float[Width];
        private int head;                      // prochain échantillon écrit (tampon circulaire)
        private float pending;                 // fraction de pixel restant à avancer
        private float phase;                   // position dans le battement en cours (0..1)
        private float beatLength = 1f;         // durée du battement en cours, en secondes
        private bool skipBeat;                 // battement manqué (arythmie)
        private readonly float noiseSeed = Random.value * 100f;

        public float Bpm { get; private set; } = 70f;
        public bool Flat { get; private set; }

        /// <summary>Calcule le pouls visé à partir de l'état du Kerbal.</summary>
        public static float TargetBpm(KerbalHealth k, double g, double cabinTemp, out bool irregular)
        {
            irregular = false;
            if (k.Dead)
                return 0f;
            if (k.Asleep)
                return 8f;

            float bpm = 70f;
            bpm += Mathf.Clamp((float)(g - 1.5) * 10f, 0f, 80f);
            // Le cœur s'accélère un peu avant que la chaleur ne blesse
            bpm += Mathf.Clamp((float)(cabinTemp - (Settings.HeatThreshold - 30.0)) * 0.4f, 0f, 60f);
            if (k.LackWater) bpm += 25f;
            if (k.LackFood) bpm += 10f;
            if (k.Health < 60) bpm += (float)(60 - k.Health) * 0.7f;
            irregular = k.Health < 25;
            return Mathf.Clamp(bpm, 40f, 190f);
        }

        /// <summary>
        /// Fait avancer le tracé de dt secondes. Renvoie true si un battement vient de commencer
        /// (pour le bip sonore).
        /// </summary>
        public bool Advance(float dt, float targetBpm, bool irregular, bool flat)
        {
            Flat = flat;
            // Le pouls affiché rejoint progressivement la cible (10 bpm par seconde)
            Bpm = flat ? 0f : Mathf.MoveTowards(Bpm, targetBpm, 10f * dt);

            bool beat = false;
            pending += dt * ScrollSpeed;
            while (pending >= 1f)
            {
                pending -= 1f;
                float value = 0f;
                if (!flat && Bpm > 0f)
                {
                    phase += 1f / (ScrollSpeed * beatLength);
                    if (phase >= 1f)
                    {
                        phase -= 1f;
                        StartBeat(irregular);
                        if (!skipBeat)
                            beat = true;
                    }
                    value = skipBeat ? 0f : Wave(phase);
                    value += (Mathf.PerlinNoise(Time.time * 3f, noiseSeed) - 0.5f) * 0.05f;
                }
                trace[head] = value;
                head = (head + 1) % Width;
            }
            return beat;
        }

        private void StartBeat(bool irregular)
        {
            beatLength = 60f / Mathf.Max(Bpm, 1f);
            skipBeat = false;
            if (irregular)
            {
                beatLength *= Random.Range(0.6f, 1.4f);
                skipBeat = Random.value < 0.12f;
            }
        }

        /// <summary>Forme d'un battement (onde P, complexe QRS, onde T), amplitude environ -0,3..1.</summary>
        private static float Wave(float p)
        {
            float v = 0f;
            v += 0.12f * Bump(p, 0.10f, 0.035f);   // onde P
            v -= 0.12f * Bump(p, 0.205f, 0.010f);  // Q
            v += 1.00f * Bump(p, 0.225f, 0.012f);  // R
            v -= 0.28f * Bump(p, 0.245f, 0.012f);  // S
            v += 0.25f * Bump(p, 0.42f, 0.055f);   // onde T
            return v;
        }

        private static float Bump(float x, float center, float width)
        {
            float d = (x - center) / width;
            return Mathf.Exp(-d * d);
        }

        /// <summary>Dessine le tracé dans rect (à appeler pendant OnGUI).</summary>
        public void Draw(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.02f, 0.06f, 0.03f, 0.95f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = color;
            float mid = rect.y + rect.height * 0.62f;
            float amp = rect.height * 0.55f;
            float xScale = rect.width / Width;
            float prevY = mid;
            for (int i = 0; i < Width; i++)
            {
                // Le plus ancien à gauche, le plus récent à droite
                float y = mid - trace[(head + i) % Width] * amp;
                float top = Mathf.Min(prevY, y);
                float h = Mathf.Max(1.5f, Mathf.Abs(prevY - y) + 1f);
                GUI.DrawTexture(new Rect(rect.x + i * xScale, top, Mathf.Max(1f, xScale), h), Texture2D.whiteTexture);
                prevY = y;
            }
            GUI.color = old;
        }
    }
}
