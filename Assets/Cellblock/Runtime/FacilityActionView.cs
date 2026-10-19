using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cellblock.Unity
{
    // Choreography for the staff action pad (Claude, 2026-10-18), on top of the escape/brawl pose helpers: the officer's
    // strike (a shoulder charge when running), the recoil or fall of the person hit, a counter-punch, the hold, cuffing and
    // escort, the pat search and the bend to pick something up, plus an amber ring under whoever the pad would act on.
    // Every beat is a live core event; the core already decided what landed, so the animation only stages it.
    public sealed partial class FacilityView
    {
        sealed class LiveBeat { public ActionBeat Beat; public float Start; public bool Hit; }
        readonly List<LiveBeat> liveBeats = new List<LiveBeat>();
        readonly HashSet<ActionBeat> seenBeats = new HashSet<ActionBeat>();
        readonly Dictionary<string, float> fellAt = new Dictionary<string, float>();
        Transform actionRing; Material actionRingMaterial; World actionWorld;
        float actionClock;
        Actor staffRisingOfficer, staffRisingSubject;
        int staffRiseMinute; float staffRiseFraction; string staffRiseSubjectId = "";
#if UNITY_EDITOR
        public float PreviewActionClock { get { return actionClock; } }
        public float PreviewActionBodyPitch(string id) { Actor a = Live(id); return a == null ? 0 : Vector3.Angle(a.Model.up, a.Root.up); }
        public float PreviewActionCentre(string id) { Actor a = Live(id); return a == null ? 10 : a.Model.TransformPoint(new Vector3(0, .95f, 0)).y - a.Root.position.y; }
        public float PreviewActionPelvis(string id) { Actor a = Live(id); return a == null ? 10 : a.Body.localPosition.y; }
#endif
        void ResetActionPresentation()
        {
            actionWorld = null; actionClock = 0; staffRisingOfficer = staffRisingSubject = null; staffRiseSubjectId = "";
            liveBeats.Clear(); seenBeats.Clear(); fellAt.Clear();
            if (actionRing != null) actionRing.gameObject.SetActive(false);
        }
        const float StrikeSeconds = 0.5f, BeatSeconds = 0.7f;

        void RefreshStaffActionScene(World world)
        {
            if (!ReferenceEquals(world, actionWorld)) { actionWorld = world; actionClock = 0; liveBeats.Clear(); seenBeats.Clear(); fellAt.Clear(); }
            float dt = Mathf.Min(SceneFrameDelta, .05f) * Mathf.Max(0, world.Data.Speed);
            actionClock += dt;
            // A paused clock still evaluates its exact pose; it must not leave a newly observed restraint upright.
            float now = actionClock, blend = world.Data.Speed == 0 ? 1 : 1 - Mathf.Exp(-dt * 16);
            RefreshActionChoreo(world);
            foreach (ActionBeat beat in world.ActionBeats)
                if (seenBeats.Add(beat)) liveBeats.Add(new LiveBeat { Beat = beat, Start = now });
            liveBeats.RemoveAll(b => now - b.Start > BeatSeconds || (world.Data.Minute - b.Beat.Minute) * .2f > BeatSeconds);
            if (seenBeats.Count > 64) seenBeats.IntersectWith(world.ActionBeats);

            Actor officer = world.CharacterKind == "Staff" ? Live(world.CharacterId) : null;
            if (staffRisingSubject != null && (!world.StaffStandingUp || officer != staffRisingOfficer || world.HeldResidentId != staffRiseSubjectId || world.BusyTarget != world.HeldResidentId))
            {
                ReleaseStaffRisePose(staffRisingOfficer); ReleaseStaffRisePose(staffRisingSubject);
                staffRisingOfficer = staffRisingSubject = null; staffRiseSubjectId = "";
            }
            // Prisoner mode: the pad belongs to the resident under control, and fights are between residents.
            RefreshActionRing(world, officer ?? (world.InmateMode && world.CharacterId != "" ? Live(world.CharacterId) : null));

            // Knocked down: fall onto the back over a third of a second, then stay down until the core lets them up.
            foreach (Resident r in world.Data.Residents)
            {
                if (!world.Downed(r)) { fellAt.Remove(r.Id); continue; }
                Actor down = Live(r.Id);
                if (down == null || heldThisFrame.Contains(down) && r.Id != world.HeldResidentId) continue;
                float fell; if (!fellAt.TryGetValue(r.Id, out fell)) fellAt[r.Id] = fell = now;
                float elapsed = now - fell;
                // Reconcile missed/explicit game-clock steps even if a previous frame already started the fall.
                // Both clocks remain fixed during an ordinary pause.
                for (int i = world.ActionBeats.Count - 1; i >= 0; i--)
                {
                    ActionBeat beat = world.ActionBeats[i];
                    if (beat.TargetId != r.Id || !beat.Down) continue;
                    elapsed = Mathf.Max(elapsed, Mathf.Clamp((world.Data.Minute - beat.Minute) * .2f, 0, .35f)); break;
                }
                Take(down, Hold.Body); down.PoseArms = true; incidentPoseCount++;
                float t = Mathf.Clamp01(elapsed / .35f);
                Lay(down, -90 * Smooth01(t), Mathf.Lerp(0.95f, 0.16f, Smooth01(t)), Vector3.zero, t < 1 ? 1 : blend);
                for (int arm = 0; arm < 2; arm++) { Joint(down.Limbs[arm], new Vector3(-20, 0, arm == 0 ? 40 : -40), blend); Joint(down.Forearms[arm], new Vector3(-30, 0, 0), blend); }
            }
            if (officer == null) { foreach (LiveBeat live in liveBeats) StageBeat(world, live, Live(live.Beat.ActorId), now, blend); return; }

            Resident heldResident = world.Data.Residents.FirstOrDefault(r => r.Id == world.HeldResidentId);
            Actor held = heldResident == null ? null : Live(heldResident.Id);
            string busy = world.BusyAction;
            if (held != null && (!world.Downed(heldResident) || busy == "Cuffing" || world.StaffGroundedRestraint))
            {
                if (world.HoldPhase == "Holding")
                {
                    Take(officer, Hold.Body); Take(held, Hold.Body); officer.PoseArms = held.PoseArms = true; incidentPoseCount += 2;
                    Turn(officer, Yaw(held.Root.position - officer.Root.position), 720); Turn(held, Yaw(officer.Root.position - held.Root.position), 720);
                    bool fighting = world.Resisting(heldResident);
                    float wrestle = fighting ? Mathf.Sin(now * 9) * 0.08f : 0;
                    // The officer has both hands on the resident's shoulders; a resisting resident shoves at the officer's chest.
                    Stance(officer, 0.84f, 0.2f + wrestle, -0.14f - wrestle, blend); Torso(officer, 10, wrestle * 60, 0, blend);
                    for (int arm = 0; arm < 2; arm++)
                        ArmPose(officer, arm, officer.Model.InverseTransformPoint(held.Body.position + Vector3.up * 0.42f + held.Root.right * (arm == 0 ? 0.17f : -0.17f)), blend);
                    if (fighting)
                    {
                        Stance(held, 0.82f, -0.16f - wrestle, 0.18f + wrestle, blend); Torso(held, -6, Mathf.Sin(now * 7) * 12, 0, blend);
                        for (int arm = 0; arm < 2; arm++)
                            ArmPose(held, arm, held.Model.InverseTransformPoint(officer.Body.position + Vector3.up * 0.38f + officer.Root.right * (arm == 0 ? 0.12f : -0.12f)), blend);
                    }
                    else { Torso(held, -4, 0, 0, blend); for (int arm = 0; arm < 2; arm++) { Joint(held.Limbs[arm], new Vector3(-18, 0, arm == 0 ? 14 : -14), blend); Joint(held.Forearms[arm], new Vector3(-40, 0, 0), blend); } }
                }
                else if (busy == "Standing" && world.StaffStandingUp)
                {
                    StageStaffRise(world, officer, held);
                }
                else if (busy == "Cuffing")
                {
                    Take(officer, world.Downed(heldResident) ? Hold.Body : Hold.Upper); Take(held, Hold.Upper); officer.PoseArms = held.PoseArms = true; incidentPoseCount += 2;
                    Turn(officer, Yaw(held.Root.position - officer.Root.position), 720);
                    if (world.Downed(heldResident))
                    {
                        KneelBoth(officer, .82f, blend); Torso(officer, 24, 0, 0, blend);
                        BehindBack(held, blend);
                        for (int arm = 0; arm < 2; arm++) ArmPose(officer, arm, officer.Model.InverseTransformPoint(held.Hands[arm].position), blend);
                    }
                    else
                    {
                    BehindBack(held, blend); Torso(held, 18, 0, 0, blend);
                    for (int arm = 0; arm < 2; arm++) ArmPose(officer, arm, officer.Model.InverseTransformPoint(held.Body.position + Vector3.up * 0.18f - held.Root.forward * 0.18f + held.Root.right * (arm == 0 ? -0.06f : 0.06f)), blend);
                    Torso(officer, 14, 0, 0, blend);
                    }
                }
                else if (world.HoldPhase == "Cuffed")
                {
                    Take(officer, world.StaffGroundedRestraint ? Hold.Body : Hold.Upper); Take(held, Hold.Upper);
                    if (world.StaffGroundedRestraint) { BehindBack(held, blend); KneelBoth(officer, .82f, blend); Torso(officer, 20, 0, 0, blend); }
                    else EscortScene(officer, held);
                }
            }
            Resident searched = (busy == "Pat search" || busy == "Pocket search") ? world.Data.Residents.FirstOrDefault(r => r.Id == world.BusyTarget) : null;
            Actor subject = searched == null ? null : Live(searched.Id);
            if (subject != null)
            {
                // Hands on the wall: the subject faces away with arms out; the officer pats from shoulders to waist.
                Take(officer, world.Downed(searched) ? Hold.Body : Hold.Upper); Take(subject, Hold.Upper); officer.PoseArms = subject.PoseArms = true; incidentPoseCount += 2;
                Turn(officer, Yaw(subject.Root.position - officer.Root.position), 720); Turn(subject, Yaw(subject.Root.position - officer.Root.position), 360);
                bool grounded = world.Downed(searched), cuffed = world.HeldResidentId == searched.Id && world.HoldPhase == "Cuffed";
                if (grounded) KneelBoth(officer, .8f, blend);
                if (!cuffed && !grounded)
                    for (int arm = 0; arm < 2; arm++) { Joint(subject.Limbs[arm], new Vector3(-95, 0, arm == 0 ? 25 : -25), blend); Joint(subject.Forearms[arm], new Vector3(-10, 0, 0), blend); }
                else BehindBack(subject, blend);
                float pat = busy == "Pocket search" ? .99f + Mathf.Sin(now * 4) * .08f : 1.25f - Mathf.PingPong(now * 1.6f, 0.55f);
                for (int arm = 0; arm < 2; arm++) ArmPose(officer, arm, officer.Model.InverseTransformPoint(subject.Body.position + Vector3.up * (pat - 0.8f) + subject.Root.right * (arm == 0 ? -0.2f : 0.2f)), blend);
                Torso(officer, 16, 0, 0, blend);
            }
            if (busy == "Pick up" || busy == "Bunk search")
            {
                // Bend to the floor (a dropped package) or the bunk, one hand reaching down.
                Take(officer, Hold.Body); officer.PoseArms = true; incidentPoseCount++;
                KneelBoth(officer, busy == "Pick up" ? 0.8f : 0.55f, blend); Torso(officer, 28, 0, 0, blend);
                ArmPose(officer, 1, new Vector3(0.12f, 0.45f, 0.42f), blend);
                if (busy == "Bunk search") ArmPose(officer, 0, new Vector3(-0.15f, 0.5f, 0.4f + Mathf.Sin(now * 5) * 0.06f), blend);
            }

            foreach (LiveBeat live in liveBeats) StageBeat(world, live, Live(live.Beat.ActorId) ?? officer, now, blend);
        }

        void StageStaffRise(World world, Actor officer, Actor subject)
        {
            int startMinute = world.Data.Minute - Mathf.RoundToInt(world.StaffInteractionProgress * 6);
            if (staffRisingSubject != subject || staffRisingOfficer != officer || staffRiseMinute != startMinute)
            {
                staffRiseMinute = startMinute;
                staffRiseFraction = world.StaffInteractionProgress < .0001f ? sceneFraction : 0;
            }
            staffRisingOfficer = officer; staffRisingSubject = subject; staffRiseSubjectId = world.HeldResidentId;
            Take(officer, Hold.Body); Take(subject, Hold.Body); officer.PoseArms = subject.PoseArms = true;
            // Fixed endpoints on the shared game clock: roll up to sitting, gather the legs, then rise with support.
            float p = Mathf.Clamp01((world.StaffInteractionProgress * 6 + sceneFraction - staffRiseFraction) / Mathf.Max(1, 6 - staffRiseFraction));
            float sit = Window(p, 0, .4f), rise = Window(p, .4f, 1);
            float centre = Mathf.Lerp(Mathf.Lerp(.16f, .48f, sit), .95f, rise);
            float pitch = Mathf.Lerp(Mathf.Lerp(-90, -28, sit), 0, rise);
            Lay(subject, pitch, centre, Vector3.zero, 1);
            float fold = Window(p, .04f, .35f) * (1 - rise);
            for (int leg = 0; leg < 2; leg++)
            {
                subject.Limbs[leg + 2].localPosition = new Vector3(subject.Limbs[leg + 2].localPosition.x, HipHeight, 0);
                Joint(subject.Limbs[leg + 2], new Vector3(-66 * fold, 0, leg == 0 ? -5 : 5), 1);
                Joint(subject.Knees[leg], new Vector3(108 * fold, 0, 0), 1);
            }
            subject.Body.localPosition = new Vector3(0, HipHeight, 0); Torso(subject, 0, 0, 0, 1); BehindBack(subject, 1);
            float stand = Window(p, .2f, 1);
            KneelBoth(officer, .82f * (1 - stand), 1); Torso(officer, 20 * (1 - stand), 0, 0, 1);
            for (int arm = 0; arm < 2; arm++)
                ArmPose(officer, arm, officer.Model.InverseTransformPoint(subject.Body.position + subject.Root.right * (arm == 0 ? -.22f : .22f) + subject.Model.up * .22f), 1);
            ResetFootContacts(subject); ResetFootContacts(officer);
        }
        void ReleaseStaffRisePose(Actor actor)
        {
            if (actor == null || actor.Model == null || heldThisFrame.Contains(actor)) return;
            // Another incident animator can take ownership first; never erase its pose.
            heldPoses.Remove(actor); Upright(actor); actor.Body.localPosition = new Vector3(0, HipHeight, 0);
            actor.Body.localRotation = Quaternion.identity;
            for (int leg = 0; leg < 2; leg++)
            {
                Transform hip = actor.Limbs[leg + 2]; hip.localPosition = new Vector3(hip.localPosition.x, HipHeight, 0);
                hip.localRotation = Quaternion.identity; actor.Knees[leg].localRotation = Quaternion.identity;
            }
            ResetFootContacts(actor); StrideFor(actor).PelvisBase = HipHeight;
        }

        // One strike, counter, grab or shove, staged over half a second from the moment the core recorded it.
        void StageBeat(World world, LiveBeat live, Actor officer, float now, float blend)
        {
            ActionBeat beat = live.Beat;
            if (officer == null) return;
            float t = Mathf.Clamp01(Mathf.Max(now - live.Start, (world.Data.Minute - beat.Minute) * .2f) / StrikeSeconds);
            Actor target = Live(beat.TargetId);
            switch (beat.Kind)
            {
                case "Talk":
                {
                    // An open hand while talking, and the other person turns to listen.
                    if (target == null || heldThisFrame.Contains(officer)) break;
                    Take(officer, Hold.Upper); officer.PoseArms = true;
                    Turn(officer, Yaw(target.Root.position - officer.Root.position), 540);
                    float open = Window(t, 0, 0.3f) * (1 - Window(t, 0.75f, 1));
                    ArmPose(officer, 1, Vector3.Lerp(new Vector3(0.2f, 0.95f, 0.05f), new Vector3(0.22f, 1.18f, 0.32f), open), blend);
                    if (!heldThisFrame.Contains(target)) Turn(target, Yaw(officer.Root.position - target.Root.position), 360);
                    break;
                }
                case "Strike":
                case "Swing":
                {
                    if (heldThisFrame.Contains(officer) && beat.Kind != "Takedown") { /* the hold owns the body; the punch still reads on the arm */ }
                    Take(officer, Hold.Upper); officer.PoseArms = true; incidentPoseCount++;
                    if (target != null) Turn(officer, Yaw(target.Root.position - officer.Root.position), 900);
                    // A quick wind-up, a fast extension, a held moment at full reach (contact), then the recovery.
                    float wind = Window(t, 0, 0.18f), extend = Window(t, 0.18f, 0.32f), back = Window(t, 0.62f, 1f);
                    Vector3 aim = target != null ? target.Head != null ? target.Head.position : target.Body.position + Vector3.up * 0.5f : officer.Body.position + officer.Root.forward * 0.8f + Vector3.up * 0.5f;
                    int strikeArm = beat.Variant % 2; float side = strikeArm == 0 ? -1 : 1;
                    // Land on the face, not inside the skull: the front of the head as seen from the striker.
                    if (target != null && target.Head != null)
                    {
                        Vector3 toward = target.Head.position - officer.Body.position; toward.y = 0;
                        if (toward.sqrMagnitude > .0001f) aim -= toward.normalized * .09f;
                    }
                    // Close the distance: a short step in when the face is past arm's reach (Claude, 2026-10-19).
                    Vector3 shoulder = officer.Limbs[strikeArm].position; Vector3 gap = aim - shoulder; gap.y = 0;
                    float stepIn = Mathf.Clamp(gap.magnitude - .56f, 0, .3f);
                    // Core resolves the step-in (ActionChoreo RootAlreadyAligned) and the rig adapter places the root there:
                    // with a published frame, no presentation step is added, so the officer never steps twice.
                    ActionChoreoFrame published;
                    if (TryGetActionChoreoFrame(beat.ActorId, out published)) stepIn = 0;
                    float lunge = published != null ? 0 : Mathf.Max(beat.Power ? .35f : 0, stepIn) * extend * (1 - back);
                    // The step carries the arm's frame forward with it, so the reach is measured from where the body ends up.
                    Vector3 guard = new Vector3(side * .18f, 1.32f, .12f), reach = officer.Model.InverseTransformPoint(aim) - new Vector3(0, 0, lunge);
                    Vector3 fist = Vector3.Lerp(Vector3.Lerp(new Vector3(side * .22f, 1.25f, -.05f), guard, wind), reach, extend * (1 - back));
                    ArmPose(officer, strikeArm, fist, 1);
                    ArmPose(officer, 1 - strikeArm, new Vector3(-side * .16f, 1.34f, .16f), blend);
                    float twist = Mathf.Lerp(-18 * wind, 24, extend) * (1 - back) * side;
                    Torso(officer, beat.Power ? 24 * extend * (1 - back) : 6 + 6 * extend, twist, 0, 1);
                    if (lunge > .001f) Offset(officer, Vector3.zero, new Vector3(0, 0, lunge), Quaternion.identity, 1);
                    if (!live.Hit && t >= 0.32f && beat.Landed && target != null)
                    {
                        live.Hit = true;
                        Vector3 at = target.Head != null ? target.Head.position : target.Body.position + Vector3.up * 0.5f;
                        Impact(at, beat.Power || beat.Down ? 0.9f : 0.55f, true, beat.Down ? 0.5f : 0);
                        HitStop(beat.Power ? 0.09f : 0.05f);
                    }
                    // The person hit snaps back (a fall is staged by the knockdown pose).
                    if (target != null && beat.Landed && !beat.Down && t > 0.32f && !heldThisFrame.Contains(target))
                    {
                        float recoil = Window(t, 0.32f, 0.45f) * (1 - Window(t, 0.75f, 1));
                        Take(target, Hold.Upper); target.PoseArms = true;
                        // Snapped away from the fist: a right hand turns the head to the target's right, a left to its left.
                        Torso(target, -16 * recoil, 14 * recoil * -side, 0, 1);
                        if (target.Head != null) Joint(target.Head, new Vector3(-22 * recoil, 26 * recoil * -side, 9 * recoil * side), 1);
                    }
                    break;
                }
                case "Takedown":
                {
                    if (target == null) break;
                    Take(officer, Hold.Body); officer.PoseArms = true; incidentPoseCount++;
                    Turn(officer, Yaw(target.Root.position - officer.Root.position), 720);
                    float lower = Smooth01(Mathf.Clamp01(t / .65f));
                    Stance(officer, Mathf.Lerp(.88f, .60f, lower), .18f, -.12f, blend);
                    Torso(officer, 14 + lower * 20, lower * 12, 0, blend);
                    for (int arm = 0; arm < 2; arm++)
                        ArmPose(officer, arm, officer.Model.InverseTransformPoint(target.Body.position + target.Root.right * (arm == 0 ? -.17f : .17f) + Vector3.up * .18f), blend);
                    if (!live.Hit && t >= .55f)
                    { live.Hit = true; Impact(target.Body.position, .45f, false, .1f); }
                    break;
                }
                case "Counter":
                {
                    if (target == null) break;
                    // The resident swings back and catches the officer.
                    Take(target, Hold.Upper); target.PoseArms = true; incidentPoseCount++;
                    Turn(target, Yaw(officer.Root.position - target.Root.position), 900);
                    float extend = Window(t, 0.2f, 0.42f), back = Window(t, 0.6f, 1f);
                    Vector3 aim = officer.Head != null ? officer.Head.position : officer.Body.position + Vector3.up * 0.5f;
                    ArmPose(target, 1, Vector3.Lerp(new Vector3(0.18f, 1.3f, 0.1f), target.Model.InverseTransformPoint(aim), extend * (1 - back)), 1);
                    Torso(target, 8 * extend, 22 * extend * (1 - back), 0, 1);
                    if (!live.Hit && t >= 0.4f) { live.Hit = true; Impact(aim, 0.7f, true, 0); HitStop(0.06f); }
                    if (t > 0.4f && !heldThisFrame.Contains(officer))
                    {
                        float recoil = Window(t, 0.4f, 0.55f) * (1 - Window(t, 0.8f, 1));
                        Take(officer, Hold.Upper); Torso(officer, -18 * recoil, -12 * recoil, 0, 1);
                    }
                    break;
                }
                case "Grab":
                {
                    if (target == null || heldThisFrame.Contains(officer)) break;
                    Take(officer, Hold.Upper); officer.PoseArms = true;
                    float lunge = Window(t, 0, 0.4f) * (1 - Window(t, 0.7f, 1));
                    for (int arm = 0; arm < 2; arm++) ArmPose(officer, arm, officer.Model.InverseTransformPoint(target.Body.position + Vector3.up * 0.35f + target.Root.right * (arm == 0 ? 0.14f : -0.14f)), lunge);
                    Torso(officer, 14 * lunge, 0, 0, 1);
                    break;
                }
                case "Shove":
                {
                    // Shoved off: the officer stumbles back a step.
                    if (heldThisFrame.Contains(officer)) break;
                    Take(officer, Hold.Body); officer.PoseArms = true;
                    float stumble = Window(t, 0, 0.3f) * (1 - Window(t, 0.6f, 1));
                    Torso(officer, -20 * stumble, 0, 8 * stumble, 1);
                    Offset(officer, Vector3.zero, new Vector3(0, 0, -0.35f * stumble), Quaternion.identity, 1);
                    break;
                }
            }
        }
        void HitStop(float seconds) { hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + seconds); }

        // An amber ring on the ground under the person the pad would act on (or under the prisoner while holding).
        void RefreshActionRing(World world, Actor officer)
        {
            string id = officer == null ? "" : world.HeldResidentId != "" ? world.HeldResidentId : world.InmateMode ? world.StaffActionHints().TargetId : world.StaffHintsFor(world.StaffFocusTargetId).TargetId;
            Actor target = Live(id);
            if (target == null) { if (actionRing != null) actionRing.gameObject.SetActive(false); return; }
            if (actionRing == null)
            {
                Material template = Resources.Load<Material>("LuminaireMaterial");
                if (template == null || template.shader == null) return;
                actionRingMaterial = new Material(template) { name = "action-target-ring", color = new Color(0.98f, 0.74f, 0.25f) };
                if (actionRingMaterial.HasProperty("_EmissionColor")) { actionRingMaterial.EnableKeyword("_EMISSION"); actionRingMaterial.SetColor("_EmissionColor", new Color(0.98f, 0.74f, 0.25f) * 0.9f); }
                actionRing = new GameObject("Action target ring / presentation only").transform; actionRing.SetParent(transform, false);
                for (int i = 0; i < 16; i++)
                {
                    // A dashed circle of thin bars reads as a ring without a custom mesh or a transparent shader.
                    var bar = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(bar.GetComponent<Collider>());
                    bar.transform.SetParent(actionRing, false);
                    float angle = i * Mathf.PI * 2 / 16;
                    bar.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.42f, 0, Mathf.Sin(angle) * 0.42f);
                    bar.transform.localRotation = Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0);
                    bar.transform.localScale = new Vector3(0.03f, 0.012f, 0.11f);
                    Renderer r = bar.GetComponent<Renderer>(); r.sharedMaterial = actionRingMaterial; r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            actionRing.gameObject.SetActive(true);
            Vector3 at = target.Root.position; actionRing.position = new Vector3(at.x, at.y + 0.03f, at.z);
            actionRing.rotation = Quaternion.Euler(0, actionClock * 40, 0);
            actionRingMaterial.color = world.HeldResidentId != "" ? new Color(0.45f, 0.78f, 0.98f) : new Color(0.98f, 0.74f, 0.25f);
        }
    }
}
