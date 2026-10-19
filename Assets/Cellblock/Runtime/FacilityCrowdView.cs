using System.Collections.Generic;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    // People don't walk through each other (Claude, 2026-10-19). The simulation keeps each person's authoritative spot
    // and timing; the renderer steers bodies around one another the way pedestrians do: someone walking toward another
    // person veers early to the side away from them (right when head-on), and bodies that end up closer than shoulder
    // to shoulder ease apart. The offset is small (≤ 0.35 m), smooth, never pushes into a wall, and is left off anyone a
    // scene, a routine pose or the player controls (the player is blocked by bodies in the simulation itself).
    public sealed partial class FacilityView
    {
        const float BodyGap = .46f, AvoidReach = .45f, LookAhead = 2.2f;
        readonly Dictionary<Actor, Vector3> crowdOffsets = new Dictionary<Actor, Vector3>();
        readonly List<Actor> crowdActors = new List<Actor>();
        int crowdFrame = -1;
#if UNITY_EDITOR
        public static bool PreviewCrowdAvoidance = true; // crowd review A/B
        public string PreviewOverlapDetail = "";
        // Pairs of free-moving bodies closer than `gap` on screen right now.
        public int PreviewOverlaps(World world, float gap, bool movingOnly = false)
        {
            var free = new List<Actor>();
            foreach (KeyValuePair<string, Actor> pair in actors)
            {
                Actor a = pair.Value; if (a.Root == null || !a.Root.gameObject.activeInHierarchy || a.PoseArms || RoutineOwnsFeet(a)) continue;
                Stride s = StrideFor(a); if (SceneHoldsBody(s, world) || world.IsCharacterControlled(pair.Key)) continue;
                free.Add(a);
            }
            int count = 0; PreviewOverlapDetail = "";
            for (int i = 0; i < free.Count; i++) for (int j = i + 1; j < free.Count; j++)
            {
                Vector3 d = free[i].Root.position - free[j].Root.position; d.y = 0;
                if (d.magnitude >= gap) continue;
                if (movingOnly && (StrideFor(free[i]).TargetPace < .3f || StrideFor(free[j]).TargetPace < .3f)) continue; // walking in the simulation, not just easing apart
                count++;
                if (count <= 6)
                {
                    Vector3 oi, oj; crowdOffsets.TryGetValue(free[i], out oi); crowdOffsets.TryGetValue(free[j], out oj);
                    PreviewOverlapDetail += free[i].Root.name + " / " + free[j].Root.name + " " + d.magnitude.ToString("0.00") + " m, speeds " + free[i].Speed.ToString("0.0") + "/" + free[j].Speed.ToString("0.0") +
                        ", offsets " + oi.magnitude.ToString("0.00") + "/" + oj.magnitude.ToString("0.00") + "; ";
                }
            }
            return count;
        }
#endif

        // Following someone the same way: hold back (up to 0.6 m behind the simulation's spot) rather than walk into
        // their back; catch up once the way is clear. Returns a speed cap for this frame, or infinity.
        float FollowCap(Actor actor, Vector3 simTarget)
        {
            if (actor.Speed < .3f) return float.PositiveInfinity;
            Vector3 me = actor.Root.position, forward = actor.Root.forward; forward.y = 0;
            Vector3 lag = simTarget - me; lag.y = 0;
            if (lag.magnitude > .6f) return float.PositiveInfinity; // too far behind: catch up regardless
            foreach (Actor other in crowdActors)
            {
                if (other == actor) continue;
                Vector3 d = other.Root.position - me; d.y = 0; float distance = d.magnitude;
                if (distance > BodyGap * 1.4f || distance < .001f || Vector3.Dot(forward, d / distance) < .7f) continue;
                Vector3 theirs = other.Root.forward; theirs.y = 0;
                if (Vector3.Dot(theirs, forward) < .3f) continue; // not going our way (oncoming is steered instead)
                return Mathf.Max(0, other.Speed * (distance - BodyGap * .8f) / (BodyGap * .6f));
            }
            return float.PositiveInfinity;
        }

        // Contact resolution after this body moved: out of anyone it now overlaps — half the overlap when both are free
        // (the other takes the rest when it moves), all of it against someone a scene or pose holds still. Never through a
        // wall. Applied to the rendered root only; the simulation's spot is untouched.
        readonly HashSet<Actor> crowdHeld = new HashSet<Actor>(); int crowdHeldFrame = -1;
        void SeparateBodies(Actor actor, World world, bool exempt)
        {
            if (crowdHeldFrame != Time.frameCount) { crowdHeldFrame = Time.frameCount; crowdHeld.Clear(); }
#if UNITY_EDITOR
            if (!PreviewCrowdAvoidance) return;
#endif
            if (exempt) { crowdHeld.Add(actor); return; }
            if (crowdActors.Count == 0) return;
            Vector3 me = actor.Root.position, push = Vector3.zero;
            foreach (Actor other in crowdActors)
            {
                if (other == actor) continue;
                Vector3 d = me - other.Root.position; d.y = 0; float distance = d.magnitude;
                if (distance >= World.PersonalSpace) continue;
                Vector3 away = distance > .005f ? d / distance : Quaternion.Euler(0, (actor.GetHashCode() & 0x7fffffff) % 360, 0) * Vector3.forward;
                push += away * (World.PersonalSpace - distance) * (crowdHeld.Contains(other) ? 1 : .5f);
            }
            if (push.sqrMagnitude < 1e-8f) return;
            push = Vector3.ClampMagnitude(push, .25f);
            Point from = new Point(me.x, me.z), to = new Point(me.x + push.x, me.z + push.z);
            if (world.Routes.Clear(from, to)) actor.Root.position = me + push;
            else if (world.Routes.Clear(from, new Point(me.x + push.x, me.z))) actor.Root.position = me + new Vector3(push.x, 0, 0);
            else if (world.Routes.Clear(from, new Point(me.x, me.z + push.z))) actor.Root.position = me + new Vector3(0, 0, push.z);
        }

        Vector3 CrowdOffset(Actor actor, Resident resident, World world, float dt, bool exempt)
        {
            Vector3 current; crowdOffsets.TryGetValue(actor, out current);
            Vector3 desired = Vector3.zero;
#if UNITY_EDITOR
            if (!PreviewCrowdAvoidance) exempt = true;
#endif
            if (crowdFrame != Time.frameCount)
            {
                crowdFrame = Time.frameCount; crowdActors.Clear();
                foreach (Actor a in actors.Values) if (a.Root != null && a.Root.gameObject.activeInHierarchy) crowdActors.Add(a);
            }
            if (!exempt)
            {
                Vector3 me = actor.Root.position, forward = actor.Root.forward; forward.y = 0;
                bool walking = actor.Speed > .3f;
                float ahead = Mathf.Max(LookAhead, actor.Speed * 1.2f); // about 1.2 s to react, at any game speed
                foreach (Actor other in crowdActors)
                {
                    if (other == actor) continue;
                    Vector3 d = other.Root.position - me; d.y = 0;
                    float distance = d.magnitude;
                    if (distance > ahead) continue;
                    // Two bodies on the very same spot still part: a direction fixed by who they are.
                    Vector3 direction = d / Mathf.Max(distance, .0001f);
                    if (distance <= .005f)
                    {
                        int low = Mathf.Min(actor.GetHashCode(), other.GetHashCode()), high = Mathf.Max(actor.GetHashCode(), other.GetHashCode());
                        direction = Quaternion.Euler(0, ((low * 31 + high) & 0x7fffffff) % 360, 0) * Vector3.forward;
                        if (actor.GetHashCode() == low) direction = -direction;
                    }
                    // Too close: ease apart (each side takes half).
                    if (distance < BodyGap) desired -= direction * (BodyGap - distance) * .5f;
                    // Walking toward someone: veer before reaching them, away from the side they're on.
                    if (walking && Vector3.Dot(forward, direction) > .5f)
                    {
                        Vector3 right = new Vector3(forward.z, 0, -forward.x);
                        float lateral = Vector3.Dot(d, right);
                        if (Mathf.Abs(lateral) < BodyGap)
                        {
                            float away = lateral > .02f ? -1 : 1; // head-on or slightly left: pass on the right
                            desired += right * away * (BodyGap - Mathf.Abs(lateral)) * (1 - distance / ahead);
                        }
                    }
                }
                desired = Vector3.ClampMagnitude(desired, AvoidReach);
                // Never into a wall or through a doorway jamb.
                Point at = resident.Position;
                if (!world.Routes.Clear(at, new Point(at.X + desired.x, at.Y + desired.z)))
                    desired = world.Routes.Clear(at, new Point(at.X + desired.x * .5f, at.Y + desired.z * .5f)) ? desired * .5f : Vector3.zero;
            }
            current = Vector3.Lerp(current, desired, 1 - Mathf.Exp(-dt * 8));
            if (current.sqrMagnitude < 1e-6f && desired == Vector3.zero) { crowdOffsets.Remove(actor); return Vector3.zero; }
            crowdOffsets[actor] = current;
            return current;
        }
    }
}
