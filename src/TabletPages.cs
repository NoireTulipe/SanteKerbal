using System.Collections.Generic;
using UnityEngine;

namespace SanteKerbal
{
    /// <summary>Pages de la tablette : Santé, Personnel, Inventaire, Serre, Recycleur, Énergie.</summary>
    public partial class Tablet
    {
        private const float EcgHeight = 40f;

        // Échelle choisie pour la courbe électrique (conservée pendant la session)
        private static int powerScale;

        private void DrawPage(Rect area)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            GUILayout.BeginVertical(GUILayout.Width(area.width - U(24)));
            if (!Settings.Enabled)
            {
                GUILayout.Label("SantéKerbal est désactivé pour cette partie.");
                GUILayout.Label("(Échap > Paramètres de difficulté > onglet SantéKerbal)");
            }
            else if (v == null)
            {
                GUILayout.Label("Aucun vaisseau actif.");
            }
            else if (v.isEVA)
            {
                GUILayout.Label("Kerbal en EVA : non suivi pour l'instant.");
            }
            else
            {
                switch (page)
                {
                    case Page.Health: DrawHealthPage(v); break;
                    case Page.Crew: DrawCrewPage(v); break;
                    case Page.Stocks: DrawStocksPage(v); break;
                    case Page.Greenhouse: DrawGreenhousePage(v); break;
                    case Page.Recycler: DrawRecyclerPage(v); break;
                    case Page.Power: DrawPowerPage(v); break;
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        #region Alertes de l'écran d'accueil

        private bool HasAlert(Page p, Vessel v)
        {
            if (!Settings.Enabled || v.isEVA)
                return false;
            List<ProtoCrewMember> crew = v.GetVesselCrew();
            switch (p)
            {
                case Page.Health:
                    foreach (ProtoCrewMember pcm in crew)
                    {
                        KerbalHealth k = HealthScenario.Instance.Get(pcm);
                        if (!k.Dead && k.Health <= 25) return true;
                    }
                    return false;
                case Page.Stocks:
                    foreach (ProtoCrewMember pcm in crew)
                    {
                        KerbalHealth k = HealthScenario.Instance.Get(pcm);
                        if (!k.Dead && !k.Asleep && (k.LackWater || k.LackFood)) return true;
                    }
                    return false;
                case Page.Crew:
                    foreach (ProtoCrewMember pcm in crew)
                    {
                        KerbalHealth k = HealthScenario.Instance.Get(pcm);
                        if (k.Asleep && k.LackWater) return true;   // caisson sans électricité
                    }
                    return false;
                case Page.Greenhouse:
                    foreach (Part part in v.parts)
                        foreach (PartModule m in part.Modules)
                            if (m is ModuleSKGreenhouse g && g.isActive && g.status.StartsWith("Manque")) return true;
                    return false;
                case Page.Recycler:
                    foreach (Part part in v.parts)
                        foreach (PartModule m in part.Modules)
                            if (m is ModuleSKRecycler r && r.isActive && r.status.StartsWith("Manque")) return true;
                    return false;
                case Page.Power:
                    return PowerHistory.HasData && PowerHistory.Capacity > 0
                           && PowerHistory.Amount / PowerHistory.Capacity < 0.15 && PowerHistory.NetRate < 0;
                default:
                    return false;
            }
        }

        #endregion

        #region Santé

        private void DrawHealthPage(Vessel v)
        {
            List<ProtoCrewMember> crew = v.GetVesselCrew();
            if (crew.Count == 0)
            {
                GUILayout.Label("Pas d'équipage à bord.");
                return;
            }
            foreach (ProtoCrewMember pcm in crew)
            {
                KerbalHealth k = HealthScenario.Instance.Get(pcm);
                GUILayout.BeginHorizontal();
                GUILayout.Label(k.Name, GUILayout.Width(U(150)));
                Rect bar = GUILayoutUtility.GetRect(U(150), U(18), GUILayout.Width(U(150)));
                DrawBar(bar, k.Dead ? 0f : (float)(k.Health / KerbalHealth.Max), HealthColor(k));
                GUI.Label(bar, k.Dead ? "MORT" : k.Health.ToString("F0"), barLabel);

                HeartMonitor m = Settings.Monitor ? MonitorSystem.Get(k.Name) : null;
                if (m != null)
                    GUILayout.Label((k.Dead ? "0" : m.Bpm.ToString("F0")) + " BPM", bpmLabel);
                GUILayout.EndHorizontal();

                if (m != null)
                {
                    Rect trace = GUILayoutUtility.GetRect(U(100), U(EcgHeight), GUILayout.ExpandWidth(true), GUILayout.Height(U(EcgHeight)));
                    m.Draw(trace, MonitorSystem.TraceColor(k), HeartMonitor.Samples);
                }

                string state = StateText(k);
                if (state != null)
                    GUILayout.Label(state, richLabel);

                if (Settings.DebugTools && !k.Dead)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("   test :", GUILayout.Width(U(60)));
                    if (GUILayout.Button("-20", smallButton)) ChangeHealth(k, -20, v);
                    if (GUILayout.Button("+20", smallButton)) ChangeHealth(k, +20, v);
                    if (GUILayout.Button("Tuer", smallButton)) Mortality.Kill(k, "outil de test", v);
                    GUILayout.EndHorizontal();
                }
                GUILayout.Space(U(6));
            }
        }

        /// <summary>État notable en une ligne colorée (ou null si tout va bien).</summary>
        private static string StateText(KerbalHealth k)
        {
            if (k.Dead) return null;
            if (k.Asleep) return k.LackWater
                ? "<color=#ff8060>   Hibernation : caisson sans électricité !</color>"
                : "<color=#9fd0ff>   En hibernation</color>";
            if (k.Waking)
            {
                double left = System.Math.Max(0, k.WakeUntil - Planetarium.GetUniversalTime());
                return $"<color=#9fd0ff>   Réveil en cours : encore {KSPUtil.PrintTimeCompact(left, false)}</color>";
            }
            if (k.LackWater || k.LackFood)
            {
                string lack = k.LackWater && k.LackFood ? "soif et faim" : k.LackWater ? "soif" : "faim";
                return "<color=#ff8060>   ! " + lack + "</color>";
            }
            return null;
        }

        private static void ChangeHealth(KerbalHealth k, double delta, Vessel vessel)
        {
            if (delta < 0)
                k.Damage(-delta);
            else
                k.Heal(delta);
            Log.Info($"[{vessel.vesselName}] Test : santé de {k.Name} -> {k.Health:F0}");
            if (k.Health <= 0)
                Mortality.Kill(k, "outil de test", vessel);
        }

        private static Color HealthColor(KerbalHealth k)
        {
            if (k.Dead) return Color.gray;
            if (k.Asleep) return new Color(0.4f, 0.65f, 0.95f);
            if (k.Health > 50) return new Color(0.2f, 0.8f, 0.2f);
            if (k.Health > 25) return new Color(0.9f, 0.75f, 0.1f);
            return new Color(0.9f, 0.2f, 0.15f);
        }

        private static void DrawBar(Rect rect, float fraction, Color color)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        #endregion

        #region Personnel

        private void DrawCrewPage(Vessel v)
        {
            List<ProtoCrewMember> crew = v.GetVesselCrew();
            if (crew.Count == 0)
            {
                GUILayout.Label("Pas d'équipage à bord.");
                return;
            }
            foreach (ProtoCrewMember pcm in crew)
            {
                KerbalHealth k = HealthScenario.Instance.Get(pcm);
                string role = pcm.experienceTrait != null ? pcm.experienceTrait.Title : "";
                GUILayout.Label($"<b>{k.Name}</b>  <color=#a0a0a0>{role}, {pcm.experienceLevel} étoile(s)</color>", richLabel);

                Part where = null;
                foreach (Part p in v.parts)
                    if (p.protoModuleCrew.Contains(pcm)) { where = p; break; }
                string state = k.Dead ? "<color=#a0a0a0>Mort</color>"
                    : k.Asleep ? "<color=#9fd0ff>Endormi</color>"
                    : k.Waking ? "<color=#9fd0ff>Se réveille</color>"
                    : "<color=#7fe08a>Éveillé</color>";
                GUILayout.Label($"   {(where != null ? where.partInfo.title : "?")} - {state}", richLabel);
                Hibernation.Action action = Hibernation.ActionFor(k, pcm, v);
                if (action != Hibernation.Action.None)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(U(16));
                    if (GUILayout.Button(Hibernation.Label(action), smallButton, GUILayout.Width(U(110))))
                        Hibernation.Do(action, k, pcm, v);
                    GUILayout.EndHorizontal();
                }

                if (k.Waking)
                {
                    double left = System.Math.Max(0, k.WakeUntil - Planetarium.GetUniversalTime());
                    GUILayout.Label($"<color=#9fd0ff>   Opérationnel dans {KSPUtil.PrintTimeCompact(left, false)}</color>", richLabel);
                }
                GUILayout.Space(U(4));
            }

            Part free = Hibernation.Available ? Hibernation.FreeCryoPart(v) : null;
            if (Hibernation.Available)
                GUILayout.Label(free != null ? $"Place libre en hibernation : {free.partInfo.title}" : "Aucune place libre en hibernation.");
        }

