using System.Collections.Generic;
using UnityEngine;

namespace SanteKerbal
{
    /// <summary>
    /// Versions blafardes des textures de peau, générées à la demande et mises en cache.
    /// Le shader des Kerbals multiplie la texture par une couleur, ce qui ne permet que d'assombrir :
    /// pour éclaircir et désaturer, il faut une autre texture.
    /// </summary>
    internal static class PaleTextures
    {
        public const int AliveLevels = 4;          // niveaux 1..4 de pâleur (0 = texture d'origine)
        public const int DeadLevel = AliveLevels + 1;
        public const int FrozenLevel = AliveLevels + 2;   // hibernation : teint bleuté givré

        private static readonly Dictionary<int, Color32[]> sourcePixels = new Dictionary<int, Color32[]>();
        private static readonly Dictionary<long, Texture2D> cache = new Dictionary<long, Texture2D>();
        private static readonly HashSet<int> failed = new HashSet<int>();
        private static readonly HashSet<string> exported = new HashSet<string>();

        // Texture « mort » personnalisée : GameData/SanteKerbal/Textures/<nom>_mort.png
        private const string CustomDeadUrl = "SanteKerbal/Textures/{0}_mort";
        // Exports pour dessiner : dossier SanteKerbal_Export à la racine de KSP (hors GameData)
        private const string ExportFolder = "SanteKerbal_Export";

        /// <summary>Texture de niveau donné, ou null si la texture n'a pas pu être copiée.</summary>
        public static Texture2D Get(Texture2D source, int level)
        {
            if (source == null || level <= 0)
                return source;
            if (level == DeadLevel)
            {
                Texture2D custom = GameDatabase.Instance.GetTexture(string.Format(CustomDeadUrl, source.name), false);
                if (custom != null)
                    return custom;
            }

            int id = source.GetInstanceID();
            if (failed.Contains(id))
                return null;

            long key = ((long)id << 8) | (uint)level;
            if (cache.TryGetValue(key, out Texture2D tex) && tex != null)
                return tex;

            Color32[] src = GetSourcePixels(source);
            if (src == null)
            {
                failed.Add(id);
                return null;
            }

            var pixels = new Color32[src.Length];
            bool dead = level == DeadLevel;
            bool frozen = level == FrozenLevel;
            float t = dead ? 1f : frozen ? 0.75f : Settings.MaxPaleness * level / AliveLevels;
            Color ice = new Color(0.72f, 0.88f, 1f);
            Color pale = Settings.PaleColor;
            Color deadTint = Settings.DeadColor;
            for (int i = 0; i < src.Length; i++)
            {
                Color c = src[i];
                float lum = 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
                Color target = dead ? deadTint * (0.45f + 0.55f * lum)                  // gris-vert terne
                    : frozen ? ice * (0.55f + 0.5f * lum)                                // bleu givré
                    : Color.Lerp(new Color(lum, lum, lum), pale, 0.6f);                  // désaturé et éclairci
                Color r = Color.Lerp(c, target, t);
                r.a = c.a;
                pixels[i] = r;
            }

            tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true)
            {
                name = source.name + "_SanteKerbal" + level,
                wrapMode = source.wrapMode,
                filterMode = source.filterMode,
                anisoLevel = source.anisoLevel
            };
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            cache[key] = tex;
            return tex;
        }

        /// <summary>
        /// Enregistre la texture d'origine en PNG (une fois par session), pour servir de modèle
        /// à une texture « mort » personnalisée.
        /// </summary>
        public static void Export(Texture2D source)
        {
            if (source == null || !exported.Add(source.name))
                return;
            Color32[] px = GetSourcePixels(source);
            if (px == null)
                return;
            try
            {
                string dir = System.IO.Path.Combine(KSPUtil.ApplicationRootPath, ExportFolder);
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, source.name + ".png");
                var tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                tex.SetPixels32(px);
                tex.Apply();
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.Destroy(tex);
                Log.Info("Texture exportée : " + System.IO.Path.GetFullPath(path));
            }
            catch (System.Exception e)
            {
                Log.Warning($"Export de la texture '{source.name}' impossible : {e.Message}");
            }
        }

        /// <summary>Copie lisible des pixels (les textures du jeu ne le sont pas) via une RenderTexture.</summary>
        private static Color32[] GetSourcePixels(Texture2D source)
        {
            int id = source.GetInstanceID();
            if (sourcePixels.TryGetValue(id, out Color32[] px))
                return px;

            RenderTexture previous = RenderTexture.active;
            RenderTexture rt = null;
            try
            {
                rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                copy.Apply();
                px = copy.GetPixels32();
                Object.Destroy(copy);
                sourcePixels[id] = px;
                Log.Info($"Texture '{source.name}' ({source.width}x{source.height}) copiée pour la pâleur");
                return px;
            }
            catch (System.Exception e)
            {
                Log.Warning($"Impossible de copier la texture '{source.name}' : {e.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (rt != null)
                    RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
