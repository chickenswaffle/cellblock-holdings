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
            public Mesh Eyes, Brows; public string[] EyeBones, BrowBones, FaceBones; public float Scale; public bool Female, HasEyes, HasBrows, HasLids, Baked;
        }
        sealed class HumanRig
        {
            public Actor Actor; public string Id; public Transform Instance, Pelvis, Spine;
            public Transform[] Bones; public Transform[] Sources; public float[] Blend; public bool[] Snap; public Quaternion[] SourceRest, BoneRest;
            public Vector3 PelvisRest, HipMidRest, TorsoRest, SpineRest; // model space
            public Transform[] KneeBones = new Transform[2], FootBones = new Transform[2], HandBones = new Transform[2], ElbowBones = new Transform[2];
            public readonly List<Finger>[] Fingers = { new List<Finger>(), new List<Finger>() };
            public readonly float[] Curl = { .25f, .25f };
            public float SoleRest; // ankle pivot height (model space) when standing
            public SkinnedMeshRenderer Body, Face; public Mesh BodyNear, BodyFar, FaceNear, FaceFar; public bool Far, WantFar, Lids; public float LidWeight = -1;
        }
        sealed class Finger { public Transform Bone; public Quaternion Rest; public Vector3 Axis; public float Max; public bool Thumb; }
        readonly Dictionary<string, BodyTemplate> bodyTemplates = new Dictionary<string, BodyTemplate>();
        readonly List<HumanRig> humanRigs = new List<HumanRig>();
        HumanRigDriver humanDriver;
        // -cellblockNoHumanModels (Claude's batch runs): keep the procedural bodies, to A/B a review or a cost.
        public static bool HumanModelsEnabled = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-cellblockNoHumanModels") < 0;
        public static double TemplateMilliseconds; // time spent preparing bodies (clothing, merge, distance meshes)
        // Distance meshes: triangle share kept, and the on-screen height (fraction of the screen) below which a person
        // switches to them — earlier on lower tiers. 15% hysteresis so nobody flickers at the boundary.
        const float FarKeep = .3f, FaceFarKeep = .5f;
        static float FarBelow { get { return PresentationQuality.Current == PresentationQuality.Tier.High ? .10f : PresentationQuality.Current == PresentationQuality.Tier.Medium ? .16f : .24f; } }
#if UNITY_EDITOR
        public static bool PreviewForceFar; // review: draw everyone with the distance meshes
        public static float PreviewCurl = -1, PreviewBlink = -1; // review: force every hand to one curl (0 open, .25 relaxed, .82 grip, 1 fist); every lid to one closure (0 open, 1 shut)
        public static int GeneratedHumanMeshes { get { return Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.StartsWith("Human ")); } }
