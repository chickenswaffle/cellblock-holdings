using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cellblock.Unity
{
    public sealed class PresentationLighting : MonoBehaviour
    {
        VolumeProfile profile;
        public static void Configure(Camera camera, Transform parent)
        {
            var root = new GameObject("Architectural color and exposure"); root.transform.SetParent(parent, false);
            var owner = root.AddComponent<PresentationLighting>();
            owner.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var volume = root.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10; volume.sharedProfile = owner.profile;
            Tonemapping tone = owner.profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            ColorAdjustments color = owner.profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.1f); color.contrast.Override(8); color.saturation.Override(-5);
            Bloom bloom=owner.profile.Add<Bloom>(true);bloom.threshold.Override(1.2f);bloom.intensity.Override(.07f);bloom.scatter.Override(.4f);
            WhiteBalance balance = owner.profile.Add<WhiteBalance>(true); balance.temperature.Override(3);
            Vignette vignette = owner.profile.Add<Vignette>(true); vignette.intensity.Override(0.1f); vignette.smoothness.Override(0.55f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
        void OnDestroy()
        {
            if (profile == null) return;
            foreach (VolumeComponent component in profile.components) if (component != null) Destroy(component);
            Destroy(profile);
        }
    }
}
