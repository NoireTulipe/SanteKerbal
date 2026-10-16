using System;
using UnityEngine;

namespace KerTech
{
    /// <summary>
    /// Pilote automatique de l'application Navigation de la tablette, assistant de mise en orbite.
    /// Virage gravitationnel classique : montée droite (SAS « Stabiliser ») jusqu'à navKickSpeed,
    /// petit basculement de navKickAngle degrés vers l'est, puis SAS natif « Prograde » : la fusée suit
    /// sa trajectoire sans jamais se présenter de biais au vent, et la gravité la couche progressivement.
    /// Coupure à l'apoapside visée, croisière hors de l'atmosphère, puis circularisation à l'apoapside
    /// (nœud de manœuvre, SAS natif Manœuvre ou Prograde). Le joueur gère les étages et peut annuler.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class Navigator : MonoBehaviour
    {
        public enum Phase { Off, Ascent, Coast, Circularize }

        public static Phase Current { get; private set; } = Phase.Off;
        public static string Status { get; private set; } = "";
        public static double TargetAltitude = -1;    // m ; -1 = valeur proposée selon le corps céleste

        // Suivi de l'opération pour la tablette et le panneau lumineux
        public static readonly string[] StepNames =
            { "Montée droite", "Basculement vers l'est", "Virage gravitationnel", "Croisière hors atmosphère", "Circularisation", "Orbite atteinte" };
        public static int Step { get; private set; }
        public static float Progress { get; private set; }       // avancement de l'étape en cours (0 à 1)
        public static string Detail { get; private set; } = "";  // ligne de détail (chiffres de l'étape)
        public static bool Succeeded { get; private set; }
        public static float FinishedAt { get; private set; } = -100f;   // Time.time de la fin (réussite ou annulation)
        private static double initialDv;

        private static void Track(int step, double progress, string detail)
        {
            Step = step;
            Progress = Mathf.Clamp01((float)progress);
            Detail = detail;
        }

        private static Guid vesselId;

        // Étapes de la montée : droite, basculement, virage naturel
        private enum Climb { Vertical, Kick, Turn }
        private static Climb climb;
        private static float kickStart;
        private static ManeuverNode node;
        private static bool warpedToNode;

        /// <summary>Altitude d'orbite proposée : 10 km au-dessus de l'atmosphère, ou 20 km sans atmosphère.</summary>
        public static double DefaultAltitude(Vessel v)
        {
            CelestialBody b = v.mainBody;
            double alt = b.atmosphere ? b.atmosphereDepth + 10000 : Math.Max(20000, b.Radius * 0.1);
            return Math.Round(alt / 1000) * 1000;
        }

        public static void StartAscent(Vessel v)
        {
            if (TargetAltitude <= 0) TargetAltitude = DefaultAltitude(v);
            vesselId = v.id;
            Current = Phase.Ascent;
            climb = Climb.Vertical;
            Succeeded = false;
            Track(0, 0, "");
            SetThrottle(1f);   // pleins gaz : la fusée part à pleine puissance dès l'allumage
            Log.Info($"Navigation : assistant de mise en orbite démarré, orbite visée {TargetAltitude / 1000:F0} km");
        }

        public static void StartCircularize(Vessel v)
        {
            vesselId = v.id;
            Current = Phase.Circularize;
            node = null;
            warpedToNode = false;
            Succeeded = false;
            Track(4, 0, "Préparation du nœud de manœuvre");
        }

        public static void Cancel(string why = "Annulé")
        {
            if (Current == Phase.Off)
                return;
            Current = Phase.Off;
            Status = why;
            FinishedAt = Time.time;
            SetThrottle(0);
            RemoveNode();
            Vessel v = FlightGlobals.ActiveVessel;
            if (v != null && v.Autopilot != null && v.Autopilot.Enabled)
                v.Autopilot.SetMode(VesselAutopilot.AutopilotMode.StabilityAssist);
            Log.Info("Navigation : " + why);
        }

        public void OnDestroy()
        {
            Current = Phase.Off;
            node = null;
        }

        public void FixedUpdate()
        {
            if (Current == Phase.Off)
                return;
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v.id != vesselId || v.isEVA)
            {
                Cancel("Vaisseau changé : assistant arrêté");
                return;
            }
            if (v.packed)
                return;   // accélération du temps : on reprend à la sortie
            switch (Current)
            {
                case Phase.Ascent: Ascent(v); break;
                case Phase.Coast: Coast(v); break;
                case Phase.Circularize: Circularize(v); break;
            }
        }

