using UnityEngine;

namespace KerTech
{
    /// <summary>
    /// Sons du moniteur cardiaque, générés par programme (aucun fichier audio) :
    /// bip de battement et bip continu de la mort.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class MonitorSounds : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const float Frequency = 1000f;

        private static MonitorSounds instance;
        private AudioSource source;
        private AudioClip beep;
        private AudioClip flatline;

        public void Awake()
        {
            instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0f;      // son « dans la tête », pas positionné en 3D
            source.playOnAwake = false;
            beep = Tone("KerTech_bip", 0.08f);
            flatline = Tone("KerTech_flatline", 2.5f);
            Mortality.Killed += OnKilled;
        }

        public void OnDestroy()
        {
            Mortality.Killed -= OnKilled;
            if (instance == this)
                instance = null;
        }

        private static float Volume => 0.35f * GameSettings.UI_VOLUME * GameSettings.MASTER_VOLUME;

        /// <summary>Bip de battement ; pitch légèrement différent selon le Kerbal.</summary>
        public static void Beep(float pitch)
        {
            if (instance == null || !Settings.MonitorSound)
                return;
            instance.source.pitch = pitch;
            instance.source.PlayOneShot(instance.beep, Volume);
        }

        private void OnKilled(string kerbalName, Vessel vessel)
        {
            if (!Settings.MonitorSound || vessel == null || vessel != FlightGlobals.ActiveVessel)
                return;
            source.pitch = 1f;
            source.PlayOneShot(flatline, Volume);
        }

        /// <summary>Sinus avec attaque et extinction douces (évite les clics).</summary>
        private static AudioClip Tone(string name, float seconds)
        {
            int n = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[n];
            float fade = 0.008f * SampleRate;
            for (int i = 0; i < n; i++)
            {
                float env = Mathf.Min(1f, Mathf.Min(i / fade, (n - 1 - i) / fade));
                data[i] = Mathf.Sin(2f * Mathf.PI * Frequency * i / SampleRate) * env;
            }
            AudioClip clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
