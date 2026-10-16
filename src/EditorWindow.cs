using System.Collections.Generic;
using KSP.UI;
using KSP.UI.Screens;
using UnityEngine;

namespace KerTech
{
    /// <summary>
    /// Dans le VAB et le SPH : bouton de barre d'outils et fenêtre montrant les stocks d'eau et de
    /// nourriture du vaisseau en construction, ses équipements et son autonomie.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class EditorWindow : MonoBehaviour
    {
        private const string IconUrl = "KerTech/KerTab/Textures/tablette-icone";
        private const int WindowId = 0x5A17F;
        private const float RefreshSeconds = 0.5f;

        private static Rect windowRect = new Rect(300, 120, 360, 0);
        private static bool visible;

        private ApplicationLauncherButton button;

        // Valeurs recalculées toutes les RefreshSeconds (le calcul parcourt toutes les pièces)
        private float nextRefresh;
        private double water, waterMax, food, foodMax;
        private int seats, assigned;
        private Equipment equipment = new Equipment();

        public void Start()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(AddButton);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(RemoveButton);
            if (ApplicationLauncher.Ready)
                AddButton();
        }

        public void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(AddButton);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(RemoveButton);
            RemoveButton();
        }

        private void AddButton()
        {
            if (button != null || ApplicationLauncher.Instance == null)
                return;
            Texture2D icon = GameDatabase.Instance.GetTexture(IconUrl, false) ?? Texture2D.whiteTexture;
            button = ApplicationLauncher.Instance.AddModApplication(
                () => visible = true, () => visible = false,
                null, null, null, null,
                ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH,
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

        public void Update()
        {
            if (!visible || Time.realtimeSinceStartup < nextRefresh)
                return;
            nextRefresh = Time.realtimeSinceStartup + RefreshSeconds;
            Refresh();
        }

        private void Refresh()
        {
            water = waterMax = food = foodMax = 0;
            seats = assigned = 0;
            List<Part> parts = EditorLogic.fetch?.ship?.parts;
            if (parts == null)
            {
                equipment = new Equipment();
                return;
            }

            foreach (Part p in parts)
            {
                seats += p.CrewCapacity;
                AddResource(p, LifeResources.Water, ref water, ref waterMax);
                AddResource(p, LifeResources.Food, ref food, ref foodMax);
            }
            equipment = Equipment.FromParts(parts);

            // Équipage assigné dans l'onglet « Équipage » de l'éditeur
            try
            {
                VesselCrewManifest manifest = CrewAssignmentDialog.Instance != null
                    ? CrewAssignmentDialog.Instance.GetManifest(false)
                    : ShipConstruction.ShipManifest;
                assigned = manifest != null ? manifest.CrewCount : 0;
            }
            catch (System.Exception e)
            {
                // L'onglet Équipage n'est pas toujours initialisé : on retombera sur le manifeste au prochain calcul
                assigned = ShipConstruction.ShipManifest != null ? ShipConstruction.ShipManifest.CrewCount : 0;
                Log.Warning("Lecture de l'équipage assigné impossible : " + e.Message);
            }
        }

        private static void AddResource(Part p, string name, ref double amount, ref double max)
        {
            PartResource r = p.Resources?.Get(name);
            if (r == null)
                return;
            amount += r.amount;
            max += r.maxAmount;
        }

        public void OnGUI()
        {
            if (!visible)
                return;
            GUI.skin = HighLogic.Skin;
            windowRect = GUILayout.Window(WindowId, windowRect, DrawWindow, "KerTab : conception", GUILayout.Width(360));
        }

        private void DrawWindow(int id)
        {
            if (!Settings.Enabled)
                GUILayout.Label("KerTech est désactivé pour cette partie : les stocks sont indicatifs.");
            if (seats == 0)
            {
                GUILayout.Label("Aucune place habitable sur ce vaisseau.");
            }
            else
            {
                GUILayout.Label($"Équipage assigné : {assigned} / {seats} places");
                GUILayout.Space(4);

                GUILayout.Label($"Avec l'équipage assigné ({assigned}) :");
                SupplyPanel.Draw(water, waterMax, food, foodMax, assigned, equipment);

                if (assigned != seats)
                {
                    GUILayout.Space(4);
                    GUILayout.Label($"Avec toutes les places occupées ({seats}) :");
                    LifeSupportSim.Autonomy(water, food, seats, equipment, out double wd, out double fd);
                    GUILayout.Label($"   eau : {LifeSupportSim.FormatDays(wd)}, nourriture : {LifeSupportSim.FormatDays(fd)}");
                }
            }

            GUILayout.Space(4);
            GUILayout.Label("(jours de jeu ; électricité supposée suffisante)");
            if (GUILayout.Button("Fermer"))
            {
                visible = false;
                if (button != null)
                    button.SetFalse(false);
            }
            GUI.DragWindow();
        }
    }
}
