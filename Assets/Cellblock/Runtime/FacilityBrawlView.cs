using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    // Resident fights on screen (Codex's FightScene decides when and whether): squaring up chest to chest, then real
    // exchanges (a cross, a hook, a haymaker, a shove, a collar grab) answered by a hit, a block or a slip, with fighting
    // stances that bob and circle between blows, and finally an officer holding the two apart while they strain at each
    // other. The exchange sequence is fixed by the scene's start minute, so a reload replays the same fight.
    // Claude's (escape and fight overhaul, user request 2026-10-17).
    public sealed partial class FacilityView
    {
        const int BrawlExchangeMinutes = 6;
        static readonly string[] BrawlMoves = { "Cross", "Hook", "Shove", "Haymaker", "Grab", "Cross", "Hook" };
        int brawlImpactKey = int.MinValue;

        bool Brawling(World world, string id)
        {
            FightScene f = world.Fight;
            return f.Active && f.Phase != "Converging" && (f.A == id || f.B == id);
        }

        void RefreshBrawl(World world)
        {
            FightScene f = world.Fight;
            if (!f.Active || f.Phase == "Converging") return;
            Actor a = Live(f.A), b = Live(f.B);
            Resident ra = world.Data.Residents.Find(r => r.Id == f.A), rb = world.Data.Residents.Find(r => r.Id == f.B);
            if (a == null || b == null || ra == null || rb == null) return;
            // FacilityIncidentView walks both onto their marks first; the fight starts when they are there.
            if (Vector3.Distance(a.Root.position, V(ra.Position, a.Root.position.y)) > .15f || Vector3.Distance(b.Root.position, V(rb.Position, b.Root.position.y)) > .15f) return;
            Take(a, Hold.Body); Take(b, Hold.Body);
            a.PoseArms = b.PoseArms = true; incidentPoseCount += 2;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f), blend = 1 - Mathf.Exp(-dt * 16), fast = 1 - Mathf.Exp(-dt * 30);
            Vector3 axis = b.Root.position - a.Root.position; axis.y = 0;
            if (axis.sqrMagnitude < 0.0001f) axis = a.Root.forward;
            float yaw = Yaw(axis), tempo = Time.unscaledTime * Mathf.Sqrt(Mathf.Max(1, world.Data.Speed));
            float minutes = sceneMinute - f.PhaseMinute + sceneFraction;
            Actor officer = f.Phase == "Separating" ? Live(f.Responder) : null;
            if (f.Phase == "Tension")
            {
                // Squaring up: chests out, arms wide, a jabbing finger, stepping in and out; once, a chest bump.
                Turn(a, yaw, 600); Turn(b, yaw + 180, 600);
                float bump = Mathf.Max(0, Mathf.Sin(Mathf.Clamp01(minutes / World.FightTensionMinutes) * Mathf.PI * 3 - 1.2f));
                Posture(a, tempo, bump, blend); Posture(b, tempo + 1.3f, bump, blend);
                Offset(a, axis.normalized * (0.12f * bump + 0.05f * Mathf.Sin(tempo * 1.7f)), Vector3.zero, Quaternion.identity, blend);
                Offset(b, -axis.normalized * (0.12f * bump + 0.05f * Mathf.Sin(tempo * 1.7f + 1)), Vector3.zero, Quaternion.identity, blend);
                ShowIncidentName(a, ra.Name + "\nSQUARING UP", Fugitive); ShowIncidentName(b, rb.Name + "\nSQUARING UP", Fugitive);
                return;
            }
            if (f.Phase == "Separating" && officer != null)
            {
                Take(officer, Hold.Body); officer.PoseArms = true; incidentPoseCount++;
                // Held apart, still straining at each other over the officer's arms.
                Turn(a, yaw, 600); Turn(b, yaw + 180, 600);
                Strain(a, b, tempo, blend); Strain(b, a, tempo + 0.9f, blend);
                Offset(a, Vector3.zero, Vector3.zero, Quaternion.identity, blend); Offset(b, Vector3.zero, Vector3.zero, Quaternion.identity, blend);
                Vector3 middle = (a.Root.position + b.Root.position) * 0.5f;
                Turn(officer, Yaw(Vector3.Cross(axis, Vector3.up) * (Vector3.Dot(officer.Root.position - middle, Vector3.Cross(axis, Vector3.up)) > 0 ? -1 : 1)), 720);
                Offset(officer, Vector3.zero, Vector3.zero, Quaternion.identity, blend);
                Stance(officer, 0.84f, 0.14f, -0.14f, blend); Torso(officer, 10, 0, 0, blend);
                ArmPose(officer, 0, officer.Model.InverseTransformPoint(Chest(a.Root.position + Vector3.Cross(Vector3.up, axis).normalized * 0f, a)), blend);
                ArmPose(officer, 1, officer.Model.InverseTransformPoint(Chest(b.Root.position, b)), blend);
                Effort(officer, tempo, 3);
                ShowIncidentName(officer, "OFFICER\nSEPARATING", Lawman);
                ShowIncidentName(a, ra.Name + "\nHELD BACK", Fugitive); ShowIncidentName(b, rb.Name + "\nHELD BACK", Fugitive);
                return;
            }
            // The scuffle: exchanges on a fixed script for this scene.
            int k = Mathf.FloorToInt(minutes / BrawlExchangeMinutes);
            float t = minutes / BrawlExchangeMinutes - k;
            uint seed = (uint)(f.Started * 7919 + k * 104729 + (f.A.GetHashCode() & 0xffff));
            seed ^= seed >> 13; seed *= 0x5bd1e995; seed ^= seed >> 15;
            bool aAttacks = (seed & 1) == 0;
            string move = BrawlMoves[(seed >> 1) % (uint)BrawlMoves.Length];
            uint react = (seed >> 6) % 20;
            string reaction = react < 11 ? "Hit" : react < 17 ? "Block" : "Slip";
            Actor attacker = aAttacks ? a : b, defender = aAttacks ? b : a;
            Turn(a, yaw + Mathf.Sin(tempo * 0.8f) * 6, 600); Turn(b, yaw + 180 + Mathf.Sin(tempo * 0.8f) * 6, 600);
            float strike = move == "Haymaker" ? 0.6f : move == "Grab" ? 0.3f : 0.5f;
            float impact = Window(t, strike - 0.06f, strike + 0.02f), after = Window(t, strike + 0.02f, strike + 0.25f), recover = Window(t, 0.7f, 1);
            Vector3 step = Attack(attacker, defender, move, t, blend, fast);
            Defend(defender, attacker, move == "Grab" ? "Grabbed" : reaction, impact * (1 - recover), after * (1 - recover), tempo, blend, fast);
            Vector3 recoil = move == "Shove" || reaction == "Hit" ? -defender.Root.forward * (move == "Shove" ? 0.35f : 0.2f) * impact * (1 - recover) :
                reaction == "Slip" ? defender.Root.right * 0.15f * impact * (1 - recover) : -defender.Root.forward * 0.07f * impact * (1 - recover);
            Offset(attacker, step, Vector3.zero, Quaternion.identity, blend);
            Offset(defender, recoil, Vector3.zero, Quaternion.identity, blend);
            int key = f.Started * 1000 + k;
            if (t >= strike && brawlImpactKey != key && move != "Grab" && reaction != "Slip")
            {
                brawlImpactKey = key;
                bool clean = reaction == "Hit" && move != "Shove";
                Impact(defender.Head != null && move != "Shove" ? defender.Head.position : defender.Body.position + Vector3.up * 0.4f, clean ? (move == "Haymaker" ? 0.6f : 0.4f) : 0.22f, clean, move == "Shove" ? 0.2f : 0);
            }
            string label = move == "Grab" ? "GRAPPLING" : reaction == "Hit" && t > strike ? "HIT" : "FIGHTING";
            ShowIncidentName(attacker, (aAttacks ? ra : rb).Name + "\n" + (move == "Grab" ? "GRAPPLING" : "SWINGING"), Fugitive);
            ShowIncidentName(defender, (aAttacks ? rb : ra).Name + "\n" + label, Fugitive);
        }

        Vector3 Chest(Vector3 root, Actor a) { return a.Body.position + Vector3.up * 0.42f + (root - a.Root.position); }

        // A bladed fighting stance with the guard up, bouncing lightly on the balls of the feet.
        void Guard(Actor a, float tempo, float blend)
        {
            float bounce = Mathf.Sin(tempo * 6.5f) * 0.012f;
            Stance(a, 0.83f + bounce, 0.17f, -0.19f, blend);
            Torso(a, 9, -14, 0, blend);
            ArmPose(a, 0, new Vector3(-0.07f, 1.56f, 0.24f), blend); ArmPose(a, 1, new Vector3(0.10f, 1.52f, 0.13f), blend);
            if (a.Head != null) Joint(a.Head, new Vector3(8, 10, 0), blend);
        }
        void Posture(Actor a, float tempo, float bump, float blend)
        {
            Stance(a, 0.88f, 0.12f + 0.08f * bump, -0.12f, blend);
            Torso(a, -5 + 14 * bump, Mathf.Sin(tempo * 1.1f) * 6, 0, blend);
            float point = Mathf.Max(0, Mathf.Sin(tempo * 1.3f));
            Joint(a.Limbs[1], new Vector3(-20 - 65 * point, 0, 18 - 12 * point), blend); Joint(a.Forearms[1], new Vector3(-30 + 25 * point, 0, 0), blend);
            Joint(a.Limbs[0], new Vector3(-10 - 30 * bump, 0, -28), blend); Joint(a.Forearms[0], new Vector3(-35, 0, 0), blend);
            if (a.Head != null) Joint(a.Head, new Vector3(10, 0, 0), blend);
        }
        void Strain(Actor a, Actor other, float tempo, float blend)
        {
            Stance(a, 0.84f, 0.22f, -0.22f, blend);
            Torso(a, 20 + Mathf.Sin(tempo * 2.7f) * 5, 0, 0, blend);
            for (int arm = 0; arm < 2; arm++) ArmPose(a, arm, a.Model.InverseTransformPoint(other.Body.position + Vector3.up * (0.5f + 0.08f * Mathf.Sin(tempo * 3 + arm)) + other.Root.right * (arm == 0 ? 0.15f : -0.15f)), blend);
            Effort(a, tempo, 5);
        }
        // Returns the attacker's step toward the target for this frame.
        Vector3 Attack(Actor at, Actor target, string move, float t, float blend, float fast)
        {
            Guard(at, Time.unscaledTime, blend);
            Vector3 head = target.Head != null ? target.Head.position + Vector3.down * 0.04f : target.Body.position + Vector3.up * 0.7f;
            Vector3 aim = at.Model.InverseTransformPoint(head), chest = at.Model.InverseTransformPoint(target.Body.position + Vector3.up * 0.4f);
            float recover = Window(t, 0.62f, 0.95f);
            switch (move)
            {
                case "Cross":
                {
                    float load = Window(t, 0.2f, 0.4f), hit = Window(t, 0.42f, 0.5f) * (1 - recover);
                    Torso(at, 9 + 8 * hit, -14 - 10 * load + 42 * hit, 0, hit > 0 ? fast : blend);
                    ArmPose(at, 1, Vector3.Lerp(new Vector3(0.12f, 1.5f, 0.06f), aim, hit), hit > 0 ? fast : blend);
                    return at.Root.forward * 0.16f * hit;
                }
                case "Hook":
                {
                    float load = Window(t, 0.2f, 0.42f), hit = Window(t, 0.42f, 0.52f) * (1 - recover);
                    Torso(at, 9, -14 - 22 * load + 50 * hit, 0, hit > 0 ? fast : blend);
                    Vector3 wide = new Vector3(-0.42f, 1.5f, 0.12f), arc = Vector3.Lerp(wide, aim, hit) + new Vector3(-0.18f, 0, 0.1f) * Mathf.Sin(hit * Mathf.PI);
                    ArmPose(at, 0, Vector3.Lerp(new Vector3(-0.07f, 1.56f, 0.24f), arc, Mathf.Max(load * 0.6f, hit)), hit > 0 ? fast : blend);
                    return at.Root.forward * 0.1f * hit;
                }
                case "Haymaker":
                {
                    float load = Window(t, 0.2f, 0.52f), hit = Window(t, 0.52f, 0.62f) * (1 - recover);
                    Torso(at, 6 + 14 * hit, -14 - 38 * load + 78 * hit, 0, hit > 0 ? fast : blend);
                    Vector3 cocked = new Vector3(0.32f, 1.52f, -0.22f);
                    ArmPose(at, 1, Vector3.Lerp(Vector3.Lerp(new Vector3(0.12f, 1.5f, 0.06f), cocked, load), aim + new Vector3(0.1f, 0.05f, 0), hit), hit > 0 ? fast : blend);
                    return at.Root.forward * (0.22f * hit - 0.05f * load * (1 - hit));
                }
                case "Shove":
                {
                    float load = Window(t, 0.2f, 0.4f), hit = Window(t, 0.4f, 0.5f) * (1 - recover);
                    Torso(at, 9 + 6 * load - 4 * hit, 0, 0, blend);
                    for (int arm = 0; arm < 2; arm++)
                        ArmPose(at, arm, Vector3.Lerp(new Vector3(arm == 0 ? -0.12f : 0.12f, 1.36f, 0.2f), chest + new Vector3(arm == 0 ? -0.13f : 0.13f, 0, 0.1f), hit), hit > 0 ? fast : blend);
                    return at.Root.forward * 0.18f * hit;
                }
                default:
                {
                    // Grab the collar and wrench the other one around.
                    float grip = Window(t, 0.05f, 0.3f) * (1 - Window(t, 0.88f, 1));
                    float wrench = Mathf.Sin(Time.unscaledTime * 7) * grip;
                    Torso(at, 14 + 6 * grip, 18 * wrench, 0, blend);
                    for (int arm = 0; arm < 2; arm++) ArmPose(at, arm, Vector3.Lerp(new Vector3(arm == 0 ? -0.07f : 0.1f, 1.54f, 0.2f), chest + new Vector3(arm == 0 ? -0.12f : 0.12f, 0.12f, 0), grip), blend);
                    return at.Root.forward * 0.18f * grip;
                }
            }
        }
        void Defend(Actor d, Actor attacker, string reaction, float impact, float after, float tempo, float blend, float fast)
        {
            Guard(d, tempo, blend);
            switch (reaction)
            {
                case "Hit":
                    // The head snaps away and the body follows; the guard drops for a moment.
                    if (d.Head != null) Joint(d.Head, new Vector3(-30 * impact, 26 * impact, 10 * impact), fast);
                    Torso(d, 9 - 22 * impact, -14 + 20 * impact, -8 * impact, fast);
                    Joint(d.Limbs[0], new Vector3(-30 + 20 * impact, 0, -20 * impact), blend); Joint(d.Limbs[1], new Vector3(-20 + 20 * impact, 0, 20 * impact), blend);
                    Stance(d, 0.83f - 0.05f * after, 0.17f - 0.1f * impact, -0.19f, blend);
                    break;
                case "Block":
                    ArmPose(d, 0, new Vector3(-0.08f, 1.68f, 0.18f), fast); ArmPose(d, 1, new Vector3(0.08f, 1.66f, 0.17f), fast);
                    Torso(d, 14 - 8 * impact, -14, 0, blend);
                    if (d.Head != null) Joint(d.Head, new Vector3(16, 0, 0), blend);
                    break;
                case "Slip":
                    Torso(d, 16, -14, 22 * impact, fast);
                    if (d.Head != null) Joint(d.Head, new Vector3(10, -20 * impact, 12 * impact), fast);
                    break;
                default:
                    // Grabbed: both hands on the attacker's wrists, pulled around.
                    for (int arm = 0; arm < 2; arm++) ArmPose(d, arm, d.Model.InverseTransformPoint(attacker.Hands[1 - arm].position), blend);
                    Torso(d, 4, Mathf.Sin(Time.unscaledTime * 7 + 1) * 14, 0, blend);
                    break;
            }
        }
    }
}