        #region Montée

        private static void Ascent(Vessel v)
        {
            double apo = v.orbit.ApA;
            if (apo >= TargetAltitude)
            {
                SetThrottle(0);
                Current = Phase.Coast;
                Status = "Apoapside atteinte : moteurs coupés, croisière vers l'espace";
                return;
            }
            if (v.ctrlState.mainThrottle < 0.01f && climb == Climb.Vertical)
                Status = "Montée : mets les gaz !";

            Vector3d up = (v.CoMD - v.mainBody.position).normalized;
            float tilt = Vector3.Angle(v.ReferenceTransform.up, (Vector3)up);   // écart à la verticale
            switch (climb)
            {
                case Climb.Vertical:
                    // Droite comme sur le pas de tir, jusqu'à avoir un peu de vitesse
                    NativeMode(v, VesselAutopilot.AutopilotMode.StabilityAssist);
                    Track(0, v.srfSpeed / Settings.NavKickSpeed, $"Vitesse {v.srfSpeed:F0} / {Settings.NavKickSpeed:F0} m/s, altitude {v.altitude:F0} m");
                    if (v.srfSpeed >= Settings.NavKickSpeed && v.altitude > 200)
                    {
                        climb = Climb.Kick;
                        kickStart = Time.time;
                    }
                    else if (v.ctrlState.mainThrottle >= 0.01f)
                        Status = $"Montée droite : {v.srfSpeed:F0} / {Settings.NavKickSpeed:F0} m/s";
                    break;

                case Climb.Kick:
                    // Petit basculement doux vers l'est (le seul moment où l'on oriente nous-mêmes)
                    // On tient l'angle jusqu'à ce que la TRAJECTOIRE se soit inclinée à son tour : passer en
                    // Prograde trop tôt redresserait la fusée sur sa trajectoire encore verticale.
                    PointTo(v, East(v, 90f - (float)Settings.NavKickAngle));
                    float pathTilt = Vector3.Angle((Vector3)v.srf_velocity.normalized, (Vector3)up);
                    Status = $"Basculement vers l'est : nez {tilt:F0}°, trajectoire {pathTilt:F0} / {Settings.NavKickAngle:F0}°";
                    Track(1, pathTilt / (Settings.NavKickAngle * 0.75), $"Nez penché de {tilt:F0}°, trajectoire {pathTilt:F0}° (cap à l'est)");
                    if (pathTilt >= Settings.NavKickAngle * 0.75 || Time.time - kickStart > 25f)
                        climb = Climb.Turn;
                    break;

                case Climb.Turn:
                    // Virage naturel : le SAS suit la trajectoire, la gravité couche la fusée
                    if (!NativeMode(v, VesselAutopilot.AutopilotMode.Prograde))
                        PointTo(v, v.srf_velocity.normalized);
                    Status = $"Virage : inclinaison {90 - tilt:F0}° au-dessus de l'horizon, apoapside {apo / 1000:F1} / {TargetAltitude / 1000:F0} km";
                    Track(2, apo / TargetAltitude, $"Inclinaison {90 - tilt:F0}°, apoapside {apo / 1000:F1} / {TargetAltitude / 1000:F0} km");
                    break;
            }
        }

        /// <summary>Croisière jusqu'à la sortie de l'atmosphère, nez sur la trajectoire, en compensant la traînée.</summary>
        private static void Coast(Vessel v)
        {
            CelestialBody b = v.mainBody;
            if (!NativeMode(v, VesselAutopilot.AutopilotMode.Prograde))
                NativeMode(v, VesselAutopilot.AutopilotMode.StabilityAssist);
            bool inAtmo = b.atmosphere && v.altitude < b.atmosphereDepth;
            if (inAtmo)
            {
                // La traînée fait baisser l'apoapside : petits coups de gaz pour la maintenir
                SetThrottle(v.orbit.ApA < TargetAltitude * 0.995 ? 0.3f : 0f);
                Status = $"Croisière : sortie de l'atmosphère ({v.altitude / 1000:F1} / {b.atmosphereDepth / 1000:F0} km)";
                Track(3, v.altitude / b.atmosphereDepth, $"Altitude {v.altitude / 1000:F1} / {b.atmosphereDepth / 1000:F0} km, apoapside {v.orbit.ApA / 1000:F1} km");
                return;
            }
            SetThrottle(0);
            StartCircularize(v);
        }

