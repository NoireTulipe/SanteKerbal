using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SanteKerbal
{
    /// <summary>
    /// Apparence d'un Kerbal de l'IVA (et donc de son portrait) selon sa santé :
    /// teint, inclinaison de la tête, vitesse des animations. Une instance par modèle IVA ;
    /// quand l'intérieur est recréé, un nouveau modèle apparaît et reçoit une nouvelle instance.
    /// </summary>
    internal class KerbalLook
    {
        public readonly Kerbal Kerbal;
        private readonly Animator[] animators;
        private readonly Transform head;

        // Matériaux de peau et leur texture d'origine
        private readonly List<Material> skinMaterials = new List<Material>();
        private readonly List<Texture2D> skinTextures = new List<Texture2D>();
        private int appliedLevel;

        // Globes oculaires : objets séparés devant le visage, masqués chez les morts
        // pour laisser voir les orbites de la texture « mort »
        private readonly Renderer[] eyes;
        private bool eyesHidden;

        // Rotation de tête : on retient ce qu'on a écrit pour ne pas cumuler l'inclinaison
        // si l'animation ne réécrit pas l'os à chaque image.
        private bool headWritten;
        private Quaternion lastBase;
        private Quaternion lastWritten;

        public KerbalLook(Kerbal kerbal)
        {
            Kerbal = kerbal;
            animators = kerbal.GetComponentsInChildren<Animator>(true);
            head = FindHead(kerbal);
            CollectSkin(kerbal);
            eyes = kerbal.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name.ToLowerInvariant().Contains("eye") || r.name.ToLowerInvariant().Contains("pupil"))
                .ToArray();
            LogDiagnostics();
        }

        public void Apply(KerbalHealth k)
        {
            float amount;
            if (k.Dead)
                amount = 1f;
            else if (!Settings.VisualDecline || Settings.VisualStartHealth <= 0)
                amount = 0f;
            else
                amount = Mathf.Clamp01((float)((Settings.VisualStartHealth - k.Health) / Settings.VisualStartHealth));

            // Animations : ralenties, figées à la mort
            float speed = k.Dead ? 0f : Mathf.Lerp(1f, Settings.MinAnimationSpeed, amount);
            foreach (Animator a in animators)
                if (a != null)
                    a.speed = speed;

            // Teint : pâlit par paliers, gris à la mort
            int level = k.Dead ? PaleTextures.DeadLevel : Mathf.CeilToInt(amount * PaleTextures.AliveLevels);
            if (level != appliedLevel)
                ApplySkinLevel(level);

            // Yeux : masqués à la mort
            bool hideEyes = k.Dead && Settings.HideEyesWhenDead;
            if (hideEyes != eyesHidden)
            {
                foreach (Renderer r in eyes)
                    if (r != null)
                        r.enabled = !hideEyes;
                eyesHidden = hideEyes;
            }

            // Tête : penche progressivement, tombe à la mort
            if (head != null)
            {
                float tilt = k.Dead ? Settings.DeadHeadTilt : amount * Settings.MaxHeadTilt;
                Quaternion current = head.localRotation;
                Quaternion baseRot = headWritten && current == lastWritten ? lastBase : current;
                Quaternion written = baseRot * Quaternion.AngleAxis(tilt, Settings.HeadTiltAxis);
                head.localRotation = written;
                lastBase = baseRot;
                lastWritten = written;
                headWritten = true;
            }
        }

        private void ApplySkinLevel(int level)
        {
            for (int i = 0; i < skinMaterials.Count; i++)
            {
                Material m = skinMaterials[i];
                if (m == null)
                    continue;
                Texture2D tex = PaleTextures.Get(skinTextures[i], level);
                m.mainTexture = tex != null ? tex : skinTextures[i];
            }
            appliedLevel = level;
        }

        private void CollectSkin(Kerbal kerbal)
        {
            Transform root = kerbal.headTransform != null ? kerbal.headTransform : kerbal.transform;
            var candidates = new List<Material>();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.materials)
                    if (m != null && m.mainTexture is Texture2D)
                        candidates.Add(m);

            string[] keywords = Settings.SkinTextureKeywords
                .Split(',').Select(s => s.Trim().ToLowerInvariant()).Where(s => s.Length > 0).ToArray();
            List<Material> skin = candidates
                .Where(m => keywords.Any(kw => m.mainTexture.name.ToLowerInvariant().Contains(kw)))
                .ToList();
            if (skin.Count == 0)
                skin = candidates; // aucun mot-clé reconnu : on prend toute la tête

            foreach (Material m in skin)
            {
                skinMaterials.Add(m);
                skinTextures.Add((Texture2D)m.mainTexture);
                if (Settings.DebugTools)
                    PaleTextures.Export((Texture2D)m.mainTexture);
            }
        }

        private static Transform FindHead(Kerbal kerbal)
        {
            if (!string.IsNullOrEmpty(Settings.HeadBoneName))
            {
                Transform named = kerbal.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == Settings.HeadBoneName);
                if (named != null)
                    return named;
                Log.Warning($"Os '{Settings.HeadBoneName}' introuvable sur {kerbal.crewMemberName}, utilisation de headTransform.");
            }
            return kerbal.headTransform;
        }

        private static readonly HashSet<string> loggedOnce = new HashSet<string>();

        /// <summary>Décrit le modèle dans KSP.log, une fois par Kerbal et par session, pour régler les effets.</summary>
        private void LogDiagnostics()
        {
            string name = Kerbal.crewMemberName;
            if (!loggedOnce.Add(name))
                return;

            string headPath = head != null ? PathOf(head, Kerbal.transform) : "(aucun)";
            string textures = string.Join(", ", skinTextures.Where(t => t != null).Select(t => t.name).Distinct());
            Log.Info($"Modèle IVA de {name} : os penché='{headPath}', animators={animators.Length}, " +
                     $"textures de peau={skinMaterials.Count} ({textures}), " +
                     $"yeux={eyes.Length} ({string.Join(", ", eyes.Select(r => r.name))})");
        }

        private static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (Transform cur = t; cur != null && cur != root; cur = cur.parent)
                parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
