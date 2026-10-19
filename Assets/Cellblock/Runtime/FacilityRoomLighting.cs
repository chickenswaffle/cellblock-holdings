using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cellblock.Unity
{
    // Room lighting (Claude, 2026-10-18; user: "corner lights and wall lights, strategically designed so the shadows
    // and everything make sense"). Every light is a spot inside a visible, wall-attached fixture, aimed into the room:
    //   cells (< 14 m²)     one tamper-resistant corner luminaire, over the entry where there is a door;
    //   rooms (14–50 m²)    corner luminaires in the two most distant corners, wall packs on walls over 5 m;
    //   halls (> 50 m²)     wall packs about every 5 m around the perimeter, corner luminaires in square corners.
    // Fixtures mount just under the wall top, so the low cut-away view never shows a lamp hanging in the air. Shadows
    // are owned by RoomLightShadows, which gives them to the few lights nearest the camera.
    public sealed partial class FacilityView
    {
        const float SconceSpacing = 5f, WallInset = .17f;
        static readonly Color FixtureSteel = new Color(.30f, .33f, .34f), LampWarm = new Color(1f, .90f, .74f);
        float lightingWallHeight = -1;
        Mesh cornerHousing, cornerLens;

        bool RoomLightingStale { get { return !Mathf.Approximately(lightingWallHeight, WallHeight); } }

        void BuildRoomLighting(World world, Transform root, List<Light> lights, List<Renderer> lenses)
        {
            lightingWallHeight = WallHeight;
            float mount = Mathf.Min(2.45f, WallHeight - .2f);
            var doors = world.Data.Walls.Where(w => w.Door >= 0).Select(w => Geometry.OnLength(w, w.Door)).ToList();
            foreach (List<Point> room in world.Rooms)
            {
                if (room.Count < 3) continue;
                float signed = 0; for (int i = 0; i < room.Count; i++) { Point a = room[i], b = room[(i + 1) % room.Count]; signed += a.X * b.Y - b.X * a.Y; }
                float area = Mathf.Abs(signed) * .5f; float orientation = Mathf.Sign(signed);
                Point center = room.Aggregate(new Point(), (sum, p) => sum + p) * (1f / room.Count);
                List<int> corners = SquareCorners(room);
                if (area < 14)
                {
                    int corner = corners.Count == 0 ? 0 : corners.OrderBy(c => doors.Count == 0 ? 0 : doors.Min(d => Geometry.Distance(d, room[c]))).First();
                    CornerLight(root, room, corner, center, mount, 4.2f, 6.5f, lights, lenses);
                    continue;
                }
                if (area <= 50)
                {
                    // The pair of corners farthest apart lights the room from both ends.
                    var pair = corners.SelectMany(a => corners.Where(b => b > a).Select(b => new[] { a, b })).OrderByDescending(p => Geometry.Distance(room[p[0]], room[p[1]])).FirstOrDefault();
                    if (pair != null) foreach (int c in pair) CornerLight(root, room, c, center, mount, 4.8f, 8f, lights, lenses);
                    WallPacks(root, room, orientation, doors, mount, true, lights, lenses);
                    continue;
                }
                WallPacks(root, room, orientation, doors, mount, false, lights, lenses);
                foreach (int c in corners.Take(6)) CornerLight(root, room, c, center, mount, 5f, 8f, lights, lenses);
            }
        }

        // Polygon corners that turn sharply (a curved wall's facets are not corners).
        static List<int> SquareCorners(List<Point> room)
        {
            var result = new List<int>();
            for (int i = 0; i < room.Count; i++)
            {
                Point prev = room[(i + room.Count - 1) % room.Count], here = room[i], next = room[(i + 1) % room.Count];
                Vector2 a = new Vector2(here.X - prev.X, here.Y - prev.Y).normalized, b = new Vector2(next.X - here.X, next.Y - here.Y).normalized;
                if (Vector2.Dot(a, b) < .6f && Geometry.Distance(prev, here) > .6f && Geometry.Distance(here, next) > .6f) result.Add(i);
            }
            return result;
        }

        void CornerLight(Transform root, List<Point> room, int index, Point center, float mount, float intensity, float range, List<Light> lights, List<Renderer> lenses)
        {
            Point corner = room[index];
            Vector2 toCenter = new Vector2(center.X - corner.X, center.Y - corner.Y);
            if (toCenter.sqrMagnitude < .01f) return;
            toCenter.Normalize();
            // Tucked into the corner: the housing's apex sits at the wall faces' meeting point.
            Vector3 apex = new Vector3(corner.X + toCenter.x * WallInset * 1.41f, mount, corner.Y + toCenter.y * WallInset * 1.41f);
            Quaternion facing = Quaternion.LookRotation(new Vector3(toCenter.x, 0, toCenter.y));
            var fixture = new GameObject("Corner luminaire").transform; fixture.SetParent(root, false); fixture.position = apex; fixture.rotation = facing;
            if (cornerHousing == null) { cornerHousing = CornerMesh(.46f, .2f, false); cornerLens = CornerMesh(.40f, .02f, true); }
            var housing = new GameObject("Tamper-resistant housing"); housing.transform.SetParent(fixture, false);
            housing.AddComponent<MeshFilter>().sharedMesh = cornerHousing; housing.AddComponent<MeshRenderer>().sharedMaterial = Material("steel", FixtureSteel);
            var lens = new GameObject("Prismatic lens"); lens.transform.SetParent(fixture, false); lens.transform.localPosition = new Vector3(0, -.205f, .01f);
            lens.AddComponent<MeshFilter>().sharedMesh = cornerLens; var lensRenderer = lens.AddComponent<MeshRenderer>(); lensRenderer.sharedMaterial = Material("ceiling-on", new Color(.91f, .87f, .73f));
            lensRenderer.shadowCastingMode = ShadowCastingMode.Off; lenses.Add(lensRenderer);
            Spot(fixture, new Vector3(0, -.26f, .16f), Quaternion.Euler(40, 0, 0), intensity, range, 130, lights);
        }

        // Wall packs along the perimeter, clear of corners and doorways. Rooms only light walls longer than 5 m.
        void WallPacks(Transform root, List<Point> room, float orientation, List<Point> doors, float mount, bool longWallsOnly, List<Light> lights, List<Renderer> lenses)
        {
            for (int i = 0; i < room.Count; i++)
            {
                Point a = room[i], b = room[(i + 1) % room.Count];
                float length = Geometry.Distance(a, b);
                if (length < (longWallsOnly ? 5f : 2.4f)) continue;
                int count = Mathf.Max(1, Mathf.RoundToInt(length / SconceSpacing));
                Vector2 along = new Vector2(b.X - a.X, b.Y - a.Y) / length;
                // Interior side: left of travel for a counter-clockwise polygon.
                Vector2 inward = new Vector2(-along.y, along.x) * orientation;
                for (int k = 0; k < count; k++)
                {
                    float t = (k + .5f) / count;
                    Point at = a + (b - a) * t;
                    if (doors.Any(d => Geometry.Distance(d, at) < 1.3f)) continue;
                    if (t * length < 1 || (1 - t) * length < 1) continue;
                    Vector3 face = new Vector3(at.X + inward.x * WallInset, mount, at.Y + inward.y * WallInset);
                    var fixture = new GameObject("Wall pack").transform; fixture.SetParent(root, false); fixture.position = face;
                    fixture.rotation = Quaternion.LookRotation(new Vector3(inward.x, 0, inward.y));
                    Box(fixture, "Cast housing", new Vector3(0, 0, .055f), new Vector3(.34f, .22f, .11f), "steel", FixtureSteel);
                    Box(fixture, "Hood", new Vector3(0, .12f, .075f), new Vector3(.38f, .03f, .15f), "steel", FixtureSteel);
                    Transform lens = Box(fixture, "Polycarbonate lens", new Vector3(0, -.025f, .114f), new Vector3(.28f, .13f, .012f), "ceiling-on", new Color(.91f, .87f, .73f));
                    Renderer lensRenderer = lens.GetComponent<Renderer>(); lensRenderer.shadowCastingMode = ShadowCastingMode.Off; lenses.Add(lensRenderer);
                    Spot(fixture, new Vector3(0, -.05f, .2f), Quaternion.Euler(38, 0, 0), 3.8f, 8f, 135, lights);
                }
            }
        }

        static void Spot(Transform fixture, Vector3 local, Quaternion aim, float intensity, float range, float angle, List<Light> lights)
        {
            var lamp = new GameObject("Interior light").AddComponent<Light>();
            lamp.transform.SetParent(fixture, false); lamp.transform.localPosition = local; lamp.transform.localRotation = aim;
            lamp.type = LightType.Spot; lamp.spotAngle = angle; lamp.innerSpotAngle = angle * .22f; lamp.range = range; lamp.intensity = intensity;
            lamp.color = LampWarm; lamp.shadows = LightShadows.None; lamp.shadowStrength = .85f; lamp.shadowBias = .04f; lamp.shadowNormalBias = .3f; lamp.shadowNearPlane = .12f;
            lights.Add(lamp);
        }

        // A right-triangle prism in local space: apex at the origin, legs running back along the two wall faces (±45°
        // from forward), hanging `depth` down. The lens variant is just its emissive underside.
        static Mesh CornerMesh(float leg, float depth, bool lensOnly)
        {
            float s = leg * .7071f;
            Vector3 apex = Vector3.zero, left = new Vector3(-s, 0, s), right = new Vector3(s, 0, s), down = Vector3.down * depth;
            var v = new List<Vector3>(); var t = new List<int>();
            // Both windings: a tiny closed fixture never shows a culled face, from any camera.
            System.Action<Vector3, Vector3, Vector3> tri = (p, q, r) => { int n = v.Count; v.Add(p); v.Add(q); v.Add(r); v.Add(p); v.Add(r); v.Add(q); t.Add(n); t.Add(n + 1); t.Add(n + 2); t.Add(n + 3); t.Add(n + 4); t.Add(n + 5); };
            if (lensOnly) tri(apex + down * .5f, right + down * .5f, left + down * .5f);
            else
            {
                tri(apex + down, right + down, left + down);           // underside
                tri(apex, left, right);                                // top (against the ceiling line)
                tri(left, left + down, right + down); tri(left, right + down, right); // front face toward the room
            }
            var mesh = new Mesh { name = lensOnly ? "Corner luminaire lens" : "Corner luminaire housing" };
            mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }

    // The few lights nearest the camera cast real shadows (each spot is one shadow-atlas slice); the rest light without
    // them. Close views and first person get believable shadows from the fixtures you can see; the overview stays cheap.
    public static class RoomLightShadows
    {
        public static void Assign(IList<Light> lights, Camera camera, bool close)
        {
            if (camera == null) return;
            int budget = close ? PresentationQuality.LightShadowBudget : 0;
            Vector3 eye = camera.transform.position;
            var ranked = lights.Where(l => l != null && l.enabled).OrderBy(l => (l.transform.position - eye).sqrMagnitude).ToList();
            for (int i = 0; i < ranked.Count; i++)
            {
                LightShadows want = i < budget ? LightShadows.Soft : LightShadows.None;
                if (ranked[i].shadows != want) ranked[i].shadows = want;
            }
        }
    }
}