        #endregion

        #region Circularisation

        private static void Circularize(Vessel v)
        {
            Orbit o = v.orbit;
            double now = Planetarium.GetUniversalTime();
            if (node == null)
            {
                if (v.patchedConicSolver == null)
                {
                    Cancel("Nœuds de manœuvre indisponibles (bâtiment de suivi à améliorer ?)");
                    return;
                }
                // Nœud à l'apoapside : vitesse circulaire moins vitesse actuelle, vers l'avant
                double ut = now + o.timeToAp;
                double r = o.ApR;
                double dv = Math.Sqrt(o.referenceBody.gravParameter / r) - o.getOrbitalVelocityAtUT(ut).magnitude;
                RemoveNode();
                node = v.patchedConicSolver.AddManeuverNode(ut);
                node.DeltaV = new Vector3d(0, 0, dv);
                initialDv = Math.Max(1, Math.Abs(dv));
                v.patchedConicSolver.UpdateFlightPlan();
                warpedToNode = false;
                Log.Info($"Navigation : nœud de circularisation dans {o.timeToAp:F0} s, {dv:F1} m/s");
            }

            Vector3d burn = node.GetBurnVector(o);
            double remaining = burn.magnitude;
            double accel = MaxAcceleration(v);
            double burnTime = accel > 0 ? remaining / accel : double.PositiveInfinity;
            double toNode = node.UT - now;

            // Orientation : mode natif « Manœuvre » (suit le nœud), sinon « Prograde » (à l'apoapside,
            // la poussée de circularisation est vers l'avant), sinon orientation maison en dernier recours
            string how;
            if (NativeMode(v, VesselAutopilot.AutopilotMode.Maneuver))
                how = "mode Manœuvre";
            else if (NativeMode(v, VesselAutopilot.AutopilotMode.Prograde))
                how = "mode Prograde";
            else
            {
                PointTo(v, burn.normalized);
                how = "orientation assistée";
            }
            float error = Vector3.Angle(v.ReferenceTransform.up, (Vector3)burn.normalized);

            // Fin : il ne reste presque rien, ou on a dépassé (le vecteur de poussée s'est retourné)
            if (remaining < 0.3 || (v.ctrlState.mainThrottle > 0 && Vector3d.Dot(burn, (Vector3d)v.ReferenceTransform.up) < 0))
            {
                SetThrottle(0);
                RemoveNode();
                Current = Phase.Off;
                Status = $"Orbite atteinte : {o.ApA / 1000:F1} x {o.PeA / 1000:F1} km";
                Succeeded = true;
                FinishedAt = Time.time;
                Track(5, 1, $"Apoapside {o.ApA / 1000:F1} km, périapside {o.PeA / 1000:F1} km");
                HealthEvents.Announce(v, Status);
                if (v.Autopilot != null)
                    v.Autopilot.SetMode(VesselAutopilot.AutopilotMode.StabilityAssist);
                return;
            }
            if (accel <= 0)
            {
                SetThrottle(0);
                Status = "Pas de poussée : allume un moteur ou passe à l'étage suivant";
                Track(4, 1 - remaining / initialDv, "Pas de poussée : sépare l'étage vide !");
                return;
            }

            // Accélère le temps jusqu'à peu avant l'allumage, une fois orienté
            double startIn = toNode - burnTime / 2;
            if (!warpedToNode && startIn > 60 && error < 10 && TimeWarp.fetch != null)
            {
                warpedToNode = true;
                TimeWarp.fetch.WarpTo(now + startIn - 20);
            }

            if (startIn > 0)
            {
                SetThrottle(0);
                Status = $"Circularisation : allumage dans {KSPUtil.PrintTimeCompact(startIn, false)} ({remaining:F0} m/s, {burnTime:F0} s)";
                Track(4, 0, $"Allumage dans {KSPUtil.PrintTimeCompact(startIn, false)} : {remaining:F0} m/s à gagner en {burnTime:F0} s ({how})");
                return;
            }
            if (error > 5 && v.ctrlState.mainThrottle < 0.01f)
            {
                Status = $"Circularisation : orientation en cours ({error:F0}°, {how})";
                Track(4, 0, $"Orientation vers la manœuvre : encore {error:F0}° ({how})");
                return;
            }
            // Pleins gaz, puis on ralentit sur la dernière seconde pour ne pas dépasser
            SetThrottle((float)Math.Max(0.05, Math.Min(1.0, remaining / accel)));
            Status = $"Circularisation : poussée, reste {remaining:F1} m/s";
            Track(4, 1 - remaining / initialDv, $"Poussée : reste {remaining:F1} m/s, périapside {o.PeA / 1000:F1} km");
        }

