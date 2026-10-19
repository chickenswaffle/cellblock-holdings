using System.Collections.Generic;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    // The controlled character responds the frame the stick moves (Claude, 2026-10-19; owner: "moving is still flawed").
    // Core advances the controlled person once per game minute (0.2 s at 1×), so chasing the simulation alone shows
    // input 100–200 ms late and steps between ticks. Instead the body is drawn where the simulation says it is plus
    // where it is heading over the next tick and a bit (Lead), through a critically damped spring: starts and stops get
    // a natural ramp, releasing the stick settles exactly on the simulation's spot, nothing pops. Walls and other bodies
    // bound the lead; escorts, actions with published choreography and anything else that pins the body use the exact
    // simulation position. Core stays authoritative for rules, contacts and saves.
    public sealed partial class FacilityView
    {
        const float Lead = .22f, FollowHertz = 3.2f;
        readonly Dictionary<Actor, Vector3> predictVelocity = new Dictionary<Actor, Vector3>();
#if UNITY_EDITOR
        public static bool PreviewPrediction = true; // move-feel review A/B
#endif

        bool PredictControlled(Actor actor, World world, Vector3 simTarget, float dt)
        {
            Stride stride = StrideFor(actor);
            bool own = stride.Resolved && world.IsCharacterControlled(stride.Id) && !choreoFrames.ContainsKey(stride.Id) && world.HeldResidentId == "";
#if UNITY_EDITOR
            if (!PreviewPrediction) own = false;
#endif
            if (!own) { predictVelocity.Remove(actor); return false; }
            Point v = world.CharacterPredictedVelocity; // metres per game minute
            Vector3 heading = new Vector3(v.X, 0, v.Y) / ActionChoreo.SecondsPerGameMinute;
            Vector3 goal = simTarget + heading * Lead;
            Point from = new Point(simTarget.x, simTarget.z);
            if (!world.Routes.Clear(from, new Point(goal.x, goal.z))) goal = simTarget; // never lead through a wall
            Vector3 position = actor.Root.position, velocity;
            predictVelocity.TryGetValue(actor, out velocity);
            float x = SpringAxis(position.x, ref velocity.x, goal.x, dt), z = SpringAxis(position.z, ref velocity.z, goal.z, dt);
            velocity.y = 0; predictVelocity[actor] = velocity;
            Vector3 next = new Vector3(x, simTarget.y, z);
            if (world.Routes.Clear(new Point(position.x, position.z), new Point(next.x, next.z))) actor.Root.position = next;
            else { actor.Root.position = Vector3.MoveTowards(new Vector3(position.x, simTarget.y, position.z), simTarget, 3.1f * dt * 1.5f); predictVelocity[actor] = Vector3.zero; } // glide back, never pop
            actor.Speed = new Vector2(velocity.x, velocity.z).magnitude;
            return true;
        }
        static float SpringAxis(float value, ref float velocity, float target, float dt) { return Spring(value, ref velocity, target, FollowHertz, dt); }
    }
}
