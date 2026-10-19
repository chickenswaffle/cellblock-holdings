using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cellblock.Unity
{
    // Sun shadow range follows the view (Claude, 2026-10-18). One shadow map spread over the full 110 m management
    // range gives ~5 cm texels, which shows as saw-toothed wall and bed shadows when zoomed in on a cell. Fitting the
    // range to what the camera can see sharpens close views several times over, at no extra cost. The pipeline asset's
    // own value is restored on exit (in the editor it is a generated asset; ProjectSetup still owns its default).
    public static class PresentationShadows
    {
        static float original = -1, applied = -1;
        public static void Fit(float cameraDistance, bool characterView)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return;
            if (original < 0) original = pipeline.shadowDistance;
            float want = characterView ? 40 : Mathf.Clamp(cameraDistance * 2.3f + 12, 30, original);
            // Steps avoid re-fitting cascades (and shimmering) on every small zoom change.
            want = Mathf.Min(original, Mathf.Ceil(want / 10) * 10);
            if (Mathf.Approximately(want, applied)) return;
            pipeline.shadowDistance = applied = want;
        }
        public static void Restore()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline != null && original > 0) pipeline.shadowDistance = original;
            original = applied = -1;
        }
    }
}
