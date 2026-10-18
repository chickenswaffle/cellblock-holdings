using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    // Yard-time bodies (Claude, 2026-10-18): what each resident is doing once they reach their yard activity. Laps and
    // pacing walk with the shared gait; this view poses the rest: push-ups, sit-ups, jumping jacks and shadow boxing, a
    // chat circle facing in with the speaker gesturing, leaning on the fence with folded arms, and sitting on the ground.
    // The core decides the activity and the spot; every motion runs on the game's scene clock and freezes when paused.
    public sealed partial class FacilityView
    {
        void RefreshYardScene(World world)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f), blend = 1 - Mathf.Exp(-dt * 10);
            float clock = world.Data.Minute * 0.2f + sceneFraction * 0.2f;
            foreach (Resident r in world.Data.Residents)
            {
                Actor a = Live(r.Id);
                if (a == null || heldThisFrame.Contains(a)) continue;
                YardPlan plan = world.YardActivity(r);
                if (plan == null || plan.Walking) continue;
                if (a.Speed > 0.12f) continue;
                Take(a, Hold.Body); a.PoseArms = true;
                float phase = clock * 6.283f + (a.Gait % 1f) * 6.283f;
                Turn(a, Yaw(V(plan.Focus) - a.Root.position), 240);
                switch (plan.Activity)
                {
                    case "Workout":
                        if (plan.Detail == "Push-ups") PushUp(a, phase, blend);
                        else if (plan.Detail == "Sit-ups") SitUp(a, phase, blend);
                        else if (plan.Detail == "Jumping jacks") JumpingJack(a, phase, blend);
                        else ShadowBox(a, phase, blend);
                        break;
                    case "Hang out": ChatCircle(a, r, world, phase, blend); break;
                    case "Lean": LeanOnFence(a, phase, blend); break;
                    case "Sit": SitOnGround(a, phase, blend); break;
                }
            }
        }
#if UNITY_EDITOR
        // Deliberately staged renderer evidence; it neither assigns an activity nor
        // advances the simulation. Uses the same pose functions as the live yard.
        public bool PreviewYardWorkout(string id, string detail, float phase)
        {
            Actor actor = Live(id); if (actor == null) return false;
            Take(actor, Hold.Body); actor.PoseArms = true;
            if (detail == "Push-ups") PushUp(actor, phase, 1);
            else if (detail == "Sit-ups") SitUp(actor, phase, 1);
            else if (detail == "Jumping jacks") JumpingJack(actor, phase, 1);
            else if (detail == "Shadow boxing") ShadowBox(actor, phase, 1);
            else return false;
            Keep(actor); return true;
        }
