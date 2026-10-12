using System;

namespace SanteKerbal
{
    /// <summary>
    /// Électricité d'un vaisseau non chargé, que KSP ne simule pas : production estimée des panneaux
    /// solaires déployés et des générateurs (RTG), consommation des équipements de survie et des
    /// caissons, batteries qui se chargent et se vident vraiment.
    /// </summary>
    internal static class PowerSim
    {
        /// <summary>Production électrique moyenne estimée (par seconde) d'un vaisseau non chargé.</summary>
        public static double UnloadedProduction(Vessel v, out double solar, out double generators)
        {
            solar = generators = 0;
            ProtoVessel pv = v.protoVessel;
            double sunMult = SunMultiplier(v);

            foreach (ProtoPartSnapshot p in pv.protoPartSnapshots)
            {
                Part prefab = p.partInfo?.partPrefab;
                if (prefab == null)
                    continue;
                foreach (ProtoPartModuleSnapshot ms in p.modules)
                {
                    if (ms.moduleName == nameof(ModuleDeployableSolarPanel))
                        solar += SolarPanel(prefab, ms, sunMult);
                    else if (ms.moduleName == nameof(ModuleGenerator))
                        generators += Generator(prefab, ms);
                }
            }
            return solar + generators;
        }

        /// <summary>
        /// Résout l'électricité pendant dt : la production couvre d'abord la consommation, le surplus
        /// charge les batteries, le manque les vide. Renvoie la part de la consommation couverte (0 à 1).
        /// </summary>
        public static double Resolve(IVesselStore store, double needPerSecond, double productionPerSecond, double dt)
        {
            double need = needPerSecond * dt;
            double produced = productionPerSecond * dt;
            double net = produced - need;
            if (net >= 0)
            {
                store.Put(LifeSupportSim.ElectricCharge, net);
                return 1.0;
            }
            double fromBatteries = store.Take(LifeSupportSim.ElectricCharge, -net);
            return need > 0 ? Math.Min(1.0, (produced + fromBatteries) / need) : 1.0;
        }

        // Dette d'électricité des vaisseaux chargés, par vaisseau (non sauvegardée)
        private static readonly System.Collections.Generic.Dictionary<Guid, double> debts =
            new System.Collections.Generic.Dictionary<Guid, double>();

        /// <summary>
        /// Vaisseau chargé : prélève la consommation de survie dans les batteries, avec une réserve de
        /// tolérance. En accélération du temps, chaque image demande d'un coup beaucoup d'électricité,
        /// parfois plus que les batteries n'en contiennent à cet instant, alors que les panneaux les
        /// rechargent à l'image suivante. Le manque est donc noté comme une dette, remboursée ensuite ;
        /// la panne n'est déclarée que si la dette dépasse powerGraceMinutes de consommation.
        /// </summary>
        public static double ResolveLoaded(IVesselStore store, Guid vesselId, double needPerSecond, double dt)
        {
            debts.TryGetValue(vesselId, out double debt);
            if (needPerSecond <= 0)
            {
                debts[vesselId] = 0;
                return 1.0;
            }
            double need = needPerSecond * dt;
            double ask = need + debt;
            double got = store.Take(LifeSupportSim.ElectricCharge, ask);
            debt = ask - got;

            double maxDebt = needPerSecond * Settings.PowerGraceMinutes * 60.0;
            double frac = 1.0;
            if (debt > maxDebt)
            {
                // Vraie pénurie : la part au-delà de la tolérance n'est pas couverte
                frac = Math.Max(0.0, 1.0 - (debt - maxDebt) / need);
                debt = maxDebt;
            }
            debts[vesselId] = debt;
            return frac;
        }

        private static double SolarPanel(Part prefab, ProtoPartModuleSnapshot ms, double sunMult)
        {
            ModuleDeployableSolarPanel panel = prefab.FindModuleImplementing<ModuleDeployableSolarPanel>();
            if (panel == null || panel.resourceName != LifeSupportSim.ElectricCharge)
                return 0;
            string state = ms.moduleValues?.GetValue("deployState");
            if (state != null && state != nameof(ModuleDeployablePart.DeployState.EXTENDED))
                return 0;   // replié ou cassé
            // Moyenne sur une orbite : ombre de la planète, orientation (les panneaux fixes suivent mal le Soleil)
            double factor = panel.isTracking ? Settings.UnloadedSolarFactor : Settings.UnloadedSolarFactor * 0.5;
            return panel.chargeRate * sunMult * factor;
        }

        private static double Generator(Part prefab, ProtoPartModuleSnapshot ms)
        {
            ModuleGenerator gen = prefab.FindModuleImplementing<ModuleGenerator>();
            if (gen == null || gen.resHandler.inputResources.Count > 0)
                return 0;   // seuls les générateurs sans carburant (RTG) sont simulés
            bool active = gen.isAlwaysActive;
            if (!active)
                ms.moduleValues?.TryGetValue("generatorIsActive", ref active);
            if (!active)
                return 0;
            double rate = 0;
            foreach (ModuleResource r in gen.resHandler.outputResources)
                if (r.name == LifeSupportSim.ElectricCharge)
                    rate += r.rate;
            return rate;
        }

        /// <summary>Ensoleillement relatif à l'orbite de Kerbin (loi en carré inverse), borné.</summary>
        public static double SunMultiplier(Vessel v)
        {
            try
            {
                CelestialBody sun = Planetarium.fetch?.Sun;
                CelestialBody home = FlightGlobals.GetHomeBody();
                if (sun == null || home?.orbit == null)
                    return 1;
                double d = (v.GetWorldPos3D() - sun.position).magnitude;
                if (double.IsNaN(d) || d < 1)
                    return 1;
                double r = home.orbit.semiMajorAxis / d;
                return Math.Min(5.0, r * r);
            }
            catch (Exception)
            {
                return 1;
            }
        }
    }
}
