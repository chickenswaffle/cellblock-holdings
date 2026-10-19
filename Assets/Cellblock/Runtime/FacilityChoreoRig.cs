using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    // ActionChoreo v1 consumer (Claude, 2026-10-19). Codex's Core publishes, per actor and frame, where the body should
    // stand and where its hands and knee must land (FacilityActionChoreo.cs, read-only). This adapter makes the visible
    // body do it, after ordinary locomotion and poses and before skinning:
    //   1. Root: ease to the published ground and yaw during Approach/Setup, hold it exactly once contact starts (Core
    //      has already resolved any step-in — RootAlreadyAligned — so nothing here adds a second one). Free-moving
    //      bodies (recoveries, counters) keep their authoritative root.
    //   2. After every rig has posed its bones from the procedural pivots (so partners' wrists are final), hands go onto
    //      their targets (wrist, elbow, collar, pocket… or a world point for a missed punch) by weight, with the forearm
    //      re-aimed at the hand and the fingers set to the published grip; the right knee goes onto the shoulder blade
    //      while prone cuffing. Bones carry the skin, so what the contact gates observe is what the player sees.
    public sealed partial class FacilityView
    {
        public static bool ChoreoRigEnabled = true;

        // Before the ordinary rig pass: roots.
        int choreoRootFrame = -1;
        readonly System.Collections.Generic.Dictionary<Actor, float> choreoPosture = new System.Collections.Generic.Dictionary<Actor, float>();
        void AlignChoreoRoots(float dt)
        {
            ActionChoreoConsumerVersion = ChoreoRigEnabled && humanRigs.Count > 0 ? ActionChoreo.Version : 0;
            if (ActionChoreoConsumerVersion == 0 || presentedWorld == null || choreoRootFrame == Time.frameCount) return;
            choreoRootFrame = Time.frameCount; // the rig pass can run more than once a frame; roots ease once
            foreach (HumanRig rig in humanRigs)
            {
                ActionChoreoFrame frame;
                if (rig.Actor.Root == null || !TryGetActionChoreoFrame(rig.Id, out frame) || frame.Root.Weight <= 0) continue;
                if (FreeMoving(frame) || KneelsOnBack(frame)) continue; // a knee-on-back kneel places its own root (below)
                Transform root = rig.Actor.Root;
                Vector3 goal = new Vector3(frame.Root.Ground.X, root.position.y, frame.Root.Ground.Y);
                bool approaching = frame.Phase == ChoreoPhase.Approach || frame.Phase == ChoreoPhase.Setup;
                float ease = approaching ? 1 - Mathf.Exp(-dt * 10) : 1;
                Vector3 next = Vector3.Lerp(root.position, goal, ease * Mathf.Clamp01(frame.Root.Weight));
                if (presentedWorld.Routes.Clear(new Point(root.position.x, root.position.z), new Point(next.x, next.z))) root.position = next;
                float yaw = Mathf.LerpAngle(root.eulerAngles.y, frame.Root.Yaw, ease * Mathf.Clamp01(frame.Root.Weight));
                root.rotation = Quaternion.Euler(0, yaw, 0);
            }
            foreach (ActionChoreoFrame published in choreoFrames.Values) CollectHits(published); // FacilityHitReactions.cs
            // Posture from the frame, so the targets are within reach of a natural body (IK then closes only the
            // remainder): kneel on the right knee to put it on the shoulder blade, crouch or kneel as hinted, lean, and
            // bring the arms toward their targets so elbows bend as people's do.
            foreach (HumanRig rig in humanRigs)
            {
                ActionChoreoFrame frame; Actor a = rig.Actor;
                bool active = a.Root != null && TryGetActionChoreoFrame(rig.Id, out frame) && !FreeMoving(frame);
                // Own fade (0→1 in an eighth of a second) applied at full strength over whatever the pose systems set this
                // frame: a per-frame partial blend from their output would never accumulate into the posture.
                float weight; choreoPosture.TryGetValue(a, out weight);
                weight = Mathf.MoveTowards(weight, active ? 1 : 0, dt * 8); choreoPosture[a] = weight;
                if (!active) continue;
                TryGetActionChoreoFrame(rig.Id, out frame);
                float settle = Smooth01(weight);
                if (frame.IsResident) GroundPosture(a, frame, dt);
                if (!KneelsOnBack(frame)) kneelFacing.Remove(a);
                if (KneelsOnBack(frame))
                {
                    // The knee rests ON the back, so the hips sit a thigh above the blade, not at a ground kneel.
                    Vector3 bladeAt;
                    float pelvis = TryResolveActionChoreoTarget(frame.KneeTarget, out bladeAt) ? Mathf.Clamp(a.Model.InverseTransformPoint(bladeAt).y + Thigh * .86f, .5f, .82f) : .5f;
                    Kneel(a, 1, Mathf.InverseLerp(HipHeight, .5f, pelvis), settle);
                    float twist = TwistToward(a, frame);
                    float leanNow = ReachLean(a, frame, Mathf.Max(frame.Lean, 38), twist);
                    Torso(a, leanNow, twist, 0, settle);
                    // Kneel against the body the player sees: right knee onto the rendered shoulder blade, facing along the
                    // back toward the wrists. (Core's prone layout and the rendered prone body differ by up to ~0.6 m;
                    // contact roots are only gated for strikes and shoves, so this stays inside the contract.)
                    Vector3 blade, wrists; ChoreoTarget onBack = BladeOf(frame);
                    if (TryResolveActionChoreoTarget(onBack, out blade))
                    {
                        Actor subject = Live(onBack.ActorId);
                        wrists = subject != null ? (subject.Hands[0].position + subject.Hands[1].position) * .5f : blade + a.Root.forward;
                        // Beside the subject, facing across the back and a little toward the hands (the knee controls the
                        // blade while the hands work at the small of the back), on whichever side the officer already is.
                        Vector3 along = wrists - blade; along.y = 0;
                        if (along.sqrMagnitude > .0004f)
                        {
                            along.Normalize(); Vector3 across = Vector3.Cross(Vector3.up, along);
                            Vector3 offset = a.Root.position - blade; float side = Vector3.Dot(offset, across) >= 0 ? 1 : -1;
                            Vector3 face = (-side * across * .8f + along * .6f).normalized;
                            // Own facing state, applied in full: the first-person camera re-faces the body every frame.
                            float shown; if (!kneelFacing.TryGetValue(a, out shown)) shown = a.Root.eulerAngles.y;
                            shown = Mathf.MoveTowardsAngle(shown, Quaternion.LookRotation(face, Vector3.up).eulerAngles.y, dt * 360); kneelFacing[a] = shown;
                            a.Root.rotation = Quaternion.Euler(0, shown, 0);
                        }
                        Vector3 kneeFromRoot = a.Knees[1].position - a.Root.position; kneeFromRoot.y = 0;
                        Vector3 goal = new Vector3(blade.x, a.Root.position.y, blade.z) - kneeFromRoot;
                        Vector3 next = Vector3.Lerp(a.Root.position, goal, 1 - Mathf.Exp(-dt * 32));
                        if (presentedWorld.Routes.Clear(new Point(a.Root.position.x, a.Root.position.z), new Point(next.x, next.z))) a.Root.position = next;
                    }
                }
                else if (frame.Crouch > .05f)
                {
                    if (frame.Crouch > .6f) Kneel(a, 1, frame.Crouch, settle); else Stance(a, Mathf.Lerp(HipHeight, .62f, frame.Crouch / .6f), .16f, -.12f, settle);
                    Torso(a, frame.Lean, 0, 0, settle);
                }
                else if (Mathf.Abs(frame.Lean) > .5f) Torso(a, frame.Lean, 0, 0, settle);
                for (int side = 0; side < 2; side++)
                {
                    ChoreoTarget target = frame.HandTargets[side]; Vector3 point;
                    if (target.Active && TryResolveActionChoreoTarget(target, out point)) ArmPose(a, side, a.Model.InverseTransformPoint(point), settle * Mathf.Clamp01(target.Weight));
                }
            }
            ApplyHitReactions(); // on top of this frame's posture, once per frame (FacilityHitReactions.cs)
        }
        // The least forward lean (from `least` up to 78°) that brings both shoulders within arm's reach of their targets.
        // Shoulders turn toward the hands' work (up to 45°): the far hand is then within reach too.
        float TwistToward(Actor a, ActionChoreoFrame frame)
        {
            Vector3 sum = Vector3.zero; int n = 0;
            for (int side = 0; side < 2; side++) { Vector3 p; if (frame.HandTargets[side].Active && TryResolveActionChoreoTarget(frame.HandTargets[side], out p)) { sum += p; n++; } }
            if (n == 0) return 0;
            Vector3 to = sum / n - a.Root.position; to.y = 0;
            return to.sqrMagnitude < .0001f ? 0 : Mathf.Clamp(Vector3.SignedAngle(a.Root.forward, to, Vector3.up), -45, 45);
        }
        float ReachLean(Actor a, ActionChoreoFrame frame, float least, float twist = 0)
        {
            Quaternion before = a.Body.localRotation; float chosen = least;
            for (float lean = least; lean <= 78; lean += 6)
            {
                a.Body.localRotation = Quaternion.Euler(lean, twist, 0); chosen = lean; bool reach = true;
                for (int side = 0; side < 2; side++)
                {
                    ChoreoTarget target = frame.HandTargets[side]; Vector3 point;
                    if (target.Active && TryResolveActionChoreoTarget(target, out point) && Vector3.Distance(a.Limbs[side].position, point) > .46f) reach = false;
                }
                if (reach) break;
            }
            a.Body.localRotation = before; return chosen;
        }
        // A subject on the ground lies as the published phase says: face-down while prone (hands then rest on top of the
        // lower back, where cuffs go), on the side for the roll, sitting up for assisted recovery. A change of more than
        // 120° (face-up after a knockout to face-down) rolls through the side instead of swinging up through standing.
        readonly System.Collections.Generic.Dictionary<Actor, Quaternion> groundTurn = new System.Collections.Generic.Dictionary<Actor, Quaternion>();
        readonly System.Collections.Generic.Dictionary<Actor, float> kneelFacing = new System.Collections.Generic.Dictionary<Actor, float>();
        readonly System.Collections.Generic.Dictionary<Actor, bool> rollViaSide = new System.Collections.Generic.Dictionary<Actor, bool>();
        void GroundPosture(Actor a, ActionChoreoFrame frame, float dt)
        {
            Quaternion target; float centre;
            switch (frame.ResidentPhase)
            {
                case ResidentActionPhase.Prone: target = Quaternion.Euler(90, 0, 0); centre = .16f; break;
                case ResidentActionPhase.SideLying: target = Quaternion.Euler(0, 0, 90); centre = .2f; break;
                case ResidentActionPhase.Seated: target = Quaternion.Euler(-28, 0, 0); centre = .48f; break;
                default: groundTurn.Remove(a); rollViaSide.Remove(a); return; // standing, kneeling, falling: the pose systems' own lay
            }
            Quaternion current; if (!groundTurn.TryGetValue(a, out current)) current = a.Model.localRotation;
            // Commit to the side as a waypoint for a big turn, until it is reached (a threshold test alone dithers there).
            Quaternion side = Quaternion.Euler(0, 0, 90);
            bool via; rollViaSide.TryGetValue(a, out via);
            if (Quaternion.Angle(current, target) > 125) via = true;
            if (via && Quaternion.Angle(current, side) < 3) via = false;
            rollViaSide[a] = via;
            Quaternion aim = via ? side : target;
            current = Quaternion.RotateTowards(current, aim, dt * 480); groundTurn[a] = current; // a quick roll: ~0.4 s face-up to face-down
            Vector3 pivot = new Vector3(0, .95f, 0);
            a.Model.localRotation = current;
            a.Model.localPosition = pivot - current * pivot + new Vector3(0, centre - .95f, 0);
        }
        // A hand never pulls its arm longer than it is (9% for skin stretch): out-of-reach targets are posture's job.
        static Vector3 WithinReach(Transform shoulder, Transform elbow, Transform hand, Vector3 wanted)
        {
            if (shoulder == null) return wanted;
            float length = (Vector3.Distance(shoulder.position, elbow.position) + Vector3.Distance(elbow.position, hand.position)) * 1.09f;
            Vector3 d = wanted - shoulder.position;
            return d.magnitude <= length ? wanted : shoulder.position + d.normalized * length;
        }
        // Prone cuffing, from following to the shoulder side until the double lock: the officer's right knee goes on the
        // rendered shoulder blade, so the kneel (not Core's prone layout estimate) decides where the officer is.
        static bool KneelsOnBack(ActionChoreoFrame frame)
        {
            return !frame.IsResident && (frame.KneeTarget.Active || frame.Action == ChoreoAction.ProneCuff && (frame.Phase == ChoreoPhase.Approach || frame.Phase == ChoreoPhase.Setup));
        }
        static ChoreoTarget BladeOf(ActionChoreoFrame frame)
        {
            return frame.KneeTarget.Active ? frame.KneeTarget : new ChoreoTarget { ActorId = frame.PartnerId, Point = ChoreoPoint.ShoulderBlade, Weight = 1 };
        }
        static bool FreeMoving(ActionChoreoFrame frame)
        {
            // Guard and free recovery follow the person's own movement; their frames publish the live root anyway.
            return frame.Action == ChoreoAction.Guard || frame.Action == ChoreoAction.RecoverFromFall;
        }

        // After the ordinary rig pass: hands and knee onto their targets.
        void ApplyChoreoContacts()
        {
            if (ActionChoreoConsumerVersion == 0 || presentedWorld == null) return;
            // Subjects first (their hands go behind the back, onto the head…), then whoever reaches for those wrists.
            for (int pass = 0; pass < 2; pass++)
            foreach (HumanRig rig in humanRigs)
            {
                ActionChoreoFrame frame;
                if (rig.Actor.Root == null || !TryGetActionChoreoFrame(rig.Id, out frame) || frame.IsResident != (pass == 0)) continue;
                for (int side = 0; side < 2; side++)
                {
                    ChoreoTarget target = frame.HandTargets[side];
                    Vector3 point;
                    if (!target.Active || rig.HandBones[side] == null || rig.ElbowBones[side] == null || !TryResolveActionChoreoTarget(target, out point)) continue;
                    float weight = Mathf.Clamp01(target.Weight);
                    Transform hand = rig.HandBones[side], elbow = rig.ElbowBones[side];
                    Vector3 placed = WithinReach(elbow.parent, elbow, hand, Vector3.Lerp(hand.position, point, weight));
                    Aim(elbow, hand, placed - elbow.position);
                    hand.position = placed;
                    rig.Curl[side] = Mathf.Lerp(rig.Curl[side], target.Grip, weight);
                    foreach (Finger f in rig.Fingers[side]) f.Bone.localRotation = f.Rest * Quaternion.AngleAxis(f.Max * (f.Thumb ? Mathf.Min(rig.Curl[side], .7f) : rig.Curl[side]), f.Axis);
                }
                if (frame.KneeTarget.Active && rig.KneeBones[1] != null && rig.FootBones[1] != null)
                {
                    Vector3 blade;
                    if (TryResolveActionChoreoTarget(frame.KneeTarget, out blade))
                    {
                        Transform knee = rig.KneeBones[1], foot = rig.FootBones[1];
                        Vector3 footAt = foot.position, placed = Vector3.Lerp(knee.position, blade, Mathf.Clamp01(frame.KneeTarget.Weight));
                        // Thigh length is fixed too (6% for skin): the posture pass has already knelt within reach.
                        if (knee.parent != null) { float thigh = Vector3.Distance(knee.parent.position, knee.position) * 1.06f; Vector3 d = placed - knee.parent.position; if (d.magnitude > thigh) placed = knee.parent.position + d.normalized * thigh; }
                        if (knee.parent != null) Aim(knee.parent, knee, placed - knee.parent.position);
                        knee.position = placed; Aim(knee, foot, footAt - placed); foot.position = footAt;
                    }
                }
            }
        }
    }
}
