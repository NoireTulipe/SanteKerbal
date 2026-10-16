using UnityEngine;

namespace KerTech
{
    /// <summary>
    /// Panneau lumineux de l'assistant de vol : en haut de l'écran, façon tableau de bord (fond sombre,
    /// texte ambré), il résume l'opération en cours quand la tablette n'est pas ouverte sur Navigation.
    /// Désactivable sur la tablette (case « Panneau à l'écran »).
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class NavPanel : MonoBehaviour
    {
        private const float ShowResultSeconds = 8f;   // durée d'affichage du résultat (réussite ou arrêt)

        private bool uiHidden;
        private GUIStyle title, step, detail;
        private float styleScale = -1f;

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
            if (Event.current.type != EventType.Repaint || uiHidden || !DisplayPrefs.NavPanel || Tablet.ShowingNavigation)
                return;
            bool running = Navigator.Current != Navigator.Phase.Off;
            bool recent = !running && Time.time - Navigator.FinishedAt < ShowResultSeconds;
            if (!running && !recent)
                return;

            float s = GameSettings.UI_SCALE;
            if (Mathf.Abs(styleScale - s) > 0.01f)
                CreateStyles(s);

            float w = 460f * s, h = 78f * s;
            var rect = new Rect((Screen.width - w) / 2f, 70f * s, w, h);

            // Fond sombre et cadre ambré, comme un afficheur de tableau de bord
            Color old = GUI.color;
            GUI.color = new Color(0.02f, 0.02f, 0.03f, 0.82f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            Color amber = new Color(1f, 0.72f, 0.2f);
            GUI.color = amber * new Color(1, 1, 1, 0.8f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f * s, rect.width, 2f * s), Texture2D.whiteTexture);
            GUI.color = old;

            float pad = 10f * s;
            string head = running
                ? $"ASSISTANT DE VOL  -  ÉTAPE {Navigator.Step + 1}/{Navigator.StepNames.Length}"
                : Navigator.Succeeded ? "ASSISTANT DE VOL  -  TERMINÉ" : "ASSISTANT DE VOL  -  ARRÊTÉ";
            GUI.Label(new Rect(rect.x + pad, rect.y + 4f * s, w - 2 * pad, 16f * s), head, title);

            string main = running || Navigator.Succeeded ? Navigator.StepNames[Navigator.Step] : Navigator.Status;
            step.normal.textColor = !running && !Navigator.Succeeded ? new Color(1f, 0.45f, 0.3f)
                : Navigator.Succeeded ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.85f, 0.45f);
            GUI.Label(new Rect(rect.x + pad, rect.y + 20f * s, w - 2 * pad, 22f * s), main, step);

            // Barre de progression de l'étape
            var bar = new Rect(rect.x + pad, rect.y + 45f * s, w - 2 * pad, 7f * s);
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = Navigator.Succeeded ? new Color(0.4f, 1f, 0.5f) : amber;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Navigator.Progress, bar.height), Texture2D.whiteTexture);
            GUI.color = old;

            if (running || Navigator.Succeeded)
                GUI.Label(new Rect(rect.x + pad, rect.y + 55f * s, w - 2 * pad, 18f * s), Navigator.Detail, detail);
        }

        private void CreateStyles(float s)
        {
            styleScale = s;
            GUISkin skin = HighLogic.Skin;
            title = new GUIStyle(skin.label) { fontSize = Mathf.RoundToInt(10 * s), fontStyle = FontStyle.Bold, wordWrap = false, normal = { textColor = new Color(1f, 0.72f, 0.2f, 0.85f) } };
            step = new GUIStyle(skin.label) { fontSize = Mathf.RoundToInt(16 * s), fontStyle = FontStyle.Bold, wordWrap = false };
            detail = new GUIStyle(skin.label) { fontSize = Mathf.RoundToInt(12 * s), wordWrap = false, normal = { textColor = new Color(0.95f, 0.85f, 0.6f) } };
        }
    }
}
