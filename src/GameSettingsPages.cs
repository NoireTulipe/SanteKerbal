using System.Reflection;

namespace SanteKerbal
{
    /// <summary>
    /// Onglet « SantéKerbal » des paramètres de difficulté : réglages propres à chaque partie.
    /// Les valeurs initiales viennent de SanteKerbal.cfg (préréglage « Normal »).
    /// </summary>
    internal static class SKSection
    {
        public const string Id = "SanteKerbal";
        public const string Display = "SantéKerbal";

        public static bool ModEnabled(GameParameters parameters)
        {
            SKCrewParams crew = parameters?.CustomParams<SKCrewParams>();
            return crew == null || crew.enabled;
        }
    }

    /// <summary>Colonne 1 : bien-être de l'équipage.</summary>
    public class SKCrewParams : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomParameterUI("Activer la santé des Kerbals",
            toolTip = "Désactivé : pas de consommation, pas de dégâts, pas d'effets. Les ressources restent dans les pièces.")]
        public bool enabled = true;

        [GameParameters.CustomFloatParameterUI("Survie sans eau (jours)", minValue = 1f, maxValue = 10f, stepCount = 19,
            displayFormat = "F1", toolTip = "Jours pour passer de 100 à 0 de santé sans eau.")]
        public float daysWithoutWater = (float)Settings.Defaults.DaysWithoutWater;

        [GameParameters.CustomFloatParameterUI("Survie sans nourriture (jours)", minValue = 5f, maxValue = 60f, stepCount = 56,
            displayFormat = "F0", toolTip = "Jours pour passer de 100 à 0 de santé sans nourriture.")]
        public float daysWithoutFood = (float)Settings.Defaults.DaysWithoutFood;

        [GameParameters.CustomFloatParameterUI("Récupération (points/jour)", minValue = 0f, maxValue = 20f, stepCount = 41,
            displayFormat = "F1", toolTip = "Santé regagnée par jour quand l'eau et la nourriture ne manquent pas.")]
        public float regenPerDay = (float)Settings.Defaults.RegenPerDay;

        [GameParameters.CustomFloatParameterUI("Consommation", minValue = 0.5f, maxValue = 2f, stepCount = 16,
            asPercentage = true, toolTip = "Multiplie la consommation d'eau et de nourriture de chaque Kerbal.")]
        public float consumption = 1f;

        [GameParameters.CustomParameterUI("Repos à la KSC",
            toolTip = "Les Kerbals rentrés récupèrent leur santé au Complexe des astronautes.")]
        public bool kscRest = true;

        [GameParameters.CustomParameterUI("Hibernation",
            toolTip = "Caissons d'hibernation : les dormeurs ne mangent ni ne boivent, mais consomment de l'électricité.")]
        public bool hibernation = true;

        public override string Title => "Bien-être de l'équipage";
        public override string DisplaySection => SKSection.Display;
        public override string Section => SKSection.Id;
        public override int SectionOrder => 1;
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override bool HasPresets => true;

        public override bool Interactible(MemberInfo member, GameParameters parameters)
        {
            return member.Name == nameof(enabled) || SKSection.ModEnabled(parameters);
        }

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    daysWithoutWater = 6f; daysWithoutFood = 30f; regenPerDay = 10f; consumption = 0.75f;
                    break;
                case GameParameters.Preset.Moderate:
                    daysWithoutWater = 2.5f; daysWithoutFood = 18f; regenPerDay = 3f; consumption = 1f;
                    break;
                case GameParameters.Preset.Hard:
                    daysWithoutWater = 2f; daysWithoutFood = 14f; regenPerDay = 2f; consumption = 1.25f;
                    break;
                default: // Normal : valeurs du .cfg
                    daysWithoutWater = (float)Settings.Defaults.DaysWithoutWater;
                    daysWithoutFood = (float)Settings.Defaults.DaysWithoutFood;
                    regenPerDay = (float)Settings.Defaults.RegenPerDay;
                    consumption = 1f;
                    break;
            }
            enabled = true;
            kscRest = true;
            hibernation = true;
        }
    }

    /// <summary>Colonne 2 : accélérations et chaleur.</summary>
    public class SKDangerParams : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomParameterUI("Blessures dues aux G", toolTip = "Les fortes accélérations prolongées blessent, les extrêmes tuent.")]
        public bool gEffects = true;

        [GameParameters.CustomFloatParameterUI("G élevés (blessures)", minValue = 3f, maxValue = 15f, stepCount = 25,
            displayFormat = "F1", toolTip = "Au-delà, pendant plus de quelques secondes, l'équipage est blessé.")]
        public float highG = (float)Settings.Defaults.HighG;

        [GameParameters.CustomFloatParameterUI("G mortels", minValue = 8f, maxValue = 40f, stepCount = 33,
            displayFormat = "F0", toolTip = "Au-delà, pendant une seconde, l'équipage meurt.")]
        public float lethalG = (float)Settings.Defaults.LethalG;

        [GameParameters.CustomParameterUI("Blessures dues à la chaleur", toolTip = "Une cabine trop chaude dégrade la santé.")]
        public bool heatEffects = true;

        [GameParameters.CustomFloatParameterUI("Température max de la cabine (K)", minValue = 300f, maxValue = 500f, stepCount = 41,
            displayFormat = "F0", toolTip = "Au-delà, l'équipage perd de la santé, d'autant plus vite qu'il fait chaud.")]
        public float heatThreshold = (float)Settings.Defaults.HeatThreshold;

        public override string Title => "Dangers";
        public override string DisplaySection => SKSection.Display;
        public override string Section => SKSection.Id;
        public override int SectionOrder => 2;
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override bool HasPresets => true;

        public override bool Interactible(MemberInfo member, GameParameters parameters)
        {
            if (!SKSection.ModEnabled(parameters))
                return false;
            if (member.Name == nameof(highG) || member.Name == nameof(lethalG))
                return gEffects;
            if (member.Name == nameof(heatThreshold))
                return heatEffects;
            return true;
        }

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    highG = 8f; lethalG = 25f; heatThreshold = 360f;
                    break;
                case GameParameters.Preset.Moderate:
                    highG = 6f; lethalG = 18f; heatThreshold = 330f;
                    break;
                case GameParameters.Preset.Hard:
                    highG = 5f; lethalG = 15f; heatThreshold = 320f;
                    break;
                default:
                    highG = (float)Settings.Defaults.HighG;
                    lethalG = (float)Settings.Defaults.LethalG;
                    heatThreshold = (float)Settings.Defaults.HeatThreshold;
                    break;
            }
            gEffects = true;
            heatEffects = true;
        }
    }

    /// <summary>Colonne 3 : effets visuels et outils de test.</summary>
    public class SKDisplayParams : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomParameterUI("Dépérissement visible", toolTip = "Les Kerbals affaiblis pâlissent, penchent la tête et ralentissent.\n(Réglage commun à toutes les parties.)")]
        public bool visualDecline = true;

        [GameParameters.CustomParameterUI("Portrait d'un mort",
            toolTip = "Dépouille visible : on voit le corps.\nSignal perdu : portrait brouillé.\nSignal fantôme : brouillé, la dépouille apparaît par instants.\n(Réglage commun à toutes les parties.)")]
        public string deathView = DeathViews.Label(Settings.Defaults.DeathPortrait);

        [GameParameters.CustomParameterUI("Moniteur cardiaque", toolTip = "Électrocardiogramme de chaque Kerbal sur la tablette.\n(Réglage commun à toutes les parties.)")]
        public bool monitor = true;

        [GameParameters.CustomParameterUI("Moniteur sur les portraits", toolTip = "Électrocardiogramme au-dessus du portrait de chaque Kerbal.\n(Réglage commun à toutes les parties.)")]
        public bool portraitMonitor = true;

        [GameParameters.CustomParameterUI("Sons du moniteur", toolTip = "Bip à chaque battement (tablette ouverte sur Santé) et bip continu à la mort.\n(Réglage commun à toutes les parties.)")]
        public bool monitorSound = true;

        [GameParameters.CustomParameterUI("Courbe électrique", toolTip = "Charge des batteries et bilan électrique sur la tablette.\n(Réglage commun à toutes les parties.)")]
        public bool powerGraph = true;

        [GameParameters.CustomParameterUI("Outils de test", toolTip = "Boutons -20 / +20 / Tuer sur la tablette, export des textures.\n(Propre à cette partie.)")]
        public bool debugTools = Settings.Defaults.DebugTools;

        public override string Title => "Affichage et tests";
        public override string DisplaySection => SKSection.Display;
        public override string Section => SKSection.Id;
        public override int SectionOrder => 3;
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override bool HasPresets => false;

        public override bool Interactible(MemberInfo member, GameParameters parameters)
        {
            if (!SKSection.ModEnabled(parameters))
                return false;
            if (member.Name == nameof(monitorSound) || member.Name == nameof(portraitMonitor))
                return monitor;
            return true;
        }

        public override System.Collections.IList ValidValues(MemberInfo member)
        {
            if (member.Name == nameof(deathView))
                return new System.Collections.Generic.List<string>(DeathViews.Labels);
            return null;
        }
    }
}
