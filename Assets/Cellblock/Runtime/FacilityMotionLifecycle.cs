using System.Linq;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        // Animation bindings have the same lifetime as the actor they describe. Court absence is
        // temporary; roster removal and replacement of the displayed world release the bindings.
        void ForgetActorMotion(Actor actor)
        {
            strides.Remove(actor); heldPoses.Remove(actor); heldThisFrame.Remove(actor);
        }
        void ResetMotionPresentation()
        {
            strides.Clear(); strideStaff.Clear(); strideStaffFrame = -1;
            heldPoses.Clear(); heldThisFrame.Clear();
            cast.ProneYaw = 0; cast.ProneKey = cast.ImpactKey = cast.LandingKey = cast.StaggerKey = cast.StaggerLanding = int.MinValue;
            cast.NextGlance = cast.GlanceUntil = 0; cast.Beat = "";
            sceneMinute = -1; sceneFraction = hitStopUntil = CameraImpulse = 0; CameraImpulsePoint = Vector3.zero;
            foreach (Puff puff in puffs) if (puff.Root != null) { puff.Root.gameObject.SetActive(false); puff.Start = -10; }
        }
#if UNITY_EDITOR
        public int PreviewMotionActorCount { get { return actors.Count; } }
        public int PreviewStrideCount { get { return strides.Count; } }
        public bool PreviewMotionBindingsAlive
        {
            get { return strides.Keys.All(a => a.Root != null && actors.ContainsValue(a)) &&
                heldPoses.Keys.All(a => a.Root != null && actors.ContainsValue(a)) &&
                heldThisFrame.All(a => a.Root != null && actors.ContainsValue(a)); }
        }
        public bool PreviewMotionWorld(World world)
        { return object.ReferenceEquals(presentedWorld, world) && PreviewMotionBindingsAlive &&
            strideStaff.Values.All(s => world.Data.Staff.Contains(s)); }
#endif
    }
}
