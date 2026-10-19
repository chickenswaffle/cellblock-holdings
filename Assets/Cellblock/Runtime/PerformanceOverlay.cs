using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // Performance overlay (Claude, 2026-10-18): what a device test needs to read off the screen — frame rate, average
    // and worst frame time, the quality tier (and any automatic step-down), render resolution, lights and shadow
    // casters, sounds playing, memory, draw calls and the population being simulated. Settings → Display, or F3.
    public static class PerformanceOverlay
    {
        static Label label; static float nextText;
        static ProfilerRecorder drawCalls, setPass, triangles;
        public static bool Visible { get; private set; }
        public static string Text { get { return label == null ? "" : label.text; } }
        static VisualElement Header { get { return label == null || label.parent == null ? null : label.parent.Q<VisualElement>(className: "header"); } }
        // Review check: inside the screen and not over the header.
        public static bool Fits
        {
            get
            {
                if (label == null) return false;
                Rect box = label.worldBound, screen = label.parent.worldBound; VisualElement header = Header;
                bool inside = box.xMin >= screen.xMin && box.xMax <= screen.xMax && box.yMin >= screen.yMin && box.yMax <= screen.yMax;
                return inside && (header == null || header.resolvedStyle.display == DisplayStyle.None || box.yMin >= header.worldBound.yMax);
            }
        }

        public static void Attach(VisualElement root)
        {
            if (label != null) return;
            label = new Label { name = "performance-overlay", pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute; label.style.right = 12; label.style.top = 76; // under the header, clear of the dock and hint bar
            label.style.paddingLeft = 10; label.style.paddingRight = 10; label.style.paddingTop = 7; label.style.paddingBottom = 7;
            label.style.backgroundColor = new Color(.04f, .05f, .06f, .78f); label.style.color = new Color(.86f, .92f, .88f);
            label.style.fontSize = 12; label.style.whiteSpace = WhiteSpace.Normal; label.style.unityTextAlign = TextAnchor.UpperLeft;
            label.style.borderTopLeftRadius = label.style.borderTopRightRadius = label.style.borderBottomLeftRadius = label.style.borderBottomRightRadius = 6;
            label.style.display = DisplayStyle.None;
            root.Add(label);
        }

        public static void Show(bool visible)
        {
            Visible = visible;
            if (label != null) { label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; if (visible) label.BringToFront(); }
            if (visible && !drawCalls.Valid)
            {
                drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            }
            if (!visible && drawCalls.Valid) { drawCalls.Dispose(); setPass.Dispose(); triangles.Dispose(); }
        }

        public static void Tick(GameController game)
        {
            if (!Visible || label == null || Time.unscaledTime < nextText) return;
            nextText = Time.unscaledTime + .5f;
            // Sit just under the header whatever its height (interface size, phone layouts, safe areas).
            VisualElement header = Header;
            if (header != null && header.resolvedStyle.display != DisplayStyle.None && !float.IsNaN(header.worldBound.yMax)) label.style.top = header.worldBound.yMax - label.parent.worldBound.yMin + 8;
            float average = PresentationQuality.AverageFrame, worst = PresentationQuality.WorstFrame;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float scale = pipeline == null ? 1 : pipeline.renderScale;
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            int on = lights.Count(l => l.enabled && l.gameObject.activeInHierarchy), shadowed = lights.Count(l => l.enabled && l.gameObject.activeInHierarchy && l.shadows != LightShadows.None);
            long allocated = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() >> 20, reserved = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() >> 20;
            var w = game.World;
            string text = (average > 0 ? Mathf.RoundToInt(1 / average) : 0) + " fps   " + (average * 1000).ToString("0.0") + " ms avg   " + (worst * 1000).ToString("0") + " ms worst   (limit " + Application.targetFrameRate + ")\n" +
                "Quality " + PresentationQuality.Current + (PresentationQuality.Automatic ? " (Auto)" : "") + "   render " + Mathf.RoundToInt(Screen.width * scale) + "×" + Mathf.RoundToInt(Screen.height * scale) + " (" + Mathf.RoundToInt(scale * 100) + "%)\n" +
                "Lights " + on + " on, " + shadowed + " with shadows   Sounds " + FacilitySoundscape.Playing + "\n" +
                (drawCalls.Valid && drawCalls.LastValue > 0 ? "Draw calls " + drawCalls.LastValue + "   SetPass " + setPass.LastValue + "   Tris " + (triangles.LastValue / 1000) + "k\n" : "") +
                "Memory " + allocated + " / " + reserved + " MB   " + (w == null ? "No holding" : w.Data.Residents.Count + " residents · " + w.Data.Staff.Count + " staff · speed " + w.Data.Speed + "×") +
                (PresentationQuality.LastChange.StartsWith("Auto lowered") ? "\n" + PresentationQuality.LastChange : "");
            label.text = text;
        }
    }
}