        #endregion

        #region Inventaire

        private void DrawStocksPage(Vessel v)
        {
            int awake = 0, sleepers = 0;
            foreach (ProtoCrewMember pcm in v.GetVesselCrew())
            {
                KerbalHealth k = HealthScenario.Instance.Get(pcm);
                if (k.Dead) continue;
                if (k.Asleep) sleepers++;
                else awake++;
            }
            GUILayout.Label($"Bouches à nourrir : {awake}" + (sleepers > 0 ? $"  (+ {sleepers} en hibernation)" : ""));
            GUILayout.Space(U(4));
            LifeResources.GetTotals(v, LifeResources.Water, out double water, out double waterMax);
            LifeResources.GetTotals(v, LifeResources.Food, out double food, out double foodMax);
            SupplyPanel.Draw(water, waterMax, food, foodMax, awake, Equipment.FromParts(v.parts), s);
            GUILayout.Space(U(4));
            GUILayout.Label($"<color=#a0a0a0>Consommation : {Settings.WaterPerDay:F1} eau et {Settings.FoodPerDay:F1} nourriture par Kerbal et par jour.</color>", richLabel);
        }

        #endregion

        #region Serre et recycleur

        private void DrawGreenhousePage(Vessel v)
        {
            int count = 0;
            double produced = 0;
            foreach (Part part in v.parts)
                foreach (PartModule m in part.Modules)
                    if (m is ModuleSKGreenhouse g)
                    {
                        count++;
                        if (g.isActive) produced += g.foodPerDay;
                        DrawEquipment(part, g, $"+{g.foodPerDay:F1} nourriture/j, -{g.foodPerDay * g.waterPerFood:F1} eau/j, {g.ecPerSecond:F2} élec/s");
                    }
            if (count == 0)
            {
                GUILayout.Label("Aucune serre à bord.");
                GUILayout.Label("<color=#a0a0a0>Serre hydroponique SK-G2 : catégorie Utilitaire.</color>", richLabel);
                return;
            }
            int eaters = 0;
            foreach (ProtoCrewMember pcm in v.GetVesselCrew())
            {
                KerbalHealth k = HealthScenario.Instance.Get(pcm);
                if (!k.Dead && !k.Asleep) eaters++;
            }
            double need = eaters * Settings.FoodPerDay;
            GUILayout.Space(U(4));
            GUILayout.Label(produced >= need
                ? $"<color=#7fe08a>Les serres nourrissent tout l'équipage ({produced:F1} / {need:F1} par jour).</color>"
                : $"Les serres couvrent {produced:F1} des {need:F1} rations quotidiennes.", richLabel);
        }

