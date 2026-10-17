using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // A score is not a probability. Callers supply the displayed unit and semantic direction.
    internal enum MeterMeaning { HigherIsBetter, HigherIsWorse, Neutral }

    internal sealed class StatMeter : VisualElement
    {
        readonly Label title, direction, number;
        readonly VisualElement track, zero;
        readonly MeterFill fill;
        internal float Value { get; private set; }
        internal float Scale { get; private set; }
        internal bool Signed { get; private set; }
        internal string Readout { get { return number.text; } }
        internal Color Tint { get; private set; }
        internal Rect TrackBounds { get { return track.worldBound; } }
        internal Rect NumberBounds { get { return number.worldBound; } }
        internal static readonly Color Green = new Color32(45, 154, 118, 255), Red = new Color32(196, 84, 73, 255), Amber = new Color32(181, 137, 65, 255), Blue = new Color32(69, 130, 151, 255);

        internal StatMeter(string label)
        {
            AddToClassList("stat-meter"); pickingMode = PickingMode.Ignore;
            var head = new VisualElement(); head.AddToClassList("stat-meter-head");
            title = new Label(label); title.AddToClassList("stat-meter-title"); head.Add(title);
            direction = new Label(); direction.AddToClassList("stat-meter-direction"); head.Add(direction); Add(head);
            track = new VisualElement(); track.AddToClassList("stat-meter-track"); Add(track);
            fill = new MeterFill(); fill.AddToClassList("stat-meter-fill"); track.Add(fill);
            zero = new VisualElement(); zero.AddToClassList("stat-meter-zero"); track.Add(zero);
            number = new Label(); number.AddToClassList("stat-meter-number"); track.Add(number);
        }

        internal void SetScore(string label, float value, MeterMeaning meaning, string readout = null, string help = null, float scale = 100, float cautionAt = .35f, float dangerAt = .65f)
        {
            Signed = false; Value = Finite(value); Scale = Mathf.Max(.001f, Finite(scale));
            float ratio = Mathf.Clamp01(Value / Scale);
            float danger = meaning == MeterMeaning.HigherIsBetter ? 1 - ratio : ratio;
            Tint = meaning == MeterMeaning.Neutral ? Blue : danger < cautionAt ? Green : danger >= dangerAt ? Red : Amber;
            EnableInClassList("metric-critical", Tint == Red);
            SetHead(label, meaning == MeterMeaning.Neutral ? "LIVE" : meaning == MeterMeaning.HigherIsBetter ? "↑ GOOD" : "↑ RISK");
            fill.style.left = Length.Percent(0); fill.style.width = Length.Percent(ratio * 100); fill.SetTint(Tint);
            zero.style.display = DisplayStyle.None;
            number.text = readout ?? Mathf.RoundToInt(Value) + " / " + Mathf.RoundToInt(Scale);
            tooltip = label + ": " + number.text + ". " + (help ?? (meaning == MeterMeaning.Neutral ? "Current score." : meaning == MeterMeaning.HigherIsBetter ? "Higher is better." : "Lower is better."));
        }

        internal void SetFactor(string label, float points, float scale)
        {
            Signed = true; Value = Finite(points); Scale = Mathf.Max(1, Finite(scale));
            float width = Mathf.Clamp01(Mathf.Abs(Value) / Scale) * 50;
            Tint = Value < 0 ? Green : Value > 0 ? Red : Blue;
            RemoveFromClassList("metric-critical");
            SetHead(label, Value < 0 ? "PROTECTIVE" : Value > 0 ? "ADDS RISK" : "NEUTRAL");
            fill.style.left = Length.Percent(Value < 0 ? 50 - width : 50); fill.style.width = Length.Percent(width); fill.SetTint(Tint);
            zero.style.display = DisplayStyle.Flex;
            number.text = (Value > 0 ? "+" : Value < 0 ? "−" : "") + Mathf.Abs(Value).ToString("0.#") + " pts";
            tooltip = label + ": " + number.text + ". Green left reduces escape risk; red right adds risk. Shared scale −" + Scale.ToString("0") + " to +" + Scale.ToString("0") + " points.";
        }

        void SetHead(string label, string hint) { title.text = label; direction.text = hint; }
        static float Finite(float value) { return float.IsNaN(value) || float.IsInfinity(value) ? 0 : value; }

        sealed class MeterFill : VisualElement
        {
            Color tint = Blue;
            internal MeterFill() { pickingMode = PickingMode.Ignore; generateVisualContent += Paint; }
            internal void SetTint(Color value) { if (tint == value) return; tint = value; MarkDirtyRepaint(); }
            void Paint(MeshGenerationContext context)
            {
                Rect r = contentRect; if (r.width <= 0 || r.height <= 0) return;
                var mesh = context.Allocate(4, 6);
                Color left = Color.Lerp(tint, Color.black, .18f), right = Color.Lerp(tint, Color.white, .12f);
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMin, r.yMin, Vertex.nearZ), tint = left });
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMax, r.yMin, Vertex.nearZ), tint = right });
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMax, r.yMax, Vertex.nearZ), tint = right });
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMin, r.yMax, Vertex.nearZ), tint = left });
                mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2); mesh.SetNextIndex(2); mesh.SetNextIndex(3); mesh.SetNextIndex(0);
            }
        }
    }
}
