using System;
using System.Collections.Generic;
using UnityEngine;

namespace KerTech
{
    /// <summary>
    /// Une application de la tablette KerTab. Les applications de KerTech et celles d'autres mods
    /// s'inscrivent toutes de la même façon, avec <see cref="KerTab.Register"/>.
    /// </summary>
    public abstract class KerTabApp
    {
        /// <summary>Identifiant unique, par exemple « monmod.carte ». Réinscrire le même identifiant remplace l'application.</summary>
        public abstract string Id { get; }

        /// <summary>Nom affiché sous l'icône et en haut de la page.</summary>
        public abstract string Title { get; }

        /// <summary>Icône : URL GameDatabase d'une image (sans extension), par exemple « MonMod/Icones/carte ».</summary>
        public virtual string IconUrl => null;

        /// <summary>Ordre sur l'écran d'accueil (croissant). KerTech utilise 100 à 900 ; les autres mods, 1000 et plus.</summary>
        public virtual int Order => 1000;

        /// <summary>L'icône apparaît-elle pour ce vaisseau ? (par exemple seulement s'il a un certain équipement)</summary>
        public virtual bool IsVisible(Vessel v) => true;

        /// <summary>Pastille rouge sur l'icône : quelque chose demande l'attention du joueur.</summary>
        public virtual bool HasAlert(Vessel v) => false;

        /// <summary>Petit compteur « x2 » sur l'icône (0 ou 1 : pas de compteur).</summary>
        public virtual int Badge(Vessel v) => 0;

        /// <summary>
        /// Dessine la page (GUILayout, dans la zone défilante de l'écran de la tablette).
        /// <paramref name="ui"/> donne les styles et la taille de la tablette ; <paramref name="v"/> est le vaisseau actif.
        /// </summary>
        public abstract void Draw(KerTabUI ui, Vessel v);
    }

    /// <summary>Styles et outils de dessin fournis aux applications, à l'échelle de la tablette.</summary>
    public class KerTabUI
    {
        /// <summary>Échelle de la tablette (multiplier les dimensions en pixels par cette valeur).</summary>
        public float Scale { get; internal set; } = 1f;
        /// <summary>Largeur utile de la page, en pixels.</summary>
        public float Width { get; internal set; }
        public GUIStyle Label { get; internal set; }
        /// <summary>Texte avec balises &lt;b&gt;, &lt;color=#...&gt;.</summary>
        public GUIStyle RichLabel { get; internal set; }
        public GUIStyle Section { get; internal set; }
        public GUIStyle Button { get; internal set; }

        /// <summary>Dimension à l'échelle de la tablette.</summary>
        public float U(float pixels) => Mathf.Round(pixels * Scale);

        /// <summary>Barre de progression (fraction 0 à 1) dans le rectangle donné.</summary>
        public void Bar(Rect rect, float fraction, Color color)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }

    /// <summary>
    /// Registre des applications de la tablette KerTab.
    /// <para>Avec une référence à KerTech.dll : dériver de <see cref="KerTabApp"/> et appeler <see cref="Register"/>.</para>
    /// <para>Sans dépendance (par réflexion) : appeler <see cref="RegisterApp"/>, qui n'utilise que des types .NET.
    /// Si KerTech n'est pas installé, le mod appelant fonctionne simplement sans icône sur la tablette.</para>
    /// </summary>
    public static class KerTab
    {
        private static readonly List<KerTabApp> apps = new List<KerTabApp>();
        private static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();

        /// <summary>Applications inscrites, dans l'ordre d'affichage.</summary>
        public static IList<KerTabApp> Apps => apps.AsReadOnly();

        /// <summary>Inscrit (ou remplace, même Id) une application.</summary>
        public static void Register(KerTabApp app)
        {
            if (app == null || string.IsNullOrEmpty(app.Id))
                return;
            apps.RemoveAll(a => a.Id == app.Id);
            apps.Add(app);
            apps.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id));
            Log.Info($"KerTab : application « {app.Title} » ({app.Id}) inscrite");
        }

        public static void Unregister(string id) => apps.RemoveAll(a => a.Id == id);

        /// <summary>
        /// Inscription sans dépendance, pour un appel par réflexion depuis un autre mod.
        /// <paramref name="draw"/> reçoit la largeur utile de la page (pixels) et dessine avec GUILayout.
        /// <paramref name="visible"/> et <paramref name="alert"/> peuvent être null.
        /// </summary>
        public static void RegisterApp(string id, string title, string iconUrl, int order,
            Action<float> draw, Func<bool> visible, Func<bool> alert)
        {
            Register(new DelegateApp(id, title, iconUrl, order, draw, visible, alert));
        }

        /// <summary>Icône d'une application (chargée une fois depuis GameDatabase), ou null.</summary>
        internal static Texture2D IconOf(KerTabApp app)
        {
            string url = app.IconUrl;
            if (string.IsNullOrEmpty(url))
                return null;
            if (!icons.TryGetValue(url, out Texture2D tex))
            {
                tex = GameDatabase.Instance.GetTexture(url, false);
                if (tex == null)
                    Log.Warning($"KerTab : icône introuvable pour « {app.Title} » : {url}");
                icons[url] = tex;
            }
            return tex;
        }

        /// <summary>Application inscrite par délégués (voir <see cref="RegisterApp"/>).</summary>
        private class DelegateApp : KerTabApp
        {
            private readonly string id, title, iconUrl;
            private readonly int order;
            private readonly Action<float> draw;
            private readonly Func<bool> visible, alert;

            public DelegateApp(string id, string title, string iconUrl, int order, Action<float> draw, Func<bool> visible, Func<bool> alert)
            {
                this.id = id; this.title = title; this.iconUrl = iconUrl; this.order = order;
                this.draw = draw; this.visible = visible; this.alert = alert;
            }

            public override string Id => id;
            public override string Title => title;
            public override string IconUrl => iconUrl;
            public override int Order => order;
            public override bool IsVisible(Vessel v) => visible == null || visible();
            public override bool HasAlert(Vessel v) => alert != null && alert();
            public override void Draw(KerTabUI ui, Vessel v) => draw?.Invoke(ui.Width);
        }
    }
}