        private void DrawRecyclerPage(Vessel v)
        {
            int count = 0;
            foreach (Part part in v.parts)
                foreach (PartModule m in part.Modules)
                    if (m is ModuleSKRecycler r)
                    {
                        count++;
                        DrawEquipment(part, r, $"{r.recycleRatio:P0} de l'eau de {r.kerbalsServed:F0} Kerbal(s), {r.ecPerSecond:F2} élec/s");
                    }
            if (count == 0)
            {
                GUILayout.Label("Aucun recycleur à bord.");
                GUILayout.Label("<color=#a0a0a0>Recycleur d'eau SK-R3 : catégorie Utilitaire.</color>", richLabel);
            }
        }

        /// <summary>Une ligne par équipement : nom, état, caractéristiques, bouton marche / arrêt.</summary>
        private void DrawEquipment(Part part, ModuleSKEquipment m, string specs)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{part.partInfo.title}</b>", richLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(m.isActive ? "Arrêter" : "Démarrer", smallButton, GUILayout.Width(U(90))))
                m.Toggle();
            GUILayout.EndHorizontal();
            string color = !m.isActive ? "#a0a0a0" : m.status.StartsWith("Manque") ? "#ff8060" : "#7fe08a";
            GUILayout.Label($"   <color={color}>{(m.isActive ? m.status : "Arrêté")}</color>", richLabel);
            GUILayout.Label($"   <color=#a0a0a0>{specs}</color>", richLabel);
            GUILayout.Space(U(4));
        }

