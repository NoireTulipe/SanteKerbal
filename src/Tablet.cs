using KSP.UI.Screens;
using UnityEngine;

namespace SanteKerbal
{
    /// <summary>
    /// Tablette de bord : un Kerbal tient une tablette (dessin de NoireTulipe) dont l'écran affiche
    /// l'interface du mod. Écran d'accueil avec des icônes, une page par sujet.
    /// Ouverte par le bouton cœur de la barre d'outils, en vol.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public partial class Tablet : MonoBehaviour
    {
        private const string TextureDir = "SanteKerbal/Textures/tablette/";
        private const string IconUrl = "SanteKerbal/Icons/toolbar";
        private const string LockId = "SanteKerbal_Tablette";
        private const int WindowId = 0x5A180;

        // Image de la tablette (1024 x 765) et position de son écran dans l'image (tools/tablet_screen.py)
        private const float ImageW = 1024f, ImageH = 765f;
        private static readonly Rect ScreenInImage = new Rect(136f, 157f, 641f, 445f);

        // Taille de base de la tablette à l'écran (avant échelle d'interface et zoom du joueur)
        private const float BaseWidth = 760f;
        private const float TopBarHeight = 26f;

        private enum Page { Home, Health, Crew, Stocks, Greenhouse, Recycler, Power }

        private static readonly Page[] HomeIcons = { Page.Health, Page.Crew, Page.Stocks, Page.Greenhouse, Page.Recycler, Page.Power };

        // Conservés pendant la session ; taille et position aussi entre les sessions (PluginData)
        private static bool visible;
        private static Vector2 position = new Vector2(-1f, -1f);   // -1 = centrée en bas au premier affichage
        private static float zoom = 1.3f;
        private static bool prefsLoaded;
        private static Page page = Page.Home;

        private static string PrefsPath =>
            System.IO.Path.Combine(KSPUtil.ApplicationRootPath, "GameData/SanteKerbal/PluginData/tablette.cfg");

        private ApplicationLauncherButton button;
        private bool uiHidden;
        private bool locked;
        private Vector2 scroll;

        private Texture2D tabletTex, thumbTex, dotTex;
        private readonly Texture2D[] iconTex = new Texture2D[7];

        private GUIStyle titleStyle, clockStyle, iconLabelStyle, smallButton, richLabel, barLabel, bpmLabel, sectionStyle;

        #region Cycle de vie et bouton

        public void Start()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(AddButton);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(RemoveButton);
            GameEvents.onHideUI.Add(OnHideUI);
            GameEvents.onShowUI.Add(OnShowUI);
            if (ApplicationLauncher.Ready)
                AddButton();
            LoadTextures();
            LoadPrefs();
        }

        /// <summary>Taille et position de la tablette, gardées d'une partie à l'autre.</summary>
        private static void LoadPrefs()
        {
            if (prefsLoaded)
                return;
            prefsLoaded = true;
            try
            {
                if (!System.IO.File.Exists(PrefsPath))
                    return;
                ConfigNode node = ConfigNode.Load(PrefsPath);
                node?.TryGetValue("zoom", ref zoom);
                node?.TryGetValue("x", ref position.x);
                node?.TryGetValue("y", ref position.y);
            }
            catch (System.Exception e)
            {
                Log.Warning("Préférences de la tablette illisibles : " + e.Message);
            }
        }

