using UnityEngine;

namespace SanteKerbal
{
    /// <summary>Messages préfixés [SanteKerbal] pour les retrouver facilement dans KSP.log.</summary>
    internal static class Log
    {
        private const string Prefix = "[SanteKerbal] ";

        public static void Info(string message) => Debug.Log(Prefix + message);
        public static void Warning(string message) => Debug.LogWarning(Prefix + message);
        public static void Error(string message) => Debug.LogError(Prefix + message);
    }
}
