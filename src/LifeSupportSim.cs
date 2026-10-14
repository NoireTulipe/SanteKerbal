using System;
using System.Collections.Generic;

namespace SanteKerbal
{
    /// <summary>Accès aux ressources d'un vaisseau, chargé ou non.</summary>
    internal interface IVesselStore
    {
        double Take(string resource, double amount);   // renvoie la quantité obtenue
        void Put(string resource, double amount);      // stocke dans la limite de la place
        double Amount(string resource);
        bool IsLoaded { get; }
        /// <summary>Part de la consommation électrique de survie couverte pendant le pas, calculée par PowerSim.</summary>
        double EcFraction { get; set; }
    }

    /// <summary>Vaisseau chargé : ressources réelles des pièces.</summary>
    internal class LoadedStore : IVesselStore
    {
        private readonly Vessel vessel;
        public LoadedStore(Vessel vessel) { this.vessel = vessel; }
        public bool IsLoaded => true;
        public double EcFraction { get; set; } = 1.0;

        public double Take(string resource, double amount)
        {
            if (amount <= 0) return 0;
            return vessel.rootPart.RequestResource(resource, amount, ResourceFlowMode.ALL_VESSEL);
        }

        public void Put(string resource, double amount)
        {
            if (amount > 0)
                vessel.rootPart.RequestResource(resource, -amount, ResourceFlowMode.ALL_VESSEL);
        }

        public double Amount(string resource)
        {
            LifeResources.GetTotals(vessel, resource, out double amount, out double _);
            return amount;
        }
    }

    /// <summary>Vaisseau non chargé : ressources sauvegardées des pièces (ProtoPartResourceSnapshot).</summary>
    internal class ProtoStore : IVesselStore
    {
        private readonly ProtoVessel pv;
        public ProtoStore(ProtoVessel pv) { this.pv = pv; }
        public bool IsLoaded => false;
        public double EcFraction { get; set; } = 1.0;

        private IEnumerable<ProtoPartResourceSnapshot> Snapshots(string resource)
        {
            foreach (ProtoPartSnapshot p in pv.protoPartSnapshots)
                foreach (ProtoPartResourceSnapshot r in p.resources)
                    if (r.resourceName == resource && r.flowState)
                        yield return r;
        }

        public double Take(string resource, double amount)
        {
            double got = 0;
            foreach (ProtoPartResourceSnapshot r in Snapshots(resource))
            {
                if (got >= amount) break;
                double take = Math.Min(r.amount, amount - got);
                r.amount -= take;
                got += take;
            }
            return got;
        }

        public void Put(string resource, double amount)
        {
            foreach (ProtoPartResourceSnapshot r in Snapshots(resource))
            {
                if (amount <= 0) break;
                double put = Math.Min(r.maxAmount - r.amount, amount);
                if (put <= 0) continue;
                r.amount += put;
                amount -= put;
            }
        }

        public double Amount(string resource)
        {
            double total = 0;
            foreach (ProtoPartResourceSnapshot r in Snapshots(resource))
                total += r.amount;
            return total;
        }
    }

    /// <summary>Recycleurs et serres actifs d'un vaisseau, additionnés.</summary>
    internal class Equipment
    {
        public double RecyclerKerbals;
        public double RecyclerRatio;      // moyenne pondérée par les Kerbals servis
        public double GreenhouseFood;     // nourriture par jour
        public double GreenhouseWater;    // eau par jour
        public double EcPerSecond;
        public readonly List<ModuleSKRecycler> Recyclers = new List<ModuleSKRecycler>();
        public readonly List<ModuleSKGreenhouse> Greenhouses = new List<ModuleSKGreenhouse>();

        // Vaisseau non chargé : modules de la sauvegarde et leur poids (Kerbals servis / rations par jour)
        public readonly List<(ProtoPartModuleSnapshot ms, double weight)> ProtoRecyclers = new List<(ProtoPartModuleSnapshot, double)>();
        public readonly List<(ProtoPartModuleSnapshot ms, double weight)> ProtoGreenhouses = new List<(ProtoPartModuleSnapshot, double)>();