        private static void RemoveNode()
        {
            if (node != null)
            {
                try { node.RemoveSelf(); } catch (Exception) { }
                node = null;
            }
        }

        #endregion

        #region Outils

        /// <summary>Direction vers l'est, inclinée de `pitch` degrés au-dessus de l'horizon.</summary>
        private static Vector3d East(Vessel v, float pitch)
        {
            Vector3d up = (v.CoMD - v.mainBody.position).normalized;
            // Est = sens de rotation du sol (vitesse du sol au point du vaisseau), ramené à l'horizontale
            Vector3d east = Vector3d.Exclude(up, v.mainBody.getRFrmVel(v.CoMD));
            if (east.sqrMagnitude < 1e-6)
                east = Vector3d.Exclude(up, v.mainBody.transform.right);
            east = east.normalized;
            double p = pitch * Math.PI / 180.0;
            return (east * Math.Cos(p) + up * Math.Sin(p)).normalized;
        }

        /// <summary>
        /// Active le SAS dans un mode natif de KSP s'il est disponible (niveau du pilote ou de la sonde).
        /// Renvoie false si ce mode n'est pas disponible.
        /// </summary>
        private static bool NativeMode(Vessel v, VesselAutopilot.AutopilotMode mode)
        {
            VesselAutopilot ap = v.Autopilot;
            if (ap == null || !ap.CanSetMode(mode))
                return false;
            if (!v.ActionGroups[KSPActionGroup.SAS])
                v.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
            if (ap.Mode != mode)
                ap.SetMode(mode);
            return true;
        }

        /// <summary>
        /// Dernier recours, si aucun mode natif n'est disponible : oriente le nez vers `direction` (monde)
        /// en verrouillant l'orientation du SAS, sans changer le roulis.
        /// </summary>
        private static void PointTo(Vessel v, Vector3d direction)
        {
            if (v.Autopilot == null)
                return;
            if (!v.ActionGroups[KSPActionGroup.SAS])
                v.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
            if (v.Autopilot.Mode != VesselAutopilot.AutopilotMode.StabilityAssist)
                v.Autopilot.SetMode(VesselAutopilot.AutopilotMode.StabilityAssist);
            Vector3 nose = ((Vector3)direction).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(v.ReferenceTransform.forward, nose);
            if (forward.sqrMagnitude < 1e-4)
                forward = Vector3.ProjectOnPlane(v.ReferenceTransform.right, nose);
            v.Autopilot.SAS.LockRotation(Quaternion.LookRotation(forward.normalized, nose));
        }

        private static void SetThrottle(float value)
        {
            if (FlightInputHandler.state != null)
                FlightInputHandler.state.mainThrottle = Mathf.Clamp01(value);
        }

        /// <summary>Accélération maximale (m/s²) des moteurs allumés en état de marche.</summary>
        public static double MaxAcceleration(Vessel v)
        {
            double thrust = 0;
            foreach (Part p in v.parts)
                foreach (PartModule m in p.Modules)
                    if (m is ModuleEngines e && e.EngineIgnited && e.isOperational && !e.flameout)
                        thrust += e.GetMaxThrust() * e.thrustPercentage / 100.0;
            return v.totalMass > 0 ? thrust / v.totalMass : 0;
        }

        #endregion
    }
}
