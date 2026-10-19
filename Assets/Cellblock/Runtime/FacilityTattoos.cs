using System.Collections.Generic;
using UnityEngine;

namespace Cellblock.Unity
{
    // Tattoos (Claude, 2026-10-19): gang members and some other residents are inked. A 512² overlay (URP Lit detail
    // albedo, ×2: grey 0.5 leaves the skin as it is, darker is ink) is painted only where the skin texture maps the
    // forearms, upper arms, backs of the hands and the neck — found from the meshes' own bone weights and UVs, so the
    // ink follows the body exactly. Three styles: full sleeves, script bands, bold tribal strokes. Built once per
    // body and style; the material is shared by everyone with that skin tone and style.
    public sealed partial class FacilityView
    {
        const int TattooSize = 512;
        public const int TattooStyles = 3;
        readonly Dictionary<string, Texture2D> tattooTextures = new Dictionary<string, Texture2D>();

        Texture2D TattooTexture(BodyTemplate t, int style)
        {
            string key = (t.Female ? "f" : "m") + style;
            Texture2D texture;
            if (tattooTextures.TryGetValue(key, out texture)) return texture;
            var mask = new bool[TattooSize * TattooSize];
            // Arms and hands from the short-sleeved body (its skin submesh is last); the neck from the face (submesh 0).
            if (t.Regions.ContainsKey("bodyshort")) MarkSkin(t.Regions["bodyshort"], t.Regions["bodyshort"].subMeshCount - 1, t.BoneNames, mask, b => b.StartsWith("lowerarm") || b.StartsWith("upperarm") || b.StartsWith("hand"));
            if (style != 1 && t.Regions.ContainsKey("face")) MarkSkin(t.Regions["face"], 0, t.FaceBones, mask, b => b.StartsWith("neck"));
            var pixels = new Color32[TattooSize * TattooSize];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(128, 128, 128, 255);
            var random = new System.Random(9137 + style * 7919 + (t.Female ? 13 : 0));
            Color32 ink = new Color32(30, 36, 44, 255), faded = new Color32(58, 66, 74, 255);
            void Dot(int x, int y, int radius, Color32 c)
            {
                for (int dy = -radius; dy <= radius; dy++) for (int dx = -radius; dx <= radius; dx++)
                {
                    int px = x + dx, py = y + dy;
                    if (px < 0 || py < 0 || px >= TattooSize || py >= TattooSize || dx * dx + dy * dy > radius * radius) continue;
                    int i = py * TattooSize + px; if (mask[i]) pixels[i] = c;
                }
            }
            // Strokes only start on marked skin, so every stroke lands on the arm/neck islands.
            var starts = new List<int>(); for (int i = 0; i < mask.Length; i += 7) if (mask[i]) starts.Add(i);
            if (starts.Count > 0)
            {
                int strokes = style == 0 ? 220 : style == 1 ? 70 : 60;
                for (int s = 0; s < strokes; s++)
                {
                    int start = starts[random.Next(starts.Count)];
                    float x = start % TattooSize, y = start / TattooSize;
                    float angle = (float)(random.NextDouble() * Mathf.PI * 2), bend = (float)(random.NextDouble() - .5) * (style == 2 ? .22f : .5f);
                    int length = style == 1 ? 10 + random.Next(16) : style == 2 ? 30 + random.Next(40) : 8 + random.Next(30);
                    int width = style == 2 ? 3 + random.Next(3) : style == 1 ? 1 : 1 + random.Next(2);
                    if (style == 1) angle = random.Next(2) * Mathf.PI + (float)(random.NextDouble() - .5) * .25f; // lettering runs along the band
                    Color32 c = random.Next(4) == 0 ? faded : ink;
                    for (int k = 0; k < length; k++)
                    {
                        Dot(Mathf.RoundToInt(x), Mathf.RoundToInt(y), style == 2 ? Mathf.Max(1, Mathf.RoundToInt(width * Mathf.Sin(Mathf.PI * k / length))) : width, c);
                        angle += bend * (style == 1 ? (float)(random.NextDouble() - .5) * 4 : 1);
                        x += Mathf.Cos(angle); y += Mathf.Sin(angle);
                    }
                    if (style == 0 && random.Next(9) == 0) Dot(Mathf.RoundToInt(x), Mathf.RoundToInt(y), 3 + random.Next(4), ink); // filled motifs
                }
            }
            texture = new Texture2D(TattooSize, TattooSize, TextureFormat.RGB24, true) { name = "Tattoos " + key, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels); texture.Apply(true, true);
            tattooTextures[key] = texture; return texture;
        }

        // Rasterise, in UV space, the triangles of one submesh whose corners are mostly skinned to the chosen bones.
        static void MarkSkin(Mesh mesh, int submesh, string[] boneNames, bool[] mask, System.Func<string, bool> wanted)
        {
            if (mesh == null || submesh < 0 || submesh >= mesh.subMeshCount || !mesh.isReadable) return;
            Vector2[] uv = mesh.uv; BoneWeight[] w = mesh.boneWeights; int[] tris = mesh.GetTriangles(submesh);
            if (uv.Length != mesh.vertexCount || w.Length != mesh.vertexCount) return;
            bool Wanted(int i)
            {
                BoneWeight b = w[i]; float total = 0;
                if (b.boneIndex0 < boneNames.Length && wanted(boneNames[b.boneIndex0])) total += b.weight0;
                if (b.boneIndex1 < boneNames.Length && wanted(boneNames[b.boneIndex1])) total += b.weight1;
                if (b.boneIndex2 < boneNames.Length && wanted(boneNames[b.boneIndex2])) total += b.weight2;
                if (b.boneIndex3 < boneNames.Length && wanted(boneNames[b.boneIndex3])) total += b.weight3;
                return total > .5f;
            }
            for (int k = 0; k + 2 < tris.Length; k += 3)
            {
                int a = tris[k], b = tris[k + 1], c = tris[k + 2];
                if (!Wanted(a) || !Wanted(b) || !Wanted(c)) continue;
                Vector2 p = uv[a] * TattooSize, q = uv[b] * TattooSize, r = uv[c] * TattooSize;
                int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p.x, Mathf.Min(q.x, r.x)))), maxX = Mathf.Min(TattooSize - 1, Mathf.CeilToInt(Mathf.Max(p.x, Mathf.Max(q.x, r.x))));
                int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p.y, Mathf.Min(q.y, r.y)))), maxY = Mathf.Min(TattooSize - 1, Mathf.CeilToInt(Mathf.Max(p.y, Mathf.Max(q.y, r.y))));
                float area = (q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x);
                if (Mathf.Abs(area) < 1e-6f) continue;
                for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
                {
                    Vector2 s = new Vector2(x + .5f, y + .5f);
                    float u = ((q.x - s.x) * (r.y - s.y) - (q.y - s.y) * (r.x - s.x)) / area, v = ((r.x - s.x) * (p.y - s.y) - (r.y - s.y) * (p.x - s.x)) / area;
                    if (u >= -.01f && v >= -.01f && u + v <= 1.01f) mask[y * TattooSize + x] = true;
                }
            }
        }
    }
}
