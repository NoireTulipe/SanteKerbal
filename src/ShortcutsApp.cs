using System;
using System.Collections.Generic;
using System.Reflection;
using KSP.UI;
using KSP.UI.Screens;
using UnityEngine;

namespace KerTech
{
    /// <summary>
    /// Application « Raccourcis » de la tablette : rassemble les boutons que les autres mods ont placés
    /// dans la barre d'outils de KSP. Un clic fait la même chose que leur bouton (ouvre ou ferme leur
    /// fenêtre). Fonctionne avec n'importe quel mod, sans rien lui demander.
    /// </summary>
    public class ShortcutsApp : KerTabApp
    {
        public const string AppId = "kertech.raccourcis";
        public static string Icon = "KerTech/KerTab/Textures/tablette-icone";

        public override string Id => AppId;
        public override string Title => "Raccourcis";
        public override string IconUrl => Icon;
        public override int Order => 950;

        public override bool IsVisible(Vessel v) => ModButtons().Count > 0;
        public override int Badge(Vessel v) => ModButtons().Count;

        // Liste privée des boutons des mods dans la barre d'outils de KSP (lue par réflexion)
        private static readonly FieldInfo modListField =
            typeof(ApplicationLauncher).GetField("appListMod", BindingFlags.Instance | BindingFlags.NonPublic);
        private static bool warned;

        /// <summary>Boutons des autres mods visibles dans la scène (le bouton de la tablette est exclu).</summary>
        public static List<ApplicationLauncherButton> ModButtons()
        {
            var result = new List<ApplicationLauncherButton>();
            if (ApplicationLauncher.Instance == null)
                return result;
            if (modListField == null)
            {
                if (!warned)
                    Log.Warning("Raccourcis : liste des boutons de mods introuvable dans cette version de KSP");
                warned = true;
                return result;
            }
            if (!(modListField.GetValue(ApplicationLauncher.Instance) is List<ApplicationLauncherButton> list))
                return result;
            Assembly self = typeof(ShortcutsApp).Assembly;
            foreach (ApplicationLauncherButton b in list)
            {
                if (b == null || b.onTrue == null || b.onTrue.Method.DeclaringType?.Assembly == self)
                    continue;
                result.Add(b);
            }
            return result;
        }

        /// <summary>Nom du mod qui a créé le bouton (celui de son assembly), faute de nom affiché.</summary>
        private static string ModName(ApplicationLauncherButton b)
        {
            try
            {
                Type t = b.onTrue.Method.DeclaringType;
                return t?.Assembly.GetName().Name ?? "?";
            }
            catch (Exception)
            {
                return "?";
            }
        }

        public override void Draw(KerTabUI ui, Vessel v)
        {
            List<ApplicationLauncherButton> buttons = ModButtons();
            if (buttons.Count == 0)
            {
                GUILayout.Label("Aucun autre mod n'a de bouton dans la barre d'outils.", ui.Label);
                return;
            }
            GUILayout.Label("Les boutons des autres mods de la barre d'outils. Un clic ouvre ou ferme leur fenêtre.", ui.RichLabel);
            GUILayout.Space(ui.U(6));

            const int cols = 4;
            float cell = Mathf.Floor(ui.Width / cols);
            float icon = Mathf.Min(ui.U(48), cell * 0.6f);
            for (int i = 0; i < buttons.Count; i += cols)
            {
                Rect row = GUILayoutUtility.GetRect(ui.Width, icon + ui.U(26), GUILayout.Width(ui.Width));
                for (int c = 0; c < cols && i + c < buttons.Count; c++)
                {
                    ApplicationLauncherButton b = buttons[i + c];
                    var cellRect = new Rect(row.x + c * cell, row.y, cell, row.height);
                    bool on = b.toggleButton != null && b.toggleButton.CurrentState == UIRadioButton.State.True;

                    Color old = GUI.color;
                    GUI.color = on ? new Color(0.3f, 0.6f, 1f, 0.25f)
                        : cellRect.Contains(Event.current.mousePosition) ? new Color(1f, 1f, 1f, 0.07f) : Color.clear;
                    GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
                    GUI.color = old;

                    Texture tex = b.sprite != null ? b.sprite.texture : null;
                    var iconRect = new Rect(cellRect.x + (cell - icon) / 2f, cellRect.y + ui.U(2), icon, icon);
                    if (tex != null)
                        GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit);
                    GUI.Label(new Rect(cellRect.x, iconRect.yMax + ui.U(2), cell, ui.U(20)), ModName(b), ui.Label);

                    if (GUI.Button(cellRect, GUIContent.none, GUIStyle.none))
                    {
                        // Même effet qu'un clic sur le bouton d'origine
                        if (on) b.SetFalse(true);
                        else b.SetTrue(true);
                    }
                }
            }
        }
    }
}
