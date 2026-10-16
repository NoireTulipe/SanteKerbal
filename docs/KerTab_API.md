# KerTab : ajouter une application à la tablette

La tablette **KerTab** de KerTech est une plateforme : n'importe quel mod peut y ajouter son
application (une icône sur l'écran d'accueil, une page). Les pages de KerTech (Santé, Énergie,
Navigation…) utilisent exactement le même mécanisme.

Une application déclare :

| Élément | Rôle |
|---|---|
| `Id` | identifiant unique, par exemple `monmod.carte` (réinscrire le même `Id` remplace l'application) |
| `Title` | nom sous l'icône et en haut de la page |
| `IconUrl` | image de l'icône, URL GameDatabase sans extension (`MonMod/Icones/carte`), idéalement 128 x 128 transparente |
| `Order` | place sur l'accueil (KerTech utilise 100 à 900 ; les autres mods, 1000 et plus) |
| `IsVisible(v)` | l'icône n'apparaît que si `true` (par exemple : le vaisseau a mon équipement) |
| `HasAlert(v)` | pastille rouge sur l'icône |
| `Badge(v)` | petit compteur « x2 » (0 ou 1 : aucun) |
| `Draw(ui, v)` | dessine la page avec `GUILayout`, dans la zone défilante de l'écran |

Une erreur dans une application est écrite dans `KSP.log` et n'empêche pas la tablette de fonctionner.


## Méthode 1 : avec une référence à KerTech.dll

Ajouter `GameData/KerTech/Plugins/KerTech.dll` aux références du projet (sans la copier), puis :

```csharp
using KerTech;
using UnityEngine;

public class AppCarte : KerTabApp
{
    public override string Id => "monmod.carte";
    public override string Title => "Carte";
    public override string IconUrl => "MonMod/Icones/carte";
    public override int Order => 1000;

    public override bool HasAlert(Vessel v) => v != null && v.altitude < 1000;

    public override void Draw(KerTabUI ui, Vessel v)
    {
        GUILayout.Label("<b>Ma carte</b>", ui.RichLabel);
        GUILayout.Label($"Altitude : {v.altitude / 1000:F1} km", ui.Label);
        if (GUILayout.Button("Action", ui.Button, GUILayout.Width(ui.U(120))))
            Debug.Log("[MonMod] clic depuis la tablette");
    }
}

[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class InscriptionCarte : MonoBehaviour
{
    public void Start() => KerTab.Register(new AppCarte());
}
```

`KerTabUI` fournit les styles de la tablette (`Label`, `RichLabel`, `Section`, `Button`), sa largeur
utile (`Width`), son échelle (`Scale`, `U(pixels)`) et une barre de progression (`Bar`).

Dans `GameData/MonMod/MonMod.version` ou le fichier CKAN, déclarer alors KerTech comme dépendance.


## Méthode 2 : sans dépendance (par réflexion)

Le mod fonctionne même si KerTech n'est pas installé ; il ajoute simplement son icône s'il l'est.
`KerTab.RegisterApp` n'utilise que des types .NET :

```csharp
using System;
using System.Linq;
using UnityEngine;

[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class InscriptionKerTab : MonoBehaviour
{
    public void Start()
    {
        Type kertab = AssemblyLoader.loadedAssemblies
            .Select(a => a.assembly.GetType("KerTech.KerTab"))
            .FirstOrDefault(t => t != null);
        if (kertab == null)
            return;   // KerTech absent : rien à faire

        Action<float> draw = width => GUILayout.Label("Bonjour depuis mon mod !");
        Func<bool> visible = () => FlightGlobals.ActiveVessel != null;
        Func<bool> alert = () => false;

        kertab.GetMethod("RegisterApp").Invoke(null, new object[]
        {
            "monmod.bonjour", "Bonjour", "MonMod/Icones/bonjour", 1000, draw, visible, alert
        });
    }
}
```

`draw` reçoit la largeur utile de la page en pixels ; les styles actifs (`GUI.skin`) sont déjà ceux de
la tablette.
