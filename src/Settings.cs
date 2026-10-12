using UnityEngine;

namespace SanteKerbal
{
    /// <summary>Ce que montre le portrait d'un Kerbal mort.</summary>
    public enum DeathView
    {
        Body,    // « Dépouille visible » : corps figé, crâne, tête tombée
        Static,  // « Signal perdu » : portrait brouillé (comportement de KSP)
        Ghost    // « Signal fantôme » : brouillé, la dépouille apparaît par instants
    }

    internal static class DeathViews
    {
        // Libellés du menu, dans l'ordre du curseur
        public static readonly string[] Labels = { "Dépouille visible", "Signal perdu", "Signal fantôme" };
        private static readonly string[] Keys = { "body", "static", "ghost" };

        public static string Label(DeathView v) => Labels[(int)v];

        /// <summary>Accepte un libellé du menu ou une clé du .cfg (body, static, ghost).</summary>
        public static DeathView Parse(string s, DeathView fallback)
        {
            if (string.IsNullOrEmpty(s))
                return fallback;
            s = s.Trim();
            for (int i = 0; i < Labels.Length; i++)
                if (string.Equals(s, Labels[i], System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s, Keys[i], System.StringComparison.OrdinalIgnoreCase))
                    return (DeathView)i;
            return fallback;
        }
    }

    /// <summary>
    /// Réglages lus dans GameData/SanteKerbal/SanteKerbal.cfg (nœud SANTEKERBAL_SETTINGS).
    /// Les valeurs ci-dessous sont les valeurs par défaut si le fichier ou une clé manque.
    /// Les principaux réglages sont ensuite remplacés par ceux de la partie en cours
    /// (paramètres de difficulté, onglet SantéKerbal) : voir ApplyGame.
    /// </summary>
    internal static class Settings
    {
        public const string NodeName = "SANTEKERBAL_SETTINGS";

        // Interrupteurs (réglables par partie)
        public static bool Enabled = true;
        public static bool GEffects = true;
        public static bool HeatEffects = true;
        public static bool KscRest = true;
        public static bool VisualDecline = true;

        // Consommation, en unités par Kerbal et par jour de jeu
        public static double WaterPerDay = 3.0;
        public static double FoodPerDay = 1.0;

        // Durée de survie sans ressource, en jours de jeu
        public static double DaysWithoutWater = 3.0;
        public static double DaysWithoutFood = 21.0;

        // Points de santé regagnés par jour quand tous les besoins sont couverts
        public static double RegenPerDay = 5.0;

        // Accélérations
        public static double HighG = 6.0;               // seuil de G élevés
        public static double HighGGraceSeconds = 5.0;   // durée tolérée avant blessure
        public static double HighGDamagePerSecond = 2.0;
        public static double LethalG = 20.0;            // seuil de G mortels
        public static double LethalGSeconds = 1.0;      // durée au-delà de laquelle c'est mortel

        // Chaleur (température interne de la pièce, en Kelvin)
        public static double HeatThreshold = 330.0;
        public static double HeatDamagePerSecondPerKelvin = 0.01;

        // Dépérissement visible (portrait IVA)
        public static double VisualStartHealth = 80.0;  // en dessous, les effets commencent
        public static float MinAnimationSpeed = 0.3f;   // vitesse des animations à santé nulle
        public static float MaxHeadTilt = 25f;          // inclinaison de la tête (degrés) à santé nulle
        public static float DeadHeadTilt = 55f;         // inclinaison de la tête d'un mort
        public static Vector3 HeadTiltAxis = Vector3.right;
        public static string HeadBoneName = "bn_neck01"; // vide = objet "tête" fourni par KSP
        public static Color PaleColor = new Color(0.85f, 0.85f, 0.8f);
        public static float MaxPaleness = 0.7f;         // part de PaleColor dans le teint à santé nulle
        public static Color DeadColor = new Color(0.55f, 0.6f, 0.55f);
        public static string SkinTextureKeywords = "head,face"; // textures considérées comme la peau
        public static bool HideEyesWhenDead = true;     // masque les globes oculaires des morts
        public static DeathView DeathPortrait = DeathView.Body; // portrait d'un mort (voir DeathView)

        // Intervalle du log de diagnostic, en secondes réelles (0 = désactivé)
        public static float DebugLogInterval = 30f;

        // Boutons de test dans la fenêtre (modifier la santé, tuer)
        public static bool DebugTools = false;

        public static bool Loaded { get; private set; }

        /// <summary>Valeurs du .cfg, qui servent de réglages « Normal » et de base aux préréglages.</summary>
        public static class Defaults
        {
            public static double WaterPerDay = 3, FoodPerDay = 1, DaysWithoutWater = 3, DaysWithoutFood = 21, RegenPerDay = 5;
            public static double HighG = 6, LethalG = 20, HeatThreshold = 330;
            public static DeathView DeathPortrait = DeathView.Body;
            public static bool DebugTools = false;
        }

