namespace KerTech
{
    /// <summary>Noms des ressources du mod et vérification de leur présence au chargement.</summary>
    internal static class LifeResources
    {
        public const string Water = "KerTech_Eau";
        public const string Food = "KerTech_Nourriture";
        public const string LifeModulePart = "KerTech_ModuleVie";

        /// <summary>Quantité et capacité totales d'une ressource dans un vaisseau chargé.</summary>
        public static void GetTotals(Vessel vessel, string resource, out double amount, out double maxAmount)
        {
            amount = maxAmount = 0;
            PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(resource);
            if (def == null || vessel == null)
                return;
            vessel.GetConnectedResourceTotals(def.id, out amount, out maxAmount);
        }

        /// <summary>Écrit dans le log l'état des ressources et des pièces patchées.</summary>
        public static void CheckLoaded()
        {
            foreach (string name in new[] { Water, Food })
            {
                PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(name);
                if (def == null)
                    Log.Error("Ressource " + name + " introuvable : le fichier Resources/KerTech_Resources.cfg n'a pas été lu.");
                else
                    Log.Info("Ressource " + name + " trouvée (id=" + def.id + ", densité=" + def.density + ")");
            }

            int habitable = 0, withWater = 0, withFood = 0;
            bool lifeModuleFound = false;
            foreach (AvailablePart ap in PartLoader.LoadedPartsList)
            {
                Part prefab = ap.partPrefab;
                if (prefab == null)
                    continue;
                if (ap.name == LifeModulePart)
                {
                    lifeModuleFound = true;
                    Log.Info("Module de vie trouvé : " + Describe(prefab));
                }
                if (prefab.CrewCapacity <= 0 || prefab.Resources == null)
                    continue;
                habitable++;
                if (HasResource(prefab, Water)) withWater++;
                if (HasResource(prefab, Food)) withFood++;
            }

            if (!lifeModuleFound)
                Log.Error("Pièce " + LifeModulePart + " introuvable : Module Manager est-il installé ?");
            Log.Info($"Pièces habitables : {habitable}, avec eau : {withWater}, avec nourriture : {withFood}");
            if (habitable > 0 && (withWater < habitable || withFood < habitable))
                Log.Warning("Certaines pièces habitables n'ont pas reçu de réserve (patch Module Manager non appliqué ?)");
        }

        // Certaines pièces stock (drapeaux, Kerbals EVA...) ont une liste de ressources non initialisée
        private static bool HasResource(Part prefab, string name)
        {
            try { return prefab.Resources.Contains(name); }
            catch (System.NullReferenceException) { return false; }
        }

        private static string Describe(Part prefab)
        {
            PartResource water = prefab.Resources?.Get(Water);
            PartResource food = prefab.Resources?.Get(Food);
            return $"eau={water?.maxAmount ?? 0}, nourriture={food?.maxAmount ?? 0}";
        }
    }
}
