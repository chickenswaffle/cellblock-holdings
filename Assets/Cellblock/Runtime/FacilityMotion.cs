using System.Collections.Generic;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    // Procedural locomotion shared by everyone on the map. A gait cycle driven by distance travelled blends a walk into a run
    // and a sprint: two-bone legs plant a stance foot that travels back at exactly the body's speed and lift the swing foot,
    // arms counter-swing (pumping with bent elbows at a sprint), the torso leans into speed and acceleration and banks into
    // turns, the pelvis rides the stride, and the head steadies itself against all of it. The game clock speeds cadence up;
    // it never turns a walk into a sprint. Claude's (escape overhaul, user request 2026-10-17).
    public sealed partial class FacilityView
    {
        enum GaitStyle { Auto, Sneak, Run, Sprint, Winded, Cuffed, Escort }
        sealed class Stride
        {
            public string Id = ""; public bool Resolved;
            public GaitStyle Style;
            public float Pace, Run, Sprint, Ground, Crouch, Lean, LeanVelocity, Bank, BankVelocity, LastYaw, LastIntent, PelvisBase = HipHeight;
            public Vector3 LastTarget; public float LastTargetTime = -1, TargetPace;
            public readonly FootContact[] Feet = { new FootContact(), new FootContact() };
            public int ContactRevision = -1;
            public float IdleTime, SpeedVelocity, HeadingVelocity, Clock, Step, Duty, HeadYaw, HeadYawVelocity;
            public Point StandAt; public float StandY = RoomStand; public int StandRevision = -1;
        }
        readonly Dictionary<Actor, Stride> strides = new Dictionary<Actor, Stride>();
        readonly Dictionary<string, StaffMember> strideStaff = new Dictionary<string, StaffMember>();
        int strideStaffFrame = -1;
        const float Thigh = 0.40f, Shin = 0.4775f, HipHeight = 0.90f, RoomStand = 0.14f, SoleRest = 0.0225f;

        // Where a person's root sits so their soles touch what they stand on. Room floors keep the established 0.14 (seats,
        // beds and work poses were tuned to it). Outside, people stood at room-floor height above paving (0.06 to 0.095)
        // and the site's grass (-0.02), floating up to 18 cm; now their soles meet the surface. The site surfaces mirror
        // FacilityView.Landscape. Cached per person and refreshed after 0.2 m of travel or a geometry change.
        float StandHeight(Actor actor, Point p, World world)
        {
            Stride stride = StrideFor(actor);
            if (stride.StandRevision == world.GeometryRevision && Geometry.Distance(stride.StandAt, p) < 0.2f) return stride.StandY;
            stride.StandRevision = world.GeometryRevision; stride.StandAt = p;
            stride.StandY = StandHeight(world, p);
            return stride.StandY;
        }
        public static float StandHeight(World world, Point p)
        {
            foreach (List<Point> room in world.Rooms) if (Geometry.Contains(room, p)) return RoomStand;
            return OutdoorSurface(p) - SoleRest + 0.004f;
        }
        static float OutdoorSurface(Point p)
        {
            float x = p.X, z = p.Y, roadZ = -Geometry.SiteY - 4.8f;
            if (Within(x, z, -2, -10, 3.4f, 1.75f)) return 0.095f;                         // entrance walk
            if (Within(x, z, -6, 0, 9.3f, 9.3f)) return 0.08f;                               // building apron
            if (Within(x, z, -16.6f, -19.2f, 11.5f, 4.4f) || Within(x, z, -14.1f, -14, 13.9f, 1.25f)) return 0.067f;   // staff apron, shuttle aisle
            if (Within(x, z, -24.5f, -9.6f, 4.5f, 6.5f) || Within(x, z, -18.2f, -10.6f, 1.55f, 8.45f) || Within(x, z, -10.1f, -18.1f, 9.7f, 1.55f)) return 0.065f;   // parking
            if (Within(x, z, -2, (roadZ - 8.7f) / 2, 2.9f, Mathf.Abs(-8.7f - roadZ) / 2)) return 0.06f;   // driveway
            if (Mathf.Abs(x) <= Geometry.SiteX && Mathf.Abs(z) <= Geometry.SiteY) return -0.02f;   // buildable ground
            return -0.10f;                                                                    // meadow beyond the property
        }
        static bool Within(float x, float z, float cx, float cz, float hx, float hz) { return Mathf.Abs(x - cx) <= hx && Mathf.Abs(z - cz) <= hz; }

        Stride StrideFor(Actor actor)
        {
            Stride stride;
            if (!strides.TryGetValue(actor, out stride)) { stride = new Stride(); strides.Add(actor, stride); }
            if (!stride.Resolved)
            {
                foreach (KeyValuePair<string, Actor> pair in actors) if (pair.Value == actor) { stride.Id = pair.Key; break; }
                stride.Resolved = stride.Id != "";
            }
            return stride;
        }

        // How fast the simulation is actually moving this person (m/s), measured from the target's own steps, so a sprinting
        // escapee or a running responder is never held back to walking pace by the renderer.
        float TrackedPace(Actor actor, Vector3 target)
        {
            Stride stride = StrideFor(actor); float now = Time.unscaledTime;
            if (stride.LastTargetTime < 0) { stride.LastTarget = target; stride.LastTargetTime = now; return 0; }
            float dx = target.x - stride.LastTarget.x, dz = target.z - stride.LastTarget.z, moved = Mathf.Sqrt(dx * dx + dz * dz);
            if (moved > 0.0005f)
            {
                float sample = moved / Mathf.Max(0.05f, now - stride.LastTargetTime);
                stride.TargetPace = stride.TargetPace <= 0 ? sample : Mathf.Lerp(stride.TargetPace, sample, 0.5f);
                stride.LastTarget = target; stride.LastTargetTime = now;
            }
            else if (now - stride.LastTargetTime > 0.6f) stride.TargetPace = Mathf.MoveTowards(stride.TargetPace, 0, Time.unscaledDeltaTime * 4);
            return stride.TargetPace * 1.12f;
        }

        GaitStyle StyleFor(Stride stride, Resident resident, World world)
        {
            if (!stride.Resolved) return GaitStyle.Auto;
            if (strideStaffFrame != Time.frameCount)
            {
                strideStaffFrame = Time.frameCount; strideStaff.Clear();
                foreach (StaffMember member in world.Data.Staff) if (!strideStaff.ContainsKey(member.Id)) strideStaff.Add(member.Id, member);
            }
            StaffMember staff;
            if (strideStaff.TryGetValue(stride.Id, out staff))
            {
                string duty = staff.Duty ?? "";
                if (duty == "Responding to escape" || duty == "Responding to fight" || duty == "Responding to alarm") return GaitStyle.Run;
                if (duty.StartsWith("Escorting")) return GaitStyle.Escort;
                return GaitStyle.Auto;
            }
            if (world.Escape.Phase != "None" && world.Escape.ResidentId == stride.Id)
                return !world.Escape.Detected ? GaitStyle.Sneak : world.Struggle.Resident == stride.Id && world.Struggle.Stamina < 30 ? GaitStyle.Winded : GaitStyle.Sprint;
            if (resident != null && resident.Legal != null && resident.Legal.Custody == "Escorted" && world.Struggle.Escorted == stride.Id) return GaitStyle.Cuffed;
            return GaitStyle.Auto;
        }

        static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
        // Critically damped spring toward a target (frequency in Hz): no overshoot for posture, frame-rate independent.
        static float Spring(float value, ref float velocity, float target, float hertz, float dt)
        {
            float omega = 2 * Mathf.PI * hertz, x = omega * dt, decay = 1 / (1 + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = value - target, temp = (velocity + omega * change) * dt;
            velocity = (velocity - omega * temp) * decay;
            return target + (change + temp) * decay;
        }

        // Replaces the old sine shuffle at the end of MoveActor: posture and limbs for whatever the root just did this frame.
        void Locomote(Actor actor, Resident resident, World world, Vector3 previous, float dt)
        {
            Stride stride = StrideFor(actor);
            TrackedPace(actor, V(resident.Position, 0.14f));
            Vector3 movement = actor.Root.position - previous; movement.y = 0;
            float distance = movement.magnitude, speed = dt > 0 ? distance / dt : 0, rate = Mathf.Max(1, world.Data.Speed);
            bool moving = speed > 0.035f;
            if (world.Data.Speed > 0 || moving) stride.Clock += dt * Mathf.Sqrt(rate);
            AnimateFaceMotion(actor, stride);
            // Speed as the person would cover it at 1x: the game clock raises cadence, it does not change the kind of gait.
            float intent = speed / rate;
            stride.Pace = Mathf.Lerp(stride.Pace, intent, 1 - Mathf.Exp(-dt * 6));
            GaitStyle style = StyleFor(stride, resident, world); stride.Style = style;
            float runTarget = style == GaitStyle.Run || style == GaitStyle.Sprint ? 1 : style == GaitStyle.Winded ? 0.75f :
                style == GaitStyle.Sneak || style == GaitStyle.Cuffed || style == GaitStyle.Escort ? 0 : Smooth01((stride.Pace - 2.3f) / 0.9f);
            float sprintTarget = style == GaitStyle.Sprint ? 1 : style == GaitStyle.Run ? Smooth01((stride.Pace - 3.0f) / 0.8f) : style == GaitStyle.Auto ? Smooth01((stride.Pace - 3.4f) / 0.9f) : 0;
            if (!moving) { runTarget = 0; sprintTarget = 0; }
            stride.Run = Mathf.MoveTowards(stride.Run, runTarget, dt * 3.5f);
            stride.Sprint = Mathf.MoveTowards(stride.Sprint, sprintTarget, dt * 3);
            float run = stride.Run, sprint = stride.Sprint * run;
            float strength = moving ? Mathf.Clamp01(intent / 0.8f) : 0;
            stride.Ground = Mathf.MoveTowards(stride.Ground, strength, dt * (moving ? 6 : 3.5f));
            float g = stride.Ground;

            // The escape scene poses the whole body of anyone it holds this frame (FacilityEscapeView.cs).
            if (SceneHoldsBody(stride, world))
            {
                ResetFootContacts(actor);
                foreach (Transform ankle in actor.Feet) ankle.localRotation = Quaternion.identity;
                stride.LastYaw = actor.Root.eulerAngles.y; stride.PelvisBase = actor.Body.localPosition.y; return;
            }
            // Heading: runners commit to turns faster; the yaw rate feeds the bank.
            if (moving)
            {
                float desiredYaw = Mathf.Atan2(movement.x, movement.z) * Mathf.Rad2Deg;
                float heading = Mathf.SmoothDampAngle(actor.Root.eulerAngles.y, desiredYaw, ref stride.HeadingVelocity,
                    Mathf.Lerp(.16f, .10f, run) / rate, Mathf.Lerp(200, 420, run) * rate, dt);
                actor.Root.rotation = Quaternion.Euler(0, heading, 0);
            }
            float yaw = actor.Root.eulerAngles.y, yawRate = dt > 0 ? Mathf.DeltaAngle(stride.LastYaw, yaw) / dt : 0; stride.LastYaw = yaw;

            // Distance drives stride phase; world contact anchors below enforce stance through turns.
            float walkStep = Mathf.Lerp(0.56f, 0.70f, Mathf.Clamp01((stride.Pace - 0.9f) / 1.1f)), runStep = Mathf.Lerp(0.95f, 1.30f, sprint);
            float step = Mathf.Lerp(walkStep, runStep, run) * (.95f + IdentitySeed(stride.Id) % 7 * .016f) * (style == GaitStyle.Sneak ? 0.85f : style == GaitStyle.Cuffed ? 0.8f : 1);
            float duty = Mathf.Lerp(.60f, .36f, run);
            // A contact must fit the rig's front/back reach. Long sprint strides with a
            // capped reach otherwise demand a second recovery step before every toe-off.
            step = Mathf.Min(step, Mathf.Lerp(.31f, .36f, run) / duty);
            float reach = duty * step * g;
            if (moving) actor.Gait += distance / step * Mathf.PI;
            stride.Step = step; stride.Duty = duty;
            float lift = Mathf.Lerp(0.075f, Mathf.Lerp(0.20f, 0.32f, sprint), run) * g;
            float cycle = actor.Gait / (2 * Mathf.PI); cycle -= Mathf.Floor(cycle);
            // The pelvis rides the stride: highest over the planted foot when walking, lowest there (compressed) when running.
            float wave = Mathf.Cos(4 * Mathf.PI * (cycle - duty * 0.5f));
            float bob = Mathf.Lerp((wave - 1) * 0.5f * 0.035f, (-wave - 1) * 0.5f * Mathf.Lerp(0.045f, 0.06f, sprint), run) * g;
            float crouchTarget = (Mathf.Lerp(0.012f, Mathf.Lerp(0.045f, 0.065f, sprint), run) + (style == GaitStyle.Sneak ? 0.05f : style == GaitStyle.Winded ? 0.03f : 0)) * g;
            stride.Crouch = Mathf.MoveTowards(stride.Crouch, crouchTarget, dt * 0.4f);
            // Rise smoothly back to a standing pelvis after a kneel or a fall that some scene held; the stride bob rides on top.
            stride.PelvisBase = Mathf.Lerp(stride.PelvisBase, HipHeight - stride.Crouch, 1 - Mathf.Exp(-dt * 6));
            float pelvis = stride.PelvisBase + bob;
            bool contacts = !RoutineOwnsFeet(actor);
            if (contacts) pelvis = PlanFootContacts(actor, stride, world, cycle, duty, reach, lift, movement / Mathf.Max(.001f, dt), pelvis, moving, dt);
            else ResetFootContacts(actor);
            // A little side-to-side transfer over the supporting leg adds weight without root jitter.
            float transfer = Mathf.Sin(cycle * Mathf.PI * 2) * .012f * g * (1 - run * .6f);
            actor.Body.localPosition = new Vector3(transfer, pelvis, 0);
            float blend = 1 - Mathf.Exp(-dt * 20);
            float[] swing = legSwing;
            for (int leg = 0; leg < 2; leg++)
            {
                float u = Mathf.Repeat(cycle + leg * .5f, 1);
                float x = u < duty ? reach * (1 - 2 * u / duty) : Mathf.Lerp(-reach, reach, ContactEase((u - duty) / (1 - duty)));
                swing[leg] = reach > .001f ? x / reach : 0;
                if (contacts) SolveContactLeg(actor, stride, leg, pelvis);
                else
                {
                    Transform hip = actor.Limbs[2 + leg]; hip.localPosition = new Vector3(hip.localPosition.x, pelvis, 0);
                    float hipPitch, kneeBend; LegReach(0, -(pelvis - SoleRest), out hipPitch, out kneeBend);
                    hip.localRotation = Quaternion.Slerp(hip.localRotation, Quaternion.Euler(hipPitch, 0, 0), blend);
                    actor.Knees[leg].localRotation = Quaternion.Slerp(actor.Knees[leg].localRotation, Quaternion.Euler(kneeBend, 0, 0), blend);
                    actor.Feet[leg].localRotation = Quaternion.identity;
                }
            }

            // Lean into speed and acceleration (a spring, so starts tip forward and stops rock back), bank into turns.
            float accel = dt > 0 ? (stride.Pace - stride.LastIntent) / dt : 0; stride.LastIntent = stride.Pace;
            float leanTarget = Mathf.Lerp(2, Mathf.Lerp(9, 17, sprint), run) * g + Mathf.Clamp(accel * 1.6f, -6, 7) +
                (style == GaitStyle.Sneak ? 10 : style == GaitStyle.Winded ? 20 : style == GaitStyle.Cuffed ? 8 : 0);
            stride.Lean = Spring(stride.Lean, ref stride.LeanVelocity, leanTarget, 2.2f, dt);
            float bankTarget = Mathf.Clamp(-(yawRate / rate) * Mathf.Min(speed / rate, 6) * 0.012f, -11, 11) * (moving ? 1 : 0);
            stride.Bank = Spring(stride.Bank, ref stride.BankVelocity, bankTarget, 1.8f, dt);
            float twist = -swing[0] * Mathf.Lerp(4, 9, run) * g;
            if (!actor.PoseArms && style != GaitStyle.Escort)
            {
                float breathe = moving ? 0 : Mathf.Sin(stride.Clock * 1.6f + IdentitySeed(stride.Id) % 19) * 0.6f;
                actor.Body.localRotation = Quaternion.Slerp(actor.Body.localRotation, Quaternion.Euler(stride.Lean + breathe, twist, stride.Bank), blend);
                float armAmplitude = Mathf.Lerp(17, Mathf.Lerp(36, 55, sprint), run) * g * (style == GaitStyle.Winded ? 0.6f : 1);
                float armBase = -Mathf.Lerp(0, Mathf.Lerp(8, 14, sprint), run);
                float elbowBase = -Mathf.Lerp(12, Mathf.Lerp(78, 92, sprint), run);
                for (int arm = 0; arm < 2; arm++)
                {
                    if (style == GaitStyle.Cuffed) continue;   // the escort view holds the hands behind the back
                    // Each arm swings with the opposite leg: forward (negative pitch) as that leg reaches forward.
                    float with = swing[1 - arm];
                    float pitch = armBase - with * armAmplitude, elbow = elbowBase - Mathf.Max(0, with) * Mathf.Lerp(8, 22, run) * g;
                    // A slight outward roll keeps the upper arm clear of the shirt as it swings.
                    float roll = (arm == 0 ? -1 : 1) * (4 + run * 3);
                    actor.Limbs[arm].localRotation = Quaternion.Slerp(actor.Limbs[arm].localRotation, Quaternion.Euler(pitch, 0, roll), blend);
                    actor.Forearms[arm].localRotation = Quaternion.Slerp(actor.Forearms[arm].localRotation, Quaternion.Euler(elbow, 0, 0), blend);
                }
            }
            AnimateNaturalGaze(actor, resident, world, stride, moving, dt);

        }
        readonly float[] legSwing = new float[2];
        bool SceneHoldsBody(Stride stride, World world)
        {
            if (!stride.Resolved) return false;
            EscapeStruggle s = world.Struggle; string id = stride.Id;
            if (s.Active && (id == s.Officer || id == s.Helper || id == s.Resident)) return true;
            if (Brawling(world, id)) return true;
            if (id == s.Staggered && s.StaggeredUntil > world.Data.Minute - 2) return true;
            return world.Escape.Phase == "Held at gate" && world.Escape.ResidentId == id;
        }

        // Analytic two-bone leg in the sagittal plane: the hip-to-foot target (forward, down) gives the hip pitch and knee bend
        // in this rig's convention (negative hip pitch swings a leg forward; positive knee bend folds the shin back).
        static void LegReach(float forward, float down, out float hipPitch, out float kneeBend)
        {
            float d = Mathf.Sqrt(forward * forward + down * down);
            d = Mathf.Clamp(d, Mathf.Abs(Thigh - Shin) + 0.01f, Thigh + Shin - 0.0005f);
            float line = Mathf.Atan2(forward, -down);
            float cosHip = Mathf.Clamp((Thigh * Thigh + d * d - Shin * Shin) / (2 * Thigh * d), -1, 1);
            float cosKnee = Mathf.Clamp((Thigh * Thigh + Shin * Shin - d * d) / (2 * Thigh * Shin), -1, 1);
            // The knee sits ahead of the hip-to-foot line, so the thigh points further forward than the line itself.
            hipPitch = -(line + Mathf.Acos(cosHip)) * Mathf.Rad2Deg;
            kneeBend = (Mathf.PI - Mathf.Acos(cosKnee)) * Mathf.Rad2Deg;
        }
    }
}
