using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cellblock.Unity
{
    public sealed class FacilityView : MonoBehaviour
    {
        Transform structures, planned, ghost, people;
        readonly List<Transform> residents = new List<Transform>();
        readonly List<Transform[]> limbs = new List<Transform[]>();
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        int revision = -1;
        string projectKey = "";
        public bool FullHeight;
        public static Vector3 V(Point point, float height = 0) { return new Vector3(point.X, height, point.Y); }
        Material Material(string name, Color color, bool metallic = false)
        {
            Material value;
            if (materials.TryGetValue(name, out value)) return value;
            Material template = Resources.Load<Material>("BuildingMaterial");
            if (template == null || template.shader == null)
                throw new System.InvalidOperationException("BuildingMaterial is missing. Run Cellblock > Prepare project before Play or building a player.");
            value = new Material(template) { name = name, color = color, enableInstancing = true };
            if (value.HasProperty("_Smoothness")) value.SetFloat("_Smoothness", metallic ? 0.55f : 0.14f);
            materials[name] = value;
            return value;
        }
        public void Initialize()
        {
            structures = Root("Operational building"); planned = Root("Construction sites");
            ghost = Root("Plan preview"); people = Root("Residents");
            var sun = new GameObject("Afternoon sun").AddComponent<Light>();
            sun.transform.SetParent(transform); sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48, -35, 0); sun.intensity = 1.7f;
            sun.color = new Color(1, 0.94f, 0.84f); sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.57f, 0.65f, 0.7f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(0.65f, 0.71f, 0.72f);
            RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 100; RenderSettings.fogEndDistance = 190;
            Box(transform, "Landscape", new Vector3(0, -0.24f, 0), new Vector3(170, 0.4f, 140), "grass", new Color(0.32f, 0.39f, 0.28f));
            Box(transform, "Property", new Vector3(0, -0.055f, 0), new Vector3(60, 0.12f, 48), "site", new Color(0.42f, 0.45f, 0.37f));
            Box(transform, "Access road", new Vector3(0, -0.01f, -20), new Vector3(150, 0.1f, 7), "asphalt", new Color(0.22f, 0.24f, 0.25f));
            Box(transform, "Drive", new Vector3(-2, 0.01f, -12), new Vector3(5, 0.1f, 10), "paving", new Color(0.48f, 0.49f, 0.46f));
            Box(transform, "Service apron", new Vector3(-6, 0, 0), new Vector3(20, 0.1f, 20), "paving", new Color(0.48f, 0.49f, 0.46f));
            for (int i = -6; i <= 6; i++)
                Box(transform, "Road marking", new Vector3(i * 6, 0.06f, -20), new Vector3(2.5f, 0.015f, 0.11f), "paint", new Color(0.83f, 0.81f, 0.69f));
            for (int i = 0; i < 10; i++)
            {
                float x = -26 + i * 5.7f;
                Fence(new Point(x, 21), new Point(x + 5.7f, 21));
                if (i < 4 || i > 5) Fence(new Point(x, -16), new Point(x + 5.7f, -16));
            }
            Fence(new Point(-26, -16), new Point(-26, 21)); Fence(new Point(31, -16), new Point(31, 21));
            for (int i = 0; i < 20; i++)
            {
                float x = i < 10 ? -35 - i % 3 * 4 : 36 + i % 3 * 4;
                float z = -10 + (i % 10) * 5;
                Box(transform, "Tree trunk", new Vector3(x, 1.4f, z), new Vector3(0.3f, 2.8f, 0.3f), "bark", new Color(0.28f, 0.24f, 0.19f));
                Sphere(transform, "Tree crown", new Vector3(x, 3.8f, z), new Vector3(3.7f, 4.2f, 3.5f), "leaf", new Color(0.21f, 0.32f, 0.2f));
            }
            Transform van = Root("Service vehicle");
            Box(van, "Body", new Vector3(6, 1, -13), new Vector3(2.2f, 1.7f, 4.7f), "van", new Color(0.75f, 0.76f, 0.7f));
            Box(van, "Windshield", new Vector3(6, 1.55f, -14.7f), new Vector3(1.8f, 0.6f, 0.08f), "glass", new Color(0.2f, 0.31f, 0.35f));
            for (int i = 0; i < 4; i++)
                Sphere(van, "Wheel", new Vector3(6 + (i % 2 == 0 ? -1.05f : 1.05f), 0.4f, -13 + (i < 2 ? -1.5f : 1.5f)),
                    new Vector3(0.32f, 0.8f, 0.8f), "rubber", new Color(0.08f, 0.09f, 0.09f));
        }
        Transform Root(string name)
        {
            var value = new GameObject(name).transform; value.SetParent(transform, false); return value;
        }
        void Clear(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;
                child.SetActive(false);
                foreach (MeshFilter filter in child.GetComponentsInChildren<MeshFilter>())
                    if (filter.sharedMesh != null && filter.sharedMesh.name == "Generated room floor") Destroy(filter.sharedMesh);
                Destroy(child);
            }
        }
        Transform Shape(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, string material, Color color)
        {
            GameObject item = GameObject.CreatePrimitive(type); item.name = name;
            item.transform.SetParent(parent, false); item.transform.localPosition = position; item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = Material(material, color);
            Destroy(item.GetComponent<Collider>());
            return item.transform;
        }
        Transform Box(Transform parent, string name, Vector3 position, Vector3 scale, string material, Color color)
        { return Shape(PrimitiveType.Cube, parent, name, position, scale, material, color); }
        Transform Sphere(Transform parent, string name, Vector3 position, Vector3 scale, string material, Color color)
        { return Shape(PrimitiveType.Sphere, parent, name, position, scale, material, color); }
        void Fence(Point a, Point b)
        {
            float length = Geometry.Distance(a, b); int posts = Mathf.CeilToInt(length / 3);
            for (int i = 0; i <= posts; i++)
            {
                Point p = a + (b - a) * (i / (float)posts);
                Box(transform, "Perimeter post", V(p, 1.3f), new Vector3(0.08f, 2.6f, 0.08f), "steel", new Color(0.26f, 0.3f, 0.29f));
            }
            for (int i = 0; i <= 6; i++) Beam(transform, a, b, 0.25f + i * 0.36f, 0.025f, 0.025f, "steel", new Color(0.26f, 0.3f, 0.29f));
            for (int i = 0; i < length * 2; i++)
            {
                Point p = a + (b - a) * (i / (length * 2));
                Box(transform, "Fence wire", V(p, 1.3f), new Vector3(0.012f, 2.5f, 0.012f), "steel", new Color(0.26f, 0.3f, 0.29f));
            }
        }
        void Beam(Transform parent, Point a, Point b, float y, float height, float thickness, string material, Color color)
        {
            Point d = b - a;
            Transform beam = Box(parent, "Wall", V((a + b) * 0.5f, y), new Vector3(d.Length + 0.015f, height, thickness), material, color);
            beam.rotation = Quaternion.Euler(0, -Mathf.Atan2(d.Y, d.X) * Mathf.Rad2Deg, 0);
        }
        void WallView(Transform parent, Wall wall, string palette = null)
        {
            float height = palette != null ? 0.25f : FullHeight ? 2.9f : 1.25f;
            Color color = palette == "ghost" ? new Color(0.22f, 0.72f, 0.63f) : palette == "planned" ?
                new Color(0.77f, 0.59f, 0.28f) : wall.Finish == 1 ? new Color(0.51f, 0.31f, 0.23f) : new Color(0.74f, 0.73f, 0.67f);
            string material = palette ?? (wall.Finish == 1 ? "brick" : "plaster");
            foreach (Segment segment in Geometry.Segments(wall, true))
            {
                Beam(parent, segment.A, segment.B, height / 2 + 0.12f, height, 0.22f, material, color);
                if (palette == null) Beam(parent, segment.A, segment.B, height + 0.13f, 0.07f, 0.25f, "cap", new Color(0.46f, 0.48f, 0.45f));
            }
            if (wall.Door >= 0)
            {
                Point center = Geometry.OnLength(wall, wall.Door);
                Point a = Geometry.OnLength(wall, Mathf.Max(0, wall.Door - 0.01f)), b = Geometry.OnLength(wall, Mathf.Min(1, wall.Door + 0.01f));
                Point tangent = (b - a) * (1 / Mathf.Max(0.001f, (b - a).Length));
                foreach (int sign in new[] { -1, 1 })
                    Box(parent, "Door frame", V(center + tangent * (sign * wall.DoorWidth / 2), height / 2 + 0.12f),
                        new Vector3(0.1f, height, 0.28f), "door", new Color(0.24f, 0.32f, 0.3f));
                Beam(parent, center - tangent * (wall.DoorWidth / 2), center + tangent * (wall.DoorWidth / 2), height + 0.15f,
                    0.14f, 0.26f, "door", new Color(0.24f, 0.32f, 0.3f));
            }
        }
        void Floor(List<List<Point>> rooms, int index)
        {
            List<Point> vertices = Geometry.FloorTriangles(rooms, index);
            int[] triangles = Enumerable.Range(0, vertices.Count).ToArray();
            for (int i = 0; i < triangles.Length; i += 3) { int swap = triangles[i + 1]; triangles[i + 1] = triangles[i + 2]; triangles[i + 2] = swap; }
            var mesh = new Mesh { name = "Generated room floor" };
            mesh.vertices = vertices.Select(p => V(p, 0.12f)).ToArray(); mesh.triangles = triangles;
            mesh.uv = vertices.Select(p => new Vector2(p.X * 0.5f, p.Y * 0.5f)).ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var floor = new GameObject("Room " + (index + 1)); floor.transform.SetParent(structures);
            floor.AddComponent<MeshFilter>().sharedMesh = mesh;
            floor.AddComponent<MeshRenderer>().sharedMaterial = Material("floor-" + index % 3,
                index % 3 == 0 ? new Color(0.59f, 0.63f, 0.6f) : new Color(0.7f, 0.68f, 0.6f));
        }
        void FixtureView(Transform parent, Fixture fixture, bool preview = false)
        {
            var root = new GameObject(fixture.Kind).transform; root.SetParent(parent, false);
            root.localPosition = V(fixture.Position, 0.13f); root.localRotation = Quaternion.Euler(0, fixture.Rotation, 0);
            Color steel = preview ? new Color(0.22f, 0.72f, 0.63f) : new Color(0.29f, 0.34f, 0.34f);
            string metal = preview ? "ghost" : "steel";
            if (fixture.Kind == "Bed")
            {
                Box(root, "Bed frame", new Vector3(0, 0.32f, 0), new Vector3(0.95f, 0.16f, 2.05f), metal, steel);
                Box(root, "Mattress", new Vector3(0, 0.49f, 0), new Vector3(0.87f, 0.2f, 1.95f), "linen", new Color(0.73f, 0.77f, 0.7f));
                Box(root, "Blanket", new Vector3(0, 0.6f, 0.2f), new Vector3(0.88f, 0.04f, 1.4f), "blanket", new Color(0.31f, 0.39f, 0.4f));
                Box(root, "Pillow", new Vector3(0, 0.63f, -0.7f), new Vector3(0.65f, 0.14f, 0.35f), "pillow", new Color(0.83f, 0.82f, 0.76f));
                foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                    Box(root, "Leg", new Vector3(x * 0.39f, 0.18f, z * 0.88f), new Vector3(0.06f, 0.35f, 0.06f), metal, steel);
            }
            else
            {
                Box(root, "Top", new Vector3(0, 0.78f, 0), new Vector3(1.8f, 0.12f, 1), "wood", new Color(0.49f, 0.4f, 0.29f));
                foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                    Box(root, "Leg", new Vector3(x * 0.75f, 0.38f, z * 0.35f), new Vector3(0.07f, 0.76f, 0.07f), metal, steel);
                if (fixture.Kind == "Table")
                    foreach (int sign in new[] { -1, 1 })
                        Box(root, "Bench", new Vector3(0, 0.45f, sign * 0.9f), new Vector3(1.8f, 0.12f, 0.35f), "wood", new Color(0.49f, 0.4f, 0.29f));
            }
        }
        void Person()
        {
            var root = new GameObject("Resident").transform; root.SetParent(people, false);
            Color skin = residents.Count % 3 == 0 ? new Color(0.48f, 0.32f, 0.23f) : new Color(0.72f, 0.54f, 0.4f);
            Sphere(root, "Head", new Vector3(0, 1.65f, 0), new Vector3(0.21f, 0.27f, 0.22f), "skin-" + residents.Count % 3, skin);
            Box(root, "Torso", new Vector3(0, 1.24f, 0), new Vector3(0.4f, 0.57f, 0.24f), "uniform", new Color(0.64f, 0.44f, 0.27f));
            var parts = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject(i < 2 ? "Shoulder" : "Hip").transform; pivot.SetParent(root, false);
                pivot.localPosition = new Vector3((i % 2 == 0 ? -1 : 1) * (i < 2 ? 0.27f : 0.11f), i < 2 ? 1.45f : 0.94f, 0);
                Box(pivot, "Limb", new Vector3(0, i < 2 ? -0.28f : -0.4f, 0), new Vector3(i < 2 ? 0.12f : 0.16f, i < 2 ? 0.58f : 0.8f, 0.15f),
                    i < 2 ? "uniform" : "trousers", i < 2 ? new Color(0.64f, 0.44f, 0.27f) : new Color(0.24f, 0.28f, 0.28f));
                parts[i] = pivot;
            }
            residents.Add(root); limbs.Add(parts);
        }
        public void Refresh(World world, bool force = false)
        {
            if (force || revision != world.GeometryRevision)
            {
                Clear(structures); revision = world.GeometryRevision;
                for (int i = 0; i < world.Rooms.Count; i++) Floor(world.Rooms, i);
                foreach (Wall wall in world.Data.Walls) WallView(structures, wall);
                foreach (Fixture fixture in world.Data.Fixtures) FixtureView(structures, fixture);
            }
            string key = string.Join("|", world.Data.Projects.Where(p => !p.Completed).Select(p => p.Id));
            if (force || key != projectKey)
            {
                Clear(planned); projectKey = key;
                foreach (Project project in world.Data.Projects.Where(p => !p.Completed))
                    foreach (WorkItem item in project.Work)
                    {
                        if (item.Wall != null) WallView(planned, item.Wall, "planned");
                        if (item.Fixture != null) FixtureView(planned, item.Fixture, true);
                    }
            }
            while (residents.Count < world.Data.Residents.Count) Person();
            for (int i = 0; i < residents.Count; i++)
            {
                residents[i].gameObject.SetActive(i < world.Data.Residents.Count);
                if (i >= world.Data.Residents.Count) continue;
                Transform actor = residents[i]; Vector3 target = V(world.Data.Residents[i].Position, 0.14f);
                if ((target - actor.position).sqrMagnitude > 200) actor.position = target;
                Vector3 step = target - actor.position;
                actor.position = Vector3.MoveTowards(actor.position, target, Time.unscaledDeltaTime * 2.8f);
                if (step.sqrMagnitude > 0.005f)
                    actor.rotation = Quaternion.Slerp(actor.rotation, Quaternion.LookRotation(step), Time.unscaledDeltaTime * 12);
                float swing = step.sqrMagnitude > 0.005f ? Mathf.Sin(Time.unscaledTime * 9 + i) * 24 : 0;
                for (int j = 0; j < 4; j++) limbs[i][j].localRotation = Quaternion.Euler(swing * (j % 2 == 0 ? 1 : -1) * (j < 2 ? -0.7f : 1), 0, 0);
            }
        }
        public void Preview(Plan plan, Wall selected = null)
        {
            Clear(ghost);
            if (plan != null)
            {
                foreach (Wall wall in plan.Walls) WallView(ghost, wall, "ghost");
                foreach (Fixture fixture in plan.Fixtures) FixtureView(ghost, fixture, true);
            }
            if (selected != null)
                foreach (Point p in new[] { selected.A, selected.B, selected.Curved ? selected.Control : (selected.A + selected.B) * 0.5f })
                    Sphere(ghost, "Edit handle", V(p, 1.5f), Vector3.one * 0.35f, "handle", new Color(0.22f, 0.72f, 0.63f));
        }
        void OnDestroy()
        {
            Clear(structures); Clear(planned); Clear(ghost);
            foreach (Material material in materials.Values) Destroy(material);
        }
    }
}
