using System.Collections.Generic;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    // Hits that land (Claude, 2026-10-19; owner: "jabs that do no type of damage to the person on the receiving end").
    // Every published Impact (ActionChoreo, once per id) throws the receiver's upper body along the hit — struck in the
    // face from the front they rock back, from the side they bend away — with the head whipping further than the
    // torso and the knees buckling on strong hits. Peaks in 60 ms, recovers over about half a second; repeated hits
    // stack. Core moves the body on the ground (its bounded impulse); this is the visible reaction above it.
    public sealed partial class FacilityView
    {
        sealed class HitReaction { public Vector3 LocalDirection; public float Strength, Started; public bool Head; }
        readonly Dictionary<Actor, List<HitReaction>> hitReactions = new Dictionary<Actor, List<HitReaction>>();
        readonly HashSet<string> hitsSeen = new HashSet<string>();
        const float HitPeak = .06f, HitLength = .6f;

        void CollectHits(ActionChoreoFrame frame)
        {
            foreach (ChoreoEvent e in frame.Events)
            {
                if (e.Kind != ChoreoEventKind.Impact || string.IsNullOrEmpty(e.Id) || !hitsSeen.Add(e.Id)) continue;
                Actor target = Live(e.PartnerId); if (target == null) continue;
                Vector3 world = new Vector3(e.Direction.X, 0, e.Direction.Z);
                if (world.sqrMagnitude < 1e-6f) world = target.Root.position - (Live(e.ActorId) != null ? Live(e.ActorId).Root.position : target.Root.position - target.Root.forward);
                world.y = 0; if (world.sqrMagnitude < 1e-6f) world = -target.Root.forward;
                List<HitReaction> list; if (!hitReactions.TryGetValue(target, out list)) hitReactions[target] = list = new List<HitReaction>();
                bool head = e.Part == ChoreoPoint.Head || e.Part == ChoreoPoint.Jaw;
                list.Add(new HitReaction { LocalDirection = target.Root.InverseTransformDirection(world.normalized), Strength = Mathf.Clamp(e.Strength, .2f, 1.2f), Started = Time.unscaledTime, Head = head });
            }
            if (hitsSeen.Count > 512) hitsSeen.Clear();
        }

        // Pre-rig, after posture: applied on top of whatever pose the person has this frame.
        void ApplyHitReactions()
        {
            float now = Time.unscaledTime;
            foreach (var pair in hitReactions)
            {
                Actor a = pair.Key; if (a.Root == null) continue;
                float pitch = 0, bank = 0, headPitch = 0, headYaw = 0, dip = 0;
                for (int i = pair.Value.Count - 1; i >= 0; i--)
                {
                    HitReaction h = pair.Value[i]; float t = now - h.Started;
                    if (t > HitLength) { pair.Value.RemoveAt(i); continue; }
                    float amount = (t < HitPeak ? t / HitPeak : Mathf.Exp(-(t - HitPeak) * 6.5f)) * h.Strength;
                    Vector3 d = h.LocalDirection;
                    // Positive torso pitch leans forward in this rig: a hit travelling backward through the body rocks it back.
                    pitch += d.z * 26 * amount; bank += -d.x * 16 * amount;
                    float whip = h.Head ? 1.6f : .8f;
                    headPitch += d.z * 30 * whip * amount; headYaw += d.x * 34 * whip * amount;
                    dip += Mathf.Max(0, h.Strength - .55f) * .09f * amount;
                }
                a.Body.localRotation = a.Body.localRotation * Quaternion.Euler(pitch, 0, bank);
                if (a.Head != null) a.Head.localRotation = a.Head.localRotation * Quaternion.Euler(headPitch, headYaw, 0);
                if (dip > 0) a.Body.localPosition -= new Vector3(0, dip, 0);
            }
        }
    }
}