        public static void Load()
        {
            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes(NodeName);
            if (nodes == null || nodes.Length == 0)
            {
                Log.Warning("Nœud " + NodeName + " introuvable, utilisation des valeurs par défaut.");
                Loaded = true;
                return;
            }
            if (nodes.Length > 1)
                Log.Warning(nodes.Length + " nœuds " + NodeName + " trouvés, seul le premier est utilisé.");

            ConfigNode n = nodes[0];
            n.TryGetValue("waterPerDay", ref WaterPerDay);
            n.TryGetValue("foodPerDay", ref FoodPerDay);
            n.TryGetValue("daysWithoutWater", ref DaysWithoutWater);
            n.TryGetValue("daysWithoutFood", ref DaysWithoutFood);
            n.TryGetValue("regenPerDay", ref RegenPerDay);
            n.TryGetValue("highG", ref HighG);
            n.TryGetValue("highGGraceSeconds", ref HighGGraceSeconds);
            n.TryGetValue("highGDamagePerSecond", ref HighGDamagePerSecond);
            n.TryGetValue("lethalG", ref LethalG);
            n.TryGetValue("lethalGSeconds", ref LethalGSeconds);
            n.TryGetValue("heatThreshold", ref HeatThreshold);
            n.TryGetValue("heatDamagePerSecondPerKelvin", ref HeatDamagePerSecondPerKelvin);
            n.TryGetValue("visualStartHealth", ref VisualStartHealth);
            n.TryGetValue("minAnimationSpeed", ref MinAnimationSpeed);
            n.TryGetValue("maxHeadTilt", ref MaxHeadTilt);
            n.TryGetValue("deadHeadTilt", ref DeadHeadTilt);
            n.TryGetValue("headTiltAxis", ref HeadTiltAxis);
            n.TryGetValue("headBoneName", ref HeadBoneName);
            n.TryGetValue("paleColor", ref PaleColor);
            n.TryGetValue("maxPaleness", ref MaxPaleness);
            n.TryGetValue("deadColor", ref DeadColor);
            n.TryGetValue("skinTextureKeywords", ref SkinTextureKeywords);
            string deathView = null;
            if (n.TryGetValue("deathView", ref deathView))
                DeathPortrait = DeathViews.Parse(deathView, DeathPortrait);
            n.TryGetValue("hideEyesWhenDead", ref HideEyesWhenDead);
            n.TryGetValue("debugLogInterval", ref DebugLogInterval);
            n.TryGetValue("debugTools", ref DebugTools);

            Defaults.WaterPerDay = WaterPerDay;
            Defaults.FoodPerDay = FoodPerDay;
            Defaults.DaysWithoutWater = DaysWithoutWater;
            Defaults.DaysWithoutFood = DaysWithoutFood;
            Defaults.RegenPerDay = RegenPerDay;
            Defaults.HighG = HighG;
            Defaults.LethalG = LethalG;
            Defaults.HeatThreshold = HeatThreshold;
            Defaults.DeathPortrait = DeathPortrait;
            Defaults.DebugTools = DebugTools;

            Loaded = true;
            Log.Info("Réglages chargés : " + Describe());
        }

        private static string lastApplied;

        /// <summary>
        /// Remplace les réglages par ceux de la partie en cours (paramètres de difficulté).
        /// Appelé régulièrement : KSP ne prévient pas de façon fiable quand ces paramètres changent.
        /// N'écrit dans le log que si quelque chose a changé.
        /// </summary>
        public static void ApplyGame()
        {
            GameParameters p = HighLogic.CurrentGame?.Parameters;
            if (p == null)
                return;
            SKCrewParams crew;
            SKDangerParams danger;
            SKDisplayParams display;
            try
            {
                crew = p.CustomParams<SKCrewParams>();
                danger = p.CustomParams<SKDangerParams>();
                display = p.CustomParams<SKDisplayParams>();
            }
            catch (System.ArgumentException e)
            {
                if (lastApplied != "erreur")
                    Log.Error("Paramètres de la partie introuvables, réglages du .cfg utilisés : " + e.Message);
                lastApplied = "erreur";
                return;
            }
            if (crew == null || danger == null || display == null)
                return;

            Enabled = crew.enabled;
            DaysWithoutWater = Mathf.Max(0.1f, crew.daysWithoutWater);
            DaysWithoutFood = Mathf.Max(0.1f, crew.daysWithoutFood);
            RegenPerDay = Mathf.Max(0f, crew.regenPerDay);
            WaterPerDay = Defaults.WaterPerDay * Mathf.Max(0f, crew.consumption);
            FoodPerDay = Defaults.FoodPerDay * Mathf.Max(0f, crew.consumption);
            KscRest = crew.kscRest;

            GEffects = danger.gEffects;
            HighG = danger.highG;
            LethalG = Mathf.Max(danger.lethalG, danger.highG);
            HeatEffects = danger.heatEffects;
            HeatThreshold = danger.heatThreshold;

            VisualDecline = display.visualDecline;
            DeathPortrait = DeathViews.Parse(display.deathView, Defaults.DeathPortrait);
            DebugTools = display.debugTools;

            string described = Describe();
            if (described != lastApplied)
            {
                lastApplied = described;
                Log.Info("Réglages de la partie appliqués : " + described);
            }
        }

        public static string Describe()
        {
            return $"actif={Enabled}, G={GEffects}, chaleur={HeatEffects}, repos KSC={KscRest}, " +
                   $"dépérissement={VisualDecline}, portrait d'un mort={DeathViews.Label(DeathPortrait)}, " +
                   $"eau={WaterPerDay}/j, nourriture={FoodPerDay}/j, survie sans eau={DaysWithoutWater} j, " +
                   $"sans nourriture={DaysWithoutFood} j, régén={RegenPerDay}/j, " +
                   $"G élevés={HighG} (après {HighGGraceSeconds} s, {HighGDamagePerSecond}/s), " +
                   $"G mortels={LethalG} pendant {LethalGSeconds} s, " +
                   $"chaleur>{HeatThreshold} K ({HeatDamagePerSecondPerKelvin}/s/K), " +
                   $"visuel sous {VisualStartHealth} (vitesse min {MinAnimationSpeed}, tête {MaxHeadTilt}°/{DeadHeadTilt}° " +
                   $"axe {HeadTiltAxis}, os '{HeadBoneName}', pâleur {MaxPaleness}), " +
                   $"log diagnostic toutes les {DebugLogInterval} s, outils de test={DebugTools}";
        }
    }
}