#endif
        // Face down on straight arms, the body rising and lowering about twice a second at full speed.
        void PushUp(Actor a, float phase, float blend)
        {
            // An inclined plank: shoulders up on the hands, feet on the ground; the arms straighten as the body presses up.
            float press = 0.5f + 0.5f * Mathf.Sin(phase * 0.35f);
            Lay(a, Mathf.Lerp(80, 72, press), Mathf.Lerp(0.3f, 0.42f, press), Vector3.zero, blend);
            for (int arm = 0; arm < 2; arm++) { Joint(a.Limbs[arm], new Vector3(Mathf.Lerp(-58, -74, press), 0, arm == 0 ? 16 : -16), blend); Joint(a.Forearms[arm], new Vector3(-62 * (1 - press), 0, 0), blend); }
            for (int leg = 0; leg < 2; leg++) { Joint(a.Limbs[2 + leg], new Vector3(0, 0, leg == 0 ? -4 : 4), blend); Joint(a.Knees[leg], Vector3.zero, blend); }
            if (a.Head != null) Joint(a.Head, new Vector3(-20, 0, 0), blend);
        }
        // Sitting with knees up, curling the torso forward and back, arms crossed on the chest.
        void SitUp(Actor a, float phase, float blend)
        {
            float curl = 0.5f + 0.5f * Mathf.Sin(phase * 0.3f);
            GroundSeat(a, 100, blend);
            Torso(a, Mathf.Lerp(-35, 28, curl), 0, 0, blend);
            ArmPose(a, 0, new Vector3(0.10f, 1.20f, 0.16f), blend); ArmPose(a, 1, new Vector3(-0.10f, 1.18f, 0.15f), blend);
        }
        void JumpingJack(Actor a, float phase, float blend)
        {
            float open = 0.5f - 0.5f * Mathf.Cos(phase * 0.45f);
            Offset(a, Vector3.zero, new Vector3(0, Mathf.Abs(Mathf.Sin(phase * 0.45f)) * 0.07f, 0), Quaternion.identity, blend);
            for (int arm = 0; arm < 2; arm++) { Joint(a.Limbs[arm], new Vector3(-8, 0, (arm == 0 ? 1 : -1) * (14 + 150 * open)), blend); Joint(a.Forearms[arm], new Vector3(-6, 0, 0), blend); }
            for (int leg = 0; leg < 2; leg++) { Joint(a.Limbs[2 + leg], new Vector3(0, 0, (leg == 0 ? -1 : 1) * (4 + 16 * open)), blend); Joint(a.Knees[leg], new Vector3(6, 0, 0), blend); }
        }
        // Guard up, feet staggered, alternating jabs with a little bounce.
        void ShadowBox(Actor a, float phase, float blend)
        {
            float bounce = Mathf.Abs(Mathf.Sin(phase * 0.6f)) * 0.03f;
            Stance(a, 0.86f - bounce, 0.18f, -0.14f, blend);
            int punching = Mathf.FloorToInt(phase * 0.25f) % 2;
            float jab = Mathf.Max(0, Mathf.Sin(phase * 0.5f));
            for (int arm = 0; arm < 2; arm++)
            {
                Vector3 guard = new Vector3(arm == 0 ? 0.13f : -0.13f, 1.35f, 0.18f), reach = new Vector3(arm == 0 ? 0.06f : -0.06f, 1.42f, 0.62f);
                ArmPose(a, arm, Vector3.Lerp(guard, reach, arm == punching ? jab : 0), blend);
            }
            Torso(a, 10, (punching == 0 ? -1 : 1) * 14 * jab, 0, blend);
            if (a.Head != null) Joint(a.Head, new Vector3(6, 0, 0), blend);
        }
        // Standing in the circle facing its centre; whoever's turn it is talks with one hand, the others listen.
        void ChatCircle(Actor a, Resident r, World world, float phase, float blend)
        {
            int turn = (world.Data.Minute / 4 + (r.Id.GetHashCode() & 7)) % 3;
            float talk = turn == 0 ? Mathf.Sin(phase * 0.7f) : 0;
            Stance(a, 0.93f, 0.05f, -0.04f, blend);
            Torso(a, 3, Mathf.Sin(phase * 0.13f) * 4, Mathf.Sin(phase * 0.09f) * 3, blend);
            if (turn == 0) { Joint(a.Limbs[1], new Vector3(-38 - talk * 14, 0, -10), blend); Joint(a.Forearms[1], new Vector3(-62 + talk * 18, 0, 0), blend); }
            else { Joint(a.Limbs[1], new Vector3(-4, 0, -6), blend); Joint(a.Forearms[1], new Vector3(-12, 0, 0), blend); }
            // A hand in the pocket (or on the hip) for the other arm.
            Joint(a.Limbs[0], new Vector3(-6, 0, 18), blend); Joint(a.Forearms[0], new Vector3(-48, 0, 0), blend);
            if (a.Head != null) Joint(a.Head, new Vector3(turn == 0 ? -2 : 4, Mathf.Sin(phase * 0.11f) * 18, 0), blend);
        }
        // Back to the fence, leaning on it with folded arms and one foot up against it.
        void LeanOnFence(Actor a, float phase, float blend)
        {
            Stance(a, 0.92f, 0.12f, -0.02f, blend);
            Joint(a.Limbs[3], new Vector3(18, 0, 6), blend); Joint(a.Knees[1], new Vector3(52, 0, 0), blend);
            Torso(a, -9, 0, Mathf.Sin(phase * 0.07f) * 2, blend);
            Offset(a, Vector3.zero, new Vector3(0, 0, -0.12f), Quaternion.Euler(-4, 0, 0), blend);
            ArmPose(a, 0, new Vector3(0.11f, 1.24f, 0.16f), blend); ArmPose(a, 1, new Vector3(-0.11f, 1.20f, 0.15f), blend);
            if (a.Head != null) Joint(a.Head, new Vector3(2, Mathf.Sin(phase * 0.05f) * 25, 0), blend);
        }
        void SitOnGround(Actor a, float phase, float blend)
        {
            GroundSeat(a, 92, blend);
            Torso(a, 12 + Mathf.Sin(phase * 0.08f) * 3, 0, 0, blend);
            ArmPose(a, 0, new Vector3(0.14f, 0.55f, 0.36f), blend); ArmPose(a, 1, new Vector3(-0.14f, 0.55f, 0.36f), blend);
            if (a.Head != null) Joint(a.Head, new Vector3(6, Mathf.Sin(phase * 0.05f) * 20, 0), blend);
        }
        // Down on the ground: the pelvis near the floor, legs forward with the knees bent up.
        void GroundSeat(Actor a, float knee, float blend)
        {
            Offset(a, Vector3.zero, new Vector3(0, -0.74f, -0.1f), Quaternion.identity, blend);
            for (int leg = 0; leg < 2; leg++) { Joint(a.Limbs[2 + leg], new Vector3(-84, 0, leg == 0 ? -18 : 18), blend); Joint(a.Knees[leg], new Vector3(knee, 0, 0), blend); }
        }
    }
}
