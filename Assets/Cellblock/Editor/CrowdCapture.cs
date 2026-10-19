using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Cellblock.Core;
using Cellblock.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace Cellblock.Editor
{
    // Crowd cost of the skinned human models (Claude). Grows one facility to 12, 48 and 96 residents and records, per
    // size: people built, skinned renderers and vertices, the time to build the new people, the CPU cost of one rig pass,
    // and a timed render. Desktop editor numbers only — they rank costs and catch regressions; phone frame rates come
    // from a device run. Builds/CrowdReview/. Batch only:
    //   Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.CrowdCapture.Run
    [InitializeOnLoad]
    public static class CrowdCapture
    {
        const string ActiveKey = "Cellblock.CrowdCapture.Active", StageKey = "Cellblock.CrowdCapture.Stage", WaitKey = "Cellblock.CrowdCapture.Wait";
        const string Output = "Builds/CrowdReview";
        static readonly int[] Sizes = { 12, 48, 96 };
        static readonly List<string> notes = new List<string>(), errors = new List<string>();
        static readonly List<double> rigCost = new List<double>();
        static double buildMs; static int serial;
        static CrowdCapture() { EditorApplication.update += Tick; }
        public static void Run()
        {
            // Claude's clone only: never open scenes, delete output or enter Play in an interactive (shared) editor.
            if (!Application.isBatchMode) { Debug.LogError("CELLBLOCK_CROWD_CAPTURE refused: batch mode only (run from Claude's clone)."); return; }
            ProjectSetup.Validate(); SurfaceLibrary.Ensure(); LightingSetup.Ensure(); HumanBake.Bake(); // measure what players load
            EditorSceneManager.OpenScene("Assets/Cellblock/Scenes/Main.unity");
            Directory.CreateDirectory(Output); foreach (string f in Directory.GetFiles(Output, "*.png")) File.Delete(f);
            notes.Clear(); errors.Clear(); rigCost.Clear(); serial = 0;
            SessionState.SetBool(ActiveKey, true); SessionState.SetInt(StageKey, 0); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 4);
            EditorApplication.EnterPlaymode();
        }
        static void Next(float s) { SessionState.SetInt(StageKey, SessionState.GetInt(StageKey, 0) + 1); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + s); }
        static void Check(bool ok, string what) { if (!ok) errors.Add(what); }

        static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || EditorApplication.isCompiling || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < SessionState.GetFloat(WaitKey, 0)) return;
            int stage = SessionState.GetInt(StageKey, 0);
            try
            {
                GameController game = UnityEngine.Object.FindFirstObjectByType<GameController>();
                FacilityView view = UnityEngine.Object.FindFirstObjectByType<FacilityView>();
                if (stage == 0) { FacilityView.UseBakedHumans = true; FacilityView.TemplateMilliseconds = 0; view.PreviewResetHumanTemplates(); game.StartArtPreview("county-maximum", "briar-county", 15 * 60); PresentationQuality.Apply(3); Next(2); return; }
                // Two stages per size: grow and build, then measure once the people have settled.
                int size = (stage - 1) / 2;
                if (size >= Sizes.Length) { Finish(); return; }
                var w = game.World;
                if ((stage - 1) % 2 == 0)
                {
                    w.Data.Speed = 0; Grow(w, Sizes[size]);
                    var watch = Stopwatch.StartNew(); game.PreviewAdvance(0); buildMs = watch.Elapsed.TotalMilliseconds;
                    Next(1.5f); return;
                }
                Measure(game, view, Sizes[size]);
                Next(.5f);
            }
            catch (Exception e) { errors.Add(e.ToString()); Finish(); }
        }

        // Extra residents beside existing ones, through the same hydration a transfer arrival uses.
        static void Grow(World w, int target)
        {
            var seeds = w.Data.Residents.Where(r => !r.Id.StartsWith("crowd-")).ToList();
            while (w.Data.Residents.Count < target)
            {
                Resident like = seeds[serial % seeds.Count]; serial++;
                float angle = serial * 2.39996f, radius = .7f + .35f * (serial / seeds.Count % 4);
                var at = like.Position + new Point(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                w.Data.Residents.Add(new Resident { Id = "crowd-" + serial.ToString("x4"), Name = PopulationRules.NameFor(w.Data, 9000 + serial), Home = like.Home, Position = at,
                    WalkSpeed = like.WalkSpeed, Impulsivity = like.Impulsivity, Discipline = like.Discipline, Patience = like.Patience, Stress = like.Stress, Legal = new LegalProfile() });
            }
            PeopleDynamics.Hydrate(w.Data); JusticeRules.EnsureProfiles(w.Data);
        }

        static void Measure(GameController game, FacilityView view, int size)
        {
            view.PreviewRigMilliseconds(5); // warm
            double rig = view.PreviewRigMilliseconds(60); rigCost.Add(rig);
            // Wide shot over the whole holding, rendered 6 times; the first is discarded (shader and buffer warm-up, and
            // the distance-mesh choice for this camera).
            Vector3 center = game.ArtCenter; game.FrameArt(center + Vector3.up, 16, 35, 38);
            var times = new List<double>();
            for (int i = 0; i < 6; i++) times.Add(Render(game.ArtCamera, i == 5 ? "crowd-" + size : null));
            double render = times.Skip(1).Average();
            var skinned = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None).Where(r => r.gameObject.activeInHierarchy).ToArray();
            int people = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "Human model");
            long vertices = skinned.Sum(r => (long)(r.sharedMesh == null ? 0 : r.sharedMesh.vertexCount));
            string line = size + " residents (" + game.World.Data.Staff.Count(s => s.Arrived) + " staff on site): " + people + " skinned people, " + view.PreviewHumanRigs + " rigs (" + view.PreviewFarRigs + " on distance meshes in the wide shot), " +
                skinned.Length + " skinned renderers (" + (skinned.Length / (float)Mathf.Max(1, people)).ToString("0.0") + " per person), " + (vertices / 1000) + "k skinned vertices · build " +
                buildMs.ToString("0") + " ms · rig pass " + rig.ToString("0.00") + " ms (" + (rig * 1000 / Mathf.Max(1, view.PreviewHumanRigs)).ToString("0") + " µs/person) · render 1600×900 High " + render.ToString("0.0") + " ms";
            notes.Add(line); Debug.Log("CELLBLOCK_CROWD " + line);
            if (size == Sizes[0])
            {
                int baked = FacilityView.PreviewBakedTemplates(view);
                notes.Add("bodies loaded from the build-time bake: " + baked + " · body preparation " + FacilityView.TemplateMilliseconds.ToString("0") + " ms");
                Check(baked > 0, "The crowd did not use the baked bodies players load.");
                // Where one person's vertices go.
                var parts = skinned.GroupBy(r => r.name).Select(g => g.Key + " " + (g.Sum(r => (long)r.sharedMesh.vertexCount) / g.Count()) + "×" + g.Count()).OrderBy(x => x);
                notes.Add("per part (vertices × renderers): " + string.Join(", ", parts));
            }
            Check(people >= size, "Only " + people + " people built for " + size + " residents.");
        }

        static double Render(Camera camera, string name)
        {
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 }; target.Create();
            float aspect = camera.aspect; camera.aspect = 1600f / 900; var img = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                var watch = Stopwatch.StartNew();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; img.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); img.Apply(); RenderTexture.active = null;
                double ms = watch.Elapsed.TotalMilliseconds; // includes the readback, which waits for the GPU
                if (name != null) File.WriteAllBytes(Path.Combine(Output, name + ".png"), img.EncodeToPNG());
                return ms;
            }
            finally { UnityEngine.Object.DestroyImmediate(img); camera.aspect = aspect; camera.ResetAspect(); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }

        static void Finish()
        {
            // The rig pass should grow about linearly with the people on screen, not faster.
            if (rigCost.Count == Sizes.Length && rigCost[0] > 0)
            {
                double growth = rigCost[Sizes.Length - 1] / rigCost[0], people = Sizes[Sizes.Length - 1] / (double)Sizes[0];
                notes.Add("rig pass growth " + growth.ToString("0.0") + "× for " + people.ToString("0") + "× residents");
            }
            bool passed = errors.Count == 0;
            File.WriteAllText(Path.Combine(Output, "report.txt"), (passed ? "PASSED" : "FAILED") + "\n\nDesktop editor measurements (not phone frame rates).\n\nNotes:\n" + string.Join("\n", notes) + "\n\nErrors:\n" + string.Join("\n", errors));
            if (passed) Debug.Log("CELLBLOCK_CROWD_CAPTURE_PASSED"); else Debug.LogError("CELLBLOCK_CROWD_CAPTURE_FAILED " + string.Join(" | ", errors.Take(4)));
            SessionState.SetBool(ActiveKey, false); PresentationQuality.Restore(); FacilityView.UseBakedHumans = false; EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