        private static void SavePrefs()
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefsPath));
                var node = new ConfigNode("TABLETTE");
                node.AddValue("zoom", zoom);
                node.AddValue("x", position.x);
                node.AddValue("y", position.y);
                node.Save(PrefsPath);
            }
            catch (System.Exception e)
            {
                Log.Warning("Préférences de la tablette non enregistrées : " + e.Message);
            }
        }

        public void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(AddButton);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(RemoveButton);
            GameEvents.onHideUI.Remove(OnHideUI);
            GameEvents.onShowUI.Remove(OnShowUI);
            RemoveButton();
            SetLock(false);
            MonitorSystem.BeepEnabled = false;
            SavePrefs();
        }

        private void OnHideUI() => uiHidden = true;
        private void OnShowUI() => uiHidden = false;

        private void LoadTextures()
        {
            tabletTex = Load("tablette");
            thumbTex = Load("tablette_pouce");
            iconTex[(int)Page.Health] = Load("sante");
            iconTex[(int)Page.Crew] = Load("personnel");
            iconTex[(int)Page.Stocks] = Load("inventaire");
            iconTex[(int)Page.Greenhouse] = Load("serre");
            iconTex[(int)Page.Recycler] = Load("recycleur");
            iconTex[(int)Page.Power] = Load("energie");
            dotTex = MakeDot(32);
        }

        private static Texture2D Load(string name)
        {
            Texture2D t = GameDatabase.Instance.GetTexture(TextureDir + name, false);
            if (t == null)
                Log.Warning("Texture de la tablette introuvable : " + TextureDir + name);
            return t;
        }

        /// <summary>Pastille ronde blanche avec bord doux (teintée en rouge à l'affichage).</summary>
        private static Texture2D MakeDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
                }
            tex.Apply();
            return tex;
        }

        private void AddButton()
        {
            if (button != null || ApplicationLauncher.Instance == null)
                return;
            Texture2D icon = GameDatabase.Instance.GetTexture(IconUrl, false) ?? Texture2D.whiteTexture;
            button = ApplicationLauncher.Instance.AddModApplication(
                () => visible = true, Close,
                null, null, null, null,
                ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
                icon);
            if (visible)
                button.SetTrue(false);
        }

        private void RemoveButton()
        {
            if (button == null)
                return;
            if (ApplicationLauncher.Instance != null)
                ApplicationLauncher.Instance.RemoveModApplication(button);
            button = null;
        }

        private void Close()
        {
            SavePrefs();
            visible = false;
            if (button != null)
                button.SetFalse(false);
            SetLock(false);
        }

        public void Update()
        {
            // Les bips ne sonnent que tablette ouverte sur la page Santé
            MonitorSystem.BeepEnabled = visible && !uiHidden && page == Page.Health;
        }

        /// <summary>Empêche les clics et la molette de passer au jeu quand la souris est sur la tablette.</summary>
        private void SetLock(bool on)
        {
            if (on == locked)
                return;
            if (on)
                InputLockManager.SetControlLock(ControlTypes.ALLBUTCAMERAS | ControlTypes.CAMERACONTROLS, LockId);
            else
                InputLockManager.RemoveControlLock(LockId);
            locked = on;
        }

        #endregion

        #region Dessin

        private float Scale => GameSettings.UI_SCALE * zoom;

        // Échelle de l'image en cours (tailles de base multipliées par s) et peau aux polices agrandies
        private float s = 1f;
        private float U(float v) => Mathf.Round(v * s);
        private GUISkin scaledSkin;
        private float skinScale = -1f;

        public void OnGUI()
        {
            if (!visible || uiHidden || HealthScenario.Instance == null || tabletTex == null)
            {
                SetLock(false);
                return;
            }
            // Tout est dessiné à sa taille réelle : dimensions et polices multipliées par s.
            // (Agrandir le dessin avec GUI.matrix coupait le texte : mesuré à une taille, affiché à une autre.)
            s = Scale;
            if (Mathf.Abs(skinScale - s) > 0.001f)
                CreateStyles();
            GUI.skin = scaledSkin;

            float w = U(BaseWidth), h = U(BaseWidth * ImageH / ImageW);
            if (position.x < 0 || position.y < 0)
                position = new Vector2((Screen.width - w) / 2f, Screen.height - h - 10f);   // centrée en bas
            // Toujours au moins en partie visible
            position.x = Mathf.Clamp(position.x, 50f - w, Screen.width - 50f);
            position.y = Mathf.Clamp(position.y, 0f, Screen.height - 50f);

            var rect = new Rect(position.x, position.y, w, h);
            rect = GUI.Window(WindowId, rect, DrawTablet, GUIContent.none, GUIStyle.none);
            position = new Vector2(rect.x, rect.y);

            SetLock(rect.Contains(Event.current.mousePosition));
        }

        private void DrawTablet(int id)
        {
            float w = U(BaseWidth), h = U(BaseWidth * ImageH / ImageW), k = w / ImageW;
            GUI.DrawTexture(new Rect(0, 0, w, h), tabletTex);

            var screen = new Rect(ScreenInImage.x * k, ScreenInImage.y * k, ScreenInImage.width * k, ScreenInImage.height * k);
            GUILayout.BeginArea(screen);
            DrawTopBar(screen.width);
            var content = new Rect(0, U(TopBarHeight), screen.width, screen.height - U(TopBarHeight));
            GUILayout.BeginArea(content);
            if (page == Page.Home)
                DrawHome(content);
            else
                DrawPage(content);
            GUILayout.EndArea();
            GUILayout.EndArea();

            // Le pouce du Kerbal reste devant l'écran
            if (thumbTex != null)
                GUI.DrawTexture(new Rect(0, 0, w, h), thumbTex);

            GUI.DragWindow();
        }

        private void DrawTopBar(float width)
        {
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.06f);
            float bar = U(TopBarHeight);
            GUI.DrawTexture(new Rect(0, 0, width, bar), Texture2D.whiteTexture);
            GUI.color = old;

            GUILayout.BeginHorizontal(GUILayout.Height(bar));
            if (page != Page.Home)
            {
                if (GUILayout.Button("<", smallButton, GUILayout.Width(U(30)), GUILayout.Height(bar - U(4))))
                {
                    page = Page.Home;
                    scroll = Vector2.zero;
                }
            }
            Vessel v = FlightGlobals.ActiveVessel;
            string title = page == Page.Home ? (v != null ? v.vesselName : "SantéKerbal") : PageTitle(page);
            GUILayout.Label(title, titleStyle, GUILayout.Height(bar - U(2)));
            GUILayout.FlexibleSpace();
            GUILayout.Label(KSPUtil.PrintDateCompact(Planetarium.GetUniversalTime(), true), clockStyle, GUILayout.Height(bar - U(2)));
            if (GUILayout.Button("-", smallButton, GUILayout.Width(U(24)), GUILayout.Height(bar - U(4))))
                zoom = Mathf.Max(0.6f, zoom - 0.1f);
            if (GUILayout.Button("+", smallButton, GUILayout.Width(U(24)), GUILayout.Height(bar - U(4))))
                zoom = Mathf.Min(1.6f, zoom + 0.1f);
            if (GUILayout.Button("X", smallButton, GUILayout.Width(U(24)), GUILayout.Height(bar - U(4))))
                Close();
            GUILayout.EndHorizontal();
        }

        private static string PageTitle(Page p)
        {
            switch (p)
            {
                case Page.Health: return "Santé";
                case Page.Crew: return "Personnel";
                case Page.Stocks: return "Inventaire";
                case Page.Greenhouse: return "Serre";
                case Page.Recycler: return "Recycleur";
                case Page.Power: return "Énergie";
                default: return "Accueil";
            }
        }

        /// <summary>Écran d'accueil : 6 icônes en 3 x 2, pastille rouge en cas de problème.</summary>
        private void DrawHome(Rect area)
        {
            const int cols = 3, rows = 2;
            float cellW = area.width / cols, cellH = area.height / rows;
            float icon = Mathf.Min(cellW, cellH - U(22)) * 0.72f;
            Vessel v = FlightGlobals.ActiveVessel;

            for (int i = 0; i < HomeIcons.Length; i++)
            {
                Page p = HomeIcons[i];
                float cx = (i % cols) * cellW, cy = (i / cols) * cellH;
                var iconRect = new Rect(cx + (cellW - icon) / 2f, cy + (cellH - icon - U(20)) / 2f, icon, icon);
                var labelRect = new Rect(cx, iconRect.yMax + U(2), cellW, U(20));
                var hit = new Rect(cx + U(4), cy + U(4), cellW - U(8), cellH - U(8));

                if (hit.Contains(Event.current.mousePosition))
                {
                    Color old = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.07f);
                    GUI.DrawTexture(hit, Texture2D.whiteTexture);
                    GUI.color = old;
                }
                if (iconTex[(int)p] != null)
                    GUI.DrawTexture(iconRect, iconTex[(int)p], ScaleMode.ScaleToFit);
                GUI.Label(labelRect, PageTitle(p), iconLabelStyle);

                if (v != null && HasAlert(p, v))
                {
                    float d = icon * 0.22f;
                    Color old = GUI.color;
                    GUI.color = new Color(0.95f, 0.15f, 0.1f);
                    GUI.DrawTexture(new Rect(iconRect.xMax - d * 0.8f, iconRect.y - d * 0.2f, d, d), dotTex);
                    GUI.color = old;
                }

                if (GUI.Button(hit, GUIContent.none, GUIStyle.none))
                {
                    page = p;
                    scroll = Vector2.zero;
                }
            }
        }

        /// <summary>
        /// Copie de la peau de KSP avec des polices et des barres de défilement à l'échelle s,
        /// et nos propres styles. Recréée quand l'échelle change.
        /// </summary>
        private void CreateStyles()
        {
            skinScale = s;
            if (scaledSkin != null)
                Destroy(scaledSkin);
            scaledSkin = Instantiate(HighLogic.Skin);
            int Font(GUIStyle st) => Mathf.Max(8, Mathf.RoundToInt((st.fontSize > 0 ? st.fontSize : 13) * s));
            foreach (GUIStyle st in new[] { scaledSkin.label, scaledSkin.button, scaledSkin.toggle, scaledSkin.box, scaledSkin.textField })
                st.fontSize = Font(st);
            scaledSkin.label.wordWrap = true;
            foreach (GUIStyle st in new[] { scaledSkin.verticalScrollbar, scaledSkin.verticalScrollbarThumb })
                if (st.fixedWidth > 0) st.fixedWidth = Mathf.Round(st.fixedWidth * s);

            GUISkin skin = scaledSkin;
            titleStyle = new GUIStyle(skin.label) { fontStyle = FontStyle.Bold, fontSize = (int)U(14), wordWrap = false, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.85f, 0.95f, 1f) } };
            clockStyle = new GUIStyle(skin.label) { fontSize = (int)U(11), wordWrap = false, alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(0.6f, 0.8f, 0.7f) } };
            iconLabelStyle = new GUIStyle(skin.label) { fontSize = (int)U(12), wordWrap = false, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, normal = { textColor = new Color(0.9f, 0.9f, 0.9f) } };
            smallButton = new GUIStyle(skin.button) { fontSize = (int)U(12), wordWrap = false, padding = new RectOffset((int)U(4), (int)U(4), (int)U(2), (int)U(2)) };
            richLabel = new GUIStyle(skin.label) { richText = true };
            barLabel = new GUIStyle(skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            bpmLabel = new GUIStyle(skin.label) { alignment = TextAnchor.MiddleRight, wordWrap = false, fontStyle = FontStyle.Bold };
            sectionStyle = new GUIStyle(skin.label) { richText = true, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.85f, 0.95f, 1f) } };
        }

        #endregion
    }
}
