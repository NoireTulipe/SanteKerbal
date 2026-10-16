using System.Reflection;
using UnityEngine;

namespace KerTech
{
    /// <summary>
    /// Point d'entrée : s'exécute une seule fois à l'arrivée au menu principal,
    /// une fois que GameDatabase a chargé tous les .cfg.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class KerTechLoader : MonoBehaviour
    {
        public void Start()
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Log.Info("Mod chargé, version " + version);
            Settings.Load();
            LifeResources.CheckLoaded();
        }
    }
}