#endif

        static readonly string[] FemaleNames = { "Maya", "Elena", "Priya", "Tasha", "Hannah", "Grace", "Nadia", "Renee", "Imani", "Lena", "Bree", "Sofia", "Ines" };

        // ---- templates (once per body) ------------------------------------------------------------------------------
        BodyTemplate Template(bool female)
        {
            string key = female ? "female" : "male"; BodyTemplate t;
            if (bodyTemplates.TryGetValue(key, out t)) return t;
            t = new BodyTemplate { Female = female }; bodyTemplates[key] = t;
            t.Prefab = Resources.Load<GameObject>("Characters/Superhero_" + (female ? "Female" : "Male") + "_FullBody");
            if (t.Prefab == null) return t;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            if (!(UseBakedHumans && LoadBaked(t, key))) Prepare(t, key);
            TemplateMilliseconds += timer.Elapsed.TotalMilliseconds;
            return t;
        }

        // Clothing regions, merged part groups and distance meshes, from the imported body. Players load these baked
        // (HumanBake, at build time); the editor prepares them from source so every review sees the current code.
        static void Prepare(BodyTemplate t, string key)
        {
            GameObject probe = Instantiate(t.Prefab); probe.SetActive(false); probe.transform.position = Vector3.zero;
            try
            {
                SkinnedMeshRenderer body = probe.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r => r.sharedMesh == null ? 0 : r.sharedMesh.vertexCount).FirstOrDefault();
                if (body == null || !body.sharedMesh.isReadable) { t.Prefab = null; return; }
                t.BoneNames = body.bones.Select(b => b.name).ToArray();
                Transform thigh = Find(probe.transform, "thigh_l"), foot = Find(probe.transform, "foot_l");
                t.Scale = thigh != null && foot != null ? .83f / Mathf.Max(.5f, thigh.position.y - foot.position.y) : .94f;
                // Model-space rest positions (for the belt line) from a baked copy.
                var baked = new Mesh(); body.BakeMesh(baked, true);
                Vector3[] rest = baked.vertices.Select(v => body.transform.TransformPoint(v)).ToArray();
                foreach (string region in new[] { "head", "skin", "top", "topshort", "skinshort", "bottom", "belt", "shoes" })
                    t.Regions[region] = Region(body, rest, region);
                Release(baked);
                foreach (SkinnedMeshRenderer extra in probe.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (extra == body || extra.sharedMesh == null) continue;
                    if (extra.name.Contains("Eye") && !extra.name.Contains("brow")) { t.Eyes = extra.sharedMesh; t.EyeBones = extra.bones.Select(b => b.name).ToArray(); }
                    if (extra.name.Contains("brow")) { t.Brows = extra.sharedMesh; t.BrowBones = extra.bones.Select(b => b.name).ToArray(); }
                }
                // One renderer per part group, not per region: crowds pay per skinned renderer (skinning dispatch,
                // culling, draw setup), so the clothes and hands are one mesh with a submesh per material, and the face,
                // eyes and brows another. Merged once per body; the region pieces are released.
                string[] noBones = t.BoneNames;
                foreach (bool shortSleeves in new[] { false, true })
                {
                    string[] order = { shortSleeves ? "topshort" : "top", "bottom", "belt", "shoes", shortSleeves ? "skinshort" : "skin" };
                    string[] merged;
                    t.Regions[shortSleeves ? "bodyshort" : "body"] = Merge("Human body" + (shortSleeves ? " short" : ""), order.Select(r => (t.Regions[r], noBones)).ToArray(), out merged);
                }
                var faceParts = new List<(Mesh, string[])> { (t.Regions["head"], noBones) };
                if (t.Eyes != null) faceParts.Add((t.Eyes, t.EyeBones));
                if (t.Brows != null) faceParts.Add((t.Brows, t.BrowBones));
                t.Regions["face"] = Merge("Human face", faceParts.ToArray(), out t.FaceBones);
                t.HasEyes = t.Eyes != null; t.HasBrows = t.Brows != null;
                if (t.HasEyes) t.HasLids = AddEyelids(t.Regions["face"], 1, System.Array.IndexOf(t.FaceBones, "Head"), body.transform.InverseTransformDirection(Vector3.up).normalized);
                foreach (string region in new[] { "head", "skin", "top", "topshort", "skinshort", "bottom", "belt", "shoes" }) { Release(t.Regions[region]); t.Regions.Remove(region); }
                // Ordinary bodies: the pack's sculpt is a bodybuilder; most people are average, slim or heavy (Reshape).
                Vector3 upAxis = body.transform.InverseTransformDirection(Vector3.up).normalized, ahead = FaceForward(t.Regions["face"], upAxis, t.HasEyes);
                foreach (string build in BodyBuilds) if (build != "")
                    foreach (string sleeve in new[] { "body", "bodyshort" }) t.Regions[sleeve + "_" + build] = Reshape(t.Regions[sleeve], t.BoneNames, build, upAxis, ahead);
                // Distance versions (about a third of the triangles) for people who are small on screen.
                var reduceTimer = System.Diagnostics.Stopwatch.StartNew();
                foreach (string key2 in BodyMeshKeys().Concat(new[] { "face" })) t.Regions[key2 + "-far"] = HumanMeshReduce.Reduce(t.Regions[key2], key2 == "face" ? FaceFarKeep : FarKeep, t.Regions[key2].name + " far");
#if UNITY_EDITOR
                foreach (string key2 in new[] { "body", "bodyshort", "body_slim", "face" }) Debug.Log("CELLBLOCK_HUMAN_LOD " + key + " " + key2 + " " + t.Regions[key2].vertexCount + " → " + t.Regions[key2 + "-far"].vertexCount + " vertices, " + t.Regions[key2].triangles.Length / 3 + " → " + t.Regions[key2 + "-far"].triangles.Length / 3 + " triangles (reduce " + reduceTimer.ElapsedMilliseconds + " ms total)");
#endif
            }
            finally { Release(probe); }
        }
        // ---- body builds ----------------------------------------------------------------------------------------------
        // "" is the pack's muscular sculpt; the others are reshaped from it at preparation time (and baked for players).
        static readonly string[] BodyBuilds = { "", "average", "slim", "heavy" };
        // Presentation age from identity (saves carry staff ages in personnel files; residents have none yet).
        static int PresentedAge(int seed, bool staff)
        {
            int roll = (seed / 7) % 100, spread = (seed / 101) % 100;
            if (staff) return 24 + spread * 36 / 100;
            if (roll < 9) return 60 + spread * 18 / 100;      // old-timers
            if (roll < 30) return 40 + spread * 20 / 100;
            return 19 + spread * 21 / 100;
        }
        static IEnumerable<string> BodyMeshKeys() { foreach (string sleeve in new[] { "body", "bodyshort" }) foreach (string build in BodyBuilds) yield return build == "" ? sleeve : sleeve + "_" + build; }
        // Mostly ordinary people: staff skew average and heavy, residents average and slim; a few of each are built.
        static string BuildFor(int seed, bool staff, bool old)
        {
            int roll = (seed / 13) % 100;
            if (old) return roll < 55 ? "slim" : roll < 80 ? "heavy" : "average";
            if (staff) return roll < 45 ? "average" : roll < 72 ? "heavy" : roll < 85 ? "slim" : "";
            return roll < 38 ? "average" : roll < 68 ? "slim" : roll < 84 ? "heavy" : "";
        }
        static Vector3 FaceForward(Mesh face, Vector3 up, bool eyes)
        {
            Vector3[] v = face.vertices;
            Vector3 head = Average(face.GetTriangles(0).Distinct().Select(i => v[i]));
            Vector3 front = eyes && face.subMeshCount > 1 ? Average(face.GetTriangles(1).Distinct().Select(i => v[i])) : head;
            Vector3 f = Vector3.ProjectOnPlane(front - head, up);
            return f.sqrMagnitude > 1e-8f ? f.normalized : Vector3.Cross(up, Vector3.right).normalized;
        }
        // Each vertex moves relative to the bones it is skinned to: radial distance from a limb's bone segment (or, for the
        // torso, horizontal distance from the spine) is scaled per region, and the heavy build gains a forward belly. The
        // neck fades back to the original so the separate head mesh still meets it exactly.
        static Mesh Reshape(Mesh source, string[] boneNames, string build, Vector3 up, Vector3 forward)
        {
            Matrix4x4[] bind = source.bindposes; int n = Mathf.Min(bind.Length, boneNames.Length);
            var at = new Vector3[n]; var index = new Dictionary<string, int>();
            for (int b = 0; b < n; b++) { at[b] = bind[b].inverse.MultiplyPoint3x4(Vector3.zero); index[boneNames[b]] = b; }
            int Bone(string name) { int i; return index.TryGetValue(name, out i) ? i : -1; }
            var child = new int[n]; for (int b = 0; b < n; b++) child[b] = -1;
            foreach (var (a, c) in new[] { ("pelvis", "spine_01"), ("spine_01", "spine_02"), ("spine_02", "spine_03"), ("spine_03", "neck_01"), ("neck_01", "Head"),
                ("clavicle_l", "upperarm_l"), ("clavicle_r", "upperarm_r"), ("upperarm_l", "lowerarm_l"), ("upperarm_r", "lowerarm_r"), ("lowerarm_l", "hand_l"), ("lowerarm_r", "hand_r"),
                ("thigh_l", "calf_l"), ("thigh_r", "calf_r"), ("calf_l", "foot_l"), ("calf_r", "foot_r") })
            { int i = Bone(a), j = Bone(c); if (i >= 0 && j >= 0) child[i] = j; }
            var factor = new float[n]; var belly = new float[n]; var torso = new bool[n];
            for (int b = 0; b < n; b++)
            {
                string name = boneNames[b]; factor[b] = 1;
                bool arm = name.StartsWith("upperarm") || name.StartsWith("lowerarm"), thigh = name.StartsWith("thigh"), calf = name.StartsWith("calf");
                torso[b] = name.StartsWith("spine") || name == "pelvis" || name.StartsWith("clavicle");
                switch (build)
                {
                    case "average": factor[b] = arm ? (name.StartsWith("upperarm") ? .72f : .82f) : name.StartsWith("clavicle") ? .74f : name == "spine_03" ? .84f : name == "spine_02" ? .92f : thigh ? .88f : calf ? .92f : 1; break;
                    case "slim": factor[b] = arm ? (name.StartsWith("upperarm") ? .64f : .76f) : name.StartsWith("clavicle") ? .68f : name == "spine_03" ? .78f : name == "spine_02" ? .85f : name == "spine_01" ? .9f : thigh ? .82f : calf ? .87f : name == "pelvis" ? .94f : 1; break;
                    case "heavy": factor[b] = arm ? .86f : name.StartsWith("clavicle") ? .84f : name == "spine_03" ? .98f : name == "spine_02" ? 1.12f : name == "spine_01" ? 1.16f : name == "pelvis" ? 1.08f : thigh ? 1.02f : 1;
                        belly[b] = name == "spine_01" ? .5f : name == "spine_02" ? .38f : name == "pelvis" ? .22f : 0; break;
                }
            }
            float neckY = Bone("neck_01") >= 0 ? Vector3.Dot(at[Bone("neck_01")], up) : float.MaxValue;
            float span = Bone("Head") >= 0 && Bone("pelvis") >= 0 ? Mathf.Abs(Vector3.Dot(at[Bone("Head")] - at[Bone("pelvis")], up)) : 1;
            Vector3[] v = source.vertices; BoneWeight[] w = source.boneWeights;
            Vector3 Shift(int b, Vector3 p)
            {
                if (b < 0 || b >= n || (factor[b] == 1 && belly[b] == 0)) return Vector3.zero;
                Vector3 c = at[b];
                if (child[b] >= 0) { Vector3 e = at[child[b]] - at[b]; float t = Mathf.Clamp01(Vector3.Dot(p - at[b], e) / Mathf.Max(1e-8f, e.sqrMagnitude)); c = at[b] + e * t; }
                Vector3 r = p - c; if (torso[b]) r = Vector3.ProjectOnPlane(r, up);
                Vector3 d = r * (factor[b] - 1);
                float front = Vector3.Dot(r, forward);
                if (belly[b] > 0 && front > 0) d += forward * front * belly[b];
                if (build != "heavy" && boneNames[b] == "spine_03" && front > 0) d -= forward * front * .08f; // flatter chest
                return d;
            }
            var result = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                BoneWeight bw = w[i]; Vector3 p = v[i];
                Vector3 d = Shift(bw.boneIndex0, p) * bw.weight0 + Shift(bw.boneIndex1, p) * bw.weight1 + Shift(bw.boneIndex2, p) * bw.weight2 + Shift(bw.boneIndex3, p) * bw.weight3;
                float y = Vector3.Dot(p, up), fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(neckY - .12f * span, neckY - .03f * span, y));
                result[i] = p + d * fade;
            }
            Mesh mesh = Object.Instantiate(source); mesh.name = source.name + " " + build;
            mesh.vertices = result; mesh.RecalculateBounds();
            return mesh;
        }

        static void Release(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        // ---- baked templates -----------------------------------------------------------------------------------------
        // Players use the meshes HumanBake writes into Resources/Characters/Baked before every build (generated, not
        // versioned). A missing or mismatched bake falls back to preparing from source.
#if UNITY_EDITOR
        public static bool UseBakedHumans = false;
#else
        public static bool UseBakedHumans = true;
#endif
        const int HumanBakeVersion = 3;
        static readonly string[] BakedMeshes = BodyMeshKeys().Concat(new[] { "face" }).SelectMany(k => new[] { k, k + "-far" }).ToArray();
        static bool LoadBaked(BodyTemplate t, string key)
        {
            TextAsset meta = Resources.Load<TextAsset>("Characters/Baked/" + key);
            if (meta == null) return false;
            string[] lines = meta.text.Split('\n');
            if (lines.Length < 5 || lines[0].Trim() != "v" + HumanBakeVersion) return false;
            var meshes = new Dictionary<string, Mesh>();
            foreach (string name in BakedMeshes)
            {
                Mesh mesh = Resources.Load<Mesh>("Characters/Baked/" + key + "-" + name);
                if (mesh == null) return false;
                meshes[name] = mesh;
            }
            t.Scale = float.Parse(lines[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            t.BoneNames = lines[2].Trim().Split(','); t.FaceBones = lines[3].Trim().Split(',');
            t.HasEyes = lines[4].Contains("eyes"); t.HasBrows = lines[4].Contains("brows"); t.HasLids = lines[4].Contains("lids");
            foreach (var pair in meshes) t.Regions[pair.Key] = pair.Value;
            t.Baked = true; return true;
        }
#if UNITY_EDITOR
        // HumanBake: prepare one body from source and hand back its meshes and the text that LoadBaked reads.
        public static Dictionary<string, Mesh> BakeHumanTemplate(bool female, out string meta)
        {
            var t = new BodyTemplate { Female = female, Prefab = Resources.Load<GameObject>("Characters/Superhero_" + (female ? "Female" : "Male") + "_FullBody") };
            meta = null; if (t.Prefab == null) return null;
            Prepare(t, female ? "female" : "male");
            if (t.Prefab == null || BakedMeshes.Any(n => !t.Regions.ContainsKey(n))) return null;
            meta = "v" + HumanBakeVersion + "\n" + t.Scale.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "\n" + string.Join(",", t.BoneNames) + "\n" + string.Join(",", t.FaceBones) + "\n" + (t.HasEyes ? "eyes " : "") + (t.HasBrows ? "brows " : "") + (t.HasLids ? "lids" : "") + "\n";
            return BakedMeshes.ToDictionary(n => n, n => t.Regions[n]);
        }
        // Review: the rig bound for this person carries their id (finger routing from action beats needs it), and what
        // each hand is heading for right now (0 open, .25 relaxed, .82 grip, 1 fist).
        public bool PreviewRigBound(string id) { return humanRigs.Any(r => r.Id == id && r.Actor.Root != null && r.Actor.Root.name.EndsWith(id)); }
        public float PreviewHandTarget(string id, int side) { HumanRig rig = humanRigs.FirstOrDefault(r => r.Id == id); return rig == null ? -1 : HandTarget(rig, side, presentedWorld); }
        public void PreviewResetHumanTemplates() { DisposeHumanModels(); }
        public static int PreviewBakedTemplates(FacilityView view) { return view.bodyTemplates.Values.Count(t => t.Baked); }
#endif

        // Geometric cut lines in the scan's rest pose (T-pose, metres): hems fall on the mesh's edge loops, so sleeves,
        // collar, waistband and shoe tops are clean rings rather than the zigzag a bone-weight split gives.
        sealed class Cuts { public float Neck, BeltLow, BeltHigh, Ankle, Wrist, Elbow; public Vector3 NeckAt; }
        static Cuts CutsFor(SkinnedMeshRenderer body)
        {
            Transform Bone(string n) { return body.bones.FirstOrDefault(b => b.name.ToLowerInvariant() == n); }
            Transform neck = Bone("neck_01"), pelvis = Bone("pelvis"), foot = Bone("foot_l"), hand = Bone("hand_l"), upper = Bone("upperarm_l"), lower = Bone("lowerarm_l");
            return new Cuts
            {
                Neck = neck.position.y - .012f, NeckAt = neck.position, BeltLow = pelvis.position.y + .02f, BeltHigh = pelvis.position.y + .075f,
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
        const float Margin = .011f, SoleLift = .0156f;
        // A band is the intersection of up to three conditions min <= field(p) <= max on rest-pose positions.
        sealed class Band
        {
            public bool Arm; public readonly List<(System.Func<Vector3, float> field, float min, float max)> Conditions = new List<(System.Func<Vector3, float>, float, float)>();
            public Band(bool arm) { Arm = arm; }
            public Band Where(System.Func<Vector3, float> field, float min, float max) { Conditions.Add((field, min, max)); return this; }
        }
        static float AbsX(Vector3 p) { return Mathf.Abs(p.x); }
        static float Y(Vector3 p) { return p.y; }
        // Crew neck: the collar dips at the front (the scan faces -Z) and rises a little at the back; only the neck
        // column (not the slope of the shoulders) belongs to the head region.
        static System.Func<Vector3, float> Collar(Cuts c) { return p => p.y - (c.Neck + Mathf.Clamp((p.z - c.NeckAt.z) / .10f, -1, 1) * .022f); }
        static System.Func<Vector3, float> NeckRadius(Cuts c) { return p => new Vector2(p.x - c.NeckAt.x, (p.z - c.NeckAt.z) * .85f).magnitude; }
        static Band[] BandsFor(string region, Cuts c)
        {
            const float Far = 9, Column = .095f, Shoulders = .05f;
            var collar = Collar(c); var radius = NeckRadius(c);
            float wrist = c.Wrist, elbow = c.Elbow;
            switch (region)
            {
                case "skin": return new[] { new Band(true).Where(AbsX, wrist, Far) };
                case "skinshort": return new[] { new Band(true).Where(AbsX, elbow, Far) };
                case "top": case "topshort":
                    float sleeve = region == "top" ? wrist : elbow;
                    return new[] {
                        new Band(true).Where(AbsX, -Far, sleeve + Margin),
                        new Band(false).Where(Y, c.BeltHigh - Margin, Far).Where(collar, -Far, Margin),
                        new Band(false).Where(collar, Margin, Far).Where(radius, Column - Margin, Far).Where(Y, -Far, c.Neck + Shoulders) };
                // The neck column, and everything above the shoulder slope (the whole head and face).
                case "head": return new[] { new Band(false).Where(collar, 0, Far).Where(radius, -Far, Column).Where(Y, -Far, c.Neck + Shoulders), new Band(false).Where(Y, c.Neck + Shoulders, Far) };
                case "belt": return new[] { new Band(false).Where(Y, c.BeltLow - Margin * .5f, c.BeltHigh + Margin * .5f) };
                case "bottom": return new[] { new Band(false).Where(Y, c.Ankle - Margin, c.BeltHigh) };
                case "shoes": return new[] { new Band(false).Where(Y, -Far, c.Ankle + Margin * .5f) };
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
        // Sutherland–Hodgman against one side of a rest-space scalar field (linear along each small edge).
        static List<Vert> Clip(List<Vert> poly, System.Func<Vector3, float> field, float limit, bool keepBelow)
        {
            var output = new List<Vert>();
            for (int i = 0; i < poly.Count; i++)
            {
                Vert a = poly[i], b = poly[(i + 1) % poly.Count];
                float fa = field(a.Rest) - limit, fb = field(b.Rest) - limit;
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
                    foreach (var cond in band.Conditions)
                    {
                        if (cond.min > -8) poly = Clip(poly, cond.field, cond.min, false);
                        if (poly.Count >= 3 && cond.max < 8) poly = Clip(poly, cond.field, cond.max, true);
                        if (poly.Count < 3) break;
                    }
                    if (poly.Count < 3) continue;
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
                if (kind == "shoes")
                {
                    // A shoe, not a foot: a flat sole at the ground with a small welt standing out around it.
                    Matrix4x4 toRest = body.transform.localToWorldMatrix, toMesh = toRest.inverse;
                    for (int i = 0; i < pos.Length; i++)
                    {
                        Vector3 r = toRest.MultiplyPoint3x4(pos[i]);
                        // The scan's ankle sits higher above its sole than the rig's planted ankle: compress the shoe
                        // below the ankle by that difference (measured by HumanCapture's planted-sole check) so a planted
                        // sole meets the ground instead of sinking ~15 mm into it.
                        if (r.y < cuts.Ankle) r.y += SoleLift * (1 - Mathf.Clamp01(r.y / cuts.Ankle));
                        if (r.y > .055f + SoleLift) { pos[i] = toMesh.MultiplyPoint3x4(r); continue; }
                        Vector3 side = toRest.MultiplyVector(normal[groupOf[i]]); side.y = 0;
                        float welt = Mathf.InverseLerp(.055f + SoleLift, .015f + SoleLift, r.y);
                        r += side.normalized * .007f * welt;
                        if (r.y < .012f + SoleLift) r.y = Mathf.Lerp(r.y, SoleLift - .002f, .85f);
                        pos[i] = toMesh.MultiplyPoint3x4(r);
                    }
                }
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

        // Concatenates skinned meshes into one, a submesh per part. Parts bound in another mesh space (the FBX eyes and
        // brows) are carried into the first part's space through a shared bone: B_first(bone)^-1 * B_part(bone).
        static Mesh Merge(string name, (Mesh mesh, string[] bones)[] parts, out string[] bones)
        {
            var names = new List<string>(parts[0].bones); var bind = new List<Matrix4x4>(parts[0].mesh.bindposes);
            var pos = new List<Vector3>(); var nor = new List<Vector3>(); var tan = new List<Vector4>(); var uv = new List<Vector2>(); var weights = new List<BoneWeight>();
            var submeshes = new List<int[]>();
            foreach (var (mesh, partBones) in parts)
            {
                Matrix4x4[] partBind = mesh.bindposes; Matrix4x4 into = Matrix4x4.identity;
                for (int b = 0; b < partBones.Length; b++)
                {
                    int u = names.IndexOf(partBones[b]);
                    if (u >= 0 && u < parts[0].bones.Length) { into = bind[u].inverse * partBind[b]; break; }
                }
                var remap = new int[partBones.Length];
                for (int b = 0; b < partBones.Length; b++)
                {
                    int u = names.IndexOf(partBones[b]);
                    if (u < 0) { u = names.Count; names.Add(partBones[b]); bind.Add(partBind[b] * into.inverse); }
                    remap[b] = u;
                }
                int offset = pos.Count; Vector3[] v = mesh.vertices, n = mesh.normals; Vector4[] t = mesh.tangents; Vector2[] tex = mesh.uv; BoneWeight[] w = mesh.boneWeights;
                for (int i = 0; i < v.Length; i++)
                {
                    pos.Add(into.MultiplyPoint3x4(v[i]));
                    nor.Add(n.Length == v.Length ? into.MultiplyVector(n[i]).normalized : Vector3.up);
                    if (t.Length == v.Length) { Vector3 d = into.MultiplyVector(t[i]).normalized; tan.Add(new Vector4(d.x, d.y, d.z, t[i].w)); } else tan.Add(new Vector4(1, 0, 0, 1));
                    uv.Add(tex.Length == v.Length ? tex[i] : Vector2.zero);
                    BoneWeight bw = w.Length == v.Length ? w[i] : new BoneWeight { weight0 = 1 };
                    bw.boneIndex0 = remap[bw.boneIndex0]; bw.boneIndex1 = remap[bw.boneIndex1]; bw.boneIndex2 = remap[bw.boneIndex2]; bw.boneIndex3 = remap[bw.boneIndex3];
                    weights.Add(bw);
                }
                var tris = new List<int>();
                for (int sub = 0; sub < mesh.subMeshCount; sub++) foreach (int index in mesh.GetTriangles(sub)) tris.Add(index + offset);
                submeshes.Add(tris.ToArray());
            }
            var merged = new Mesh { name = name };
            if (pos.Count > 65000) merged.indexFormat = IndexFormat.UInt32;
            merged.SetVertices(pos); merged.SetNormals(nor); merged.SetTangents(tan); merged.SetUVs(0, uv); merged.boneWeights = weights.ToArray(); merged.bindposes = bind.ToArray();
            merged.subMeshCount = submeshes.Count; for (int i = 0; i < submeshes.Count; i++) merged.SetTriangles(submeshes[i], i);
            merged.RecalculateBounds();
            bones = names.ToArray(); return merged;
        }

        // Eyelids (Claude, 2026-10-19). The pack's faces have no lid bones or blend shapes, so each eye gets a thin skin
        // shell just outside its eyeball (sphere fitted to the eye vertices), skinned to the head, with one "blink" blend
        // shape. At rest every row lies folded on the upper rim (zero area, nothing drawn); closed, the rows spread down
        // over the eye like an upper lid. In-between frames keep the lid on the sphere rather than cutting through the eye.
        static bool AddEyelids(Mesh face, int eyeSubmesh, int headBone, Vector3 up)
        {
            if (headBone < 0 || face.subMeshCount <= eyeSubmesh) return false;
            Vector3[] v = face.vertices;
            var eye = new HashSet<int>(face.GetTriangles(eyeSubmesh)); var skin = new HashSet<int>(face.GetTriangles(0));
            if (eye.Count < 32 || skin.Count == 0) return false;
            Vector3 head = Average(skin.Select(i => v[i])), eyes = Average(eye.Select(i => v[i]));
            Vector3 forward = Vector3.ProjectOnPlane(eyes - head, up).normalized, side = Vector3.Cross(up, forward).normalized;
            if (forward.sqrMagnitude < .5f) return false;
            Vector2[] uv = face.uv;
            var pos = new List<Vector3>(); var nor = new List<Vector3>(); var tan = new List<Vector4>(); var tex = new List<Vector2>();
            var closedPos = new List<Vector3>(); var tris = new List<int>();
            const int Columns = 14, Rows = 7; const int Frames = 4;
            const float Across = 58 * Mathf.Deg2Rad, Top = 52 * Mathf.Deg2Rad, Bottom = -30 * Mathf.Deg2Rad;
            var frames = new List<Vector3>[Frames]; for (int k = 0; k < Frames; k++) frames[k] = new List<Vector3>();
            float lastRadius = 0; Vector3 lastCentre = Vector3.zero;
            foreach (int s in new[] { -1, 1 })
            {
                var points = eye.Where(i => Mathf.Sign(Vector3.Dot(v[i] - eyes, side)) == s).Select(i => v[i]).ToList();
                Vector3 c; float r;
                if (points.Count < 16 || !FitSphere(points, out c, out r)) return false;
                // A sane eyeball: smaller than the gap between the eyes, and in front of the head's centre.
                if (lastRadius > 0 && (Mathf.Abs(r / lastRadius - 1) > .25f || (c - lastCentre).magnitude < 2 * r)) return false;
                if (Vector3.Dot(c - head, forward) <= 0) return false;
                lastRadius = r; lastCentre = c;
                float R = r * 1.035f;
                Vector3 On(float theta, float phi) { return c + R * (Mathf.Cos(phi) * (Mathf.Cos(theta) * forward + Mathf.Sin(theta) * side) + Mathf.Sin(phi) * up); }
                // Skin tone from the upper-lid crease under the brow (the lid rim itself is painted pink, inner lid).
                Vector3 lidSkin = c + R * (.6f * forward + 1.35f * up);
                int nearest = skin.OrderBy(i => (v[i] - lidSkin).sqrMagnitude).First();
                int start = v.Length + pos.Count;
                for (int row = 0; row < Rows; row++) for (int column = 0; column <= Columns; column++)
                {
                    float theta = Mathf.Lerp(-Across, Across, column / (float)Columns), shut = Mathf.Lerp(Top, Bottom, row / (float)(Rows - 1));
                    Vector3 rest = On(theta, Top); pos.Add(rest); nor.Add((rest - c).normalized);
                    Vector3 along = -Mathf.Sin(theta) * forward + Mathf.Cos(theta) * side; tan.Add(new Vector4(along.x, along.y, along.z, 1));
                    tex.Add(uv.Length == v.Length ? uv[nearest] : Vector2.zero);
                    for (int k = 0; k < Frames; k++) frames[k].Add(On(theta, Mathf.Lerp(Top, shut, (k + 1) / (float)Frames)));
                }
                for (int row = 0; row < Rows - 1; row++) for (int column = 0; column < Columns; column++)
                {
                    int a = start + row * (Columns + 1) + column, b = a + 1, d = a + Columns + 1, e = d + 1;
                    // Outward facing, checked on the closed lid.
                    Vector3 pa = frames[Frames - 1][a - v.Length], pb = frames[Frames - 1][b - v.Length], pd = frames[Frames - 1][d - v.Length];
                    bool outward = Vector3.Dot(Vector3.Cross(pb - pa, pd - pa), pa - c) > 0;
                    if (outward) { tris.Add(a); tris.Add(b); tris.Add(d); tris.Add(b); tris.Add(e); tris.Add(d); }
                    else { tris.Add(a); tris.Add(d); tris.Add(b); tris.Add(b); tris.Add(d); tris.Add(e); }
                }
            }
            var allPos = new List<Vector3>(v); allPos.AddRange(pos);
            var allNor = new List<Vector3>(); face.GetNormals(allNor); allNor.AddRange(nor);
            var allTan = new List<Vector4>(); face.GetTangents(allTan); allTan.AddRange(tan);
            var allUv = new List<Vector2>(); face.GetUVs(0, allUv); allUv.AddRange(tex);
            var weights = face.boneWeights.ToList(); weights.AddRange(pos.Select(_ => new BoneWeight { boneIndex0 = headBone, weight0 = 1 }));
            if (allNor.Count != allPos.Count || allTan.Count != allPos.Count || allUv.Count != allPos.Count || weights.Count != allPos.Count) return false;
            if (allPos.Count > 65000) face.indexFormat = IndexFormat.UInt32;
            face.SetVertices(allPos); face.SetNormals(allNor); face.SetTangents(allTan); face.SetUVs(0, allUv); face.boneWeights = weights.ToArray();
            int lidSubmesh = face.subMeshCount; face.subMeshCount = lidSubmesh + 1; face.SetTriangles(tris, lidSubmesh);
            for (int k = 0; k < Frames; k++)
            {
                var dv = new Vector3[allPos.Count]; var dn = new Vector3[allPos.Count];
                for (int i = 0; i < pos.Count; i++) dv[v.Length + i] = frames[k][i] - pos[i];
                face.AddBlendShapeFrame("blink", 100f * (k + 1) / Frames, dv, dn, null);
            }
            face.RecalculateBounds();
            return true;
        }
        static Vector3 Average(IEnumerable<Vector3> points) { Vector3 sum = Vector3.zero; int n = 0; foreach (Vector3 p in points) { sum += p; n++; } return n == 0 ? sum : sum / n; }
        // Least-squares sphere: |p|^2 = 2 c.p + k, solved about the points' mean for conditioning.
        static bool FitSphere(List<Vector3> points, out Vector3 centre, out float radius)
        {
            Vector3 mean = Average(points); var m = new double[4, 5];
            foreach (Vector3 q in points)
            {
                Vector3 p = q - mean; double[] row = { 2 * p.x, 2 * p.y, 2 * p.z, 1 }; double y = p.sqrMagnitude;
                for (int i = 0; i < 4; i++) { for (int j = 0; j < 4; j++) m[i, j] += row[i] * row[j]; m[i, 4] += row[i] * y; }
            }
            for (int col = 0; col < 4; col++)
            {
                int pivot = col; for (int r2 = col + 1; r2 < 4; r2++) if (System.Math.Abs(m[r2, col]) > System.Math.Abs(m[pivot, col])) pivot = r2;
                if (System.Math.Abs(m[pivot, col]) < 1e-14) { centre = mean; radius = 0; return false; }
                for (int j = 0; j < 5; j++) { double swap = m[col, j]; m[col, j] = m[pivot, j]; m[pivot, j] = swap; }
                for (int r2 = 0; r2 < 4; r2++)
                {
                    if (r2 == col) continue; double f = m[r2, col] / m[col, col];
                    for (int j = col; j < 5; j++) m[r2, j] -= f * m[col, j];
                }
            }
            double cx = m[0, 4] / m[0, 0], cy = m[1, 4] / m[1, 1], cz = m[2, 4] / m[2, 2], k = m[3, 4] / m[3, 3];
            double r2sum = k + cx * cx + cy * cy + cz * cz;
            centre = mean + new Vector3((float)cx, (float)cy, (float)cz); radius = r2sum > 0 ? (float)System.Math.Sqrt(r2sum) : 0;
            return radius > 0 && !float.IsNaN(radius);
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root) { Transform found = Find(child, name); if (found != null) return found; }
            return null;
        }

        // ---- materials ----------------------------------------------------------------------------------------------
        Material SkinMaterial(bool female, Color tone, BodyTemplate t = null, int tattoo = -1)
        {
            string name = "skin-model-" + (female ? "f" : "m") + "-" + ColorUtility.ToHtmlStringRGB(tone) + (tattoo >= 0 ? "-ink" + tattoo : "");
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
            if (tattoo >= 0 && t != null)
            {
                // Ink through the detail albedo slot (×2 around grey), on the same UVs as the skin (FacilityTattoos.cs).
                value.SetTexture("_DetailAlbedoMap", TattooTexture(t, tattoo)); value.SetTextureScale("_DetailAlbedoMap", Vector2.one);
                value.SetFloat("_DetailAlbedoMapScale", 1); value.EnableKeyword("_DETAIL_MULX2");
            }
            materials[name] = value; return value;
        }
        // The lids sample one skin texel, so they miss the occlusion the mask paints into the socket around them and
        // would read pale and glossy against it: the same skin, shaded down to the socket's level.
        Material LidMaterial(Material skin)
        {
            string name = skin.name + "-lid";
            Material value; if (materials.TryGetValue(name, out value)) return value;
            Color tone = skin.color * .58f; tone.a = 1;
            value = new Material(skin) { name = name, color = tone };
            value.SetFloat("_Smoothness", .45f);
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
            // Ink: most gang members and about one in five other residents; staff rarely (sleeves under the uniform).
            int tattoo = -1;
            if (!staff)
            {
                Resident inked = presentedWorld == null ? null : presentedWorld.Data.Residents.FirstOrDefault(r => r.Id == id);
                bool gang = inked != null && !string.IsNullOrEmpty(inked.Gang) && inked.Gang != "Unaffiliated";
                if (gang ? seed % 5 != 0 : seed % 5 == 2) tattoo = (seed / 17) % TattooStyles;
            }
            Material shoes = Material("rubber", new Color(.10f, .12f, .12f)), skinMaterial = SkinMaterial(female, skin, t, tattoo);
            // Who this is: an age (residents 19 to the 70s, a few old-timers), and an ordinary build for most people.
            int age = PresentedAge(seed, staff); bool old = age >= 60;
            string build = BuildFor(seed, staff, old);
            string bodyKey = (shortSleeves ? "bodyshort" : "body") + (build == "" ? "" : "_" + build);
            if (!t.Regions.ContainsKey(bodyKey)) bodyKey = shortSleeves ? "bodyshort" : "body";
            actor.Age = age;
            // Shoulder width follows the build: the rig pins the skeleton's shoulders to these pivots (0.23 suited only the
            // bodybuilder). Poses and reaches are solved from the pivots, so hands still land where they did.
            float shoulder = build == "slim" ? .19f : build == "average" ? .2f : build == "heavy" ? .21f : .23f;
            for (int arm = 0; arm < 2; arm++) { Vector3 p = actor.Limbs[arm].localPosition; actor.Limbs[arm].localPosition = new Vector3((arm == 0 ? -1 : 1) * shoulder, p.y, p.z); }
            SkinnedMeshRenderer bodyRenderer = Skin(instance.transform, "Body", t.Regions[bodyKey], skeleton, pelvis, top, trousers, staff ? shoes : trousers, shoes, skinMaterial);
            // The head (and hair, eyes, brows) live under the gaze pivot so first person hides them from your own eyes.
            Color hairColor = old ? (seed % 2 == 0 ? new Color(.72f, .71f, .69f) : new Color(.48f, .47f, .45f)) : age > 45 && seed % 3 == 0 ? new Color(.30f, .29f, .27f) :
                seed % 5 == 0 ? new Color(.30f, .29f, .27f) : seed % 3 == 0 ? new Color(.17f, .12f, .08f) : new Color(.07f, .06f, .055f);
            var faceMaterials = new List<Material> { skinMaterial }; if (t.HasEyes) faceMaterials.Add(EyeMaterial()); if (t.HasBrows) faceMaterials.Add(HairMaterial(1, hairColor)); if (t.HasLids) faceMaterials.Add(LidMaterial(skinMaterial));
            SkinnedMeshRenderer faceRenderer = Skin(actor.Head, "Head", t.Regions["face"], t.FaceBones.Select(n => bones.ContainsKey(n) ? bones[n] : null).ToArray(), pelvis, faceMaterials.ToArray());
            string[] styles = female ? new[] { "Hair_Buns", "Hair_Long", "Hair_BuzzedFemale" } : staff ? new[] { "Hair_SimpleParted", "Hair_Buzzed", "Hair_SimpleParted", "" } : new[] { "Hair_Buzzed", "Hair_Buzzed", "Hair_SimpleParted", "", "Hair_Buzzed" };
            string style = styles[seed / 7 % styles.Length];
            if (style != "") Hair(actor.Head, style, bones, pelvis, HairMaterial(style == "Hair_Long" || style == "Hair_Buns" ? 2 : 1, hairColor));
            if (!female && (seed % 6 == 1 || old && seed % 3 == 0)) Hair(actor.Head, "Hair_Beard", bones, pelvis, HairMaterial(1, hairColor));

            // Hide the capsule body, face and limbs; keep hats, badges, vests, tools, the name tag and selection ring.
            foreach (Renderer r in existing)
            {
                if (r == null || Keep(actor, r)) continue;
                r.forceRenderingOff = true;
            }
            int rigs = humanRigs.Count;
            BindRig(actor, id, instance.transform, bones);
            if (humanRigs.Count > rigs)
            {
                HumanRig rig = humanRigs[humanRigs.Count - 1];
                if (rig.Id != id) Debug.LogError("Human rig bound with id '" + rig.Id + "' for actor " + id + ".");
                rig.Body = bodyRenderer; rig.BodyNear = t.Regions[bodyKey]; rig.BodyFar = t.Regions[bodyKey + "-far"];
                rig.Face = faceRenderer; rig.FaceNear = t.Regions["face"]; rig.FaceFar = t.Regions["face-far"]; rig.Lids = t.HasLids;
            }
            RefitHeadwear(actor, bones["head"]);
            if (!staff && age >= 68 && seed % 3 == 0) AddWalker(actor); // some old-timers use a walking frame
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

        static SkinnedMeshRenderer Skin(Transform parent, string name, Mesh mesh, Transform[] bones, Transform root, params Material[] materials)
        {
            if (mesh == null || mesh.vertexCount == 0 || bones.Any(b => b == null)) return null;
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var r = go.AddComponent<SkinnedMeshRenderer>(); r.sharedMesh = mesh; r.bones = bones; r.rootBone = root; r.sharedMaterials = materials;
            r.updateWhenOffscreen = false; r.skinnedMotionVectors = false; r.quality = SkinQuality.Bone2;
            r.localBounds = new Bounds(new Vector3(0, 0, 0), new Vector3(2.4f, 2.4f, 2.4f));
            return r;
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
        // id is passed in: Person() builds the model before the actor is registered, so it cannot be looked up yet.
        void BindRig(Actor actor, string id, Transform instance, Dictionary<string, Transform> b)
        {
            Transform model = actor.Model;
            for (int side = 0; side < 2; side++)
            {
                string s = side == 0 ? "_l" : "_r";
                // The joints start on the rig's own pivots, with the rig's segment lengths, so knees, ankles and grips are
                // exactly where every planted-contact, prop and pose system puts them.
                b["upperarm" + s].position = actor.Limbs[side].position; b["thigh" + s].position = actor.Limbs[2 + side].position;
                Stretch(b["upperarm" + s], b["lowerarm" + s], Vector3.Distance(actor.Limbs[side].position, actor.Forearms[side].position));
                Stretch(b["lowerarm" + s], b["hand" + s], Vector3.Distance(actor.Forearms[side].position, actor.Hands[side].position) * HandReach);
                Stretch(b["thigh" + s], b["calf" + s], Vector3.Distance(actor.Limbs[2 + side].position, actor.Knees[side].position));
                Stretch(b["calf" + s], b["foot" + s], Vector3.Distance(actor.Knees[side].position, actor.Feet[side].position));
                // Arms hang like the rig's (the scan is in a T-pose); legs line up with the rig's hips and knees.
                Aim(b["upperarm" + s], b["lowerarm" + s], actor.Forearms[side].position - actor.Limbs[side].position);
                Aim(b["lowerarm" + s], b["hand" + s], actor.Hands[side].position - actor.Forearms[side].position);
                Aim(b["thigh" + s], b["calf" + s], actor.Knees[side].position - actor.Limbs[2 + side].position);
                Aim(b["calf" + s], b["foot" + s], actor.Feet[side].position - actor.Knees[side].position);
            }
            var pairs = new List<(Transform bone, Transform source, float blend, bool snap)>
            {
                (b["spine_01"], actor.Body, .34f, false), (b["spine_02"], actor.Body, .67f, false), (b["spine_03"], actor.Body, 1, false),
                (b["neck_01"], actor.Head, .5f, false), (b["head"], actor.Head, 1, false),
            };
            for (int side = 0; side < 2; side++)
            {
                string s = side == 0 ? "_l" : "_r";
                pairs.Add((b["upperarm" + s], actor.Limbs[side], 1, true)); pairs.Add((b["lowerarm" + s], actor.Forearms[side], 1, false)); pairs.Add((b["hand" + s], actor.Forearms[side], 1, false));
                pairs.Add((b["thigh" + s], actor.Limbs[2 + side], 1, true)); pairs.Add((b["calf" + s], actor.Knees[side], 1, false)); pairs.Add((b["foot" + s], actor.Feet[side], 1, false));
            }
            Quaternion inverse = Quaternion.Inverse(model.rotation);
            var rig = new HumanRig
            {
                Actor = actor, Id = id ?? "", Instance = instance, Pelvis = b["pelvis"], Spine = b["spine_01"],
                Bones = pairs.Select(p => p.bone).ToArray(), Sources = pairs.Select(p => p.source).ToArray(), Blend = pairs.Select(p => p.blend).ToArray(), Snap = pairs.Select(p => p.snap).ToArray(),
            };
            rig.SourceRest = rig.Sources.Select(x => inverse * x.rotation).ToArray();
            rig.BoneRest = rig.Bones.Select(x => inverse * x.rotation).ToArray();
            rig.PelvisRest = model.InverseTransformPoint(rig.Pelvis.position); rig.SpineRest = model.InverseTransformPoint(rig.Spine.position);
            rig.HipMidRest = model.InverseTransformPoint((actor.Limbs[2].position + actor.Limbs[3].position) * .5f);
            rig.TorsoRest = model.InverseTransformPoint(actor.Body.position);
            rig.SoleRest = actor.Root.InverseTransformPoint(actor.Feet[0].position).y;
            for (int side = 0; side < 2; side++)
            {
                string s = side == 0 ? "_l" : "_r";
                rig.KneeBones[side] = b["calf" + s]; rig.FootBones[side] = b["foot" + s]; rig.HandBones[side] = b["hand" + s]; rig.ElbowBones[side] = b["lowerarm" + s];
                // Curl axes from the hand's own geometry: each finger bends toward the palm (the side the thumb's base
                // is on gives the palm normal's sign), measured once in bind space.
                Transform hand = b["hand" + s];
                Transform index1, pinky1, middle1, thumb1;
                if (!b.TryGetValue("index_01" + s, out index1) || !b.TryGetValue("pinky_01" + s, out pinky1) || !b.TryGetValue("middle_01" + s, out middle1) || !b.TryGetValue("thumb_01" + s, out thumb1)) continue;
                Vector3 along = (middle1.position - hand.position).normalized, across = (index1.position - pinky1.position).normalized;
                Vector3 palm = Vector3.Cross(along, across).normalized;
                if (Vector3.Dot(palm, thumb1.position - middle1.position) < 0) palm = -palm; // palm faces the thumb's side of the hand
                foreach (string finger in new[] { "index", "middle", "ring", "pinky", "thumb" })
                    for (int j = 1; j <= 3; j++)
                    {
                        Transform f, child; if (!b.TryGetValue(finger + "_0" + j + s, out f)) continue;
                        child = f.childCount > 0 ? f.GetChild(0) : null; if (child == null) continue;
                        Vector3 dir = (child.position - f.position).normalized;
                        // Fingers fold toward the palm; the thumb folds across it, toward the middle finger's base.
                        Vector3 toward = finger == "thumb" ? (middle1.position - f.position).normalized : palm;
                        Vector3 axis = Vector3.Cross(dir, toward).normalized;
                        // Sign so a positive angle moves the tip toward the palm.
                        Vector3 test = Quaternion.AngleAxis(10, axis) * dir; if (Vector3.Dot(test, toward) < Vector3.Dot(dir, toward)) axis = -axis;
                        bool thumb = finger == "thumb";
                        rig.Fingers[side].Add(new Finger { Bone = f, Rest = f.localRotation, Axis = f.InverseTransformDirection(axis), Max = thumb ? (j == 1 ? 25 : 40) : j == 1 ? 72 : j == 2 ? 95 : 62, Thumb = thumb });
                    }
            }
            humanRigs.Add(rig);
        }
        const float HandReach = .92f;
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

        static readonly List<Renderer> gripBuffer = new List<Renderer>();
        // Finger targets: something parented to the hand's grip → grip; a recent strike → fist; a grab/shove/reach → open.
        float HandTarget(HumanRig rig, int side, World world)
        {
            Transform grip = rig.Actor.Hands[side];
            if (grip != null)
            {
                grip.GetComponentsInChildren(false, gripBuffer);
                foreach (Renderer r in gripBuffer) if (r.enabled && !r.forceRenderingOff) return .82f;
            }
            if (world != null && rig.Id != "")
            {
                IReadOnlyList<ActionBeat> beats = world.ActionBeats;
                for (int i = beats.Count - 1; i >= 0 && i >= beats.Count - 6; i--)
                {
                    ActionBeat beat = beats[i];
                    if (world.Data.Minute - beat.Minute > 1) break;
                    if (beat.ActorId != rig.Id) continue;
                    if (beat.Kind == "Strike" || beat.Kind == "Counter" || beat.Kind == "Swing") return 1;
                    if (beat.Kind == "Grab" || beat.Kind == "Shove" || beat.Kind == "Reach" || beat.Kind == "Cuff") return 0;
                }
            }
            return .25f; // relaxed
        }

        // After every animator has written this frame's pose, before any camera renders.
        internal void ApplyHumanRigs(Camera camera = null)
        {
            World world = presentedWorld; bool running = world != null && world.Data.Speed > 0;
            float farBelow = FarBelow, perMetre = 0; Vector3 eye = Vector3.zero;
            if (camera != null) { eye = camera.transform.position; perMetre = camera.orthographic ? 1 / (2 * camera.orthographicSize) : 1 / (2 * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad)); }
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            AlignChoreoRoots(dt); // ActionChoreo roots before the bones follow the pivots (FacilityChoreoRig.cs)
            for (int i = humanRigs.Count - 1; i >= 0; i--)
            {
                HumanRig rig = humanRigs[i];
                if (rig.Actor.Root == null || rig.Instance == null) { humanRigs.RemoveAt(i); continue; }
                if (!rig.Actor.Root.gameObject.activeInHierarchy) continue;
                if (camera != null)
                {
                    // Height of a 1.8 m person as a share of the screen.
                    float share = camera.orthographic ? 1.8f * perMetre : 1.8f * perMetre / Mathf.Max(.1f, Vector3.Distance(eye, rig.Actor.Root.position));
                    bool far = rig.Far ? share < farBelow * 1.15f : share < farBelow;
#if UNITY_EDITOR
                    if (PreviewForceFar) far = true;
#endif
                    // Only decided here: skinning buffers for this frame are already sized, so the mesh changes in
                    // LateUpdate (ApplyHumanDistance), before the next frame skins.
                    rig.WantFar = far;
                }
                Transform modelT = rig.Actor.Model;
                Quaternion model = modelT.rotation, inverse = Quaternion.Inverse(model);
                // The pelvis rides with the hips (crouch, kneel, stance, gait bob); the spine base with the waist pivot.
                Vector3 hipMid = modelT.InverseTransformPoint((rig.Actor.Limbs[2].position + rig.Actor.Limbs[3].position) * .5f);
                Vector3 hipShift = hipMid - rig.HipMidRest;
                rig.Pelvis.position = modelT.TransformPoint(rig.PelvisRest + hipShift);
                Vector3 torsoShift = modelT.InverseTransformPoint(rig.Actor.Body.position) - rig.TorsoRest;
                rig.Spine.position = modelT.TransformPoint(rig.SpineRest + torsoShift);
                for (int k = 0; k < rig.Bones.Length; k++)
                {
                    Transform source = rig.Sources[k]; if (source == null) continue;
                    if (rig.Snap[k]) rig.Bones[k].position = source.position;
                    Quaternion delta = inverse * source.rotation * Quaternion.Inverse(rig.SourceRest[k]);
                    if (rig.Blend[k] < .999f) delta = Quaternion.Slerp(Quaternion.identity, delta, rig.Blend[k]);
                    rig.Bones[k].rotation = model * delta * rig.BoneRest[k];
                }
                // Poses also move joints (reach, grip and stance pivots), not only rotate them: every limb joint sits on its
                // pivot, so the visible elbows, wrists, knees and ankles are exactly where contacts and props are.
                for (int side = 0; side < 2; side++)
                {
                    Transform elbow = rig.Actor.Forearms[side], grip = rig.Actor.Hands[side];
                    rig.ElbowBones[side].position = elbow.position;
                    rig.HandBones[side].position = elbow.position + (grip.position - elbow.position) * HandReach;
                    rig.KneeBones[side].position = rig.Actor.Knees[side].position;
                    rig.FootBones[side].position = rig.Actor.Feet[side].position;
                }
                for (int side = 0; side < 2; side++)
                {
                    // Paused: hold the pose exactly. Otherwise ease toward the target (about a tenth of a second).
                    if (running) rig.Curl[side] = Mathf.MoveTowards(rig.Curl[side], HandTarget(rig, side, world), dt * 7);
#if UNITY_EDITOR
                    if (PreviewCurl >= 0) rig.Curl[side] = PreviewCurl;
#endif
                    float curl = rig.Curl[side];
                    foreach (Finger f in rig.Fingers[side]) f.Bone.localRotation = f.Rest * Quaternion.AngleAxis(f.Max * (f.Thumb ? Mathf.Min(curl, .7f) : curl), f.Axis);
                }
#if UNITY_EDITOR
                if (HumanContact.Enabled && HumanContact.SoleCheck && Time.frameCount % 7 == i % 7) HumanContact.Sole(rig.Actor.Root, rig.Instance, rig.Actor.Feet, rig.SoleRest);
                if (HumanContact.Enabled) HumanContact.Sample(rig.Actor.Knees, rig.KneeBones, rig.Actor.Feet, rig.FootBones, rig.Actor.Forearms, rig.Actor.Hands, rig.HandBones, HandReach);
#endif
            }
            ApplyChoreoContacts(); // ActionChoreo hands and knee onto their targets, now every partner is posed (FacilityChoreoRig.cs)
            PlaceCuffs(); // handcuffs on the final wrists (FacilityCuffs.cs)
        }

#if UNITY_EDITOR
        // Crowd review: CPU cost of one full rig pass (every visible person), averaged over repeated passes.
        public double PreviewRigMilliseconds(int passes)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            bool sole = HumanContact.SoleCheck; HumanContact.SoleCheck = false;
            for (int i = 0; i < passes; i++) ApplyHumanRigs();
            HumanContact.SoleCheck = sole;
            return watch.Elapsed.TotalMilliseconds / passes;
        }
        public int PreviewHumanRigs { get { return humanRigs.Count; } }
        public int PreviewFarRigs { get { return humanRigs.Count(r => r.Far); } }
#endif
        internal void ApplyHumanDistance()
        {
            foreach (HumanRig rig in humanRigs)
            {
                if (rig.Body == null || rig.Face == null) continue;
                if (rig.WantFar != rig.Far)
                {
                    rig.Far = rig.WantFar; rig.Body.sharedMesh = rig.Far ? rig.BodyFar : rig.BodyNear; rig.Face.sharedMesh = rig.Far ? rig.FaceFar : rig.FaceNear;
                    rig.LidWeight = -1;
                }
                // Blinks follow the shared face-motion clock (the procedural lid overlay's closure); distance faces have
                // no lids to move. Only changes are written: an unchanged zero weight costs the skinning nothing.
                if (!rig.Lids || rig.Far || rig.Actor.Blink == null) continue;
                float lid = Mathf.Round(Mathf.Clamp01(rig.Actor.Blink.localScale.y) * 100);
#if UNITY_EDITOR
                if (PreviewBlink >= 0) lid = PreviewBlink * 100;
#endif
                if (lid != rig.LidWeight) { rig.Face.SetBlendShapeWeight(0, lid); rig.LidWeight = lid; }
            }
        }

        // Generated region meshes belong to this view; imported eye/hair/FBX meshes and Resources textures do not.
        internal void DisposeHumanModels()
        {
            // Baked templates are assets (Resources), not ours to destroy.
            foreach (BodyTemplate t in bodyTemplates.Values) if (!t.Baked) foreach (Mesh mesh in t.Regions.Values) if (mesh != null) Destroy(mesh);
            foreach (Texture2D ink in tattooTextures.Values) if (ink != null) Destroy(ink);
            if (cuffRing != null) Destroy(cuffRing); if (cuffLink != null) Destroy(cuffLink); cuffs.Clear();
            bodyTemplates.Clear(); hairMeshes.Clear(); humanRigs.Clear(); tattooTextures.Clear();
        }
    }

#if UNITY_EDITOR
    // Review only: distance between the visible skeleton's knees/ankles/hands and the rig pivots that contact, prop and
    // pose systems use, over every driven frame. Bones carry the skin, so this is the visible joint error.
    public static class HumanContact
    {
        public static int Frames; public static float Knee, Ankle, Hand; static double kneeSum, ankleSum, handSum;
        public static void Reset() { Frames = 0; Knee = Ankle = Hand = 0; kneeSum = ankleSum = handSum = 0; Soles = 0; SoleMin = 9; SoleMax = -9; soleSum = 0; soleSamples.Clear(); }
        // Lowest visible shoe point above the person's ground, sampled when a foot pivot is planted (within 1 cm of its
        // standing height). Measures the skinned mesh itself, not the hidden rig.
        // Off in ordinary Editor Play (no mesh baking or sampling distorts what you measure). Reviews switch it on:
        // HumanCapture in code, the shared feature reviews via -cellblockHumanContact on Claude's batch command line.
        public static bool Enabled = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-cellblockHumanContact") >= 0;
        public static bool SoleCheck = true; public static int Soles; public static float SoleMin = 9, SoleMax = -9; static double soleSum;
        static readonly Mesh baked = new Mesh(); static readonly List<float> soleSamples = new List<float>();
        static float Percentile(float p) { if (soleSamples.Count == 0) return 0; var sorted = soleSamples.OrderBy(x => x).ToList(); return sorted[Mathf.Clamp(Mathf.RoundToInt(p * (sorted.Count - 1)), 0, sorted.Count - 1)]; }
        internal static void Sole(Transform root, Transform instance, Transform[] feet, float rest)
        {
            // Upright standing or walking only: kneeling, sitting, lying and falls legitimately put toes below the ankle.
            if (Vector3.Dot(instance.up, Vector3.up) < .97f) return;
            bool[] planted = new bool[2];
            for (int s = 0; s < 2; s++)
            {
                Vector3 local = root.InverseTransformPoint(feet[s].position);
                planted[s] = Mathf.Abs(local.y - rest) < .01f && Vector3.Dot(feet[s].up, root.up) > .9f;
            }
            if (!planted[0] && !planted[1]) return;
            SkinnedMeshRenderer body = instance.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(r => r.name == "Body");
            if (body == null) return;
            body.BakeMesh(baked, true); Vector3[] vertices = baked.vertices; Matrix4x4 toWorld = body.transform.localToWorldMatrix;
            // Each planted foot on its own: only that shoe's vertices (near its ankle, horizontally) count, so a lifted
            // or pitched other foot cannot stand in for it.
            for (int s = 0; s < 2; s++)
            {
                if (!planted[s]) continue;
                Vector3 ankle = feet[s].position; float lowest = float.MaxValue;
                foreach (Vector3 v in vertices)
                {
                    Vector3 w = toWorld.MultiplyPoint3x4(v);
                    if (w.y < ankle.y + .02f && new Vector2(w.x - ankle.x, w.z - ankle.z).sqrMagnitude < .18f * .18f) lowest = Mathf.Min(lowest, w.y);
                }
                if (lowest == float.MaxValue) continue;
                float height = lowest - root.position.y;
                Soles++; soleSum += height; SoleMin = Mathf.Min(SoleMin, height); SoleMax = Mathf.Max(SoleMax, height);
                if (soleSamples.Count < 50000) soleSamples.Add(height);
            }
        }
        internal static void Sample(Transform[] knees, Transform[] kneeBones, Transform[] feet, Transform[] footBones, Transform[] forearms, Transform[] hands, Transform[] handBones, float reach)
        {
            Frames++;
            for (int s = 0; s < 2; s++)
            {
                float k = Vector3.Distance(knees[s].position, kneeBones[s].position), a = Vector3.Distance(feet[s].position, footBones[s].position);
                Vector3 wrist = forearms[s].position + (hands[s].position - forearms[s].position) * reach;
                float h = Vector3.Distance(wrist, handBones[s].position);
                Knee = Mathf.Max(Knee, k); Ankle = Mathf.Max(Ankle, a); Hand = Mathf.Max(Hand, h); kneeSum += k; ankleSum += a; handSum += h;
            }
        }
        public static string Report { get { int n = Mathf.Max(1, Frames * 2); return "samples " + Frames + " · knee max " + (Knee * 1000).ToString("0.0") + " mm (mean " + (kneeSum / n * 1000).ToString("0.00") + ") · ankle max " + (Ankle * 1000).ToString("0.0") + " mm (mean " + (ankleSum / n * 1000).ToString("0.00") + ") · wrist max " + (Hand * 1000).ToString("0.0") + " mm (mean " + (handSum / n * 1000).ToString("0.00") + ")" + (Soles > 0 ? " · planted sole " + (SoleMin * 1000).ToString("0") + "…" + (SoleMax * 1000).ToString("0") + " mm (mean " + (soleSum / Soles * 1000).ToString("0.0") + ", 5–95% " + (Percentile(.05f) * 1000).ToString("0") + "…" + (Percentile(.95f) * 1000).ToString("0") + " mm, " + Soles + " samples)" : ""); } }
    }
#endif

    // Owns the render-time hook for FacilityView's human rigs (FacilityView keeps its own OnDestroy).
    public sealed class HumanRigDriver : MonoBehaviour
    {
        internal FacilityView View;
        void OnEnable() { RenderPipelineManager.beginContextRendering += Apply; }
        void OnDisable()
        {
            RenderPipelineManager.beginContextRendering -= Apply;
#if UNITY_EDITOR
            if (HumanContact.Frames > 0) Debug.Log("CELLBLOCK_HUMAN_CONTACT " + HumanContact.Report);
#endif
        }
        void OnDestroy() { if (View != null) View.DisposeHumanModels(); }
        void LateUpdate() { if (View != null) View.ApplyHumanDistance(); }
        void Apply(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (View == null) return;
            Camera camera = null; foreach (Camera c in cameras) if (c != null && c.cameraType == CameraType.Game) { camera = c; break; }
            View.ApplyHumanRigs(camera);
        }
    }
}