        #endregion

        #region Énergie

        private void DrawPowerPage(Vessel v)
        {
            if (!Settings.PowerGraph || !PowerHistory.HasData)
            {
                GUILayout.Label(PowerHistory.HasData ? "Courbe électrique désactivée (menu de difficulté)." : "Pas de batterie à bord.");
                return;
            }
            double amount = PowerHistory.Amount, capacity = PowerHistory.Capacity, rate = PowerHistory.NetRate;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>Batteries</b> : {amount:F0} / {capacity:F0} ({amount / capacity:P0})", richLabel);
            GUILayout.FlexibleSpace();
            for (int i = 0; i < PowerHistory.ScaleLabels.Length; i++)
                if (GUILayout.Toggle(powerScale == i, PowerHistory.ScaleLabels[i], smallButton, GUILayout.Width(U(54))))
                    powerScale = i;
            GUILayout.EndHorizontal();

            Rect graph = GUILayoutUtility.GetRect(U(100), U(90), GUILayout.ExpandWidth(true), GUILayout.Height(U(90)));
            PowerHistory.Draw(graph, powerScale);

            string trend;
            if (rate > 0.005)
            {
                string full = capacity - amount > 0.5 ? $", pleines dans {KSPUtil.PrintTimeCompact((capacity - amount) / rate, false)}" : ", pleines";
                trend = $"<color=#7fe08a>+{rate:F2}/s en charge{full}</color>";
            }
            else if (rate < -0.005)
                trend = $"<color=#ff8060>{rate:F2}/s en décharge, vides dans {KSPUtil.PrintTimeCompact(amount / -rate, false)}</color>";
            else
                trend = "Stable";
            GUILayout.Label(trend, richLabel);

            // Part de la survie dans la consommation
            Equipment eq = Equipment.FromParts(v.parts);
            int sleepers = 0;
            foreach (ProtoCrewMember pcm in v.GetVesselCrew())
            {
                KerbalHealth k = HealthScenario.Instance.Get(pcm);
                if (k.Asleep && !k.Dead) sleepers++;
            }
            double cryo = sleepers * Settings.CryoEcPerSecond;
            double recyclers = 0, greenhouses = 0;
            foreach (ModuleSKRecycler r in eq.Recyclers) recyclers += r.ecPerSecond;
            foreach (ModuleSKGreenhouse g in eq.Greenhouses) greenhouses += g.ecPerSecond;
            if (cryo + recyclers + greenhouses > 0)
            {
                GUILayout.Label("<b>Survie</b>", sectionStyle);
                if (cryo > 0) GUILayout.Label($"   Caissons d'hibernation : {cryo:F2}/s ({sleepers} dormeur(s))");
                if (greenhouses > 0) GUILayout.Label($"   Serres : {greenhouses:F2}/s");
                if (recyclers > 0) GUILayout.Label($"   Recycleurs : {recyclers:F2}/s");
                GUILayout.Label($"   Total : {cryo + recyclers + greenhouses:F2}/s");
            }
        }

        #endregion
    }
}
