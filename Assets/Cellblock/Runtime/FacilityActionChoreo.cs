using System.Collections.Generic;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    // Data publication only. Claude's rig/IK adapter consumes these frames; this file never writes transforms.
    public sealed partial class FacilityView
    {
        sealed class ChoreoSlot
        {
            public ActionChoreoContext Context;
            public readonly ActionChoreoCursor Cursor = new ActionChoreoCursor();
            public readonly ActionChoreoPair Pair = new ActionChoreoPair();
            public float FractionAtStart;
            public bool Seen;
        }
        readonly List<WorldActionChoreoState> choreoStates = new List<WorldActionChoreoState>(8);
        readonly Dictionary<string, ChoreoSlot> choreoSlots = new Dictionary<string, ChoreoSlot>();
        readonly Dictionary<string, ActionChoreoFrame> choreoFrames = new Dictionary<string, ActionChoreoFrame>();
        readonly List<string> choreoRetired = new List<string>();
        // Set by Claude's consuming adapter ONLY when it is actively applying this contract.
        public int ActionChoreoConsumerVersion { get; set; }
        World choreoWorld;
        int choreoFrame = -1;

        public bool TryGetActionChoreoFrame(string actorId, out ActionChoreoFrame frame)
        { RefreshActionChoreo(presentedWorld); return choreoFrames.TryGetValue(actorId ?? "", out frame); }
        void RefreshActionChoreo(World world)
        {
            if (choreoWorld != world)
            { choreoSlots.Clear(); choreoFrames.Clear(); choreoWorld = world; choreoFrame = -1; }
            if (world == null || choreoFrame == Time.frameCount) return;
            choreoFrame = Time.frameCount; choreoFrames.Clear();
            foreach (ChoreoSlot old in choreoSlots.Values) old.Seen = false;
            world.GetActionChoreoStates(choreoStates);
            foreach (WorldActionChoreoState state in choreoStates)
            {
                string key = state.Context.ActionId; ChoreoSlot slot;
                if (!choreoSlots.TryGetValue(key, out slot))
                {
                    slot = new ChoreoSlot { Context = state.Context, FractionAtStart = state.ElapsedSeconds < .0001f ? sceneFraction : 0 };
                    Actor actor = Live(state.Context.OfficerId), subject = Live(state.Context.ResidentId);
                    if (actor != null) slot.Context.OfficerYaw = actor.Root.eulerAngles.y;
                    if (slot.Context.PartnerHeadingKnown) slot.Context.ResidentYaw = state.Context.ResidentYaw;
                    else if (subject != null) slot.Context.ResidentYaw = subject.Root.eulerAngles.y;
                    choreoSlots.Add(key, slot);
                }
                if (state.Context.RootAlreadyAligned && !slot.Context.RootAlreadyAligned)
                { slot.Context.OfficerGround = state.Context.OfficerGround; slot.Context.ResidentGround = state.Context.ResidentGround; }
                slot.Context.RootAlreadyAligned = state.Context.RootAlreadyAligned;
                slot.Context.PartnerFreeMovement = state.Context.PartnerFreeMovement; slot.Context.ActorFreeMovement = state.Context.ActorFreeMovement;
                if (state.Context.PartnerFreeMovement) slot.Context.ResidentGround = state.Context.ResidentGround;
                if (state.Context.ActorFreeMovement) slot.Context.OfficerGround = state.Context.OfficerGround;
                slot.Context.ContactPending = state.Context.ContactPending; slot.Context.PartnerGuarding = state.Context.PartnerGuarding;
                slot.Context.Landed = state.Context.Landed; slot.Context.Reaction = state.Context.Reaction; slot.Context.Strength = state.Context.Strength;
                slot.Seen = true; slot.Context.Resisting = state.Context.Resisting;
                slot.Context.NarrowEscort = state.Context.NarrowEscort; slot.Context.EscortContact = state.Context.EscortContact;
                slot.Context.PartnerGrounded = state.Context.PartnerGrounded; slot.Context.PartnerCuffed = state.Context.PartnerCuffed;
                // Continuous escort goals follow the current source positions. Other actions retain their start anchors.
                if (state.Sustained)
                {
                    slot.Context.OfficerGround = state.Context.OfficerGround; slot.Context.ResidentGround = state.Context.ResidentGround;
                    Actor actor = Live(state.Context.OfficerId), subject = Live(state.Context.ResidentId);
                    if (actor != null) slot.Context.OfficerYaw = actor.Root.eulerAngles.y;
                    if (slot.Context.PartnerHeadingKnown) slot.Context.ResidentYaw = state.Context.ResidentYaw;
                    else if (subject != null) slot.Context.ResidentYaw = subject.Root.eulerAngles.y;
                }
                float elapsed = Mathf.Max(0, state.ElapsedSeconds + (sceneFraction - slot.FractionAtStart) * ActionChoreo.SecondsPerGameMinute);
                if (state.Sustained)
                {
                    float hold = slot.Context.Action == ChoreoAction.Escort ? 0 : ActionChoreo.Track(ChoreoAction.Grab).Steps[0].End + .01f;
                    ActionChoreo.Sample(slot.Context, hold, hold, slot.Pair);
                    if (slot.Context.Action == ChoreoAction.Guard) slot.Pair.Officer.Phase = ChoreoPhase.Control;
                    if (slot.Context.Action == ChoreoAction.Grab) { slot.Pair.Officer.Phase = ChoreoPhase.Control; slot.Pair.Resident.ResidentPhase = slot.Context.PartnerGrounded ? ResidentActionPhase.Prone : slot.Context.Resisting ? ResidentActionPhase.Resist : ResidentActionPhase.Brace; }
                }
                else slot.Cursor.Sample(slot.Context, elapsed, slot.Pair);
                // Primary actions win over another action's response for the same actor.
                choreoFrames[slot.Pair.Officer.ActorId] = slot.Pair.Officer;
                if (slot.Pair.Resident.ActorId != "" && !choreoFrames.ContainsKey(slot.Pair.Resident.ActorId)) choreoFrames[slot.Pair.Resident.ActorId] = slot.Pair.Resident;
            }
            choreoRetired.Clear(); foreach (var old in choreoSlots) if (!old.Value.Seen) choreoRetired.Add(old.Key);
            foreach (string id in choreoRetired) choreoSlots.Remove(id);
        }

        // Read-only observations for the joint contact gates, from visible bones where available.
        public bool TryGetActionChoreoObservation(string actorId, out Vector3 ground, out Vector3 leftHand, out Vector3 rightHand, out Vector3 knee)
        {
            ground = leftHand = rightHand = knee = Vector3.zero; Actor actor = Live(actorId); if (actor == null) return false;
            ground = actor.Root.position; leftHand = actor.Hands[0].position; rightHand = actor.Hands[1].position;
            knee = actor.Knees[1].position;
            foreach (var rig in humanRigs) if (rig.Id == actorId)
            {
                if (rig.HandBones[0] != null) leftHand = rig.HandBones[0].position;
                if (rig.HandBones[1] != null) rightHand = rig.HandBones[1].position;
                if (rig.KneeBones[1] != null) knee = rig.KneeBones[1].position;
                break;
            }
            return true;
        }
        public bool TryResolveActionChoreoTarget(ChoreoTarget target, out Vector3 point)
        {
            point = Vector3.zero; if (!target.Active) return false;
            if (target.IsWorld) { point = new Vector3(target.World.X, target.World.Y, target.World.Z); return true; }
            Actor a = Live(target.ActorId); if (a == null) return false;
            Transform basis = a.Body;
            switch (target.Point)
            {
                case ChoreoPoint.WristL: case ChoreoPoint.WristR:
                    int hand = target.Point == ChoreoPoint.WristL ? 0 : 1;
                    point = a.Hands[hand].position; foreach (var rig in humanRigs) if (rig.Id == target.ActorId && rig.HandBones[hand] != null) { point = rig.HandBones[hand].position; break; } break;
                case ChoreoPoint.ElbowL: point = a.Forearms[0].position; break;
                case ChoreoPoint.ElbowR: point = a.Forearms[1].position; break;
                case ChoreoPoint.ShoulderBlade: point = (a.Limbs[0].position + a.Limbs[1].position) * .5f - a.Body.forward * .12f; break;
                case ChoreoPoint.SmallOfBack: point = a.Body.position + a.Body.up * .08f - a.Body.forward * .14f; break;
                case ChoreoPoint.Head: basis = a.Head; point = a.Head.position; break;
                case ChoreoPoint.Jaw: basis = a.Head; point = a.Head.position - a.Head.up * .10f + a.Head.forward * .08f; break;
                case ChoreoPoint.Chest: point = a.Body.position + a.Body.up * .32f + a.Body.forward * .13f; break;
                case ChoreoPoint.AnkleL: point = a.Feet[0].position; break;
                case ChoreoPoint.AnkleR: point = a.Feet[1].position; break;
                case ChoreoPoint.KneeL: point = a.Knees[0].position; break;
                case ChoreoPoint.KneeR: point = a.Knees[1].position; break;
                case ChoreoPoint.InterlacedHands: point = (a.Hands[0].position + a.Hands[1].position) * .5f; break;
                default:
                    bool left = target.Name.EndsWith("_l"); float side = left ? -.18f : .18f;
                    point = a.Body.position + a.Body.right * side;
                    switch (target.Point)
                    {
                        case ChoreoPoint.CollarL: case ChoreoPoint.CollarR: point += a.Body.up * .47f; break;
                        case ChoreoPoint.UpperArmL: point = (a.Limbs[0].position + a.Forearms[0].position) * .5f; break;
                        case ChoreoPoint.UpperArmR: point = (a.Limbs[1].position + a.Forearms[1].position) * .5f; break;
                        case ChoreoPoint.ArmpitL: case ChoreoPoint.ArmpitR: point += a.Body.up * .31f; break;
                        case ChoreoPoint.PocketL: case ChoreoPoint.PocketR: point -= a.Body.up * .10f; break;
                        case ChoreoPoint.GroinL: case ChoreoPoint.GroinR: point += -a.Body.up * .16f + a.Body.forward * .1f; break;
                        case ChoreoPoint.ThighL: point = (a.Limbs[2].position + a.Knees[0].position) * .5f; break;
                        case ChoreoPoint.ThighR: point = (a.Limbs[3].position + a.Knees[1].position) * .5f; break;
                    }
                    break;
            }
            point += basis.TransformVector(new Vector3(target.Offset.X, target.Offset.Y, target.Offset.Z)); return true;
        }
    }
}
