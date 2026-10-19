using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cellblock.Unity
{
    // Human models (Claude, 2026-10-18). Each person's capsule body is replaced by a skinned, anatomically modelled
    // human (Quaternius "Universal Base Characters", CC0) while the game's own rig stays in charge: every animation,
    // gait, gaze, pose, fight and escape system keeps writing the same pivots (waist, shoulders, elbows, hips, knees,
    // ankles, head), and just before rendering each frame the model's bones copy those pivots' rotations.
    //
    // Clothing comes from the body itself: triangles are split by the bone that drives them into skin, a top (shirt or
    // jumpsuit), trousers, a belt and shoes, the cloth regions pushed out 1–2.5 cm so they read as fabric. The top uses
    // the person's uniform material and the legs "trousers", so the classification jumpsuit recolouring in
    // FacilityLivesView keeps working. The head is its own renderer under the gaze pivot, so first person still hides
    // your own head. Hats, badges, vests, tools and props stay as they were.
    public sealed partial class FacilityView
    {
        sealed class BodyTemplate
        {
            public GameObject Prefab; public string[] BoneNames; public Transform RootBoneTemplate;
            public readonly Dictionary<string, Mesh> Regions = new Dictionary<string, Mesh>();
            public Mesh Eyes, Brows; public string[] EyeBones, BrowBones; public float Scale; public bool Female;
        }
        sealed class HumanRig
        {
            public Actor Actor; public Transform Instance;
            public Transform[] Bones; public Transform[] Sources; public float[] Blend; public Quaternion[] SourceRest, BoneRest;
        }
        readonly Dictionary<string, BodyTemplate> bodyTemplates = new Dictionary<string, BodyTemplate>();
        readonly List<HumanRig> humanRigs = new List<HumanRig>();
        HumanRigDriver humanDriver;
        public static bool HumanModelsEnabled = true;

        static readonly string[] FemaleNames = { "Maya", "Elena", "Priya", "Tasha", "Hannah", "Grace", "Nadia", "Renee", "Imani", "Lena", "Bree", "Sofia", "Ines" };

        // ---- templates (once per body) ------------------------------------------------------------------------------
        BodyTemplate Template(bool female)
        {
            string key = female ? "female" : "male"; BodyTemplate t;
            if (bodyTemplates.TryGetValue(key, out t)) return t;
            t = new BodyTemplate { Female = female }; bodyTemplates[key] = t;
            t.Prefab = Resources.Load<GameObject>("Characters/Superhero_" + (female ? "Female" : "Male") + "_FullBody");
            if (t.Prefab == null) return t;
            GameObject probe = Instantiate(t.Prefab); probe.SetActive(false); probe.transform.position = Vector3.zero;
            try
            {
                SkinnedMeshRenderer body = probe.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r => r.sharedMesh == null ? 0 : r.sharedMesh.vertexCount).FirstOrDefault();
                if (body == null || !body.sharedMesh.isReadable) { t.Prefab = null; return t; }
                t.BoneNames = body.bones.Select(b => b.name).ToArray();
                Transform thigh = Find(probe.transform, "thigh_l"), foot = Find(probe.transform, "foot_l");
                t.Scale = thigh != null && foot != null ? .83f / Mathf.Max(.5f, thigh.position.y - foot.position.y) : .94f;
                // Model-space rest positions (for the belt line) from a baked copy.
                var baked = new Mesh(); body.BakeMesh(baked, true);
                Vector3[] rest = baked.vertices.Select(v => body.transform.TransformPoint(v)).ToArray();
                foreach (string region in new[] { "head", "skin", "top", "topshort", "skinshort", "bottom", "belt", "shoes" })
                    t.Regions[region] = Region(body, rest, region);
                Destroy(baked);
                foreach (SkinnedMeshRenderer extra in probe.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (extra == body || extra.sharedMesh == null) continue;
                    if (extra.name.Contains("Eye") && !extra.name.Contains("brow")) { t.Eyes = extra.sharedMesh; t.EyeBones = extra.bones.Select(b => b.name).ToArray(); }
                    if (extra.name.Contains("brow")) { t.Brows = extra.sharedMesh; t.BrowBones = extra.bones.Select(b => b.name).ToArray(); }
                }
            }
            finally { Destroy(probe); }
            return t;
        }

        // Geometric cut lines in the scan's rest pose (T-pose, metres): hems fall on the mesh's edge loops, so sleeves,
        // collar, waistband and shoe tops are clean rings rather than the zigzag a bone-weight split gives.
        sealed class Cuts { public float Neck, BeltLow, BeltHigh, Ankle, Wrist, Elbow; }
        static Cuts CutsFor(SkinnedMeshRenderer body)
        {
            Transform Bone(string n) { return body.bones.FirstOrDefault(b => b.name.ToLowerInvariant() == n); }
            Transform neck = Bone("neck_01"), pelvis = Bone("pelvis"), foot = Bone("foot_l"), hand = Bone("hand_l"), upper = Bone("upperarm_l"), lower = Bone("lowerarm_l");
            return new Cuts
            {
                Neck = neck.position.y - .012f, BeltLow = pelvis.position.y + .02f, BeltHigh = pelvis.position.y + .075f,
                Ankle = foot.position.y + .045f, Wrist = Mathf.Abs(hand.position.x) - .035f, Elbow = Mathf.Abs(lower.position.x) - .03f,
            };
        }
        static bool ArmBone(string bone)
        {
            bone = bone.ToLowerInvariant();
            return bone.StartsWith("upperarm") || bone.StartsWith("lowerarm") || bone.StartsWith("hand") || bone.StartsWith("thumb") || bone.StartsWith("index") || bone.StartsWith("middle") || bone.StartsWith("ring") || bone.StartsWith("pinky");
        }
        // Each region is a band on one rest-pose axis: |x| along the T-posed arms, y on the torso and legs. Cloth bands
        // reach `Margin` into their neighbour (they lie over it), skin bands stop exactly on the hem.
        const float Margin = .011f;
        struct Band { public bool Arm; public bool UseX; public float Min, Max; public Band(bool arm, bool useX, float min, float max) { Arm = arm; UseX = useX; Min = min; Max = max; } }
        static Band[] BandsFor(string region, Cuts c)
        {
            const float Far = 9;
            switch (region)
            {
                case "skin": return new[] { new Band(true, true, c.Wrist, Far) };
                case "skinshort": return new[] { new Band(true, true, c.Elbow, Far) };
                case "top": return new[] { new Band(true, true, -Far, c.Wrist + Margin), new Band(false, false, c.BeltHigh - Margin, c.Neck + Margin) };
                case "topshort": return new[] { new Band(true, true, -Far, c.Elbow + Margin), new Band(false, false, c.BeltHigh - Margin, c.Neck + Margin) };
                case "head": return new[] { new Band(false, false, c.Neck, Far) };
                case "belt": return new[] { new Band(false, false, c.BeltLow - Margin * .5f, c.BeltHigh + Margin * .5f) };
                case "bottom": return new[] { new Band(false, false, c.Ankle - Margin, c.BeltHigh) };
                case "shoes": return new[] { new Band(false, false, -Far, c.Ankle + Margin * .5f) };
            }
            return new Band[0];
        }

        sealed class Vert { public Vector3 Pos, Rest; public Vector2 Uv; public Dictionary<int, float> Weights; }
        static Vert Lerp(Vert a, Vert b, float t)
        {
            var w = new Dictionary<int, float>();
            foreach (var p in a.Weights) w[p.Key] = p.Value * (1 - t);
            foreach (var p in b.Weights) { float have; w.TryGetValue(p.Key, out have); w[p.Key] = have + p.Value * t; }
            return new Vert { Pos = Vector3.Lerp(a.Pos, b.Pos, t), Rest = Vector3.Lerp(a.Rest, b.Rest, t), Uv = Vector2.Lerp(a.Uv, b.Uv, t), Weights = w };
        }
        // Sutherland–Hodgman against one side of an axis-aligned plane in rest space.
        static List<Vert> Clip(List<Vert> poly, bool useX, float limit, bool keepBelow)
        {
            var output = new List<Vert>();
            for (int i = 0; i < poly.Count; i++)
            {
                Vert a = poly[i], b = poly[(i + 1) % poly.Count];
                float fa = (useX ? Mathf.Abs(a.Rest.x) : a.Rest.y) - limit, fb = (useX ? Mathf.Abs(b.Rest.x) : b.Rest.y) - limit;
                bool ina = keepBelow ? fa <= 0 : fa >= 0, inb = keepBelow ? fb <= 0 : fb >= 0;
                if (ina) output.Add(a);
                if (ina != inb && Mathf.Abs(fa - fb) > 1e-7f) output.Add(Lerp(a, b, fa / (fa - fb)));
            }
            return output;
        }
        static BoneWeight Pack(Dictionary<int, float> weights)
        {
            var top = weights.Where(p => p.Value > 1e-5f).OrderByDescending(p => p.Value).Take(4).ToArray();
            float sum = top.Sum(p => p.Value); if (sum <= 0) sum = 1;
            var bw = new BoneWeight();
            if (top.Length > 0) { bw.boneIndex0 = top[0].Key; bw.weight0 = top[0].Value / sum; }
            if (top.Length > 1) { bw.boneIndex1 = top[1].Key; bw.weight1 = top[1].Value / sum; }
            if (top.Length > 2) { bw.boneIndex2 = top[2].Key; bw.weight2 = top[2].Value / sum; }
            if (top.Length > 3) { bw.boneIndex3 = top[3].Key; bw.weight3 = top[3].Value / sum; }
            return bw;
        }

        // One region as its own compact skinned mesh, cut exactly on its hems. Cloth is relaxed with its hems pinned
        // (so the scan's muscle detail doesn't read through and edges stay put), re-normalled and pushed out.
        static Mesh Region(SkinnedMeshRenderer body, Vector3[] restWorld, string region)
        {
            Mesh src = body.sharedMesh;
            Vector3[] v = src.vertices; Vector2[] uv = src.uv; BoneWeight[] w = src.boneWeights; int[] tri = src.triangles;
            string[] names = body.bones.Select(b => b.name).ToArray();
            Cuts cuts = CutsFor(body); Band[] bands = BandsFor(region, cuts);
            string kind = region == "topshort" ? "top" : region == "skinshort" ? "skin" : region;
            bool cloth = kind == "top" || kind == "bottom" || kind == "belt" || kind == "shoes";
            var verts = new Vert[v.Length];
            Vert Source(int i)
            {
                if (verts[i] != null) return verts[i];
                var d = new Dictionary<int, float>();
                void Add(int bone, float weight) { if (weight <= 0) return; float have; d.TryGetValue(bone, out have); d[bone] = have + weight; }
                Add(w[i].boneIndex0, w[i].weight0); Add(w[i].boneIndex1, w[i].weight1); Add(w[i].boneIndex2, w[i].weight2); Add(w[i].boneIndex3, w[i].weight3);
                return verts[i] = new Vert { Pos = v[i], Rest = restWorld[i], Uv = uv.Length > i ? uv[i] : Vector2.zero, Weights = d };
            }
            var outVerts = new List<Vert>(); var outIndex = new Dictionary<Vert, int>(); var tris = new List<int>();
            int Emit(Vert x) { int index; if (!outIndex.TryGetValue(x, out index)) { index = outVerts.Count; outIndex[x] = index; outVerts.Add(x); } return index; }
            for (int k = 0; k < tri.Length; k += 3)
            {
                bool arm = ArmBone(names[w[tri[k]].boneIndex0]) || ArmBone(names[w[tri[k + 1]].boneIndex0]) || ArmBone(names[w[tri[k + 2]].boneIndex0]);
                foreach (Band band in bands)
                {
                    if (band.Arm != arm) continue;
                    var poly = new List<Vert> { Source(tri[k]), Source(tri[k + 1]), Source(tri[k + 2]) };
                    poly = Clip(poly, band.UseX, band.Min, false); if (poly.Count < 3) continue;
                    poly = Clip(poly, band.UseX, band.Max, true); if (poly.Count < 3) continue;
                    int first = Emit(poly[0]);
                    for (int j = 1; j < poly.Count - 1; j++) { tris.Add(first); tris.Add(Emit(poly[j])); tris.Add(Emit(poly[j + 1])); }
                }
            }
            Vector3[] pos = outVerts.Select(x => x.Pos).ToArray();
            if (cloth && pos.Length > 0)
            {
                // Weld UV-seam duplicates by position: relaxation, hems and normals work on the welded surface, so the
                // copies on either side of a seam move together and no crack opens.
                var groupOf = new int[pos.Length]; var groups = new Dictionary<Vector3Int, int>(); var members = new List<List<int>>();
                for (int i = 0; i < pos.Length; i++)
                {
                    var key = new Vector3Int(Mathf.RoundToInt(pos[i].x * 20000), Mathf.RoundToInt(pos[i].y * 20000), Mathf.RoundToInt(pos[i].z * 20000));
                    int g; if (!groups.TryGetValue(key, out g)) { g = members.Count; groups[key] = g; members.Add(new List<int>()); }
                    groupOf[i] = g; members[g].Add(i);
                }
                int n = members.Count; var gp = new Vector3[n]; for (int g = 0; g < n; g++) gp[g] = pos[members[g][0]];
                var edgeUse = new Dictionary<long, int>();
                for (int k = 0; k < tris.Count; k += 3) for (int j = 0; j < 3; j++)
                {
                    int a = groupOf[tris[k + j]], b = groupOf[tris[k + (j + 1) % 3]]; if (a == b) continue;
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; int c; edgeUse.TryGetValue(key, out c); edgeUse[key] = c + 1;
                }
                var neighbours = new List<int>[n]; for (int g = 0; g < n; g++) neighbours[g] = new List<int>();
                var ring = Enumerable.Repeat(int.MaxValue, n).ToArray(); var frontier = new Queue<int>();
                foreach (var e in edgeUse)
                {
                    int a = (int)(e.Key >> 32), b = (int)(e.Key & 0xffffffff); neighbours[a].Add(b); neighbours[b].Add(a);
                    if (e.Value == 1) { if (ring[a] != 0) { ring[a] = 0; frontier.Enqueue(a); } if (ring[b] != 0) { ring[b] = 0; frontier.Enqueue(b); } }
                }
                // Distance in rings from the nearest hem: relaxation fades in over three rings so hems don't spike.
                while (frontier.Count > 0) { int g = frontier.Dequeue(); foreach (int q in neighbours[g]) if (ring[q] > ring[g] + 1) { ring[q] = ring[g] + 1; frontier.Enqueue(q); } }
                int passes = kind == "shoes" ? 10 : kind == "belt" ? 2 : 4;
                var next = new Vector3[n];
                for (int pass = 0; pass < passes; pass++)
                {
                    for (int g = 0; g < n; g++)
                    {
                        float weight = .5f * Mathf.Clamp01(ring[g] / 3f);
                        if (weight <= 0 || neighbours[g].Count == 0) { next[g] = gp[g]; continue; }
                        Vector3 sum = Vector3.zero; foreach (int q in neighbours[g]) sum += gp[q];
                        next[g] = Vector3.Lerp(gp[g], sum / neighbours[g].Count, weight);
                    }
                    var swap = gp; gp = next; next = swap;
                }
                var normal = new Vector3[n];
                for (int k = 0; k < tris.Count; k += 3)
                {
                    int a = groupOf[tris[k]], b = groupOf[tris[k + 1]], c = groupOf[tris[k + 2]];
                    Vector3 face = Vector3.Cross(gp[b] - gp[a], gp[c] - gp[a]); normal[a] += face; normal[b] += face; normal[c] += face;
                }
                // Outer layers push further, in the order they are worn: belt over shirt over trousers over shoes.
                float push = kind == "top" ? .016f : kind == "bottom" ? .012f : kind == "belt" ? .022f : .009f;
                for (int i = 0; i < pos.Length; i++) { int g = groupOf[i]; pos[i] = gp[g] + normal[g].normalized * push; }
            }
            var mesh = new Mesh { name = "Human " + region };
            if (pos.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(pos); mesh.SetUVs(0, outVerts.Select(x => x.Uv).ToList()); mesh.boneWeights = outVerts.Select(x => Pack(x.Weights)).ToArray();
            mesh.bindposes = src.bindposes; mesh.SetTriangles(tris, 0);
            // Unity's angle-weighted normals, then averaged across welded seam copies so lighting has no seam either.
            mesh.RecalculateNormals();
            {
                Vector3[] nn = mesh.normals; var sums = new Dictionary<Vector3Int, Vector3>();
                for (int i = 0; i < pos.Length; i++) { var key = new Vector3Int(Mathf.RoundToInt(pos[i].x * 20000), Mathf.RoundToInt(pos[i].y * 20000), Mathf.RoundToInt(pos[i].z * 20000)); Vector3 have; sums.TryGetValue(key, out have); sums[key] = have + nn[i]; }
                for (int i = 0; i < pos.Length; i++) nn[i] = sums[new Vector3Int(Mathf.RoundToInt(pos[i].x * 20000), Mathf.RoundToInt(pos[i].y * 20000), Mathf.RoundToInt(pos[i].z * 20000))].normalized;
                mesh.normals = nn;
            }
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root) { Transform found = Find(child, name); if (found != null) return found; }
            return null;
        }

        // ---- materials ----------------------------------------------------------------------------------------------
        Material SkinMaterial(bool female, Color tone)
        {
            string name = "skin-model-" + (female ? "f" : "m") + "-" + ColorUtility.ToHtmlStringRGB(tone);
            Material value; if (materials.TryGetValue(name, out value)) return value;
            Material template = Resources.Load<Material>("SurfaceMaterial") ?? Resources.Load<Material>("DetailedBuildingMaterial");
            value = new Material(template) { name = name, enableInstancing = true };
            // The scan's albedo is a light skin; the tone tints it toward each person's own.
            Color reference = new Color(.65f, .47f, .345f);
            value.color = new Color(tone.r / reference.r, tone.g / reference.g, tone.b / reference.b);
            value.mainTexture = Resources.Load<Texture2D>("Characters/body_" + (female ? "female" : "male") + "_a"); value.mainTextureScale = Vector2.one;
            Texture2D normal = Resources.Load<Texture2D>("Characters/body_" + (female ? "female" : "male") + "_n"), mask = Resources.Load<Texture2D>("Characters/body_" + (female ? "female" : "male") + "_m");
            if (normal != null) { value.SetTexture("_BumpMap", normal); value.SetFloat("_BumpScale", .8f); value.EnableKeyword("_NORMALMAP"); }
            if (mask != null) { value.SetTexture("_MetallicGlossMap", mask); value.SetTexture("_OcclusionMap", mask); value.SetFloat("_Smoothness", 1); value.EnableKeyword("_METALLICSPECGLOSSMAP"); value.EnableKeyword("_OCCLUSIONMAP"); }
            value.SetFloat("_DetailAlbedoMapScale", 0);
            materials[name] = value; return value;
        }
        Material HairMaterial(int style, Color color)
        {
            string name = "hair-model-" + style + "-" + ColorUtility.ToHtmlStringRGB(color);
            Material value; if (materials.TryGetValue(name, out value)) return value;
            Material template = Resources.Load<Material>("SurfaceMaterial") ?? Resources.Load<Material>("DetailedBuildingMaterial");
            value = new Material(template) { name = name, enableInstancing = true, color = color * 1.6f };
            value.mainTexture = Resources.Load<Texture2D>("Characters/hair_" + style + "_a"); value.mainTextureScale = Vector2.one;
            Texture2D normal = Resources.Load<Texture2D>("Characters/hair_" + style + "_n");
            if (normal != null) { value.SetTexture("_BumpMap", normal); value.EnableKeyword("_NORMALMAP"); }
            value.SetTexture("_MetallicGlossMap", null); value.DisableKeyword("_METALLICSPECGLOSSMAP"); value.SetFloat("_Smoothness", .32f);
            value.SetTexture("_OcclusionMap", null); value.DisableKeyword("_OCCLUSIONMAP"); value.SetFloat("_DetailAlbedoMapScale", 0);
            materials[name] = value; return value;
        }

        // ---- per person -----------------------------------------------------------------------------------------------
        // Called at the end of Person(), with the rig at rest. Leaves the capsule body untouched if anything is missing.
        void BuildHumanModel(Actor actor, string id, int seed, bool staff, string role, string uniformName, Color uniform, Color skin)
        {
            if (!HumanModelsEnabled || actor.Limbs == null || actor.Limbs.Length < 4 || actor.Head == null) return;
            string first = "";
            if (presentedWorld != null)
            {
                StaffMember member = staff ? presentedWorld.Data.Staff.FirstOrDefault(s => s.Id == id) : null;
                Resident resident = staff ? null : presentedWorld.Data.Residents.FirstOrDefault(r => r.Id == id);
                string full = member != null ? member.Name : resident != null ? resident.Name : "";
                first = full.Split(' ')[0];
            }
            bool female = staff && (FemaleNames.Contains(first) || (first == "Avery" || first == "Jordan") && seed % 2 == 0);
            BodyTemplate t = Template(female);
            if (t.Prefab == null || t.Regions.Count == 0) return;

            var existing = actor.Root.GetComponentsInChildren<Renderer>(true).ToList();
            GameObject instance = Instantiate(t.Prefab, actor.Model, false); instance.name = "Human model";
            instance.transform.localPosition = Vector3.zero; instance.transform.localRotation = Quaternion.Euler(0, 180, 0); instance.transform.localScale = Vector3.one * t.Scale;
            foreach (SkinnedMeshRenderer original in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Destroy(original.gameObject);
            var bones = new Dictionary<string, Transform>();
            foreach (Transform b in instance.GetComponentsInChildren<Transform>(true)) { if (!bones.ContainsKey(b.name)) bones[b.name] = b; string lower = b.name.ToLowerInvariant(); if (!bones.ContainsKey(lower)) bones[lower] = b; }
            Transform[] skeleton = t.BoneNames.Select(n => bones.ContainsKey(n) ? bones[n] : null).ToArray();
            if (skeleton.Any(b => b == null)) { Destroy(instance); return; }
            Transform pelvis = bones["pelvis"];

            // Clothes. Officers and other staff wear short sleeves about half the time; residents' jumpsuits too.
            bool shortSleeves = seed % 2 == 0;
            Material top = Material(uniformName, uniform), trousers = Material("trousers", new Color(0.30f, 0.34f, 0.33f));
            Material shoes = Material("rubber", new Color(.10f, .12f, .12f)), skinMaterial = SkinMaterial(female, skin);
            Skin(instance.transform, "Top", t.Regions[shortSleeves ? "topshort" : "top"], skeleton, pelvis, top);
            Skin(instance.transform, "Trousers", t.Regions["bottom"], skeleton, pelvis, trousers);
            Skin(instance.transform, "Belt", t.Regions["belt"], skeleton, pelvis, staff ? shoes : trousers);
            Skin(instance.transform, "Shoes", t.Regions["shoes"], skeleton, pelvis, shoes);
            Skin(instance.transform, "Hands", t.Regions[shortSleeves ? "skinshort" : "skin"], skeleton, pelvis, skinMaterial);
            // The head (and hair, eyes, brows) live under the gaze pivot so first person hides them from your own eyes.
            Skin(actor.Head, "Head", t.Regions["head"], skeleton, pelvis, skinMaterial);
            Color hairColor = seed % 5 == 0 ? new Color(.30f, .29f, .27f) : seed % 3 == 0 ? new Color(.17f, .12f, .08f) : new Color(.07f, .06f, .055f);
            if (t.Eyes != null) Skin(actor.Head, "Eyes", t.Eyes, t.EyeBones.Select(n => bones.ContainsKey(n) ? bones[n] : null).ToArray(), pelvis, EyeMaterial());
            if (t.Brows != null) Skin(actor.Head, "Brows", t.Brows, t.BrowBones.Select(n => bones.ContainsKey(n) ? bones[n] : null).ToArray(), pelvis, HairMaterial(1, hairColor));
            string[] styles = female ? new[] { "Hair_Buns", "Hair_Long", "Hair_BuzzedFemale" } : staff ? new[] { "Hair_SimpleParted", "Hair_Buzzed", "Hair_SimpleParted", "" } : new[] { "Hair_Buzzed", "Hair_Buzzed", "Hair_SimpleParted", "", "Hair_Buzzed" };
            string style = styles[seed / 7 % styles.Length];
            if (style != "") Hair(actor.Head, style, bones, pelvis, HairMaterial(style == "Hair_Long" || style == "Hair_Buns" ? 2 : 1, hairColor));
            if (!female && seed % 6 == 1) Hair(actor.Head, "Hair_Beard", bones, pelvis, HairMaterial(1, hairColor));

            // Hide the capsule body, face and limbs; keep hats, badges, vests, tools, the name tag and selection ring.
            foreach (Renderer r in existing)
            {
                if (r == null || Keep(actor, r)) continue;
                r.forceRenderingOff = true;
            }
            BindRig(actor, instance.transform, bones);
            RefitHeadwear(actor, bones["head"]);
            if (humanDriver == null) { humanDriver = people.gameObject.AddComponent<HumanRigDriver>(); humanDriver.View = this; }
        }

        static readonly string[] KeptMaterials = { "badge", "reflective", "hi-vis", "tool", "radio", "phone", "clipboard", "paper", "handle", "lamp", "camera", "tape", "package", "crate", "court" };
        static bool Keep(Actor actor, Renderer r)
        {
            if (actor.Marker != null && r.transform.IsChildOf(actor.Marker)) return true;
            if (actor.NameTag != null && r.gameObject == actor.NameTag.gameObject) return true;
            foreach (Transform prop in new[] { actor.WorkTool, actor.Cargo, actor.Workbench }) if (prop != null && r.transform.IsChildOf(prop)) return true;
            if (actor.Tools != null) foreach (Transform tool in actor.Tools) if (tool != null && r.transform.IsChildOf(tool)) return true;
            string n = r.gameObject.name;
            if (n == "Hard hat" || n == "Hat brim" || n == "Medical cap") return true;
            string m = r.sharedMaterial == null ? "" : r.sharedMaterial.name;
            foreach (string kept in KeptMaterials) if (m.StartsWith(kept)) return true;
            return false;
        }

        Material EyeMaterial()
        {
            Material value; if (materials.TryGetValue("eye-model", out value)) return value;
            value = new Material(Resources.Load<Material>("BuildingMaterial")) { name = "eye-model", enableInstancing = true, color = Color.white };
            value.mainTexture = Resources.Load<Texture2D>("Characters/eye_a"); value.SetFloat("_Smoothness", .75f);
            materials["eye-model"] = value; return value;
        }

        static void Skin(Transform parent, string name, Mesh mesh, Transform[] bones, Transform root, Material material)
        {
            if (mesh == null || mesh.vertexCount == 0 || bones.Any(b => b == null)) return;
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var r = go.AddComponent<SkinnedMeshRenderer>(); r.sharedMesh = mesh; r.bones = bones; r.rootBone = root; r.sharedMaterial = material;
            r.updateWhenOffscreen = false; r.skinnedMotionVectors = false; r.quality = SkinQuality.Bone2;
            r.localBounds = new Bounds(new Vector3(0, 0, 0), new Vector3(2.4f, 2.4f, 2.4f));
        }

        readonly Dictionary<string, (Mesh mesh, string[] bones)> hairMeshes = new Dictionary<string, (Mesh, string[])>();
        void Hair(Transform parent, string style, Dictionary<string, Transform> bones, Transform root, Material material)
        {
            (Mesh mesh, string[] names) hair;
            if (!hairMeshes.TryGetValue(style, out hair))
            {
                GameObject prefab = Resources.Load<GameObject>("Characters/" + style); hair = (null, null);
                if (prefab != null)
                {
                    SkinnedMeshRenderer source = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault();
                    if (source != null) hair = (source.sharedMesh, source.bones.Select(b => b.name).ToArray());
                }
                hairMeshes[style] = hair;
            }
            if (hair.mesh == null) return;
            Skin(parent, style, hair.mesh, hair.names.Select(n => bones.ContainsKey(n) ? bones[n] : null).ToArray(), root, material);
        }

        // Hard hats and medical caps were placed for the old head; seat them on the model's crown instead.
        void RefitHeadwear(Actor actor, Transform headBone)
        {
            SkinnedMeshRenderer head = actor.Head.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Head");
            if (head == null) return;
            float crown = actor.Root.position.y; var baked = new Mesh(); head.BakeMesh(baked, true);
            foreach (Vector3 v in baked.vertices) crown = Mathf.Max(crown, head.transform.TransformPoint(v).y);
            Destroy(baked);
            foreach (Transform part in actor.Root.GetComponentsInChildren<Transform>(true))
                if (part.name == "Hard hat" || part.name == "Hat brim" || part.name == "Medical cap")
                {
                    float lift = part.name == "Hat brim" ? -.035f : part.name == "Medical cap" ? -.02f : -.005f;
                    Vector3 at = new Vector3(headBone.position.x, crown + lift, headBone.position.z);
                    part.SetParent(headBone, true); part.position = at;
                }
        }

        // ---- the drive -------------------------------------------------------------------------------------------------
        void BindRig(Actor actor, Transform instance, Dictionary<string, Transform> b)
        {
            Transform model = actor.Model;
            // Match segment lengths to the game rig so hands reach grips and feet land on planted contacts.
            for (int side = 0; side < 2; side++)
            {
                string s = side == 0 ? "_l" : "_r";
                Stretch(b["upperarm" + s], b["lowerarm" + s], Vector3.Distance(actor.Limbs[side].position, actor.Forearms[side].position));
                Stretch(b["lowerarm" + s], b["hand" + s], Vector3.Distance(actor.Forearms[side].position, actor.Hands[side].position) * .92f);
                Stretch(b["thigh" + s], b["calf" + s], Vector3.Distance(actor.Limbs[2 + side].position, actor.Knees[side].position));
                Stretch(b["calf" + s], b["foot" + s], Vector3.Distance(actor.Knees[side].position, actor.Feet[side].position));
                // Arms hang like the rig's (the scan is in a T-pose); legs line up with the rig's hips and knees.
                Aim(b["upperarm" + s], b["lowerarm" + s], actor.Forearms[side].position - actor.Limbs[side].position);
                Aim(b["lowerarm" + s], b["hand" + s], actor.Hands[side].position - actor.Forearms[side].position);
                Aim(b["thigh" + s], b["calf" + s], actor.Knees[side].position - actor.Limbs[2 + side].position);
                Aim(b["calf" + s], b["foot" + s], actor.Feet[side].position - actor.Knees[side].position);
                // A relaxed hand: fingers curl a little toward the palm.
                foreach (string finger in new[] { "index", "middle", "ring", "pinky" })
                    for (int j = 1; j <= 3; j++) { Transform f; if (b.TryGetValue(finger + "_0" + j + s, out f)) f.localRotation *= Quaternion.Euler(0, 0, side == 0 ? -12 - j * 6 : 12 + j * 6); }
            }
            var pairs = new List<(Transform bone, Transform source, float blend)>
            {
                (b["spine_01"], actor.Body, .34f), (b["spine_02"], actor.Body, .67f), (b["spine_03"], actor.Body, 1),
                (b["neck_01"], actor.Head, .5f), (b["head"], actor.Head, 1),
            };
            for (int side = 0; side < 2; side++)
            {
                string s = side == 0 ? "_l" : "_r";
                pairs.Add((b["upperarm" + s], actor.Limbs[side], 1)); pairs.Add((b["lowerarm" + s], actor.Forearms[side], 1)); pairs.Add((b["hand" + s], actor.Forearms[side], 1));
                pairs.Add((b["thigh" + s], actor.Limbs[2 + side], 1)); pairs.Add((b["calf" + s], actor.Knees[side], 1)); pairs.Add((b["foot" + s], actor.Feet[side], 1));
            }
            Quaternion inverse = Quaternion.Inverse(model.rotation);
            var rig = new HumanRig { Actor = actor, Instance = instance, Bones = pairs.Select(p => p.bone).ToArray(), Sources = pairs.Select(p => p.source).ToArray(), Blend = pairs.Select(p => p.blend).ToArray() };
            rig.SourceRest = rig.Sources.Select(s => inverse * s.rotation).ToArray();
            rig.BoneRest = rig.Bones.Select(x => inverse * x.rotation).ToArray();
            humanRigs.Add(rig);
        }
        static void Stretch(Transform bone, Transform child, float length)
        {
            float current = Vector3.Distance(bone.position, child.position);
            if (current > .01f && length > .01f) child.localPosition *= length / current;
        }
        static void Aim(Transform bone, Transform child, Vector3 direction)
        {
            Vector3 now = child.position - bone.position;
            if (now.sqrMagnitude > 1e-6f && direction.sqrMagnitude > 1e-6f) bone.rotation = Quaternion.FromToRotation(now, direction) * bone.rotation;
        }

        // After every animator has written this frame's pose, before any camera renders.
        internal void ApplyHumanRigs()
        {
            for (int i = humanRigs.Count - 1; i >= 0; i--)
            {
                HumanRig rig = humanRigs[i];
                if (rig.Actor.Root == null || rig.Instance == null) { humanRigs.RemoveAt(i); continue; }
                if (!rig.Actor.Root.gameObject.activeInHierarchy) continue;
                Quaternion model = rig.Actor.Model.rotation, inverse = Quaternion.Inverse(model);
                for (int k = 0; k < rig.Bones.Length; k++)
                {
                    Transform source = rig.Sources[k]; if (source == null) continue;
                    Quaternion delta = inverse * source.rotation * Quaternion.Inverse(rig.SourceRest[k]);
                    if (rig.Blend[k] < .999f) delta = Quaternion.Slerp(Quaternion.identity, delta, rig.Blend[k]);
                    rig.Bones[k].rotation = model * delta * rig.BoneRest[k];
                }
            }
        }
    }

    // Owns the render-time hook for FacilityView's human rigs (FacilityView keeps its own OnDestroy).
    public sealed class HumanRigDriver : MonoBehaviour
    {
        internal FacilityView View;
        void OnEnable() { RenderPipelineManager.beginContextRendering += Apply; }
        void OnDisable() { RenderPipelineManager.beginContextRendering -= Apply; }
        void Apply(ScriptableRenderContext context, List<Camera> cameras) { if (View != null) View.ApplyHumanRigs(); }
    }
}