        /// <summary>
        /// Répartit l'eau recyclée et la nourriture produite pendant un pas entre les équipements allumés,
        /// au prorata de leur capacité, et l'ajoute à leur compteur « depuis le lancement ».
        /// </summary>
        public void AddOutput(double recycled, double produced)
        {
            if (recycled > 0 && RecyclerKerbals > 0)
            {
                foreach (ModuleSKRecycler r in Recyclers)
                    r.totalOutput += recycled * r.kerbalsServed / RecyclerKerbals;
                foreach (var e in ProtoRecyclers)
                    AddToSnapshot(e.ms, recycled * e.weight / RecyclerKerbals);
            }
            if (produced > 0 && GreenhouseFood > 0)
            {
                foreach (ModuleSKGreenhouse g in Greenhouses)
                    g.totalOutput += produced * g.foodPerDay / GreenhouseFood;
                foreach (var e in ProtoGreenhouses)
                    AddToSnapshot(e.ms, produced * e.weight / GreenhouseFood);
            }
        }

        private static void AddToSnapshot(ProtoPartModuleSnapshot ms, double amount)
        {
            if (ms.moduleValues == null)
                return;
            double total = 0;
            ms.moduleValues.TryGetValue("totalOutput", ref total);
            ms.moduleValues.SetValue("totalOutput", (total + amount).ToString("R"), true);
        }

        public bool Any => RecyclerKerbals > 0 || GreenhouseFood > 0;

        private void Add(ModuleSKRecycler r)
        {
            if (r.kerbalsServed <= 0)
                return;
            RecyclerRatio = (RecyclerRatio * RecyclerKerbals + r.recycleRatio * r.kerbalsServed) / (RecyclerKerbals + r.kerbalsServed);
            RecyclerKerbals += r.kerbalsServed;
            EcPerSecond += r.ecPerSecond;
        }

        private void Add(ModuleSKGreenhouse g)
        {
            GreenhouseFood += g.foodPerDay;
            GreenhouseWater += g.foodPerDay * g.waterPerFood;
            EcPerSecond += g.ecPerSecond;
        }

        /// <summary>Pièces réelles (vaisseau chargé ou éditeur). Seuls les modules allumés comptent.</summary>
        public static Equipment FromParts(IEnumerable<Part> parts)
        {
            var eq = new Equipment();
            foreach (Part p in parts)
            {
                foreach (PartModule m in p.Modules)
                {
                    if (m is ModuleSKRecycler r && r.isActive) { eq.Add(r); eq.Recyclers.Add(r); }
                    else if (m is ModuleSKGreenhouse g && g.isActive) { eq.Add(g); eq.Greenhouses.Add(g); }
                }
            }
            return eq;
        }

        /// <summary>Vaisseau non chargé : état marche/arrêt lu dans la sauvegarde, réglages lus sur la pièce d'origine.</summary>
        public static Equipment FromProto(ProtoVessel pv)
        {
            var eq = new Equipment();
            foreach (ProtoPartSnapshot p in pv.protoPartSnapshots)
            {
                Part prefab = p.partInfo?.partPrefab;
                if (prefab == null)
                    continue;
                foreach (ProtoPartModuleSnapshot ms in p.modules)
                {
                    if (ms.moduleName != nameof(ModuleSKRecycler) && ms.moduleName != nameof(ModuleSKGreenhouse))
                        continue;
                    bool active = true;
                    ms.moduleValues?.TryGetValue("isActive", ref active);
                    if (!active)
                        continue;
                    if (ms.moduleName == nameof(ModuleSKRecycler))
                    {
                        ModuleSKRecycler r = prefab.FindModuleImplementing<ModuleSKRecycler>();
                        if (r != null)
                        {
                            eq.Add(r);
                            eq.ProtoRecyclers.Add((ms, r.kerbalsServed));
                        }
                    }
                    else
                    {
                        ModuleSKGreenhouse g = prefab.FindModuleImplementing<ModuleSKGreenhouse>();
                        if (g != null)
                        {
                            eq.Add(g);
                            eq.ProtoGreenhouses.Add((ms, g.foodPerDay));
                        }
                    }
                }
            }
            return eq;
        }
    }

    /// <summary>Résultat d'un pas de simulation des besoins.</summary>
    internal struct NeedsResult
    {
        public double WaterFrac;   // part des besoins en eau couverte (0 à 1)
        public double FoodFrac;
        public double EcFrac;
        public double Recycled;    // eau recyclée pendant le pas
        public double Produced;    // nourriture produite pendant le pas
        public bool GreenhouseLacksWater;
    }

    /// <summary>
    /// Calcul des besoins d'eau et de nourriture, commun aux vaisseaux chargés et non chargés.
    /// Les fractions obtenues donnent la part du temps passée avec ou sans chaque ressource,
    /// ce qui gère correctement un épuisement au milieu d'un long pas (accélération du temps).
    /// </summary>
    internal static class LifeSupportSim
    {
        public const string ElectricCharge = "ElectricCharge";

