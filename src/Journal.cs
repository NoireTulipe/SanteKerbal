using System.Collections.Generic;

namespace KerTech
{
    /// <summary>Une ligne du journal de bord.</summary>
    public class JournalEntry
    {
        public double UT;
        public string Vessel;
        public string Text;
        public bool Alert;   // événement grave (panne, manque, mort...) : affiché en rouge

        public void Save(ConfigNode node)
        {
            node.AddValue("ut", UT.ToString("R"));
            node.AddValue("vessel", Vessel ?? "");
            node.AddValue("text", Text ?? "");
            node.AddValue("alert", Alert);
        }

        public static JournalEntry Load(ConfigNode node)
        {
            var e = new JournalEntry();
            node.TryGetValue("ut", ref e.UT);
            e.Vessel = node.GetValue("vessel");
            e.Text = node.GetValue("text");
            node.TryGetValue("alert", ref e.Alert);
            return string.IsNullOrEmpty(e.Text) ? null : e;
        }
    }

    /// <summary>
    /// Journal de bord : historique des événements de santé de tous les vaisseaux, sauvegardé dans la
    /// partie (par HealthScenario). Permet de comprendre ce qui s'est passé pendant une longue
    /// accélération du temps, quand les messages à l'écran ont disparu.
    /// </summary>
    internal static class Journal
    {
        public const int MaxEntries = 300;
        public static readonly List<JournalEntry> Entries = new List<JournalEntry>();

        public static void Add(string vessel, string text, bool alert)
        {
            double ut = Planetarium.fetch != null ? Planetarium.GetUniversalTime() : 0;
            // Pas de doublon immédiat (un même message répété dans la même seconde de jeu)
            if (Entries.Count > 0)
            {
                JournalEntry last = Entries[Entries.Count - 1];
                if (last.Text == text && last.Vessel == vessel && System.Math.Abs(last.UT - ut) < 1)
                    return;
            }
            Entries.Add(new JournalEntry { UT = ut, Vessel = vessel, Text = text, Alert = alert });
            if (Entries.Count > MaxEntries)
                Entries.RemoveRange(0, Entries.Count - MaxEntries);
        }

        public static void Save(ConfigNode parent)
        {
            ConfigNode node = parent.AddNode("JOURNAL");
            foreach (JournalEntry e in Entries)
                e.Save(node.AddNode("ENTRY"));
        }

        public static void Load(ConfigNode parent)
        {
            Entries.Clear();
            ConfigNode node = parent.GetNode("JOURNAL");
            if (node == null)
                return;
            foreach (ConfigNode en in node.GetNodes("ENTRY"))
            {
                JournalEntry e = JournalEntry.Load(en);
                if (e != null)
                    Entries.Add(e);
            }
        }
    }
}
