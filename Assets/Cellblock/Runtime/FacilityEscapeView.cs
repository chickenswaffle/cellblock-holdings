using System.Collections.Generic;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    // The escape on screen. The core decides each exchange of the struggle when it starts (EscapeStruggle), so this view
    // stages the build-up to the blow that actually lands: clinch and sway, a wind-up and a haymaker, a two-handed shove, a
    // level-change takedown, a pin with a knee in the back, cuffs, surrender, knockdowns and get-ups, the cuffed walk back,
    // a prisoner rattling a closed gate and glancing back at the officers closing in. Dust, a flash and a camera kick mark
    // the impacts. Claude's (escape overhaul, user request 2026-10-17). It runs last in RefreshIncidentVisuals and keeps its
    // own copy of every body part it holds, so other animators writing the same joints earlier in the frame cannot water
    // its poses down.
    public sealed partial class FacilityView
    {
        sealed class EscapeCast
        {
            public float ProneYaw; public int ProneKey = int.MinValue;
            public int ImpactKey = int.MinValue, LandingKey = int.MinValue, StaggerKey = int.MinValue, StaggerLanding = int.MinValue;
            public float NextGlance, GlanceUntil;
            public string Beat = "";
        }
        enum Hold { Head, Upper, Body }
        sealed class HeldPose
        {
            public readonly Quaternion[] Rotations = new Quaternion[10];
            public Vector3 ModelPosition, BodyPosition; public Quaternion ModelRotation; public float HipY; public float Yaw;
            public int Frame = -100; public Hold Parts;
        }
        sealed class Puff { public Transform Root; public float Start = -10, Life = 0.7f, Size = 0.5f; public bool Flash; }
        readonly EscapeCast cast = new EscapeCast();
        readonly Dictionary<Actor, HeldPose> heldPoses = new Dictionary<Actor, HeldPose>();
        readonly List<Actor> heldThisFrame = new List<Actor>();
        readonly List<Puff> puffs = new List<Puff>();
        Transform puffRoot;
        int sceneMinute = -1; float sceneFraction, hitStopUntil;
        // Felt by the camera when an impact lands; GameController turns it into a short shake.
        public float CameraImpulse { get; private set; }
        public Vector3 CameraImpulsePoint { get; private set; }
        const float FootRest = HipHeight - Thigh - Shin;

        // The controller advances one game minute every 0.2 s per unit of speed: this is how far into the current minute we are.
        void AdvanceSceneClock(World world)
        {
            float dt = Mathf.Min(SceneFrameDelta, 0.1f);
            if (world.Data.Minute != sceneMinute) { sceneMinute = world.Data.Minute; sceneFraction = 0; }
            else if (Time.unscaledTime >= hitStopUntil) sceneFraction = Mathf.Min(0.999f, sceneFraction + dt * 5 * world.Data.Speed);
            CameraImpulse = Mathf.MoveTowards(CameraImpulse, 0, dt * 1.4f);
        }
        float Beat(int start, int end) { return Mathf.Clamp01((sceneMinute - start + sceneFraction) / Mathf.Max(1, end - start)); }
        static float Window(float t, float from, float to) { return Smooth01((t - from) / Mathf.Max(0.0001f, to - from)); }

        void RefreshEscapeScene(World world)
        {
            AdvanceSceneClock(world); heldThisFrame.Clear();
            RefreshBrawl(world);
            EscapeStruggle s = world.Struggle; Resident escapee = world.Escapee;
            Actor runner = Live(escapee == null ? "" : escapee.Id), lead = Live(s.Officer), helper = Live(s.Helper);
            Actor down = s.Staggered != "" && s.StaggeredUntil > world.Data.Minute - 2 ? Live(s.Staggered) : null;
            if (down != null) { Take(down, Hold.Body); StaggerScene(down, runner, s); }
            if (runner != null)
            {
                if (s.Active && s.Resident == escapee.Id && lead != null)
                {
                    Take(runner, Hold.Body); Take(lead, Hold.Body); if (helper != null) Take(helper, Hold.Body);
                    StruggleScene(runner, escapee, lead, helper, s, world);
                }
                else RunScene(runner, escapee, world);
            }
            Actor escort = Live(s.Escort), prisoner = Live(s.Escorted);
            if (escort != null && prisoner != null && escort != down) { Take(escort, Hold.Upper); Take(prisoner, Hold.Upper); EscortScene(escort, prisoner); }
            RefreshStaffActionScene(world);
            foreach (Actor actor in heldThisFrame) Keep(actor);
            UpdatePuffs();
            RefreshAlarmMood(world);
            RefreshContactShadows();
        }
        Actor Live(string id)
        {
            Actor actor;
            return !string.IsNullOrEmpty(id) && actors.TryGetValue(id, out actor) && actor.Root.gameObject.activeInHierarchy ? actor : null;
        }

        // Restore this scene's own copy of the parts it holds (or adopt the current pose when the hold is new), pose, then Keep.
        void Take(Actor a, Hold parts)
        {
            if (heldThisFrame.Contains(a)) return;
            heldThisFrame.Add(a);
            HeldPose pose;
            if (!heldPoses.TryGetValue(a, out pose)) { pose = new HeldPose(); heldPoses.Add(a, pose); }
            bool fresh = Time.frameCount - pose.Frame > 2 || pose.Parts != parts;
            pose.Parts = parts;
            Transform[] joints = JointsOf(a);
            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i] == null || !Owns(parts, i)) continue;
                if (fresh) pose.Rotations[i] = joints[i].localRotation; else joints[i].localRotation = pose.Rotations[i];
            }
            if (parts != Hold.Body) return;
            if (fresh)
            {
                pose.ModelPosition = a.Model.localPosition; pose.ModelRotation = a.Model.localRotation; pose.BodyPosition = a.Body.localPosition;
                pose.HipY = a.Limbs[2].localPosition.y; pose.Yaw = a.Root.eulerAngles.y;
            }
            else
            {
                a.Model.localPosition = pose.ModelPosition; a.Model.localRotation = pose.ModelRotation; a.Body.localPosition = pose.BodyPosition;
                for (int leg = 2; leg < 4; leg++) a.Limbs[leg].localPosition = new Vector3(a.Limbs[leg].localPosition.x, pose.HipY, 0);
                a.Root.rotation = Quaternion.Euler(0, pose.Yaw, 0);
            }
        }
        void Keep(Actor a)
        {
            HeldPose pose = heldPoses[a]; pose.Frame = Time.frameCount;
            Transform[] joints = JointsOf(a);
            for (int i = 0; i < joints.Length; i++) if (joints[i] != null && Owns(pose.Parts, i)) pose.Rotations[i] = joints[i].localRotation;
            if (pose.Parts != Hold.Body) return;
            pose.ModelPosition = a.Model.localPosition; pose.ModelRotation = a.Model.localRotation; pose.BodyPosition = a.Body.localPosition;
            pose.HipY = a.Limbs[2].localPosition.y; pose.Yaw = a.Root.eulerAngles.y;
        }
        readonly Transform[] jointBuffer = new Transform[10];
        Transform[] JointsOf(Actor a)
        {
            jointBuffer[0] = a.Body; jointBuffer[1] = a.Head;
            for (int i = 0; i < 4; i++) jointBuffer[2 + i] = a.Limbs[i];
            jointBuffer[6] = a.Forearms[0]; jointBuffer[7] = a.Forearms[1]; jointBuffer[8] = a.Knees[0]; jointBuffer[9] = a.Knees[1];
            return jointBuffer;
        }
        // Head only; the upper body is torso, head, shoulders and elbows; the whole body adds the legs and the model frame.
        static bool Owns(Hold parts, int joint)
        {
            if (parts == Hold.Body) return true;
            if (parts == Hold.Head) return joint == 1;
            return joint == 0 || joint == 1 || joint == 2 || joint == 3 || joint == 6 || joint == 7;
        }

        // ---- the struggle --------------------------------------------------------------------------------------------
        void StruggleScene(Actor e, Resident escapee, Actor o, Actor helper, EscapeStruggle s, World world)
        {
            float t = Beat(s.MoveMinute, s.MoveEnd), dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f), blend = 1 - Mathf.Exp(-dt * 16);
            incidentPoseCount += 2; e.PoseArms = o.PoseArms = true;
            Vector3 ep = e.Root.position, op = o.Root.position, axis = op - ep; axis.y = 0;
            if (axis.sqrMagnitude < 0.0001f) axis = e.Root.forward;
            Vector3 along = axis.normalized;
            float axisYaw = Yaw(axis);
