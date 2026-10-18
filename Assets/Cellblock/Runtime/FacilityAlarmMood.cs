using Cellblock.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cellblock.Unity
{
    // Alarm atmosphere. While the alarm sounds, a second global volume eases in over the presentation grade: a faint red
    // filter, less saturation, more contrast, a little darker at night, and a red-edged vignette that breathes at 0.7 Hz.
    // The vignette is a slow breath, never a strobe (well under three flashes a second), and it holds still with Reduced
    // Motion. Claude's (escape overhaul, research brief section 6.4).
    public sealed partial class FacilityView
    {
        Volume alarmVolume;
        VolumeProfile alarmProfile;
        ColorAdjustments alarmColor;
        Vignette alarmVignette;
        float alarmWeight, alarmVelocity;
        // Set by GameController from the player's preferences each frame.
        public bool ReducedMotion { get; set; }

        void RefreshAlarmMood(World world)
        {
            bool alarm = world != null && world.Data.Operations.Alarm;
            if (alarmVolume == null)
            {
                if (!alarm) return;
                var root = new GameObject("Alarm atmosphere / presentation only"); root.transform.SetParent(transform, false);
                alarmProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                alarmVolume = root.AddComponent<Volume>(); alarmVolume.isGlobal = true; alarmVolume.priority = 11; alarmVolume.sharedProfile = alarmProfile; alarmVolume.weight = 0;
                alarmColor = alarmProfile.Add<ColorAdjustments>(true);
                alarmColor.colorFilter.Override(new Color(1f, 0.86f, 0.84f)); alarmColor.saturation.Override(-17); alarmColor.contrast.Override(14);
                alarmVignette = alarmProfile.Add<Vignette>(true);
                alarmVignette.color.Override(new Color(0.35f, 0.03f, 0.03f)); alarmVignette.smoothness.Override(0.55f); alarmVignette.intensity.Override(0.2f);
            }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            alarmWeight = Mathf.Clamp01(Spring(alarmWeight, ref alarmVelocity, alarm ? 1 : 0, 0.45f, dt));
            alarmVolume.weight = alarmWeight;
            if (alarmVolume.gameObject.activeSelf != alarmWeight > 0.002f) alarmVolume.gameObject.SetActive(alarmWeight > 0.002f);
            if (alarmWeight <= 0.002f) return;
            int minute = world == null ? 720 : world.Data.Minute % 1440;
            float night = minute < 300 || minute >= 1200 ? 1 : minute < 390 ? (390 - minute) / 90f : minute >= 1110 ? (minute - 1110) / 90f : 0;
            alarmColor.postExposure.Override(0.1f - 0.15f * night);
            alarmVignette.intensity.Override(0.2f + (ReducedMotion ? 0 : Mathf.Sin(Time.unscaledTime * 2 * Mathf.PI * 0.7f) * 0.04f));
        }
#if UNITY_EDITOR
        public float PreviewAlarmMood { get { return alarmWeight; } }
#endif
    }
}
