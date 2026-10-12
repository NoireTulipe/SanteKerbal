using KSP.UI;
using KSP.UI.Screens.Flight;
using UnityEngine;

namespace SanteKerbal
{
    /// <summary>
    /// Électrocardiogramme dessiné juste au-dessus de chaque portrait (en bas à droite).
    /// Les portraits sont dans une zone découpée à leur hauteur : on dessine donc par-dessus
    /// toute l'interface, à la position du portrait recalculée à chaque image.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class PortraitMonitors : MonoBehaviour
    {
        private const float TraceHeight = 22f;   // hauteur du tracé, en pixels à l'échelle 100 %
        private const float Gap = 2f;

        private readonly Vector3[] corners = new Vector3[4];
        private bool uiHidden;
        private GUIStyle bpmStyle;

        public void Start()
        {
            GameEvents.onHideUI.Add(OnHideUI);
            GameEvents.onShowUI.Add(OnShowUI);
        }

        public void OnDestroy()
        {
            GameEvents.onHideUI.Remove(OnHideUI);
            GameEvents.onShowUI.Remove(OnShowUI);
        }

        private void OnHideUI() => uiHidden = true;
        private void OnShowUI() => uiHidden = false;

        public void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || uiHidden || !Settings.PortraitMonitor || !MonitorSystem.Active)
                return;
            KerbalPortraitGallery gallery = KerbalPortraitGallery.Instance;
            Camera uiCam = UIMasterController.Instance != null ? UIMasterController.Instance.uiCamera : null;
            if (gallery == null || gallery.Portraits == null || uiCam == null)
                return;

            if (bpmStyle == null)
            {
                bpmStyle = new GUIStyle(HighLogic.Skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white },
                    padding = new RectOffset(0, 3, 0, 0)
                };
            }

            float scale = GameSettings.UI_SCALE;
            foreach (KerbalPortrait portrait in gallery.Portraits)
            {
                if (portrait == null || portrait.crewPcm == null || portrait.portrait == null
                    || !portrait.gameObject.activeInHierarchy)
                    continue;
                HeartMonitor m = MonitorSystem.Get(portrait.crewPcm.name);
                if (m == null)
                    continue;

                if (!ScreenRect(portrait.portrait.rectTransform, uiCam, out Rect img))
                    continue;
                float h = TraceHeight * scale;
                var rect = new Rect(img.x, img.y - h - Gap * scale, img.width, h);

                KerbalHealth k = HealthScenario.Instance.Get(portrait.crewPcm);
                m.Draw(rect, MonitorSystem.TraceColor(k));
                GUI.Label(rect, k.Dead ? "0" : m.Bpm.ToString("F0"), bpmStyle);
            }
        }

        /// <summary>Rectangle à l'écran (coordonnées IMGUI, origine en haut à gauche) d'un élément d'interface.</summary>
        private bool ScreenRect(RectTransform rt, Camera uiCam, out Rect rect)
        {
            rt.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(uiCam, corners[0]); // bas gauche
            Vector2 max = RectTransformUtility.WorldToScreenPoint(uiCam, corners[2]); // haut droit
            rect = new Rect(min.x, Screen.height - max.y, max.x - min.x, max.y - min.y);
            return rect.width > 4f && rect.height > 4f;
        }
    }
}
