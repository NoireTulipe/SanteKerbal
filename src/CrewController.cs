using System.Collections.Generic;
using KSP.UI.Screens.Flight;
using UnityEngine;

namespace SanteKerbal
{
    /// <summary>
    /// En vol : apparence des Kerbals de l'IVA selon leur santé, et règles pour les morts
    /// (pas de contrôle, pas d'EVA, pas de transfert).
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CrewController : MonoBehaviour
    {
        private readonly Dictionary<Kerbal, KerbalLook> looks = new Dictionary<Kerbal, KerbalLook>();
        private readonly List<Kerbal> stale = new List<Kerbal>();

        // Nombre de Kerbals sautés d'affilée en IVA (évite de tourner en rond)
        private int ivaSkips;

        public void Start()
        {
            GameEvents.onAttemptEva.Add(OnAttemptEva);
            GameEvents.onAttemptTransfer.Add(OnAttemptTransfer);
            GameEvents.onCrewTransferSelected.Add(OnCrewTransferSelected);
        }

        public void OnDestroy()
        {
            GameEvents.onAttemptEva.Remove(OnAttemptEva);
            GameEvents.onAttemptTransfer.Remove(OnAttemptTransfer);
            GameEvents.onCrewTransferSelected.Remove(OnCrewTransferSelected);
        }

        /// <summary>
        /// Après les animations de l'image : on applique l'apparence, ce qui couvre aussi
        /// les intérieurs recréés (changement de vaisseau, rechargement, retour d'IVA).
        /// </summary>
        public void LateUpdate()
        {
            if (HealthScenario.Instance == null || !Settings.Enabled)
                return;

            foreach (Vessel v in FlightGlobals.VesselsLoaded)
            {
                if (v == null || v.isEVA)
                    continue;
                foreach (ProtoCrewMember pcm in v.GetVesselCrew())
                {
                    KerbalHealth k = HealthScenario.Instance.Get(pcm);
                    if (k.Dead)
                        Mortality.Incapacitate(pcm);

                    Kerbal kerbal = pcm.KerbalRef;
                    // avatarTexture est créée dans Kerbal.Start : avant, le modèle n'est pas prêt
                    if (kerbal == null || kerbal.avatarTexture == null)
                        continue;
                    if (!looks.TryGetValue(kerbal, out KerbalLook look))
                    {
                        look = new KerbalLook(kerbal);
                        looks.Add(kerbal, look);
                    }
                    look.Apply(k);
                }
            }

            UpdateDeadPortraits();
            CheckIVACamera();

            // Oubli des modèles détruits (Unity : un objet détruit vaut null)
            stale.Clear();
            foreach (Kerbal kerbal in looks.Keys)
                if (kerbal == null)
                    stale.Add(kerbal);
            foreach (Kerbal kerbal in stale)
                looks.Remove(kerbal);
        }

        // Crâne intermittent : prochain « retour du signal » et fin de l'apparition, par portrait
        private readonly Dictionary<KerbalPortrait, float> nextFlicker = new Dictionary<KerbalPortrait, float>();
        private readonly Dictionary<KerbalPortrait, float> flickerEnd = new Dictionary<KerbalPortrait, float>();
        private const float FlickerMinInterval = 3f, FlickerMaxInterval = 8f;
        private const float FlickerMinDuration = 0.12f, FlickerMaxDuration = 0.35f;

        /// <summary>
        /// KSP brouille le portrait d'un Kerbal inactif (nos morts). Selon les réglages, on annule ce
        /// brouillage après son Update (on voit le corps), ou on le laisse en le perçant par instants
        /// (le visage du mort apparaît une fraction de seconde).
        /// </summary>
        private void UpdateDeadPortraits()
        {
            KerbalPortraitGallery gallery = KerbalPortraitGallery.Instance;
            if (gallery == null || gallery.Portraits == null)
                return;
            float now = Time.time;
            foreach (KerbalPortrait portrait in gallery.Portraits)
            {
                if (portrait == null || portrait.crewPcm == null || !Mortality.IsDead(portrait.crewPcm))
                    continue;

                bool show;
                if (Settings.DeathPortrait == DeathView.Body)
                {
                    show = true;
                }
                else if (Settings.DeathPortrait == DeathView.Static)
                {
                    show = false;
                }
                else
                {
                    if (!nextFlicker.TryGetValue(portrait, out float next))
                        next = nextFlicker[portrait] = now + Random.Range(FlickerMinInterval, FlickerMaxInterval);
                    if (now >= next)
                    {
                        flickerEnd[portrait] = now + Random.Range(FlickerMinDuration, FlickerMaxDuration);
                        nextFlicker[portrait] = now + Random.Range(FlickerMinInterval, FlickerMaxInterval);
                    }
                    show = flickerEnd.TryGetValue(portrait, out float end) && now < end;
                }

                if (show)
                    portrait.OverlayUpdate(false, (float)Kerbal.States.ALIVE);
            }
        }

        /// <summary>La vue IVA se place dans les yeux d'un Kerbal : jamais dans ceux d'un mort.</summary>
        private void CheckIVACamera()
        {
            CameraManager cam = CameraManager.Instance;
            Vessel v = FlightGlobals.ActiveVessel;
            if (cam == null || v == null || cam.currentCameraMode != CameraManager.CameraMode.IVA
                || cam.IVACameraActiveKerbal == null || !Mortality.IsDead(cam.IVACameraActiveKerbal.protoCrewMember))
            {
                ivaSkips = 0;
                return;
            }

            bool anyAlive = false;
            foreach (ProtoCrewMember pcm in v.GetVesselCrew())
                if (!Mortality.IsDead(pcm) && pcm.KerbalRef != null)
                    anyAlive = true;

            if (!anyAlive || ivaSkips >= v.GetCrewCount())
            {
                ivaSkips = 0;
                cam.SetCameraFlight();
                ScreenMessages.PostScreenMessage("Plus personne de vivant à bord : pas de vue intérieure.", 4f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            ivaSkips++;
            cam.NextCameraIVA();
        }

        private void OnAttemptEva(ProtoCrewMember pcm, Part part, Transform hatch)
        {
            if (!Mortality.IsDead(pcm))
                return;
            FlightEVA.fetch.overrideEVA = true;
            ScreenMessages.PostScreenMessage($"{pcm.name} est mort : il ne peut pas sortir.", 4f, ScreenMessageStyle.UPPER_CENTER);
        }

        private void OnAttemptTransfer(ProtoCrewMember pcm, Part part, CrewHatchController hatch)
        {
            if (!Mortality.IsDead(pcm))
                return;
            hatch.overrideTransfer = true;
            ScreenMessages.PostScreenMessage($"{pcm.name} est mort : il ne peut pas être transféré.", 4f, ScreenMessageStyle.UPPER_CENTER);
        }

        private void OnCrewTransferSelected(CrewTransfer.CrewTransferData data)
        {
            if (!Mortality.IsDead(data.crewMember))
                return;
            data.canTransfer = false;
            ScreenMessages.PostScreenMessage($"{data.crewMember.name} est mort : il ne peut pas être transféré.", 4f, ScreenMessageStyle.UPPER_CENTER);
        }
    }
}
