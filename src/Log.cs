using UnityEngine;

namespace KerTech
{
    /// <summary>Messages préfixés [KerTech] pour les retrouver facilement dans KSP.log.</summary>
    internal static class Log
    {
        private const string Prefix = "[KerTech] ";

        public static void Info(string message) => Debug.Log(Prefix + message);
        public static void Warning(string message) => Debug.LogWarning(Prefix + message);
        public static void Error(string message) => Debug.LogError(Prefix + message);
    }
}
