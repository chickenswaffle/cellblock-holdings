using System.Collections.Generic;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    // Visual measurements consume the same sampled wall/footprint data as construction.
    // They never add collision or operational space.
    public sealed class ConstructionGuides : MonoBehaviour
    {
        Camera cameraRig;
        LineRenderer path, ruler, tickA, tickB;
        Transform labelRoot, markerA, markerB;
        TextMesh label;
        Material lineMaterial, markerMaterial, plateMaterial;
        Vector3 labelPosition;
        bool showing;
        public float Height = 1.65f;

        public void Initialize(Camera camera)
        {
            cameraRig = camera;
            Material template = Resources.Load<Material>("BuildingMaterial");
            lineMaterial = new Material(template) { name = "Construction guide" };
            markerMaterial = new Material(template) { name = "Construction handles" };
            plateMaterial = new Material(template) { name = "Measurement backing", color = new Color(0.055f, 0.085f, 0.09f) };
            path = Line("Measured geometry"); ruler = Line("Dimension line"); tickA = Line("Start dimension tick"); tickB = Line("End dimension tick");
            markerA = Marker("Start handle"); markerB = Marker("End handle");
            labelRoot = new GameObject("Measurement label").transform; labelRoot.SetParent(transform, false);
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube); plate.name = "Label backing"; plate.transform.SetParent(labelRoot, false);
            plate.transform.localPosition = new Vector3(0, 0, 0.035f); plate.GetComponent<Renderer>().sharedMaterial = plateMaterial; Destroy(plate.GetComponent<Collider>());
            var text = new GameObject("Dimension"); text.transform.SetParent(labelRoot, false); label = text.AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 64; label.characterSize = 0.16f;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = new Color(0.85f, 0.98f, 0.94f);
            text.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            Show(null);
        }
        LineRenderer Line(string title)
        {
            var item = new GameObject(title); item.transform.SetParent(transform, false);
            var line = item.AddComponent<LineRenderer>(); line.sharedMaterial = lineMaterial;
            line.useWorldSpace = true; line.startWidth = line.endWidth = 0.035f; line.numCapVertices = 3;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }
        Transform Marker(string title)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cylinder); item.name = title; item.transform.SetParent(transform, false);
            item.transform.localScale = new Vector3(0.2f, 0.025f, 0.2f); item.GetComponent<Renderer>().sharedMaterial = markerMaterial;
            Destroy(item.GetComponent<Collider>()); return item.transform;
        }
        void SetColor(bool invalid)
        {
            Color color = invalid ? new Color(1, 0.37f, 0.25f) : new Color(0.25f, 0.84f, 0.76f);
            lineMaterial.color = markerMaterial.color = color;
            if (lineMaterial.HasProperty("_EmissionColor"))
            { lineMaterial.EnableKeyword("_EMISSION"); lineMaterial.SetColor("_EmissionColor", color * 0.4f); }
            label.color = invalid ? new Color(1, 0.75f, 0.65f) : new Color(0.86f, 1, 0.96f);
        }
        void Display(bool value)
        {
            showing = value; path.gameObject.SetActive(value); ruler.gameObject.SetActive(value);
            tickA.gameObject.SetActive(value); tickB.gameObject.SetActive(value);
            markerA.gameObject.SetActive(value); markerB.gameObject.SetActive(value); labelRoot.gameObject.SetActive(value);
        }
        void Segment(LineRenderer line, Vector3 a, Vector3 b)
        { line.positionCount = 2; line.SetPosition(0, a); line.SetPosition(1, b); }
        public void Show(Wall wall, bool invalid = false)
        {
            if (path == null) return;
            Display(wall != null); if (wall == null) return; SetColor(invalid);
            List<Point> samples = Geometry.Sample(wall); path.positionCount = samples.Count;
            for (int i = 0; i < samples.Count; i++) path.SetPosition(i, FacilityView.V(samples[i], 0.22f));
            Point direction = wall.B - wall.A;
            Point perpendicular = new Point(-direction.Y, direction.X) * (0.7f / Mathf.Max(direction.Length, 0.01f));
            Vector3 a = FacilityView.V(wall.A + perpendicular, 0.24f), b = FacilityView.V(wall.B + perpendicular, 0.24f);
            Segment(ruler, a, b);
            Vector3 tick = FacilityView.V(perpendicular * 0.24f);
            Segment(tickA, a - tick, a + tick); Segment(tickB, b - tick, b + tick);
            markerA.position = FacilityView.V(wall.A, 0.24f); markerB.position = FacilityView.V(wall.B, 0.24f);
            label.text = Geometry.Length(wall).ToString("0.0") + " m" + (wall.Curved ? "  /  curve" : "");
            labelPosition = FacilityView.V(Geometry.OnLength(wall, 0.5f) + perpendicular, Height);
            labelRoot.GetChild(0).localScale = new Vector3(Mathf.Max(1.1f, label.text.Length * 0.105f), 0.34f, 0.035f);
        }
        public void ShowFixture(Fixture fixture, bool invalid = false)
        {
            if (path == null) return;
            Display(fixture != null); if (fixture == null) return; SetColor(invalid);
            List<Point> points = Geometry.Footprint(fixture); path.positionCount = points.Count + 1;
            for (int i = 0; i <= points.Count; i++) path.SetPosition(i, FacilityView.V(points[i % points.Count], 0.2f));
            ruler.gameObject.SetActive(false); tickA.gameObject.SetActive(false); tickB.gameObject.SetActive(false);
            markerA.gameObject.SetActive(false); markerB.gameObject.SetActive(false);
            FixtureDefinition definition = FixtureCatalog.Get(fixture.Kind);
            label.text = definition.Name.ToUpperInvariant() + "  /  " + definition.Width.ToString("0.0") + " × " + definition.Depth.ToString("0.0") + " m";
            labelPosition = FacilityView.V(fixture.Position, Height);
            labelRoot.GetChild(0).localScale = new Vector3(Mathf.Max(2.35f, label.text.Length * 0.105f), 0.34f, 0.035f);
        }
        void LateUpdate()
        {
            if (!showing || cameraRig == null) return;
            labelRoot.position = labelPosition; labelRoot.rotation = cameraRig.transform.rotation;
            float scale = cameraRig.orthographic ? cameraRig.orthographicSize / 16 : Vector3.Distance(cameraRig.transform.position, labelPosition) / 45;
            labelRoot.localScale = Vector3.one * Mathf.Clamp(scale, 0.4f, 2.8f);
            float width = Mathf.Clamp(scale * 0.035f, 0.018f, 0.09f);
            path.startWidth = path.endWidth = ruler.startWidth = ruler.endWidth = tickA.startWidth = tickA.endWidth = tickB.startWidth = tickB.endWidth = width;
        }
        void OnDestroy()
        { if (lineMaterial != null) Destroy(lineMaterial); if (markerMaterial != null) Destroy(markerMaterial); if (plateMaterial != null) Destroy(plateMaterial); }
    }
}
