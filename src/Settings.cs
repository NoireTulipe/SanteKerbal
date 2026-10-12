using UnityEngine;

namespace SanteKerbal
{
    /// <summary>
    /// Réglages lus dans GameData/SanteKerbal/SanteKerbal.cfg (nœud SANTEKERBAL_SETTINGS).
    /// Les valeurs ci-dessous sont les valeurs par défaut si le fichier ou une clé manque.
    /// </summary>
    internal static class Settings
    {
        public const string NodeName = "SANTEKERBAL_SETTINGS";

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
        public static bool DeathPortraitStatic = true;  // true = portrait brouillé à la mort (comportement KSP)

        // Intervalle du log de diagnostic, en secondes réelles (0 = désactivé)
        public static float DebugLogInterval = 30f;

        // Boutons de test dans la fenêtre (modifier la santé, tuer)
        public static bool DebugTools = false;

        public static bool Loaded { get; private set; }

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
            n.TryGetValue("deathPortraitStatic", ref DeathPortraitStatic);
            n.TryGetValue("hideEyesWhenDead", ref HideEyesWhenDead);
            n.TryGetValue("debugLogInterval", ref DebugLogInterval);
            n.TryGetValue("debugTools", ref DebugTools);

            Loaded = true;
            Log.Info("Réglages chargés : " + Describe());
        }

        public static string Describe()
        {
            return $"eau={WaterPerDay}/j, nourriture={FoodPerDay}/j, survie sans eau={DaysWithoutWater} j, " +
                   $"sans nourriture={DaysWithoutFood} j, régén={RegenPerDay}/j, " +
                   $"G élevés={HighG} (après {HighGGraceSeconds} s, {HighGDamagePerSecond}/s), " +
                   $"G mortels={LethalG} pendant {LethalGSeconds} s, " +
                   $"chaleur>{HeatThreshold} K ({HeatDamagePerSecondPerKelvin}/s/K), " +
                   $"visuel sous {VisualStartHealth} (vitesse min {MinAnimationSpeed}, tête {MaxHeadTilt}°/{DeadHeadTilt}° " +
                   $"axe {HeadTiltAxis}, os '{HeadBoneName}', pâleur {MaxPaleness}, portrait brouillé={DeathPortraitStatic}), " +
                   $"log diagnostic toutes les {DebugLogInterval} s, outils de test={DebugTools}";
        }
    }
}
