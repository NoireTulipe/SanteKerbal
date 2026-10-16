using System;
using System.IO;

namespace KerTech
{
    /// <summary>
    /// Préférences d'affichage communes à toutes les parties (goûts du joueur, pas règles du jeu) :
    /// dépérissement visible, portrait d'un mort, moniteur, sons, courbe électrique.
    /// Elles restent modifiables dans le menu de difficulté (colonne « Affichage et tests »), mais sont
    /// enregistrées aussitôt dans PluginData/preferences.cfg : KSP n'enregistre les paramètres d'une
    /// partie qu'à sa prochaine sauvegarde, et un changement fait juste avant de quitter était perdu.
    /// </summary>
    internal static class DisplayPrefs
    {
        public static bool VisualDecline = true;
        public static string DeathView;
        public static bool Monitor = true;
        public static bool PortraitMonitor = true;
        public static bool MonitorSound = true;
        public static bool PowerGraph = true;
        public static bool NavPanel = true;   // panneau lumineux de l'assistant de vol (réglé sur la tablette)

        private static bool loaded;
        private static Game syncedGame;
        private static string snapshot;

        private static string PrefsPath =>
            Path.Combine(KSPUtil.ApplicationRootPath, "GameData/KerTech/PluginData/preferences.cfg");

        private static string Snapshot() =>
            $"{VisualDecline}|{DeathView}|{Monitor}|{PortraitMonitor}|{MonitorSound}|{PowerGraph}";

        private static string Snapshot(SKDisplayParams d) =>
            $"{d.visualDecline}|{d.deathView}|{d.monitor}|{d.portraitMonitor}|{d.monitorSound}|{d.powerGraph}";

        /// <summary>
        /// Synchronise les préférences et les paramètres de la partie : à l'arrivée dans une partie, les
        /// préférences remplacent les valeurs de la partie ; ensuite, un changement fait dans le menu
        /// devient la nouvelle préférence et est enregistré immédiatement.
        /// </summary>
        public static void Sync(SKDisplayParams d)
        {
            Load();
            Game game = HighLogic.CurrentGame;
            if (!ReferenceEquals(game, syncedGame))
            {
                syncedGame = game;
                d.visualDecline = VisualDecline;
                d.deathView = DeathView;
                d.monitor = Monitor;
                d.portraitMonitor = PortraitMonitor;
                d.monitorSound = MonitorSound;
                d.powerGraph = PowerGraph;
                snapshot = Snapshot();
                return;
            }
            if (Snapshot(d) == snapshot)
                return;

            VisualDecline = d.visualDecline;
            DeathView = d.deathView;
            Monitor = d.monitor;
            PortraitMonitor = d.portraitMonitor;
            MonitorSound = d.monitorSound;
            PowerGraph = d.powerGraph;
            snapshot = Snapshot();
            Save();
        }

        private static void Load()
        {
            if (loaded)
                return;
            loaded = true;
            DeathView = DeathViews.Label(Settings.Defaults.DeathPortrait);
            try
            {
                if (!File.Exists(PrefsPath))
                    return;
                ConfigNode n = ConfigNode.Load(PrefsPath);
                if (n == null)
                    return;
                n.TryGetValue("visualDecline", ref VisualDecline);
                string view = null;
                if (n.TryGetValue("deathView", ref view))
                    DeathView = DeathViews.Label(DeathViews.Parse(view, Settings.Defaults.DeathPortrait));
                n.TryGetValue("monitor", ref Monitor);
                n.TryGetValue("portraitMonitor", ref PortraitMonitor);
                n.TryGetValue("monitorSound", ref MonitorSound);
                n.TryGetValue("powerGraph", ref PowerGraph);
                n.TryGetValue("navPanel", ref NavPanel);
                Log.Info("Préférences d'affichage lues : " + Snapshot());
            }
            catch (Exception e)
            {
                Log.Warning("Préférences d'affichage illisibles, valeurs par défaut : " + e.Message);
            }
        }

        public static void SetNavPanel(bool on)
        {
            Load();
            if (NavPanel == on)
                return;
            NavPanel = on;
            Save();
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PrefsPath));
                var n = new ConfigNode("KERTECH_PREFERENCES");
                n.AddValue("visualDecline", VisualDecline);
                n.AddValue("deathView", DeathView);
                n.AddValue("monitor", Monitor);
                n.AddValue("portraitMonitor", PortraitMonitor);
                n.AddValue("monitorSound", MonitorSound);
                n.AddValue("powerGraph", PowerGraph);
                n.AddValue("navPanel", NavPanel);
                n.Save(PrefsPath);
                Log.Info("Préférences d'affichage enregistrées : " + Snapshot());
            }
            catch (Exception e)
            {
                Log.Warning("Préférences d'affichage non enregistrées : " + e.Message);
            }
        }
    }
}
