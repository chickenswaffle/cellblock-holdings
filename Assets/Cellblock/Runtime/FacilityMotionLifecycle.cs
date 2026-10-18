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
#if UNITY_EDITOR
            naturalMotionProbes.Remove(actor);
#endif
            strides.Remove(actor); heldPoses.Remove(actor); heldThisFrame.Remove(actor);
            ShadowSlot shadow;
            if (shadowSlots.TryGetValue(actor, out shadow))
            {
                if (shadow.Root != null)
                {
                    if (shadow.Root.gameObject.activeInHierarchy) shadowsDrawn = Mathf.Max(0, shadowsDrawn - 1);
                    shadow.Root.gameObject.SetActive(false); Destroy(shadow.Root.gameObject);
                }
                shadowSlots.Remove(actor);
            }
        }
        void ResetMotionPresentation()
        {
            foreach (ShadowSlot shadow in shadowSlots.Values)
                if (shadow.Root != null) { shadow.Root.gameObject.SetActive(false); Destroy(shadow.Root.gameObject); }
            shadowSlots.Clear(); shadowRetired.Clear(); shadowsDrawn = 0;
            alarmWeight = alarmVelocity = 0;
            if (alarmVolume != null) { alarmVolume.weight = 0; alarmVolume.gameObject.SetActive(false); }
#if UNITY_EDITOR
            naturalMotionProbes.Clear();
#endif
            strides.Clear(); strideStaff.Clear(); strideStaffFrame = -1;
            heldPoses.Clear(); heldThisFrame.Clear();
            cast.ProneYaw = 0; cast.ProneKey = cast.ImpactKey = cast.LandingKey = cast.StaggerKey = cast.StaggerLanding = int.MinValue;
            cast.NextGlance = cast.GlanceUntil = 0; cast.Beat = "";
            sceneMinute = -1; sceneFraction = hitStopUntil = CameraImpulse = 0; CameraImpulsePoint = Vector3.zero;
            foreach (Puff puff in puffs) if (puff.Root != null) { puff.Root.gameObject.SetActive(false); puff.Start = -10; }
        }
        void DisposeMotionResources()
        {
            ResetMotionPresentation();
            for (int i = 0; i < shadowQuads.Length; i++) if (shadowQuads[i] != null) { Destroy(shadowQuads[i]); shadowQuads[i] = null; }
            if (shadowMaterial != null) { Destroy(shadowMaterial); shadowMaterial = null; }
            if (alarmProfile != null)
            {
                foreach (var component in alarmProfile.components) if (component != null) Destroy(component);
                Destroy(alarmProfile); alarmProfile = null;
            }
        }
#if UNITY_EDITOR
        public float PreviewAlarmVignette { get { return alarmVignette == null ? 0 : alarmVignette.intensity.value; } }
        public int PreviewShadowPoolCount { get { return shadowSlots.Count; } }
        public int PreviewShadowActiveCount { get { return shadowSlots.Values.Count(s => s.Root != null && s.Root.gameObject.activeInHierarchy); } }
        public int PreviewMotionActorCount { get { return actors.Count; } }
        public int PreviewStrideCount { get { return strides.Count; } }
        public bool PreviewMotionBindingsAlive
        {
            get { return strides.Keys.All(a => a.Root != null && actors.ContainsValue(a)) &&
                heldPoses.Keys.All(a => a.Root != null && actors.ContainsValue(a)) &&
                heldThisFrame.All(a => a.Root != null && actors.ContainsValue(a)) &&
                shadowSlots.Keys.All(a => a.Root != null && actors.ContainsValue(a)); }
        }
        public bool PreviewMotionWorld(World world)
        { return object.ReferenceEquals(presentedWorld, world) && PreviewMotionBindingsAlive &&
            strideStaff.Values.All(s => world.Data.Staff.Contains(s)); }
#endif
    }
}
