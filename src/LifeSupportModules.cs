namespace SanteKerbal
{
    /// <summary>
    /// Base commune du recycleur et de la serre : un équipement de survie qu'on allume ou éteint.
    /// Le calcul lui-même est fait par LifeSupportSim, vaisseau chargé ou non.
    /// </summary>
    public abstract class ModuleSKEquipment : PartModule
    {
        [KSPField(isPersistant = true)]
        public bool isActive = true;

        [KSPField] public double ecPerSecond = 0.1;

        [KSPField(guiActive = true, guiName = "État")]
        public string status = "—";

        protected abstract string EquipmentName { get; }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            Fields[nameof(status)].guiName = EquipmentName;
            UpdateToggleName();
        }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Arrêter")]
        public void Toggle()
        {
            isActive = !isActive;
            UpdateToggleName();
            if (!isActive)
                status = "Arrêté";
        }

        [KSPAction("Basculer marche/arrêt")]
        public void ToggleAction(KSPActionParam param) => Toggle();

        private void UpdateToggleName()
        {
            Events[nameof(Toggle)].guiName = (isActive ? "Arrêter : " : "Démarrer : ") + EquipmentName.ToLowerInvariant();
        }
    }

    /// <summary>Récupère une partie de l'eau consommée par l'équipage, contre de l'électricité.</summary>
    public class ModuleSKRecycler : ModuleSKEquipment
    {
        [KSPField] public double kerbalsServed = 3;     // nombre de Kerbals dont l'eau est recyclée
        [KSPField] public double recycleRatio = 0.8;    // part de l'eau récupérée

        protected override string EquipmentName => "Recycleur d'eau";

        public override string GetInfo()
        {
            return $"Recycle {recycleRatio:P0} de l'eau de {kerbalsServed:F0} Kerbal(s).\n" +
                   $"Électricité : {ecPerSecond:F2}/s";
        }
    }

    /// <summary>Produit de la nourriture à partir d'eau et d'électricité.</summary>
    public class ModuleSKGreenhouse : ModuleSKEquipment
    {
        [KSPField] public double foodPerDay = 2;        // nourriture produite par jour
        [KSPField] public double waterPerFood = 1;      // eau consommée par unité de nourriture

        protected override string EquipmentName => "Serre";

        public override string GetInfo()
        {
            return $"Produit {foodPerDay:F1} nourriture par jour\n" +
                   $"(consomme {foodPerDay * waterPerFood:F1} eau par jour).\n" +
                   $"Électricité : {ecPerSecond:F2}/s";
        }
    }
}