#if UNITY_EDITOR
            cast.Beat = s.Phase + "/" + s.Move;
#endif
            bool prone = s.Prone && (s.Phase == "Down" || s.Phase == "Cuffing" || s.Phase == "Rising");
            if (s.Phase == "Grappling" && s.Move == "Tackle")
            {
                TackleBeat(e, o, t, blend, s);
                ShowIncidentName(e, escapee.Name + "\n" + (t < 0.6f ? "RUNNING" : "TACKLED"), Fugitive);
                ShowIncidentName(o, "OFFICER\nTACKLE", Lawman);
            }
            else if (s.Phase == "Grappling")
            {
                // Wrestling: a quick push-pull along the line between them (about 1.5 Hz), a slower sway of the pair around
                // their middle, feet scuffling, and strain shaking through both torsos.
                float tempo = Time.unscaledTime * Mathf.Sqrt(Mathf.Max(1, world.Data.Speed)), seed = (s.MoveMinute % 7) * 0.9f;
                float sway = Mathf.Sin(tempo * 2.5f + seed) * (s.Move == "Hold" ? 14 : 9), rock = Mathf.Sin(tempo * 9.4f + seed) * 0.07f;
                float start = s.Move == "Strike" ? 0.5f : s.Move == "Shove" || s.Move == "Takedown" ? 0.55f : 2;
                float clinch = 1 - Window(t, start, start + 0.12f);
                Vector3 mid = (ep + op) * 0.5f;
                Vector3 swayE = ((Quaternion.Euler(0, sway, 0) * (ep - mid)) + mid - ep + along * rock) * clinch;
                Vector3 swayO = ((Quaternion.Euler(0, sway, 0) * (op - mid)) + mid - op + along * rock) * clinch;
                float yawE = axisYaw + sway * clinch, yawO = axisYaw + 180 + sway * clinch;
                Vector3 shiftE = Vector3.zero, shiftO = Vector3.zero; float pitchE = 0;
                if (s.Move == "Takedown" && t > 0.72f) yawE = Mathf.LerpAngle(yawE, ProneYaw(s, e, axisYaw), Window(t, 0.72f, 0.92f));
                Turn(e, yawE, 900); Turn(o, yawO, 900);
                if (t < start)
                {
                    float step = Mathf.Sin(tempo * 4.4f) * (s.Move == "Hold" ? 0.07f : 0.04f);
                    // Some holds are not quiet: the resident rips an arm free and swings, the officer ducks and grabs again.
                    bool swing = s.Move == "Hold" && (s.MoveMinute + s.Exchanges) % 2 == 1;
                    float duck = swing ? Window(t, 0.5f, 0.58f) * (1 - Window(t, 0.66f, 0.8f)) : 0;
                    Vector2 shakeE = Tremor(tempo, 4), shakeO = Tremor(tempo + 1.7f, 3);
                    Clinch(o, e, 18 + 14 * duck + shakeO.x, true, step, blend, -1, shakeO.y);
                    Clinch(e, o, 13 + shakeE.x, false, -step, blend, swing ? 1 : -1, (swing ? SwingTwist(t) : 0) + shakeE.y);
                    if (swing) WildSwing(e, o, t, blend, s);
                    if (o.Head != null && duck > 0) Joint(o.Head, new Vector3(24 * duck, -10 * duck, 0), blend);
                }
                else if (s.Move == "Shove") ShoveBeat(e, o, t, blend, s);
                else if (s.Move == "Strike") shiftE = StrikeBeat(e, o, t, blend, s);
                else if (s.Move == "Takedown") TakedownBeat(e, o, t, blend, s, out shiftO, out pitchE);
                Lay(e, pitchE, 0.95f - 0.35f * Mathf.Clamp01(pitchE / 45), swayE + shiftE, blend);
                Offset(o, swayO + shiftO, Vector3.zero, Quaternion.identity, blend);
                bool going = s.Move == "Takedown" && t > 0.6f;
                ShowIncidentName(e, escapee.Name + "\n" + (going ? "TAKEN DOWN" : "FIGHTING TO BREAK FREE"), Fugitive);
                ShowIncidentName(o, "OFFICER\n" + (going ? "TAKEDOWN" : "HOLDING ON"), Lawman);
                if (helper != null) SecondHand(helper, e, blend);
            }
            else if (prone)
            {
                float proneYaw = ProneYaw(s, e, axisYaw);
                bool tackled = s.LastMove == "Tackle";
                float landing = s.Phase == "Down" ? Window(t, 0, tackled ? 0.06f : 0.12f) : 1;
                bool rolling = s.Phase == "Down" && s.Move == "Roll free";
                float rise = rolling ? Window(t, 0.72f, 1) : 0, lift = s.Phase == "Rising" ? Window(t, 0, 0.4f) : 0;
                float pitch = Mathf.Lerp(tackled ? 80 : 45, 90, landing) * (1 - rise) * (1 - lift);
                float bounce = s.Phase == "Down" ? Mathf.Sin(Mathf.Clamp01(t / 0.12f) * Mathf.PI) * 0.04f : 0;
                Turn(e, proneYaw, 900);
                // Tackled runners lie where they fell, ahead of their feet; a spun takedown lies across the officer's front.
                Vector3 shift = tackled ? Quaternion.Euler(0, cast.ProneYaw, 0) * Vector3.forward * 0.35f : -e.Root.forward * 0.12f;
                if (s.Phase != "Rising") Lay(e, pitch, Mathf.Lerp(0.95f, 0.16f + bounce, Mathf.Clamp01(pitch / 90)), shift, blend);
                if (s.Phase == "Down" && landing > 0.6f && cast.LandingKey != s.MoveMinute)
                { cast.LandingKey = s.MoveMinute; Impact(e.Body.position, 0.55f, false, 0.55f); }
                if (s.Phase == "Down") Thrash(e, rise, blend);
                else if (s.Phase == "Cuffing") CuffedProne(e, blend);
                else Rise(e, o, t, blend, true);
                if (s.Phase != "Rising") Pin(o, e, s.Phase == "Cuffing", rise, blend); else Lift(o, e, t, blend);
                if (helper != null) HoldLegs(helper, e, blend);
                ShowIncidentName(e, escapee.Name + "\n" + (s.Phase == "Rising" ? "IN CUSTODY" : s.Phase == "Cuffing" ? "CUFFED" : rise > 0 ? "BREAKING FREE" : "PINNED"), Fugitive);
                ShowIncidentName(o, "OFFICER\n" + (s.Phase == "Rising" ? "LIFTING" : s.Phase == "Cuffing" ? "CUFFING" : "PINNING"), Lawman);
            }
            else
            {
                // Surrender: hands up, turn away, down onto both knees, hands on the head; then cuffs from behind, then up.
                float turn = s.Phase == "Surrendered" ? Window(t, 0.3f, 0.65f) : 1;
                Turn(e, axisYaw + 180 * turn, 360); Turn(o, axisYaw + 180, 600);
                Offset(e, Vector3.zero, Vector3.zero, Quaternion.identity, blend);
                if (s.Phase == "Rising") { Rise(e, o, t, blend, false); Lift(o, e, t, blend); }
                else
                {
                    KneelBoth(e, s.Phase == "Surrendered" ? Window(t, 0.35f, 0.75f) : 1, blend);
                    Torso(e, 4, 0, 0, blend);
                }
                if (s.Phase == "Surrendered")
                {
                    float onHead = Window(t, 0.72f, 0.95f);
                    for (int arm = 0; arm < 2; arm++)
                    {
                        float side = arm == 0 ? -1 : 1;
                        Joint(e.Limbs[arm], new Vector3(Mathf.Lerp(-168, -150, onHead), 0, side * Mathf.Lerp(-18, -48, onHead)), blend);
                        Joint(e.Forearms[arm], new Vector3(Mathf.Lerp(-15, -118, onHead), 0, 0), blend);
                    }
                    Offset(o, Vector3.zero, Vector3.zero, Quaternion.identity, blend);
                    Command(o, blend);
                }
                else if (s.Phase == "Cuffing")
                {
                    BehindBack(e, blend);
                    Offset(o, o.Root.forward * Mathf.Clamp(Vector3.Distance(op, ep) - 0.42f, 0, 0.35f), Vector3.zero, Quaternion.identity, blend);
                    Torso(o, 24, 0, 0, blend); Stance(o, 0.84f, 0.16f, -0.12f, blend);
                    for (int arm = 0; arm < 2; arm++) ArmPose(o, arm, o.Model.InverseTransformPoint(e.Hands[arm].position), blend);
                }
                if (helper != null) SecondHand(helper, e, blend);
                ShowIncidentName(e, escapee.Name + "\n" + (s.Phase == "Rising" ? "IN CUSTODY" : s.Phase == "Cuffing" ? "CUFFED" : "SURRENDERING"), Fugitive);
                ShowIncidentName(o, "OFFICER\n" + (s.Phase == "Rising" ? "LIFTING" : s.Phase == "Cuffing" ? "CUFFING" : "COVERING"), Lawman);
            }
        }
        static readonly Color Fugitive = new Color(.98f, .62f, .40f), Lawman = new Color(.58f, .80f, .92f), Hurt = new Color(.95f, .55f, .45f);

        // Grip the partner: an officer takes the upper arms, a resident braces on the officer's chest and shoulders.
        void Clinch(Actor a, Actor partner, float lean, bool officer, float step, float blend, int freeArm = -1, float twist = 0)
        {
            Stance(a, 0.82f + Mathf.Abs(step) * 0.2f, (officer ? 0.18f : -0.18f) + step, (officer ? -0.16f : 0.16f) - step, blend);
            Torso(a, lean, twist, 0, blend);
            for (int arm = 0; arm < 2; arm++)
            {
                if (arm == freeArm) continue;
                // Facing each other, my left meets their right.
                Vector3 grip = officer ? partner.Forearms[1 - arm].position + Vector3.up * 0.12f
                    : partner.Body.position + Vector3.up * 0.42f + partner.Root.right * (arm == 0 ? 0.13f : -0.13f);
                ArmPose(a, arm, a.Model.InverseTransformPoint(grip), blend);
            }
        }
        // Inside a hold: rip the right arm free, swing a wide hook at the officer's head, miss as the officer ducks, grab back on.
        void WildSwing(Actor e, Actor o, float t, float blend, EscapeStruggle s)
        {
            float rip = Window(t, 0.3f, 0.42f), cock = Window(t, 0.42f, 0.5f), hook = Window(t, 0.5f, 0.6f), back = Window(t, 0.68f, 0.85f);
            float fast = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 26);
            Vector3 grip = e.Model.InverseTransformPoint(o.Body.position + Vector3.up * 0.42f + o.Root.right * -0.13f);
            Vector3 wide = new Vector3(0.42f, 1.45f, 0.02f);
            Vector3 head = e.Model.InverseTransformPoint((o.Head != null ? o.Head.position : o.Body.position + Vector3.up * 0.7f) + Vector3.up * 0.12f);
            Vector3 hand = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(grip, wide, Mathf.Max(rip, cock)), head + new Vector3(-0.2f, 0, 0), hook), grip, back);
            ArmPose(e, 1, hand, hook > 0 && back < 1 ? fast : blend);
        }
        static float SwingTwist(float t)
        {
            float cock = Window(t, 0.42f, 0.5f), hook = Window(t, 0.5f, 0.6f), back = Window(t, 0.68f, 0.85f);
            return -22 * cock * (1 - hook) + 30 * hook * (1 - back);
        }
        void ShoveBeat(Actor e, Actor o, float t, float blend, EscapeStruggle s)
        {
            float load = Window(t, 0.55f, 0.8f), burst = Window(t, 0.8f, 0.9f);
            // Gather: drop the hips and pull both hands in to the chest; then drive both arms straight through the officer.
            Stance(e, 0.82f - 0.06f * load + 0.04f * burst, -0.2f, 0.2f + 0.1f * burst, blend);
            Torso(e, 13 + 10 * load - 20 * burst, 0, 0, blend);
            for (int arm = 0; arm < 2; arm++)
            {
                Vector3 chest = new Vector3(arm == 0 ? -0.12f : 0.12f, 1.38f, 0.22f);
                Vector3 through = e.Model.InverseTransformPoint(o.Body.position + Vector3.up * 0.42f + o.Root.right * (arm == 0 ? 0.13f : -0.13f) - o.Root.forward * 0.25f);
                ArmPose(e, arm, Vector3.Lerp(chest, through, burst), burst > 0 ? 1 - Mathf.Exp(-Time.unscaledDeltaTime * 30) : blend);
            }
            // The officer is driven back with both arms thrown up.
            Stance(o, 0.84f, 0.14f - 0.1f * burst, -0.16f, blend);
            Torso(o, 18 - 42 * burst, 0, 0, blend);
            for (int arm = 0; arm < 2; arm++) { Joint(o.Limbs[arm], new Vector3(-60 - 50 * burst, 0, (arm == 0 ? -1 : 1) * 35 * burst), blend); Joint(o.Forearms[arm], new Vector3(-30, 0, 0), blend); }
            if (o.Head != null) Joint(o.Head, new Vector3(-22 * burst, 0, 0), blend);
            if (burst > 0.6f && cast.ImpactKey != s.MoveMinute) { cast.ImpactKey = s.MoveMinute; Impact(o.Body.position + Vector3.up * 0.4f, 0.3f, false, 0.25f); }
        }
        // Returns the resident's step (world) for this frame.
        Vector3 StrikeBeat(Actor e, Actor o, float t, float blend, EscapeStruggle s)
        {
            float rip = Window(t, 0.5f, 0.62f), wind = Window(t, 0.62f, 0.86f), punch = Window(t, 0.86f, 0.92f), recover = Window(t, 0.93f, 1);
            float fast = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 30);
            float twist = Mathf.Lerp(0, -38, wind) + 70 * punch - 25 * recover;
            Torso(e, Mathf.Lerp(6, 3, wind) + 15 * punch, twist, 0, punch > 0 ? fast : blend);
            Stance(e, 0.85f, punch > 0 ? 0.22f : 0.12f, -0.22f, blend);
            // The rear hand cocks back and then drives through the officer's jaw; the lead hand guards.
            Vector3 chamber = new Vector3(0.16f, 1.25f, 0.18f), cocked = new Vector3(0.26f, 1.45f, -0.16f);
            Vector3 jaw = e.Model.InverseTransformPoint(o.Head != null ? o.Head.position + Vector3.down * 0.05f : o.Body.position + Vector3.up * 0.7f);
            ArmPose(e, 1, Vector3.Lerp(Vector3.Lerp(chamber, cocked, wind), jaw, punch * (1 - 0.4f * recover)), punch > 0 ? fast : blend);
            ArmPose(e, 0, new Vector3(-0.08f, 1.55f, 0.24f), blend);
            float snap = Window(t, 0.91f, 0.96f);
            // The officer's grip opens as the resident tears away; then the head snaps and the body goes.
            Stance(o, 0.84f, 0.16f, -0.16f, blend);
            Torso(o, Mathf.Lerp(16, 6, rip) - 26 * snap, 12 * snap, -6 * snap, blend);
            for (int arm = 0; arm < 2; arm++) { Joint(o.Limbs[arm], new Vector3(Mathf.Lerp(-62, -20, snap), 0, (arm == 0 ? -1 : 1) * (8 + 30 * snap)), blend); Joint(o.Forearms[arm], new Vector3(-35, 0, 0), blend); }
            if (o.Head != null) Joint(o.Head, new Vector3(-38 * snap, 28 * snap, 8 * snap), fast);
            if (t >= 0.91f && cast.ImpactKey != s.MoveMinute)
            {
                cast.ImpactKey = s.MoveMinute;
                Impact(o.Head != null ? o.Head.position : o.Body.position + Vector3.up * 0.7f, 0.5f, true, 0.3f);
                if (incidentWorld == null || incidentWorld.Data.Speed <= 1) hitStopUntil = Time.unscaledTime + 0.08f;
            }
            // Rip free and step back to make room, then step into the punch.
            return -e.Root.forward * 0.14f * rip * (1 - punch) + e.Root.forward * 0.18f * punch;
        }
        void TakedownBeat(Actor e, Actor o, float t, float blend, EscapeStruggle s, out Vector3 lunge, out float pitch)
        {
            float level = Window(t, 0.55f, 0.72f), drive = Window(t, 0.72f, 0.92f), tip = Window(t, 0.8f, 1);
            // Change levels, wrap the waist, drive through: the resident is spun and pitched forward toward the ground.
            Stance(o, 0.82f - 0.2f * level, 0.28f, -0.25f, blend);
            Torso(o, 18 + 30 * level + 20 * drive, 0, 0, blend);
            for (int arm = 0; arm < 2; arm++) ArmPose(o, arm, o.Model.InverseTransformPoint(e.Body.position + Vector3.up * 0.1f + e.Root.right * (arm == 0 ? 0.16f : -0.16f)), blend);
            lunge = o.Root.forward * 0.28f * drive + Vector3.down * 0.18f * tip;
            pitch = 45 * tip * tip;
            Stance(e, 0.84f - 0.08f * drive, 0.1f, -0.1f, blend);
            Torso(e, 10 + 20 * drive, 0, 0, blend);
            for (int arm = 0; arm < 2; arm++) { Joint(e.Limbs[arm], new Vector3(-60 - 60 * tip, 0, (arm == 0 ? -1 : 1) * 30 * drive), blend); Joint(e.Forearms[arm], new Vector3(-25, 0, 0), blend); }
            for (int leg = 0; leg < 2; leg++) Joint(e.Knees[leg], new Vector3(35 * drive, 0, 0), blend);
            if (drive > 0.3f && cast.ImpactKey != s.MoveMinute) { cast.ImpactKey = s.MoveMinute; Impact(o.Body.position, 0.4f, false, 0.25f); }
        }
        // Caught from behind at a run: the officer gathers, dives, wraps the waist, and both go down the way the resident was running.
        void TackleBeat(Actor e, Actor o, float t, float blend, EscapeStruggle s)
        {
            float gather = Window(t, 0, 0.25f), dive = Window(t, 0.25f, 0.6f), fall = Window(t, 0.6f, 1);
            float fast = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 28);
            if (cast.ProneKey != s.MoveMinute * 31 + 7) { cast.ProneKey = s.MoveMinute * 31 + 7; cast.ProneYaw = e.Root.eulerAngles.y; }
            float runYaw = cast.ProneYaw;
            Turn(e, runYaw, 900);
            Vector3 runDir = Quaternion.Euler(0, runYaw, 0) * Vector3.forward;
            // The runner keeps driving forward until the arms close round the waist, then pitches over.
            float carry = 0.35f * Mathf.Min(1, t / 0.6f);
            Lay(e, 82 * fall * fall, Mathf.Lerp(0.95f, 0.17f, fall), runDir * carry, fast);
            Stance(e, 0.84f, 0.25f * (1 - fall), -0.25f * (1 - fall), blend);
            Torso(e, 16 - 26 * fall, 0, 0, blend);
            for (int arm = 0; arm < 2; arm++)
            {
                float pump = Mathf.Sin(Time.unscaledTime * 18 + arm * Mathf.PI) * (1 - fall);
                Joint(e.Limbs[arm], new Vector3(Mathf.Lerp(-40 * pump, -140, fall), 0, (arm == 0 ? -1 : 1) * Mathf.Lerp(10, 32, fall)), blend);
                Joint(e.Forearms[arm], new Vector3(Mathf.Lerp(-90, -30, fall), 0, 0), blend);
            }
            if (e.Head != null) Joint(e.Head, new Vector3(-20 - 20 * fall, 0, 0), blend);
            // The officer: crouch with the arms back, launch nearly horizontal, wrap, land on the resident's legs.
            Vector3 toward = e.Root.position + runDir * carry - o.Root.position; toward.y = 0;
            float reach = Mathf.Max(0, toward.magnitude - 0.35f);
            Turn(o, Yaw(toward), 900);
            float divePitch = Mathf.Lerp(0, 68, dive) + 17 * fall, height = Mathf.Lerp(0.95f - 0.12f * gather, 0.85f, dive) + 0.1f * Mathf.Sin(dive * Mathf.PI) - 0.62f * fall;
            Lay(o, divePitch, height, toward.normalized * reach * Mathf.Max(dive, fall), fast);
            Stance(o, 0.9f - 0.15f * gather * (1 - dive), 0.25f, -0.2f, blend);
            Torso(o, 34 * gather * (1 - dive) + 4 * dive, 0, 0, blend);
            for (int arm = 0; arm < 2; arm++)
            {
                Joint(o.Limbs[arm], new Vector3(Mathf.Lerp(42 * gather, -115, dive), 0, (arm == 0 ? -1 : 1) * Mathf.Lerp(10, 50, fall)), dive > 0 ? fast : blend);
                Joint(o.Forearms[arm], new Vector3(Mathf.Lerp(-60, -18, dive) - 80 * fall, 0, 0), dive > 0 ? fast : blend);
            }
            for (int leg = 0; leg < 2; leg++) { Joint(o.Limbs[2 + leg], new Vector3(Mathf.Lerp(-40 * gather, 16, dive), 0, (leg == 0 ? -1 : 1) * 4), blend); Joint(o.Knees[leg], new Vector3(Mathf.Lerp(70 * gather, 30 + 30 * leg, dive), 0, 0), blend); }
            if (t >= 0.6f && cast.ImpactKey != s.MoveMinute)
            {
                cast.ImpactKey = s.MoveMinute;
                Impact(e.Body.position, 0.65f, false, 0);
                if (incidentWorld == null || incidentWorld.Data.Speed <= 1) hitStopUntil = Time.unscaledTime + 0.1f;
            }
            if (t >= 0.97f && cast.LandingKey != s.MoveMinute + 100000) { cast.LandingKey = s.MoveMinute + 100000; Impact(e.Body.position, 0.5f, false, 0.6f); }
        }
        float ProneYaw(EscapeStruggle s, Actor e, float axisYaw)
        {
            // A tackled runner lies the way they were running. Otherwise the body lands across the line to the officer, so
            // it lies beside the officer and not through them. Chosen when the takedown starts and kept for the pin and cuffs.
            if (s.LastMove == "Tackle" || s.Move == "Tackle") return cast.ProneYaw;
            if (s.Phase == "Grappling")
            {
                int key = s.MoveMinute * 31 + s.Exchanges;
                if (cast.ProneKey != key) { cast.ProneKey = key; cast.ProneYaw = axisYaw + 90; }
            }
            else if (cast.ProneKey == int.MinValue) { cast.ProneKey = s.PhaseMinute; cast.ProneYaw = axisYaw + 90; }
            return cast.ProneYaw;
        }
        void Thrash(Actor e, float rise, float blend)
        {
            float time = Time.unscaledTime;
            for (int leg = 0; leg < 2; leg++)
            {
                float kick = Mathf.Max(0, Mathf.Sin(time * 9 + leg * Mathf.PI));
                Joint(e.Limbs[2 + leg], new Vector3(8 - 70 * rise, 0, (leg == 0 ? -1 : 1) * 6), blend);
                Joint(e.Knees[leg], new Vector3(Mathf.Lerp(12 + 65 * kick, 95, rise), 0, 0), blend);
            }
            float push = 0.5f + 0.5f * Mathf.Sin(time * 4.2f);
            Torso(e, -10 - 10 * push + 30 * rise, Mathf.Sin(time * 3) * 8, 0, blend);
            for (int arm = 0; arm < 2; arm++)
            {
                Joint(e.Limbs[arm], new Vector3(-55 - 25 * push + 40 * rise, 0, (arm == 0 ? -1 : 1) * 35), blend);
                Joint(e.Forearms[arm], new Vector3(-95 + 45 * push, 0, 0), blend);
            }
            if (e.Head != null) Joint(e.Head, new Vector3(-30 - 12 * push, Mathf.Sin(time * 2.4f) * 35, 0), blend);
        }
        void CuffedProne(Actor e, float blend)
        {
            for (int leg = 0; leg < 2; leg++) { Joint(e.Limbs[2 + leg], new Vector3(4, 0, (leg == 0 ? -1 : 1) * 5), blend); Joint(e.Knees[leg], new Vector3(8 + 10 * Mathf.Max(0, Mathf.Sin(Time.unscaledTime * 2 + leg)), 0, 0), blend); }
            Torso(e, -4, 0, 0, blend);
            BehindBack(e, blend);
            if (e.Head != null) Joint(e.Head, new Vector3(-20, 70, 0), blend);
        }
        // Cuffed and lifted: from the ground (or from the knees) up to standing, bent at the waist with the head down.
        void Rise(Actor e, Actor o, float t, float blend, bool fromProne)
        {
            float kneel = fromProne ? Window(t, 0.1f, 0.45f) : 1, stand = Window(t, fromProne ? 0.62f : 0.45f, 0.95f);
            if (fromProne) Lay(e, 90 * (1 - Window(t, 0, 0.4f)), Mathf.Lerp(0.16f, 0.95f, Window(t, 0, 0.4f)), Vector3.zero, blend);
            KneelBoth(e, kneel * (1 - stand), blend);
            Torso(e, Mathf.Lerp(8, 28, stand), 0, 0, blend);
            BehindBack(e, blend);
            if (e.Head != null) Joint(e.Head, new Vector3(14, 0, 0), blend);
        }
        // The officer stands over the prisoner with a hand under the near arm, lifting as they come up.
        void Lift(Actor o, Actor e, float t, float blend)
        {
            Vector3 arm = e.Forearms[0].position + Vector3.up * 0.12f;
            Turn(o, Yaw(e.Body.position - o.Root.position), 600);
            float gap = new Vector2(arm.x - o.Root.position.x, arm.z - o.Root.position.z).magnitude;
            Offset(o, o.Root.forward * Mathf.Clamp(gap - 0.45f, 0, 0.6f), Vector3.zero, Quaternion.identity, blend);
            float up = Window(t, 0.55f, 0.95f);
            Stance(o, Mathf.Lerp(0.74f, 0.88f, up), 0.18f, -0.18f, blend);
            Torso(o, Mathf.Lerp(38, 12, up), 0, 0, blend);
            ArmPose(o, 1, o.Model.InverseTransformPoint(arm), blend);
            ArmPose(o, 0, o.Model.InverseTransformPoint(e.Hands[0].position), blend);
        }
        // Knee beside the body, a hand on the shoulder blade and one on the arm: never on the neck.
        void Pin(Actor o, Actor e, bool cuffing, float knockedOff, float blend)
        {
            Vector3 back = e.Body.position + e.Model.rotation * new Vector3(0, 0.25f, -0.05f);
            Turn(o, Yaw(back - o.Root.position), 720);
            float gap = new Vector2(back.x - o.Root.position.x, back.z - o.Root.position.z).magnitude;
            Offset(o, o.Root.forward * (Mathf.Clamp(gap - 0.5f, 0, 0.8f) * (1 - knockedOff) - 0.3f * knockedOff), Vector3.zero, Quaternion.Euler(-35 * knockedOff, 0, 0), blend);
            Kneel(o, 0, 1, blend);
            Torso(o, 52 - 44 * knockedOff, 0, 0, blend);
            for (int arm = 0; arm < 2; arm++)
            {
                Vector3 target = cuffing ? e.Hands[arm].position + Vector3.up * (0.03f * Mathf.Sin(Time.unscaledTime * 7 + arm)) :
                    back + e.Model.rotation * new Vector3(arm == 0 ? -0.12f : 0.12f, 0.15f, 0);
                ArmPose(o, arm, o.Model.InverseTransformPoint(target), blend * (1 - knockedOff));
            }
        }
        void HoldLegs(Actor h, Actor e, float blend)
        {
            h.PoseArms = true; incidentPoseCount++;
            Vector3 legs = (e.Knees[0].position + e.Knees[1].position) * 0.5f;
            Turn(h, Yaw(legs - h.Root.position), 720);
            float gap = new Vector2(legs.x - h.Root.position.x, legs.z - h.Root.position.z).magnitude;
            Offset(h, h.Root.forward * Mathf.Clamp(gap - 0.5f, 0, 0.6f), Vector3.zero, Quaternion.identity, blend);
            Kneel(h, 1, 1, blend); Torso(h, 48, 0, 0, blend);
            for (int arm = 0; arm < 2; arm++) ArmPose(h, arm, h.Model.InverseTransformPoint(e.Knees[arm].position), blend);
        }
        // A second officer standing: takes the far arm from the side.
        void SecondHand(Actor h, Actor e, float blend)
        {
            h.PoseArms = true; incidentPoseCount++;
            Turn(h, Yaw(e.Root.position - h.Root.position), 720);
            Offset(h, Vector3.zero, Vector3.zero, Quaternion.identity, blend);
            Torso(h, 16, 0, 0, blend); Stance(h, 0.84f, 0.15f, -0.15f, blend);
            for (int arm = 0; arm < 2; arm++) ArmPose(h, arm, h.Model.InverseTransformPoint(e.Forearms[arm].position), blend);
            ShowIncidentName(h, "OFFICER\nASSISTING", Lawman);
        }
        void Command(Actor o, float blend)
        {
            Torso(o, 6, 0, 0, blend); Stance(o, 0.86f, 0.16f, -0.14f, blend);
            Joint(o.Limbs[1], new Vector3(-82, 0, 6), blend); Joint(o.Forearms[1], new Vector3(-6, 0, 0), blend);
            Joint(o.Limbs[0], new Vector3(-20, 0, -12), blend); Joint(o.Forearms[0], new Vector3(-70, 0, 0), blend);
        }

        // ---- before and after the struggle ----------------------------------------------------------------------------------
        void RunScene(Actor e, Resident escapee, World world)
        {
            incidentPoseCount++;
            EscapeAttempt attempt = world.Escape; EscapeStruggle s = world.Struggle;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f), blend = 1 - Mathf.Exp(-dt * 12);
            Actor chaser = NearestPursuer(e, world);
            bool cornered = attempt.Phase == "Held at gate";
            if (cornered || chaser != null) Take(e, cornered ? Hold.Body : Hold.Head);
            if (cornered)
            {
                e.PoseArms = true;
                Vector3 gate = V(new Point(attempt.Exit.X, attempt.Exit.Y - 1.4f), 0.14f);
                Turn(e, Yaw(gate - e.Root.position), 360);
                Offset(e, Vector3.zero, Vector3.zero, Quaternion.identity, blend);
                if (s.Stamina < 20)
                {
                    // Spent: bent over with the hands on the knees, chest heaving.
                    float breath = Mathf.Sin(Time.unscaledTime * 5) * 3;
                    Torso(e, 50 + breath, 0, 0, blend); Stance(e, 0.8f, 0.14f, -0.12f, blend);
                    for (int arm = 0; arm < 2; arm++) ArmPose(e, arm, e.Model.InverseTransformPoint(e.Knees[arm].position + Vector3.up * 0.06f + e.Root.forward * 0.05f), blend);
                    if (e.Head != null) Joint(e.Head, new Vector3(-28, 0, 0), blend);
                    ShowIncidentName(e, escapee.Name + "\nEXHAUSTED", Fugitive);
                }
                else
                {
                    // Hands on the closed gate, shaking it, looking back over a shoulder at whoever is coming.
                    float shake = Mathf.Sin(Time.unscaledTime * 17) * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.3f));
                    Torso(e, 8 + 4 * shake, 6 * shake, 0, blend); Stance(e, 0.86f, 0.12f, -0.18f, blend);
                    for (int arm = 0; arm < 2; arm++) ArmPose(e, arm, new Vector3(arm == 0 ? -0.22f : 0.22f, 1.62f + 0.04f * shake * (arm == 0 ? 1 : -1), 0.42f), blend);
                    ShowIncidentName(e, escapee.Name + "\nGATE CLOSED", Fugitive);
                }
            }
            else if (attempt.Detected)
                ShowIncidentName(e, escapee.Name + "\n" + (s.Stamina < 30 ? "WINDED" : s.FleeUntil > world.Data.Minute ? "BROKE FREE" : "RUNNING"), Fugitive);
            if (chaser != null) Glance(e, chaser, dt, cornered);
        }
        Actor NearestPursuer(Actor e, World world)
        {
            Actor best = null; float bestDistance = 14;
            foreach (StaffMember staff in world.Data.Staff)
            {
                if (staff.Duty != "Responding to escape" && staff.Duty != "Responding to alarm") continue;
                Actor actor = Live(staff.Id); if (actor == null) continue;
                float distance = Vector3.Distance(actor.Root.position, e.Root.position);
                if (distance < bestDistance) { best = actor; bestDistance = distance; }
            }
            return best;
        }
        // Every couple of seconds a look back over the shoulder toward the nearest pursuer.
        void Glance(Actor e, Actor chaser, float dt, bool cornered)
        {
            float now = Time.unscaledTime;
            if (now >= cast.NextGlance) { cast.GlanceUntil = now + (cornered ? 0.8f : 0.45f); cast.NextGlance = now + (cornered ? 1.4f : 1.6f + Mathf.PerlinNoise(now, 0.3f) * 1.4f); }
            if (e.Head == null) return;
            float yaw = 0;
            if (now < cast.GlanceUntil)
            {
                Vector3 local = e.Root.InverseTransformDirection(chaser.Root.position - e.Root.position);
                yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -110, 110) * 0.75f;
            }
            e.Head.localRotation = Quaternion.Slerp(e.Head.localRotation, Quaternion.Euler(cornered ? -4 : -10, yaw, 0), 1 - Mathf.Exp(-dt * 16));
        }
        void EscortScene(Actor o, Actor prisoner)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f), blend = 1 - Mathf.Exp(-dt * 12);
            incidentPoseCount += 2; prisoner.PoseArms = true; o.PoseArms = true;
            BehindBack(prisoner, blend);
            // Bent at the waist with the head down: an instant read of "caught".
            if (prisoner.Head != null) Joint(prisoner.Head, new Vector3(12, 0, 0), blend);
            Torso(prisoner, 26, 0, 0, blend);
            // The officer walks at the prisoner's side with a hand on the near elbow.
            Vector3 local = o.Root.InverseTransformPoint(prisoner.Root.position);
            int near = local.x >= 0 ? 1 : 0;
            ArmPose(o, near, o.Model.InverseTransformPoint(prisoner.Forearms[1 - near].position), blend);
            Joint(o.Limbs[1 - near], new Vector3(-6, 0, near == 1 ? -6 : 6), blend); Joint(o.Forearms[1 - near], new Vector3(-20, 0, 0), blend);
            Torso(o, 4, near == 1 ? 8 : -8, 0, blend);
            ShowIncidentName(prisoner, "IN CUSTODY", new Color(.86f, .78f, .58f));
        }
        void BehindBack(Actor a, float blend)
        {
            ArmPose(a, 0, new Vector3(-0.07f, 1.0f, -0.21f), blend); ArmPose(a, 1, new Vector3(0.07f, 1.0f, -0.21f), blend);
        }

        // An officer knocked off their feet: a punch drops them onto their back, a shove sends them stumbling to a knee,
        // a resident rolling out from under them tips them back onto the ground. Then they get up.
        void StaggerScene(Actor o, Actor resident, EscapeStruggle s)
        {
            float span = s.StaggerMove == "Strike" ? World.StruggleStrikeStagger : World.StruggleShoveStagger;
            float p = Mathf.Clamp01((sceneMinute - s.StaggeredMinute + sceneFraction) / span);
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f), blend = 1 - Mathf.Exp(-dt * 14);
            incidentPoseCount++; o.PoseArms = true;
            // Keep facing where the blow came from while being driven backward.
            if (cast.StaggerKey != s.StaggeredMinute)
            {
                cast.StaggerKey = s.StaggeredMinute;
                Vector3 from = resident != null ? resident.Root.position - o.Root.position : o.Root.forward;
                o.Root.rotation = Quaternion.Euler(0, Yaw(from), 0);
            }
            if (s.StaggerMove == "Strike")
            {
                float fall = Window(p, 0, 0.12f), roll = Window(p, 0.62f, 0.8f), up = Window(p, 0.8f, 1);
                if (fall > 0.85f && cast.StaggerLanding != s.StaggeredMinute) { cast.StaggerLanding = s.StaggeredMinute; Impact(o.Root.position - o.Root.forward * 0.8f, 0.6f, false, 0.45f); }
                float pitch = -84 * fall * (1 - roll) - 30 * roll * (1 - up);
                Offset(o, Vector3.zero, new Vector3(0, 0.12f * fall * (1 - roll) - 0.38f * roll * (1 - up), 0), Quaternion.Euler(pitch, 0, 18 * roll * (1 - up)), blend);
                for (int arm = 0; arm < 2; arm++)
                {
                    Joint(o.Limbs[arm], new Vector3(Mathf.Lerp(-150 * fall, -40, roll), 0, (arm == 0 ? -1 : 1) * Mathf.Lerp(55, 20, roll)), blend);
                    Joint(o.Forearms[arm], new Vector3(-20 - 60 * roll, 0, 0), blend);
                }
                for (int leg = 0; leg < 2; leg++)
                {
                    Joint(o.Limbs[2 + leg], new Vector3(Mathf.Lerp(-25 * fall, -70, roll * (1 - up)), 0, (leg == 0 ? -1 : 1) * 8), blend);
                    Joint(o.Knees[leg], new Vector3(Mathf.Lerp(30 * fall, 100, roll * (1 - up)), 0, 0), blend);
                }
                Torso(o, -10 * fall * (1 - roll) + 25 * roll * (1 - up), 0, 0, blend);
                if (o.Head != null) Joint(o.Head, new Vector3(-20 * fall * (1 - roll) + 20 * roll, 0, 0), blend);
                ShowIncidentName(o, "OFFICER\n" + (up > 0 ? "GETTING UP" : "KNOCKED DOWN"), Hurt);
            }
            else if (s.StaggerMove == "Shove")
            {
                float stumble = Window(p, 0, 0.3f), knee = Window(p, 0.3f, 0.5f), up = Window(p, 0.7f, 1);
                float wheel = Mathf.Sin(Time.unscaledTime * 14) * (1 - knee);
                Offset(o, Vector3.zero, Vector3.zero, Quaternion.identity, blend);
                Torso(o, -24 * stumble * (1 - knee) + 22 * knee * (1 - up), 0, 0, blend);
                for (int arm = 0; arm < 2; arm++)
                {
                    Joint(o.Limbs[arm], new Vector3(-90 * stumble * (1 - knee) + 40 * wheel * (arm == 0 ? 1 : -1) - 20 * knee, 0, (arm == 0 ? -1 : 1) * 40 * (1 - knee)), blend);
                    Joint(o.Forearms[arm], new Vector3(-30 - 40 * knee, 0, 0), blend);
                }
                if (knee * (1 - up) > 0.001f) Kneel(o, 1, knee * (1 - up), blend); else Stance(o, 0.88f, 0.1f, -0.25f * stumble, blend);
                ShowIncidentName(o, "OFFICER\nOFF BALANCE", Hurt);
            }
            else
            {
                float sit = Window(p, 0, 0.18f), up = Window(p, 0.65f, 1);
                Offset(o, Vector3.zero, new Vector3(0, -0.5f * sit * (1 - up), 0), Quaternion.Euler(-25 * sit * (1 - up), 0, 0), blend);
                for (int leg = 0; leg < 2; leg++) { Joint(o.Limbs[2 + leg], new Vector3(-80 * sit * (1 - up), 0, (leg == 0 ? -1 : 1) * 10), blend); Joint(o.Knees[leg], new Vector3(40 * sit * (1 - up), 0, 0), blend); }
                for (int arm = 0; arm < 2; arm++) { Joint(o.Limbs[arm], new Vector3(30 * sit * (1 - up), 0, (arm == 0 ? -1 : 1) * 20), blend); Joint(o.Forearms[arm], new Vector3(-10, 0, 0), blend); }
                Torso(o, 10 * sit * (1 - up), 0, 0, blend);
                ShowIncidentName(o, "OFFICER\nTHROWN OFF", Hurt);
            }
        }

        // ---- pose helpers in this rig's convention ---------------------------------------------------------------------
        // Negative pitch swings a shoulder or hip forward; negative elbow pitch bends the arm; positive knee pitch folds the shin back.
        static void Joint(Transform joint, Vector3 euler, float blend) { if (joint != null) joint.localRotation = Quaternion.Slerp(joint.localRotation, Quaternion.Euler(euler), blend); }
        static float Yaw(Vector3 direction) { return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg; }
        void Turn(Actor a, float yaw, float degreesPerSecond)
        {
            float current = Mathf.MoveTowardsAngle(a.Root.eulerAngles.y, yaw, Mathf.Min(Time.unscaledDeltaTime, 0.05f) * degreesPerSecond);
            a.Root.rotation = Quaternion.Euler(0, current, 0);
        }
        // Shift the whole figure (world shift plus a shift in the root's own frame) and tilt it about the feet.
        void Offset(Actor a, Vector3 world, Vector3 local, Quaternion tilt, float blend)
        {
            Vector3 target = a.Root.InverseTransformVector(world) + local;
            a.Model.localPosition = Vector3.Lerp(a.Model.localPosition, target, blend);
            a.Model.localRotation = Quaternion.Slerp(a.Model.localRotation, tilt, blend);
        }
        void Torso(Actor a, float lean, float twist, float bank, float blend) { a.Body.localRotation = Quaternion.Slerp(a.Body.localRotation, Quaternion.Euler(lean, twist, bank), blend); }
        // Pitch the whole figure about its centre of mass (not its feet, which fells it like a tree), carrying the centre of
        // mass to the given height: 0.95 is standing; about 0.16 lies on the ground. World shift is added on top.
        void Lay(Actor a, float pitch, float centre, Vector3 world, float blend)
        {
            Quaternion tilt = Quaternion.Euler(pitch, 0, 0);
            Vector3 pivot = new Vector3(0, 0.95f, 0);
            Vector3 target = pivot - tilt * pivot + new Vector3(0, centre - 0.95f, 0) + a.Root.InverseTransformVector(world);
            a.Model.localPosition = Vector3.Lerp(a.Model.localPosition, target, blend);
            a.Model.localRotation = Quaternion.Slerp(a.Model.localRotation, tilt, blend);
        }
        // Feet planted on the ground at the given forward offsets (m) under a pelvis at the given height.
        void Stance(Actor a, float pelvis, float leftForward, float rightForward, float blend)
        {
            a.Body.localPosition = new Vector3(0, pelvis, 0);
            for (int leg = 0; leg < 2; leg++)
            {
                Transform hip = a.Limbs[2 + leg]; hip.localPosition = new Vector3(hip.localPosition.x, pelvis, 0);
                float hipPitch, knee; LegReach(leg == 0 ? leftForward : rightForward, -(pelvis - FootRest), out hipPitch, out knee);
                Joint(hip, new Vector3(hipPitch, 0, (leg == 0 ? -1 : 1) * 7), blend); Joint(a.Knees[leg], new Vector3(knee, 0, 0), blend);
            }
        }
        // One knee on the ground: the pelvis drops so the down knee touches; the other foot stays planted in front.
        void Kneel(Actor a, int downLeg, float amount, float blend)
        {
            float pelvis = Mathf.Lerp(HipHeight, 0.50f, amount);
            a.Body.localPosition = new Vector3(0, pelvis, 0);
            for (int leg = 0; leg < 2; leg++)
            {
                Transform hip = a.Limbs[2 + leg]; hip.localPosition = new Vector3(hip.localPosition.x, pelvis, 0);
                if (leg == downLeg) { Joint(hip, new Vector3(8 * amount, 0, 0), blend); Joint(a.Knees[leg], new Vector3(96 * amount, 0, 0), blend); }
                else
                {
                    float hipPitch, knee; LegReach(0.42f * amount, -(pelvis - FootRest), out hipPitch, out knee);
                    Joint(hip, new Vector3(hipPitch, 0, (leg == 0 ? -1 : 1) * 6), blend); Joint(a.Knees[leg], new Vector3(knee, 0, 0), blend);
                }
            }
        }
        void KneelBoth(Actor a, float amount, float blend)
        {
            float pelvis = Mathf.Lerp(HipHeight, 0.49f, amount);
            a.Body.localPosition = new Vector3(0, pelvis, 0);
            for (int leg = 0; leg < 2; leg++)
            {
                Transform hip = a.Limbs[2 + leg]; hip.localPosition = new Vector3(hip.localPosition.x, pelvis, 0);
                Joint(hip, new Vector3(-4 * amount, 0, (leg == 0 ? -1 : 1) * 5), blend); Joint(a.Knees[leg], new Vector3(98 * amount, 0, 0), blend);
            }
        }
        // Strain shaking through a torso (about 7 Hz): added to the torso's target angles, so it never accumulates.
        static Vector2 Tremor(float tempo, float amount)
        {
            return new Vector2((Mathf.PerlinNoise(tempo * 7f, 0.7f) - 0.5f) * 2 * amount, (Mathf.PerlinNoise(0.3f, tempo * 6.3f) - 0.5f) * 3.2f * amount);
        }

        // ---- impact feedback -----------------------------------------------------------------------------------------------
        void Impact(Vector3 point, float strength, bool flash, float dust)
        {
            CameraImpulse = Mathf.Max(CameraImpulse, strength); CameraImpulsePoint = point;
            if (dust > 0) SpawnPuff(new Vector3(point.x, 0.2f, point.z), dust, false);
            if (flash) SpawnPuff(point, 0.22f, true);
        }
        void SpawnPuff(Vector3 at, float size, bool flash)
        {
            if (puffRoot == null) { puffRoot = new GameObject("Impact dust and flashes / presentation only").transform; puffRoot.SetParent(transform, false); }
            Puff puff = null;
            foreach (Puff candidate in puffs) if (candidate.Flash == flash && Time.unscaledTime - candidate.Start > candidate.Life) { puff = candidate; break; }
            if (puff == null && puffs.Count < 16)
            {
                puff = new Puff { Flash = flash, Root = new GameObject(flash ? "Impact flash" : "Dust puff").transform };
                puff.Root.SetParent(puffRoot, false);
                if (flash)
                    for (int ray = 0; ray < 4; ray++)
                    {
                        // A comic impact star: four thin rays crossing at the point of contact.
                        Transform beam = Box(puff.Root, "Flash ray", Vector3.zero, new Vector3(0.09f, 1f, 0.09f), "lamp", new Color(1f, .92f, .70f));
                        beam.localRotation = Quaternion.Euler(ray * 45, 0, ray * 45 + 20);
                    }
                else for (int i = 0; i < 4; i++)
                    Sphere(puff.Root, "Dust", new Vector3(Mathf.Cos(i * 1.7f) * 0.35f, 0.1f + (i % 2) * 0.12f, Mathf.Sin(i * 1.7f) * 0.35f), Vector3.one * (0.55f + (i % 3) * 0.18f), "dust", new Color(.62f, .58f, .50f));
                Combine(puff.Root); puffs.Add(puff);
            }
            if (puff == null) return;
            puff.Start = Time.unscaledTime; puff.Life = flash ? 0.12f : 0.75f; puff.Size = size;
            puff.Root.position = at; puff.Root.localScale = Vector3.one * 0.001f; puff.Root.gameObject.SetActive(true);
        }
        void UpdatePuffs()
        {
            foreach (Puff puff in puffs)
            {
                float age = (Time.unscaledTime - puff.Start) / puff.Life;
                if (age >= 1) { if (puff.Root.gameObject.activeSelf) puff.Root.gameObject.SetActive(false); continue; }
                // Dust billows out and sinks away; a flash pops and is gone.
                float scale = puff.Flash ? Mathf.Sin(age * Mathf.PI) : Mathf.Sin(Mathf.Min(1, age * 1.6f) * Mathf.PI * 0.5f) * (1 - age * age);
                puff.Root.localScale = Vector3.one * puff.Size * Mathf.Max(0.001f, scale);
                if (!puff.Flash) puff.Root.position += Vector3.up * Time.unscaledDeltaTime * 0.12f;
            }
        }
#if UNITY_EDITOR
        public string PreviewEscapeBeat { get { return cast.Beat; } }
        public int PreviewActivePuffs { get { int count = 0; foreach (Puff p in puffs) if (p.Root != null && p.Root.gameObject.activeSelf) count++; return count; } }
#endif
    }
}