        public static NeedsResult Consume(IVesselStore store, int crew, Equipment eq, double dt)
        {
            var res = new NeedsResult { WaterFrac = 1, FoodFrac = 1, EcFrac = 1 };
            double days = dt / KSPUtil.dateTimeFormatter.Day;

            // Électricité des équipements : déjà résolue par PowerSim pour tout le vaisseau
            // (avec réserve de tolérance si chargé, production estimée sinon)
            if (eq.EcPerSecond > 0)
                res.EcFrac = store.EcFraction;

            // Eau de l'équipage, moins la part recyclée
            double crewWater = crew * Settings.WaterPerDay * days;
            res.Recycled = Math.Min(crew, eq.RecyclerKerbals) * Settings.WaterPerDay * days * eq.RecyclerRatio * res.EcFrac;
            double gotWater = store.Take(LifeResources.Water, Math.Max(0, crewWater - res.Recycled));
            if (crewWater > 0)
                res.WaterFrac = Math.Min(1.0, (res.Recycled + gotWater) / crewWater);

            // Serre : produit de la nourriture avec l'eau qui reste
            double maxProduced = eq.GreenhouseFood * days * res.EcFrac;
            double ghWaterNeed = eq.GreenhouseWater * days * res.EcFrac;
            res.Produced = maxProduced;
            if (ghWaterNeed > 0)
            {
                double gotGh = store.Take(LifeResources.Water, ghWaterNeed);
                res.Produced = maxProduced * gotGh / ghWaterNeed;
                res.GreenhouseLacksWater = gotGh < ghWaterNeed * 0.999;
            }

            // Nourriture : d'abord la récolte du jour, puis les stocks ; le surplus est stocké
            double crewFood = crew * Settings.FoodPerDay * days;
            double fromHarvest = Math.Min(res.Produced, crewFood);
            double gotFood = store.Take(LifeResources.Food, crewFood - fromHarvest);
            if (res.Produced > fromHarvest)
                store.Put(LifeResources.Food, res.Produced - fromHarvest);
            if (crewFood > 0)
                res.FoodFrac = Math.Min(1.0, (fromHarvest + gotFood) / crewFood);

            return res;
        }

        /// <summary>
        /// Effet des besoins sur la santé pendant dt. Les changements notables (manque, retour,
        /// seuil, mort) sont signalés par les rappels fournis.
        /// </summary>
        public static void ApplyHealth(KerbalHealth k, NeedsResult r, double dt,
            Action<KerbalHealth, bool, bool> onNeedsChanged, Action<KerbalHealth, double, string> onHealthChanged)
        {
            double days = dt / KSPUtil.dateTimeFormatter.Day;
            double before = k.Health;

            // 100 points perdus en DaysWithoutWater jours sans eau, en DaysWithoutFood jours sans nourriture
            k.Damage((1.0 - r.WaterFrac) * days * KerbalHealth.Max / Settings.DaysWithoutWater);
            k.Damage((1.0 - r.FoodFrac) * days * KerbalHealth.Max / Settings.DaysWithoutFood);

            // Régénération pendant la part du temps où tout était couvert (estimation prudente)
            double satisfied = Math.Max(0, r.WaterFrac + r.FoodFrac - 1.0);
            k.Heal(Settings.RegenPerDay * days * satisfied);

            bool lackWater = r.WaterFrac < 0.999;
            bool lackFood = r.FoodFrac < 0.999;
            if (lackWater != k.LackWater || lackFood != k.LackFood)
                onNeedsChanged(k, lackWater, lackFood);
            k.LackWater = lackWater;
            k.LackFood = lackFood;

            onHealthChanged(k, before, lackWater ? "soif" : "faim");
        }

        /// <summary>Autonomie en jours (infinie si la consommation nette est nulle ou négative).</summary>
        public static void Autonomy(double water, double food, int crew, Equipment eq, out double waterDays, out double foodDays)
        {
            double waterPerDay = crew * Settings.WaterPerDay
                                 - Math.Min(crew, eq.RecyclerKerbals) * Settings.WaterPerDay * eq.RecyclerRatio
                                 + eq.GreenhouseWater;
            double foodPerDay = crew * Settings.FoodPerDay - eq.GreenhouseFood;
            waterDays = waterPerDay > 1e-9 ? water / waterPerDay : double.PositiveInfinity;
            foodDays = foodPerDay > 1e-9 ? food / foodPerDay : double.PositiveInfinity;
        }

        public static string FormatDays(double days)
        {
            if (double.IsPositiveInfinity(days)) return "illimitée";
            return days.ToString("F1") + " j";
        }
    }
}
