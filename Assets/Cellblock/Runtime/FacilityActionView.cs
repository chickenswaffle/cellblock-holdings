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
#if UNITY_EDITOR
        public float PreviewActionClock { get { return actionClock; } }
#endif
        void ResetActionPresentation()
        {
            actionWorld = null; actionClock = 0;
            liveBeats.Clear(); seenBeats.Clear(); fellAt.Clear();
            if (actionRing != null) actionRing.gameObject.SetActive(false);
        }
        const float StrikeSeconds = 0.5f, BeatSeconds = 0.7f;

        void RefreshStaffActionScene(World world)
        {
            if (!ReferenceEquals(world, actionWorld)) { actionWorld = world; actionClock = 0; liveBeats.Clear(); seenBeats.Clear(); fellAt.Clear(); }
            float dt = Mathf.Min(SceneFrameDelta, .05f) * Mathf.Max(0, world.Data.Speed);
            actionClock += dt;
            float now = actionClock, blend = 1 - Mathf.Exp(-dt * 16);
            foreach (ActionBeat beat in world.ActionBeats)
                if (seenBeats.Add(beat)) liveBeats.Add(new LiveBeat { Beat = beat, Start = now });
            liveBeats.RemoveAll(b => now - b.Start > BeatSeconds);
            if (seenBeats.Count > 64) seenBeats.IntersectWith(world.ActionBeats);

            Actor officer = world.CharacterKind == "Staff" ? Live(world.CharacterId) : null;
            RefreshActionRing(world, officer);

            // Knocked down: fall onto the back over a third of a second, then stay down until the core lets them up.
            foreach (Resident r in world.Data.Residents)
            {
                if (!world.Downed(r)) { fellAt.Remove(r.Id); continue; }
                Actor down = Live(r.Id);
                if (down == null || heldThisFrame.Contains(down) && r.Id != world.HeldResidentId) continue;
                float fell; if (!fellAt.TryGetValue(r.Id, out fell)) fellAt[r.Id] = fell = now;
                Take(down, Hold.Body); down.PoseArms = true; incidentPoseCount++;
                float t = Mathf.Clamp01((now - fell) / 0.35f);
                Lay(down, -90 * Smooth01(t), Mathf.Lerp(0.95f, 0.16f, Smooth01(t)), Vector3.zero, t < 1 ? 1 : blend);
                for (int arm = 0; arm < 2; arm++) { Joint(down.Limbs[arm], new Vector3(-20, 0, arm == 0 ? 40 : -40), blend); Joint(down.Forearms[arm], new Vector3(-30, 0, 0), blend); }
            }
            if (officer == null) return;

            Resident heldResident = world.Data.Residents.FirstOrDefault(r => r.Id == world.HeldResidentId);
            Actor held = heldResident == null ? null : Live(heldResident.Id);
            string busy = world.BusyAction;
            if (held != null && !world.Downed(heldResident))
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
                else if (busy == "Cuffing")
                {
                    Take(officer, Hold.Upper); Take(held, Hold.Upper); officer.PoseArms = held.PoseArms = true; incidentPoseCount += 2;
                    Turn(officer, Yaw(held.Root.position - officer.Root.position), 720);
                    BehindBack(held, blend); Torso(held, 18, 0, 0, blend);
                    for (int arm = 0; arm < 2; arm++) ArmPose(officer, arm, officer.Model.InverseTransformPoint(held.Body.position + Vector3.up * 0.18f - held.Root.forward * 0.18f + held.Root.right * (arm == 0 ? -0.06f : 0.06f)), blend);
                    Torso(officer, 14, 0, 0, blend);
                }
                else if (world.HoldPhase == "Cuffed") { Take(officer, Hold.Upper); Take(held, Hold.Upper); EscortScene(officer, held); }
            }
            Resident searched = busy == "Pat search" ? world.Data.Residents.FirstOrDefault(r => r.Id == world.BusyTarget) : null;
            Actor subject = searched == null ? null : Live(searched.Id);
            if (subject != null)
            {
                // Hands on the wall: the subject faces away with arms out; the officer pats from shoulders to waist.
                Take(officer, Hold.Upper); Take(subject, Hold.Upper); officer.PoseArms = subject.PoseArms = true; incidentPoseCount += 2;
                Turn(officer, Yaw(subject.Root.position - officer.Root.position), 720); Turn(subject, Yaw(subject.Root.position - officer.Root.position), 360);
                for (int arm = 0; arm < 2; arm++) { Joint(subject.Limbs[arm], new Vector3(-95, 0, arm == 0 ? 25 : -25), blend); Joint(subject.Forearms[arm], new Vector3(-10, 0, 0), blend); }
                float pat = 1.25f - Mathf.PingPong(now * 1.6f, 0.55f);
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

            foreach (LiveBeat live in liveBeats) StageBeat(world, live, officer, now, blend);
        }

        // One strike, counter, grab or shove, staged over half a second from the moment the core recorded it.
        void StageBeat(World world, LiveBeat live, Actor officer, float now, float blend)
        {
            ActionBeat beat = live.Beat;
            float t = Mathf.Clamp01((now - live.Start) / StrikeSeconds);
            Actor target = Live(beat.TargetId);
            switch (beat.Kind)
            {
                case "Strike":
                case "Swing":
                case "Takedown":
                {
                    if (heldThisFrame.Contains(officer) && beat.Kind != "Takedown") { /* the hold owns the body; the punch still reads on the arm */ }
                    Take(officer, Hold.Upper); officer.PoseArms = true; incidentPoseCount++;
                    if (target != null) Turn(officer, Yaw(target.Root.position - officer.Root.position), 900);
                    // A quick wind-up, a fast extension, a held moment at full reach (contact), then the recovery.
                    float wind = Window(t, 0, 0.18f), extend = Window(t, 0.18f, 0.32f), back = Window(t, 0.62f, 1f);
                    Vector3 aim = target != null ? target.Head != null ? target.Head.position : target.Body.position + Vector3.up * 0.5f : officer.Body.position + officer.Root.forward * 0.8f + Vector3.up * 0.5f;
                    Vector3 guard = new Vector3(0.18f, 1.32f, 0.12f), reach = officer.Model.InverseTransformPoint(aim);
                    Vector3 fist = Vector3.Lerp(Vector3.Lerp(new Vector3(0.22f, 1.25f, -0.05f), guard, wind), reach, extend * (1 - back));
                    ArmPose(officer, 1, fist, 1);
                    ArmPose(officer, 0, new Vector3(-0.16f, 1.34f, 0.16f), blend);
                    float twist = Mathf.Lerp(-18 * wind, 24, extend) * (1 - back);
                    Torso(officer, beat.Power ? 24 * extend * (1 - back) : 6 + 6 * extend, twist, 0, 1);
                    if (beat.Power) Offset(officer, Vector3.zero, new Vector3(0, 0, 0.35f * extend * (1 - back)), Quaternion.identity, 1);
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
                        Torso(target, -16 * recoil, 14 * recoil, 0, 1);
                        if (target.Head != null) Joint(target.Head, new Vector3(-22 * recoil, 18 * recoil, 0), 1);
                    }
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
            string id = officer == null ? "" : world.HeldResidentId != "" ? world.HeldResidentId : world.StaffActionHints().TargetId;
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
