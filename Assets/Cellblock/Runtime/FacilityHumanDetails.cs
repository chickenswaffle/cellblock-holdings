using System.Collections.Generic;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        // A continuous, adult-sized head with a flatter facial plane and a tapered mandible.
        // Low resolution rings replace the intersecting full-resolution primitive spheres.
        static readonly float[] HumanHeadY = { -.132f, -.119f, -.098f, -.071f, -.035f, .002f, .034f, .068f, .101f, .126f, .140f };
        static readonly float[] HumanHeadWidth = { .031f, .057f, .076f, .087f, .096f, .100f, .098f, .094f, .081f, .052f, .003f };
        static readonly float[] HumanHeadFront = { .057f, .084f, .099f, .105f, .106f, .099f, .094f, .089f, .072f, .043f, -.004f };
        static readonly float[] HumanHeadBack = { .036f, .062f, .084f, .097f, .107f, .114f, .116f, .109f, .094f, .061f, .004f };

        static Vector3 HumanHeadPoint(float angle, float y, float width)
        {
            int ring = 0;
            while (ring + 1 < HumanHeadY.Length - 1 && HumanHeadY[ring + 1] < y) ring++;
            float t = Mathf.InverseLerp(HumanHeadY[ring], HumanHeadY[ring + 1], y);
            float halfWidth = Mathf.Lerp(HumanHeadWidth[ring], HumanHeadWidth[ring + 1], t) * width;
            float front = Mathf.Lerp(HumanHeadFront[ring], HumanHeadFront[ring + 1], t);
            float back = Mathf.Lerp(HumanHeadBack[ring], HumanHeadBack[ring + 1], t);
            float cosine = Mathf.Cos(angle);
            return new Vector3(Mathf.Sin(angle) * halfWidth, y,
                cosine >= 0 ? front * Mathf.Pow(cosine, .52f) : -back * Mathf.Pow(-cosine, .90f));
        }

        static Vector3 HumanFacePoint(float x, float y, float width, float relief = .002f)
        {
            float halfWidth = HumanHeadPoint(Mathf.PI / 2, y, width).x;
            Vector3 point = HumanHeadPoint(Mathf.Asin(Mathf.Clamp(x / Mathf.Max(.001f, halfWidth), -.99f, .99f)), y, width);
            point.z += relief; return point;
        }

        void HumanEyeOpening(Transform parent, float x, float y, float width, string material, Color color)
        {
            const int sectors = 12;
            var vertices = new List<Vector3> { HumanFacePoint(x, y, width, .0035f) }; var triangles = new List<int>();
            for (int i = 0; i < sectors; i++)
            {
                float angle = i * Mathf.PI * 2 / sectors;
                vertices.Add(HumanFacePoint(x + Mathf.Cos(angle) * .015f, y + Mathf.Sin(angle) * .0048f, width, .0028f));
            }
            for (int i = 0; i < sectors; i++) { triangles.Add(0); triangles.Add(i + 1); triangles.Add((i + 1) % sectors + 1); }
            HumanMesh(parent, "Recessed eye opening", vertices, triangles, material, color);
        }

        Transform HumanMesh(Transform parent, string name, List<Vector3> vertices, List<int> indices, string material, Color color)
        {
            var mesh = new Mesh { name = "Generated human detail " + name };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var uv = new Vector2[vertices.Count];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(vertices[i].x, vertices[i].y);
            mesh.uv = uv;
            var item = new GameObject(name).transform; item.SetParent(parent, false);
            item.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Material(material, color);
            return item;
        }

        void HumanLoft(Transform parent, string name, Vector3[] rings, int sectors, string material, Color color)
        {
            // Each ring is stored as a centre (x,y,z), followed by local half-width/depth.
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            int count = rings.Length / 2;
            for (int row = 0; row < count; row++) for (int column = 0; column < sectors; column++)
            {
                float angle = column * Mathf.PI * 2 / sectors;
                vertices.Add(rings[row * 2] + new Vector3(Mathf.Sin(angle) * rings[row * 2 + 1].x, 0, Mathf.Cos(angle) * rings[row * 2 + 1].z));
            }
            for (int row = 0; row < count - 1; row++) for (int column = 0; column < sectors; column++)
            {
                int a = row * sectors + column, b = row * sectors + (column + 1) % sectors;
                triangles.Add(a); triangles.Add(b); triangles.Add(a + sectors);
                triangles.Add(b); triangles.Add(b + sectors); triangles.Add(a + sectors);
            }
            for (int cap = 0; cap < 2; cap++)
            {
                int start = cap == 0 ? 0 : (count - 1) * sectors, center = vertices.Count;
                vertices.Add(rings[(cap == 0 ? 0 : count - 1) * 2]);
                for (int column = 0; column < sectors; column++)
                {
                    triangles.Add(center); triangles.Add(start + (cap == 0 ? (column + 1) % sectors : column));
                    triangles.Add(start + (cap == 0 ? column : (column + 1) % sectors));
                }
            }
            HumanMesh(parent, name, vertices, triangles, material, color);
        }

        void HumanHeadShell(Transform parent, float width, string material, Color color)
        {
            const int sectors = 24;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int row = 0; row < HumanHeadY.Length; row++) for (int column = 0; column < sectors; column++)
                vertices.Add(HumanHeadPoint(column * Mathf.PI * 2 / sectors, HumanHeadY[row], width));
            for (int row = 0; row < HumanHeadY.Length - 1; row++) for (int column = 0; column < sectors; column++)
            {
                int a = row * sectors + column, b = row * sectors + (column + 1) % sectors;
                triangles.Add(a); triangles.Add(b); triangles.Add(a + sectors);
                triangles.Add(b); triangles.Add(b + sectors); triangles.Add(a + sectors);
            }
            for (int cap = 0; cap < 2; cap++)
            {
                int row = cap == 0 ? 0 : HumanHeadY.Length - 1, center = vertices.Count;
                vertices.Add(new Vector3(0, HumanHeadY[row], 0));
                for (int column = 0; column < sectors; column++)
                {
                    triangles.Add(center); triangles.Add(row * sectors + (cap == 0 ? (column + 1) % sectors : column));
                    triangles.Add(row * sectors + (cap == 0 ? column : (column + 1) % sectors));
                }
            }
            HumanMesh(parent, "Connected jaw, cheeks and cranium", vertices, triangles, material, color);
        }

        void HumanOval(Transform parent, string name, Vector3 center, Vector3 horizontal, Vector3 vertical, string material, Color color, float bulge = .001f)
        {
            const int sectors = 12;
            Vector3 normal = Vector3.Cross(horizontal, vertical).normalized;
            var vertices = new List<Vector3> { center + normal * bulge }; var triangles = new List<int>();
            for (int i = 0; i < sectors; i++)
            {
                float angle = i * Mathf.PI * 2 / sectors;
                vertices.Add(center + horizontal * Mathf.Cos(angle) + vertical * Mathf.Sin(angle));
            }
            for (int i = 0; i < sectors; i++) { triangles.Add(0); triangles.Add(i + 1); triangles.Add((i + 1) % sectors + 1); }
            HumanMesh(parent, name, vertices, triangles, material, color);
        }

        void HumanRibbon(Transform parent, string name, Vector3[] path, float width, string material, Color color, Vector3 normal)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 tangent = path[Mathf.Min(path.Length - 1, i + 1)] - path[Mathf.Max(0, i - 1)];
                Vector3 edge = Vector3.Cross(normal, tangent).normalized * width / 2;
                vertices.Add(path[i] - edge); vertices.Add(path[i] + edge);
                if (i == 0) continue;
                int a = (i - 1) * 2;
                triangles.Add(a); triangles.Add(a + 2); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(a + 2); triangles.Add(a + 3);
            }
            HumanMesh(parent, name, vertices, triangles, material, color);
        }

        void HumanPolygon(Transform parent, string name, Vector3[] outline, string material, Color color)
        {
            var vertices = new List<Vector3>(outline); var triangles = new List<int>();
            bool front = Vector3.Cross(outline[1] - outline[0], outline[2] - outline[0]).z >= 0;
            for (int i = 1; i < outline.Length - 1; i++)
            {
                triangles.Add(0); triangles.Add(front ? i : i + 1); triangles.Add(front ? i + 1 : i);
            }
            HumanMesh(parent, name, vertices, triangles, material, color);
        }

        void HumanHair(Transform parent, int seed, float width, string material, Color color)
        {
            const int sectors = 24, rows = 7;
            int style = seed % 4;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int row = 0; row < rows; row++) for (int column = 0; column < sectors; column++)
            {
                float t = row / (float)(rows - 1), angle = column * Mathf.PI * 2 / sectors, cosine = Mathf.Cos(angle);
                float edge = cosine > 0 ? Mathf.Lerp(.004f, .078f, Mathf.Pow(cosine, .8f)) : Mathf.Lerp(.004f, -.072f, -cosine);
                if (style == 3 && cosine > 0) edge += Mathf.Pow(Mathf.Abs(Mathf.Sin(angle)), 2) * .025f;
                Vector3 point = HumanHeadPoint(angle, Mathf.Lerp(.139f, edge, t), width);
                float lift = style == 1 ? .010f + Mathf.Sin(angle) * .004f : style == 2 ? .006f + Mathf.Sin(angle * 11 + t * 9) * .0015f : .003f;
                point += new Vector3(Mathf.Sin(angle), .45f, Mathf.Cos(angle)) * (.0035f + t * .001f);
                point.y += lift * (1 - t * .6f);
                vertices.Add(point);
            }
            // Rings run from the crown down to the hairline, so reverse the head-shell winding.
            for (int row = 0; row < rows - 1; row++) for (int column = 0; column < sectors; column++)
            {
                int a = row * sectors + column, b = row * sectors + (column + 1) % sectors;
                triangles.Add(a); triangles.Add(a + sectors); triangles.Add(b);
                triangles.Add(b); triangles.Add(a + sectors); triangles.Add(b + sectors);
            }
            int center = vertices.Count; Vector3 crown = Vector3.zero;
            for (int column = 0; column < sectors; column++) crown += vertices[column] / sectors;
            vertices.Add(crown + Vector3.up * .0005f);
            for (int column = 0; column < sectors; column++) { triangles.Add(center); triangles.Add(column); triangles.Add((column + 1) % sectors); }
            HumanMesh(parent, "Fitted hairline and crown", vertices, triangles, material, color);
        }

        void HumanBeard(Transform parent, float width, string material, Color color)
        {
            const int sectors = 16, rows = 4;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int row = 0; row < rows; row++) for (int column = 0; column <= sectors; column++)
            {
                float angle = Mathf.Lerp(-1.35f, 1.35f, column / (float)sectors);
                float upper = -.075f + Mathf.Pow(Mathf.Abs(angle) / 1.35f, 2) * .062f;
                float y = Mathf.Lerp(-.124f, upper, row / (float)(rows - 1));
                vertices.Add(HumanHeadPoint(angle, y, width) + new Vector3(Mathf.Sin(angle), -.2f, Mathf.Cos(angle)) * .0025f);
            }
            for (int row = 0; row < rows - 1; row++) for (int column = 0; column < sectors; column++)
            {
                int a = row * (sectors + 1) + column, b = a + 1;
                triangles.Add(a); triangles.Add(b); triangles.Add(a + sectors + 1);
                triangles.Add(b); triangles.Add(b + sectors + 1); triangles.Add(a + sectors + 1);
            }
            HumanMesh(parent, "Trimmed beard contour", vertices, triangles, material, color);
        }

        static Vector3 HumanShirtPoint(float x, float y, float relief = .003f, bool back = false)
        {
            float cap = Mathf.Clamp01((Mathf.Abs(y - 1.20f) - .145f) / .145f);
            float z = .125f * Mathf.Sqrt(Mathf.Max(0, 1 - cap * cap - x * x / (.19f * .19f)));
            return new Vector3(x, y, (back ? -1 : 1) * (z + relief));
        }

        void HumanClothFold(Transform parent, string name, Vector3[] path, float width, string cloth, Color color)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 tangent = path[Mathf.Min(path.Length - 1, i + 1)] - path[Mathf.Max(0, i - 1)];
                Vector3 normal = path[i].z < 0 ? Vector3.back : Vector3.forward;
                Vector3 edge = Vector3.Cross(normal, tangent).normalized * width / 2;
                vertices.Add(path[i] - edge); vertices.Add(path[i] + normal * .003f); vertices.Add(path[i] + edge);
                if (i == 0) continue;
                int a = (i - 1) * 3;
                for (int half = 0; half < 2; half++)
                {
                    triangles.Add(a + half); triangles.Add(a + half + 3); triangles.Add(a + half + 1);
                    triangles.Add(a + half + 1); triangles.Add(a + half + 3); triangles.Add(a + half + 4);
                }
            }
            HumanMesh(parent, name, vertices, triangles, cloth, color);
        }
    }
}
