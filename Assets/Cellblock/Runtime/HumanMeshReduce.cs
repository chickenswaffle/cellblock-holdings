using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cellblock.Unity
{
    // Distance version of a skinned human mesh (Claude, 2026-10-19). Quadric-error half-edge collapse (Garland &
    // Heckbert): a vertex folds onto a neighbour that already exists, so every surviving vertex keeps its own position,
    // normal, UV and bone weights — no attribute is ever invented. Hems, region borders and UV seams are locked, so the
    // collar, cuffs, waistband and shoe tops keep their outline and textures don't tear. Collapses that would flip or
    // pinch a face, or make the surface non-manifold, are refused. Runs once per body when the first person is built.
    public static class HumanMeshReduce
    {
        // keep: fraction of triangles to keep (per whole mesh). Submeshes keep their order and materials.
        public static Mesh Reduce(Mesh source, float keep, string name)
        {
            Vector3[] pos = source.vertices; int n = pos.Length;
            var tris = new List<int[]>(); var sub = new List<int>();
            for (int s = 0; s < source.subMeshCount; s++)
            {
                int[] t = source.GetTriangles(s);
                for (int k = 0; k < t.Length; k += 3) { tris.Add(new[] { t[k], t[k + 1], t[k + 2] }); sub.Add(s); }
            }
            int target = Mathf.Max(4, Mathf.RoundToInt(tris.Count * keep));

            // Welded positions: a group with more than one vertex is a UV/region seam.
            var group = new int[n]; var groups = new Dictionary<Vector3Int, int>(); var groupSize = new List<int>();
            for (int i = 0; i < n; i++)
            {
                var key = new Vector3Int(Mathf.RoundToInt(pos[i].x * 20000), Mathf.RoundToInt(pos[i].y * 20000), Mathf.RoundToInt(pos[i].z * 20000));
                int g; if (!groups.TryGetValue(key, out g)) { g = groupSize.Count; groups[key] = g; groupSize.Add(0); }
                group[i] = g; groupSize[g]++;
            }
            var locked = new bool[n];
            for (int i = 0; i < n; i++) if (groupSize[group[i]] > 1) locked[i] = true;
            // Open edges (counted on the welded surface) are hems and borders.
            var edgeUse = new Dictionary<long, int>();
            foreach (int[] t in tris) for (int j = 0; j < 3; j++) { long e = EdgeKey(group[t[j]], group[t[(j + 1) % 3]]); int c; edgeUse.TryGetValue(e, out c); edgeUse[e] = c + 1; }
            var openGroup = new HashSet<int>();
            foreach (var pair in edgeUse) if (pair.Value == 1) { openGroup.Add((int)(pair.Key >> 32)); openGroup.Add((int)(pair.Key & 0xffffffff)); }
            for (int i = 0; i < n; i++) if (openGroup.Contains(group[i])) locked[i] = true;

            // Plane quadrics, area weighted.
            var q = new double[n][]; for (int i = 0; i < n; i++) q[i] = new double[10];
            var around = new List<int>[n]; for (int i = 0; i < n; i++) around[i] = new List<int>();
            for (int f = 0; f < tris.Count; f++)
            {
                int[] t = tris[f]; foreach (int v in t) around[v].Add(f);
                Vector3 cross = Vector3.Cross(pos[t[1]] - pos[t[0]], pos[t[2]] - pos[t[0]]); float area = cross.magnitude;
                if (area < 1e-12f) continue;
                Vector3 nn = cross / area; double d = -Vector3.Dot(nn, pos[t[0]]);
                AddPlane(q[t[0]], nn, d, area); AddPlane(q[t[1]], nn, d, area); AddPlane(q[t[2]], nn, d, area);
            }
            // Texture term: folding a onto b makes a's corners sample b's UV. Convert the UV jump to metres with the mesh's
            // own texel density and charge it like geometric error over a's surrounding area, so collapses that would drag
            // lips, nails or eye texture across skin cost far more than ones within an even patch.
            Vector2[] texture = source.uv; bool textured = texture.Length == n;
            var areaAround = new double[n]; double surface = 0, uvSurface = 0;
            foreach (int[] t in tris)
            {
                float a = Vector3.Cross(pos[t[1]] - pos[t[0]], pos[t[2]] - pos[t[0]]).magnitude * .5f; surface += a;
                foreach (int v in t) areaAround[v] += a / 3;
                if (textured) { Vector2 e1 = texture[t[1]] - texture[t[0]], e2 = texture[t[2]] - texture[t[0]]; uvSurface += Mathf.Abs(e1.x * e2.y - e1.y * e2.x) * .5f; }
            }
            double metresPerUv = textured && uvSurface > 1e-9 ? System.Math.Sqrt(surface / uvSurface) : 0;
            double TextureCost(int a, int b) { if (!textured) return 0; double d = (texture[a] - texture[b]).magnitude * metresPerUv; return UvWeight * d * d * areaAround[a]; }
            var alive = Enumerable.Repeat(true, tris.Count).ToArray(); int count = tris.Count;
            var touched = new int[n]; int pass = 0;
            var candidates = new List<(double cost, int from, int to)>();
            var neighbourA = new HashSet<int>(); var neighbourB = new HashSet<int>();
            while (count > target && pass < 40)
            {
                pass++; candidates.Clear();
                for (int f = 0; f < tris.Count; f++)
                {
                    if (!alive[f]) continue; int[] t = tris[f];
                    for (int j = 0; j < 3; j++)
                    {
                        int a = t[j], b = t[(j + 1) % 3];
                        if (!locked[a]) candidates.Add((Cost(q[a], q[b], pos[b]) + TextureCost(a, b), a, b));
                        if (!locked[b]) candidates.Add((Cost(q[b], q[a], pos[a]) + TextureCost(b, a), b, a));
                    }
                }
                if (candidates.Count == 0) break;
                candidates.Sort((x, y) => x.cost.CompareTo(y.cost));
                int collapsed = 0;
                foreach (var (cost, a, b) in candidates)
                {
                    if (count <= target) break;
                    if (touched[a] == pass || touched[b] == pass) continue;
                    // Link condition: an interior edge shares exactly two neighbours.
                    neighbourA.Clear(); neighbourB.Clear();
                    foreach (int f in around[a]) if (alive[f]) foreach (int v in tris[f]) if (v != a) neighbourA.Add(group[v]);
                    foreach (int f in around[b]) if (alive[f]) foreach (int v in tris[f]) if (v != b) neighbourB.Add(group[v]);
                    neighbourA.IntersectWith(neighbourB); if (neighbourA.Count != 2) continue;
                    // No flipped or sliver faces.
                    bool ok = true;
                    foreach (int f in around[a])
                    {
                        if (!alive[f]) continue; int[] t = tris[f]; if (t[0] == b || t[1] == b || t[2] == b) continue;
                        Vector3 p0 = pos[t[0]], p1 = pos[t[1]], p2 = pos[t[2]];
                        Vector3 before = Vector3.Cross(p1 - p0, p2 - p0);
                        if (t[0] == a) p0 = pos[b]; else if (t[1] == a) p1 = pos[b]; else p2 = pos[b];
                        Vector3 after = Vector3.Cross(p1 - p0, p2 - p0);
                        if (after.sqrMagnitude < before.sqrMagnitude * .01f || Vector3.Dot(before.normalized, after.normalized) < .5f) { ok = false; break; }
                    }
                    if (!ok) continue;
                    foreach (int f in around[a])
                    {
                        if (!alive[f]) continue; int[] t = tris[f];
                        if (t[0] == b || t[1] == b || t[2] == b) { alive[f] = false; count--; continue; }
                        for (int j = 0; j < 3; j++) if (t[j] == a) t[j] = b;
                        around[b].Add(f);
                        foreach (int v in t) touched[v] = pass;
                    }
                    around[a].Clear();
                    for (int k = 0; k < 10; k++) q[b][k] += q[a][k];
                    areaAround[b] += areaAround[a];
                    touched[a] = touched[b] = pass; collapsed++;
                }
                if (collapsed == 0) break;
            }

            // Compact.
            var remap = Enumerable.Repeat(-1, n).ToArray(); var order = new List<int>();
            var bySub = new List<int>[source.subMeshCount]; for (int s = 0; s < bySub.Length; s++) bySub[s] = new List<int>();
            for (int f = 0; f < tris.Count; f++)
            {
                if (!alive[f]) continue;
                foreach (int v in tris[f]) { if (remap[v] < 0) { remap[v] = order.Count; order.Add(v); } bySub[sub[f]].Add(remap[v]); }
            }
            Vector3[] normals = source.normals; Vector4[] tangents = source.tangents; Vector2[] uv = source.uv; BoneWeight[] weights = source.boneWeights;
            var mesh = new Mesh { name = name };
            if (order.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(order.Select(v => pos[v]).ToList());
            if (normals.Length == n) mesh.SetNormals(order.Select(v => normals[v]).ToList());
            if (tangents.Length == n) mesh.SetTangents(order.Select(v => tangents[v]).ToList());
            if (uv.Length == n) mesh.SetUVs(0, order.Select(v => uv[v]).ToList());
            if (weights.Length == n) mesh.boneWeights = order.Select(v => weights[v]).ToArray();
            mesh.bindposes = source.bindposes;
            mesh.subMeshCount = bySub.Length; for (int s = 0; s < bySub.Length; s++) mesh.SetTriangles(bySub[s], s);
            mesh.RecalculateBounds();
            return mesh;
        }

        const double UvWeight = 4;
        static long EdgeKey(int a, int b) { return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; }
        static void AddPlane(double[] q, Vector3 n, double d, double w)
        {
            double a = n.x, b = n.y, c = n.z;
            q[0] += w * a * a; q[1] += w * a * b; q[2] += w * a * c; q[3] += w * a * d;
            q[4] += w * b * b; q[5] += w * b * c; q[6] += w * b * d;
            q[7] += w * c * c; q[8] += w * c * d; q[9] += w * d * d;
        }
        static double Cost(double[] qa, double[] qb, Vector3 p)
        {
            double x = p.x, y = p.y, z = p.z;
            double a0 = qa[0] + qb[0], a1 = qa[1] + qb[1], a2 = qa[2] + qb[2], a3 = qa[3] + qb[3], a4 = qa[4] + qb[4];
            double a5 = qa[5] + qb[5], a6 = qa[6] + qb[6], a7 = qa[7] + qb[7], a8 = qa[8] + qb[8], a9 = qa[9] + qb[9];
            return a0 * x * x + 2 * a1 * x * y + 2 * a2 * x * z + 2 * a3 * x + a4 * y * y + 2 * a5 * y * z + 2 * a6 * y + a7 * z * z + 2 * a8 * z + a9;
        }
    }
}
