using KSP.UI.Screens.SpaceCenter.MissionSummaryDialog;
using UnityEngine;

namespace KerTech
{
    /// <summary>
    /// Récapitulatif de fin de mission : KSP affiche « Récupéré » pour tout l'équipage, y compris nos
    /// morts (déclarés officiellement morts à la récupération). Chaque fiche réécrit son statut quand
    /// l'onglet Équipage s'affiche : tant que le récapitulatif est ouvert, on corrige donc régulièrement
    /// les fiches des morts (statut « Mort », pas d'expérience gagnée).
    /// </summary>
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class RecoveryDialogFix : MonoBehaviour
    {
        private float nextCheck;

        public void LateUpdate()
        {
            if (Time.unscaledTime < nextCheck)
                return;
            nextCheck = Time.unscaledTime + 0.2f;

            foreach (CrewWidget w in FindObjectsOfType<CrewWidget>())
            {
                if (w == null || w.crew == null)
                    continue;
                if (w.crew.rosterStatus != ProtoCrewMember.RosterStatus.Dead && !Mortality.IsDead(w.crew))
                    continue;
                if (w.statusField != null)
                    w.statusField.text = "<color=#ff5040>Mort</color>";
                if (w.xpEarnedField != null)
                    w.xpEarnedField.gameObject.SetActive(false);
                if (w.lvlProgressField != null)
                    w.lvlProgressField.gameObject.SetActive(false);
            }
        }
    }
}
