#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    // Editor-only instrumentation of the real rendered rig. No target is written into the simulation,
    // and these methods, samples and ownership overrides are absent from player builds.
    public sealed partial class FacilityView
    {
        [Serializable] public sealed class NaturalMotionFootSample
        {
            public bool Active, Planted;
            public Vector3 Position, Anchor, Desired;
            public float Phase, Tilt;
        }
        [Serializable] public sealed class NaturalMotionSample
        {
            public string Id;
            public Vector3 Position, Head;
            public Quaternion Rotation;
            public float Pace, Speed, Gait, Clock;
            public bool Finite;
            public NaturalMotionFootSample[] Feet;
            public Vector3[] JointPositions;
            public Quaternion[] JointRotations;
        }

        struct NaturalMotionProbeSettings { public float WalkSpeed; public int Rate; }
        readonly Dictionary<Actor, NaturalMotionProbeSettings> naturalMotionProbes = new Dictionary<Actor, NaturalMotionProbeSettings>();
        bool NaturalMotionProbeOwns(Actor actor) { return naturalMotionProbes.ContainsKey(actor); }

        public void ConfigureNaturalMotionProbe(string id, float walkSpeed = 1, int rate = 1)
        {
            Actor actor;
            if (!actors.TryGetValue(id, out actor)) throw new InvalidOperationException("The motion probe requires an actual generated person: " + id);
            if (float.IsNaN(walkSpeed) || float.IsInfinity(walkSpeed) || walkSpeed <= 0) throw new ArgumentOutOfRangeException("walkSpeed");
            if (rate != 0 && rate != 1 && rate != 2 && rate != 4) throw new ArgumentOutOfRangeException("rate");
            naturalMotionProbes[actor] = new NaturalMotionProbeSettings { WalkSpeed = walkSpeed, Rate = rate };
        }

        public NaturalMotionSample ProbeNaturalMotion(string id, Point target, float dt, bool snap = false)
        {
            Actor actor; NaturalMotionProbeSettings settings;
            if (presentedWorld == null || presentedWorld.Data.Speed != 0) throw new InvalidOperationException("Natural motion probes require a paused disposable company.");
            if (!actors.TryGetValue(id, out actor) || !naturalMotionProbes.TryGetValue(actor, out settings)) throw new InvalidOperationException("Configure the actual actor before probing: " + id);
            if (dt <= 0 || dt > .1f || !FiniteNumber(dt)) throw new ArgumentOutOfRangeException("dt");
            actor.Motion.Id = id; actor.Motion.Position = target; actor.Motion.WalkSpeed = settings.WalkSpeed;
            actor.PoseArms = false; actor.Model.localPosition = Vector3.zero; actor.Model.localRotation = Quaternion.identity;
            // Exercise the real rate-dependent render path synchronously. No core clock advances,
            // and the disposable company is paused again before control returns to Unity's loop.
            try { presentedWorld.Data.Speed = settings.Rate; MoveActor(actor, actor.Motion, presentedWorld, snap, dt); }
            finally { presentedWorld.Data.Speed = 0; }
            actor.Root.gameObject.SetActive(true);
            return SampleNaturalMotion(actor, id);
        }

        public NaturalMotionSample ReadNaturalMotionProbe(string id)
        {
            Actor actor;
            if (!actors.TryGetValue(id, out actor)) throw new InvalidOperationException("No generated actor: " + id);
            return SampleNaturalMotion(actor, id);
        }

        NaturalMotionSample SampleNaturalMotion(Actor actor, string id)
        {
            Stride stride = StrideFor(actor);
            var sample = new NaturalMotionSample { Id = id, Position = actor.Root.position, Head = actor.Head == null ? actor.Root.position + Vector3.up * 1.6f : actor.Head.position,
                Rotation = actor.Root.rotation, Pace = stride.Pace, Speed = actor.Speed, Gait = actor.Gait, Clock = stride.Clock, Finite = true, Feet = new NaturalMotionFootSample[2] };
            Transform[] joints = actor.Root.GetComponentsInChildren<Transform>(true);
            sample.JointPositions = new Vector3[joints.Length]; sample.JointRotations = new Quaternion[joints.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                Transform joint = joints[i]; sample.JointPositions[i] = joint.position; sample.JointRotations[i] = joint.rotation;
                if (!FiniteVector(joint.position) || !FiniteRotation(joint.rotation) || !FiniteVector(joint.localScale)) sample.Finite = false;
            }
            sample.Finite &= FiniteNumber(stride.Pace) && FiniteNumber(actor.Speed) && FiniteNumber(actor.Gait) && FiniteNumber(stride.Clock);
            for (int leg = 0; leg < 2; leg++)
            {
                var contact = stride.Feet[leg]; Transform ankle = actor.Feet[leg];
                sample.Feet[leg] = new NaturalMotionFootSample { Active = contact.Active, Planted = contact.Planted, Anchor = contact.Anchor,
                    Desired = contact.Desired, Position = ankle.position, Phase = contact.LastPhase, Tilt = Vector3.Angle(ankle.up, Vector3.up) };
                sample.Finite &= FiniteVector(contact.Anchor) && FiniteVector(contact.Desired) && FiniteVector(ankle.position);
            }
            return sample;
        }

        public void ReleaseNaturalMotionProbes() { naturalMotionProbes.Clear(); }
        static bool FiniteNumber(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static bool FiniteVector(Vector3 value) { return FiniteNumber(value.x) && FiniteNumber(value.y) && FiniteNumber(value.z); }
        static bool FiniteRotation(Quaternion value) { return FiniteNumber(value.x) && FiniteNumber(value.y) && FiniteNumber(value.z) && FiniteNumber(value.w); }
    }
}
#endif
