using System;

namespace KerTech
{
    /// <summary>État de santé d'un Kerbal, sauvegardé dans la partie par HealthScenario.</summary>
    public class KerbalHealth
    {
        public const double Max = 100.0;

        public string Name;
        public double Health = Max;
        public bool Dead;
        public bool Asleep;        // en hibernation
        public double WakeUntil;   // UT de fin du réveil (0 = pas en train de se réveiller)
        public uint ReturnPartId;  // pièce d'origine (persistentId) à regagner après le réveil, 0 = aucune

        public bool Waking => WakeUntil > 0;
        /// <summary>Ne peut ni piloter, ni sortir, ni être transféré.</summary>
        public bool Incapacitated => Dead || Asleep || Waking;

        // Derniers états connus, pour n'envoyer les messages qu'au changement
        public bool LackWater;
        public bool LackFood;

        public KerbalHealth(string name)
        {
            Name = name;
        }

        public void Damage(double amount)
        {
            if (Dead || amount <= 0)
                return;
            Health = Math.Max(0.0, Health - amount);
        }

        public void Heal(double amount)
        {
            if (Dead || amount <= 0)
                return;
            Health = Math.Min(Max, Health + amount);
        }

        public void Save(ConfigNode node)
        {
            node.AddValue("name", Name);
            node.AddValue("health", Health);
            node.AddValue("dead", Dead);
            node.AddValue("asleep", Asleep);
            node.AddValue("wakeUntil", WakeUntil.ToString("R"));
            node.AddValue("returnPartId", ReturnPartId);
            node.AddValue("lackWater", LackWater);
            node.AddValue("lackFood", LackFood);
        }

        public static KerbalHealth Load(ConfigNode node)
        {
            string name = node.GetValue("name");
            if (string.IsNullOrEmpty(name))
                return null;
            var k = new KerbalHealth(name);
            node.TryGetValue("health", ref k.Health);
            node.TryGetValue("dead", ref k.Dead);
            node.TryGetValue("asleep", ref k.Asleep);
            node.TryGetValue("wakeUntil", ref k.WakeUntil);
            string ret = node.GetValue("returnPartId");
            if (ret != null)
                uint.TryParse(ret, out k.ReturnPartId);
            node.TryGetValue("lackWater", ref k.LackWater);
            node.TryGetValue("lackFood", ref k.LackFood);
            return k;
        }

        public override string ToString()
        {
            if (Dead) return $"{Name} (mort)";
            if (Asleep) return $"{Name} {Health:F1} (endormi)";
            if (Waking) return $"{Name} {Health:F1} (réveil)";
            return $"{Name} {Health:F1}";
        }
    }
}
