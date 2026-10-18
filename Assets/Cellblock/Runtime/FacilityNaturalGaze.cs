using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        void AnimateNaturalGaze(Actor actor, Resident resident, World world, Stride stride, bool moving, float dt)
        {
            if (actor.Head == null || actor.PoseArms || RoutineOwnsFeet(actor)) return;
            float yaw = 0, pitch = 0;
            if (moving)
            {
                Vector3 direction = V(resident.Position) - actor.Root.position; direction.y = 0;
                if (actor.RenderRoute != null && actor.RouteIndex + 1 < actor.RenderRoute.Count)
                    direction = V(actor.RenderRoute[actor.RouteIndex + 1]) - actor.Root.position;
                if (direction.sqrMagnitude > .02f)
                    yaw = Mathf.Clamp(Mathf.DeltaAngle(actor.Root.eulerAngles.y, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg), -32, 32);
                pitch = -stride.Lean * .55f;
            }
            else
            {
                // A brief occasional glance followed by a settled hold, rather than an
                // endless sine-wave head wag. Identity only changes timing and amplitude.
                int seed = IdentitySeed(stride.Id);
                float period = 8 + seed % 5, clock = Mathf.Repeat(stride.Clock + seed % 17, period);
                float glance = Smooth01(clock / .65f) * (1 - Smooth01((clock - 1.25f) / .8f));
                yaw = (seed % 2 == 0 ? -1 : 1) * (8 + seed % 9) * glance;
                pitch = Mathf.Sin(stride.Clock * .8f + seed % 11) * .7f;
            }
            stride.HeadYaw = Spring(stride.HeadYaw, ref stride.HeadYawVelocity, yaw, 2.4f, dt);
            Quaternion target = Quaternion.Euler(pitch, stride.HeadYaw, -stride.Bank * .55f);
            actor.Head.localRotation = Quaternion.Slerp(actor.Head.localRotation, target, 1 - Mathf.Exp(-dt * 10));
        }
    }
}
