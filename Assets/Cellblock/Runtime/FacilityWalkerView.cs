using UnityEngine;

namespace Cellblock.Unity
{
    // A walking frame for some of the oldest residents (Claude, 2026-10-19): a light aluminium frame carried just ahead,
    // both hands on its grips, put aside while the person sits, lies down or is held by a scene. One merged mesh.
    public sealed partial class FacilityView
    {
        const float WalkerHalfWidth = .26f, WalkerGripHeight = .9f, WalkerGripAhead = .22f;

        void AddWalker(Actor actor)
        {
            var frame = new GameObject("Walker frame").transform; frame.SetParent(actor.Root, false);
            Color metal = new Color(.72f, .74f, .76f), grip = new Color(.12f, .12f, .13f), ball = new Color(.78f, .86f, .22f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * WalkerHalfWidth;
                foreach (float z in new[] { .1f, .5f }) Cylinder(frame, "Leg", new Vector3(x, .45f, z), new Vector3(.024f, .45f, .024f), "walker-metal", metal);
                Transform rail = Cylinder(frame, "Rail", new Vector3(x, .86f, .3f), new Vector3(.024f, .21f, .024f), "walker-metal", metal);
                rail.localRotation = Quaternion.Euler(90, 0, 0);
                Transform low = Cylinder(frame, "Brace", new Vector3(x, .32f, .3f), new Vector3(.018f, .2f, .018f), "walker-metal", metal);
                low.localRotation = Quaternion.Euler(90, 0, 0);
                Transform handle = Cylinder(frame, "Grip", new Vector3(x, WalkerGripHeight, WalkerGripAhead), new Vector3(.034f, .055f, .034f), "walker-grip", grip);
                handle.localRotation = Quaternion.Euler(90, 0, 0);
                Sphere(frame, "Glide", new Vector3(x, .035f, .1f), new Vector3(.065f, .065f, .065f), "walker-ball", ball);
            }
            Transform front = Cylinder(frame, "Front bar", new Vector3(0, .62f, .5f), new Vector3(.022f, WalkerHalfWidth, .022f), "walker-metal", metal);
            front.localRotation = Quaternion.Euler(0, 0, 90);
            Combine(frame);
            actor.Walker = frame;
        }

        // From Locomote, after the arm swing: hands on the grips while walking or standing; the frame is set aside when
        // a routine pose or a scene owns the body.
        void HoldWalker(Actor actor, bool free, float blend)
        {
            if (actor.Walker == null) return;
            if (actor.Walker.gameObject.activeSelf != free) actor.Walker.gameObject.SetActive(free);
            if (!free) return;
            for (int arm = 0; arm < 2; arm++)
            {
                Vector3 gripAt = actor.Walker.TransformPoint(new Vector3((arm == 0 ? -1 : 1) * WalkerHalfWidth, WalkerGripHeight, WalkerGripAhead));
                ArmPose(actor, arm, actor.Model.InverseTransformPoint(gripAt), blend);
            }
        }
    }
}
