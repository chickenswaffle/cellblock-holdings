using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        // Contacts belong to the displayed actor, never to saved navigation or gameplay.
        // A planted ankle is fixed in world space. The two-bone solve follows that point,
        // including through turns, while a separate ankle keeps the boot level.
        sealed class FootContact
        {
            public bool Active, Planted, Correcting, Stopping;
            public Vector3 Anchor, Desired, LiftFrom, Landing;
            public Quaternion Rotation = Quaternion.identity;
            public float LastPhase, Progress, Duration = .22f;
        }
        const float AnkleHeight = .07f, LowerLeg = .43f, ContactReach = Thigh + LowerLeg - .001f;

        void ResetFootContacts(Actor actor)
        {
            Stride stride;
            if (!strides.TryGetValue(actor, out stride)) return;
            foreach (FootContact foot in stride.Feet) { foot.Active = foot.Planted = foot.Correcting = foot.Stopping = false; foot.Progress = 0; }
            stride.SpeedVelocity = 0;
        }

        bool RoutineOwnsFeet(Actor actor)
        {
            Look look;
            return looks.TryGetValue(actor, out look) && look.Pose != LifePose.None && look.Pose != LifePose.Sway;
        }

        float PlanFootContacts(Actor actor, Stride stride, World world, float cycle, float duty, float reach, float lift,
            Vector3 velocity, float pelvis, bool moving, float dt)
        {
            float distanceRate = velocity.magnitude;
            stride.IdleTime = moving ? 0 : stride.IdleTime + dt;
            float recoveryDuration = .22f / Mathf.Sqrt(Mathf.Max(1, world.Data.Speed));
            for (int leg = 0; leg < 2; leg++)
            {
                FootContact foot = stride.Feet[leg];
                float u = Mathf.Repeat(cycle + leg * .5f, 1);
                float side = actor.Limbs[2 + leg].localPosition.x;
                Vector3 neutral = FootSurface(actor, world, actor.Model.TransformPoint(new Vector3(side, AnkleHeight, 0)));
                if (!foot.Active)
                {
                    foot.Active = foot.Planted = true; foot.Correcting = foot.Stopping = false;
                    foot.Anchor = foot.Desired = neutral; foot.Rotation = actor.Root.rotation;
                    foot.LastPhase = u;
                }
                bool stance = u < duty;
                bool wasStance = foot.LastPhase < duty;
                if (moving)
                {
                    if (foot.Stopping)
                    {
                        foot.Stopping = false; foot.Correcting = true; foot.Progress = 0; foot.LiftFrom = foot.Desired;
                        foot.Duration = recoveryDuration;
                        foot.Landing = FootSurface(actor, world, neutral + actor.Root.forward * .12f + velocity * foot.Duration);
                    }
                    if (foot.Correcting)
                    {
                        foot.Progress = Mathf.Min(1, foot.Progress + dt / foot.Duration);
                        SwingFoot(foot, foot.Progress, lift * .65f);
                        if (foot.Progress >= 1) PlantFoot(foot, actor.Root.rotation);
                    }
                    else if (stance)
                    {
                        if (!wasStance && !foot.Planted) PlantFoot(foot, actor.Root.rotation);
                        foot.Desired = foot.Anchor;
                    }
                    else
                    {
                        float s = (u - duty) / (1 - duty);
                        // Predict the touchdown from the remaining root travel, then stop
                        // adjusting in the final third so a landing never chases the ground.
                        float remaining = distanceRate > .001f ? 2 * stride.Step * (1 - u) / distanceRate : 0;
                        Vector3 landing = FootSurface(actor, world, actor.Model.TransformPoint(new Vector3(side, AnkleHeight, reach)) + velocity * Mathf.Min(.5f, remaining));
                        if (foot.Planted) { foot.Planted = false; foot.LiftFrom = foot.Desired; foot.Landing = landing; }
                        if (s < .65f) foot.Landing = Vector3.Lerp(foot.Landing, landing, 1 - Mathf.Exp(-dt * 12));
                        foot.Rotation = Quaternion.Slerp(foot.Rotation, actor.Root.rotation, 1 - Mathf.Exp(-dt * 8));
                        SwingFoot(foot, s, lift);
                    }
                }
                else
                {
                    // Once stopped, take alternating small settling steps. Retaining an
                    // old split stance indefinitely leaves the pelvis in a permanent squat.
                    if (foot.Planted && stride.IdleTime > .25f && stride.Feet[1 - leg].Planted &&
                        Vector2.Distance(new Vector2(foot.Desired.x, foot.Desired.z), new Vector2(neutral.x, neutral.z)) > .065f)
                    {
                        foot.Planted = false; foot.Stopping = true; foot.Correcting = false; foot.Progress = 0;
                        foot.LiftFrom = foot.Desired; foot.Landing = neutral;
                    }
                    if (!foot.Planted)
                    {
                    // Finish the airborne step on stopping. Never freeze a raised boot or
                    // collapse both legs toward a neutral pose in the middle of a stride.
                    if (!foot.Stopping)
                    {
                        foot.Stopping = true; foot.Correcting = false; foot.Progress = 0;
                        foot.LiftFrom = foot.Desired;
                        Vector3 local = actor.Model.InverseTransformPoint(foot.Landing);
                        local.x = side; local.y = AnkleHeight; local.z = Mathf.Clamp(local.z, -.22f, .22f);
                        foot.Landing = FootSurface(actor, world, actor.Model.TransformPoint(local));
                    }
                    foot.Progress = Mathf.Min(1, foot.Progress + dt / (.18f / Mathf.Sqrt(Mathf.Max(1, world.Data.Speed))));
                    SwingFoot(foot, foot.Progress, .025f);
                    if (foot.Progress >= 1) { PlantFoot(foot, actor.Root.rotation); foot.Stopping = false; }
                    }
                }
                // Abrupt reversals need a small recovery step, not a stretched/crossed leg.
                Vector3 relative = actor.Model.InverseTransformPoint(foot.Desired);
                Vector2 horizontal = new Vector2(relative.x - side, relative.z);
                if (moving && foot.Planted && (horizontal.magnitude > .42f || Mathf.Abs(relative.x - side) > .23f))
                {
                    foot.Planted = false; foot.Correcting = true; foot.Progress = 0; foot.LiftFrom = foot.Desired;
                    foot.Duration = recoveryDuration;
                    // The body continues moving during this corrective step. Aim at its
                    // predicted support, otherwise each landing is already behind it.
                    foot.Landing = FootSurface(actor, world, neutral + actor.Root.forward * .12f + velocity * foot.Duration);
                }
                // Keep the common pelvis within both legs' reach; exact IK must not be
                // followed by joint smoothing, which would break the contact constraint.
                float vertical = Mathf.Sqrt(Mathf.Max(.01f, ContactReach * ContactReach - horizontal.sqrMagnitude));
                pelvis = Mathf.Min(pelvis, relative.y + vertical);
                foot.LastPhase = u;
            }
            return Mathf.Max(.64f, pelvis);
        }

        static Vector3 FootSurface(Actor actor, World world, Vector3 position)
        {
            position.y = StandHeight(world, new Point(position.x, position.z)) + actor.Root.lossyScale.y * AnkleHeight;
            return position;
        }

        static float ContactEase(float s) { s = Mathf.Clamp01(s); return s * s * s * (s * (s * 6 - 15) + 10); }
        static void SwingFoot(FootContact foot, float s, float lift)
        {
            foot.Desired = Vector3.Lerp(foot.LiftFrom, foot.Landing, ContactEase(s));
            float arc = Mathf.Sin(Mathf.Clamp01(s) * Mathf.PI);
            foot.Desired.y += arc * arc * lift;
        }
        static void PlantFoot(FootContact foot, Quaternion heading)
        {
            foot.Anchor = foot.Desired = foot.Landing;
            foot.Planted = true; foot.Correcting = false; foot.Rotation = heading;
        }

        void SolveContactLeg(Actor actor, Stride stride, int leg, float pelvis)
        {
            FootContact foot = stride.Feet[leg]; Transform hip = actor.Limbs[2 + leg], knee = actor.Knees[leg];
            hip.localPosition = new Vector3(hip.localPosition.x, pelvis, 0);
            Vector3 target = actor.Model.InverseTransformPoint(foot.Desired), delta = target - hip.localPosition;
            float distance = Mathf.Clamp(delta.magnitude, .08f, ContactReach);
            Vector3 direction = delta.normalized;
            Vector3 pole = Vector3.ProjectOnPlane(Vector3.forward, direction).normalized;
            float along = (Thigh * Thigh + distance * distance - LowerLeg * LowerLeg) / (2 * distance);
            float bend = Mathf.Sqrt(Mathf.Max(0, Thigh * Thigh - along * along));
            Vector3 kneeDirection = direction * along + pole * bend;
            hip.localRotation = Quaternion.FromToRotation(Vector3.down, kneeDirection);
            Vector3 actualTarget = hip.localPosition + direction * distance;
            Vector3 shinWorld = actor.Model.TransformVector(actualTarget - hip.localPosition - kneeDirection);
            knee.localRotation = Quaternion.FromToRotation(Vector3.down, hip.InverseTransformVector(shinWorld));
            // Keep stance soles level. Swing gets a restrained heel-up/toe-clearance roll.
            float phase = Mathf.Repeat(foot.LastPhase, 1);
            float toe = foot.Planted || foot.Stopping ? 0 : Mathf.Sin(Mathf.InverseLerp(stride.Duty, 1, phase) * Mathf.PI * 2) * 12;
            actor.Feet[leg].rotation = foot.Rotation * Quaternion.Euler(toe, 0, 0);
        }
    }
}
