using System;

namespace SanteKerbal
{
    /// <summary>État de santé d'un Kerbal, sauvegardé dans la partie par HealthScenario.</summary>
    public class KerbalHealth
    {
        public const double Max = 100.0;

        public string Name;
        public double Health = Max;
        public bool Dead;

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
            node.TryGetValue("lackWater", ref k.LackWater);
            node.TryGetValue("lackFood", ref k.LackFood);
            return k;
        }

        public override string ToString()
        {
            return Dead ? $"{Name} (mort)" : $"{Name} {Health:F1}";
        }
    }
}
