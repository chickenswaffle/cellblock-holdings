using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cellblock.Core;
using Cellblock.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cellblock.Editor
{
    // Population line-up (Claude, 2026-10-19): residents and staff stood in rows on open ground and photographed from the
    // front and close up, to judge builds, ages, skin tones, hair, tattoos and walkers. Builds/PeopleReview/. Batch only:
    //   Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.PeopleCapture.Run
    [InitializeOnLoad]
    public static class PeopleCapture
    {
        const string ActiveKey = "Cellblock.PeopleCapture.Active", StageKey = "Cellblock.PeopleCapture.Stage", WaitKey = "Cellblock.PeopleCapture.Wait";
        const string Output = "Builds/PeopleReview";
        static readonly List<string> notes = new List<string>(), errors = new List<string>();
        static PeopleCapture() { EditorApplication.update += Tick; }
        public static void Run()
        {
            if (!Application.isBatchMode) { Debug.LogError("CELLBLOCK_PEOPLE_CAPTURE refused: batch mode only."); return; }
            ProjectSetup.Validate(); SurfaceLibrary.Ensure(); LightingSetup.Ensure();
            EditorSceneManager.OpenScene("Assets/Cellblock/Scenes/Main.unity");
            Directory.CreateDirectory(Output); foreach (string f in Directory.GetFiles(Output, "*.png")) File.Delete(f);
            notes.Clear(); errors.Clear();
            SessionState.SetBool(ActiveKey, true); SessionState.SetInt(StageKey, 0); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 4);
            EditorApplication.EnterPlaymode();
        }
        static void Next(float s) { SessionState.SetInt(StageKey, SessionState.GetInt(StageKey, 0) + 1); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + s); }
        static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || EditorApplication.isCompiling || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < SessionState.GetFloat(WaitKey, 0)) return;
            int stage = SessionState.GetInt(StageKey, 0);
            GameController game = UnityEngine.Object.FindFirstObjectByType<GameController>();
            try
            {
                World w = game.World;
                if (stage == 0) { game.StartArtPreview("county-maximum", "briar-county", 15 * 60); Next(2); return; }
                if (stage == 1)
                {
                    // More people than the holding has, through the same hydration a transfer uses, so the mix shows.
                    var seeds = w.Data.Residents.ToList(); int serial = 0;
                    while (w.Data.Residents.Count < 16)
                    {
                        Resident like = seeds[serial % seeds.Count]; serial++;
                        w.Data.Residents.Add(new Resident { Id = "lineup-" + serial.ToString("x4"), Name = PopulationRules.NameFor(w.Data, 7000 + serial), Home = like.Home, Position = like.Position,
                            WalkSpeed = like.WalkSpeed, Gang = serial % 3 == 0 ? like.Gang : "Unaffiliated", Legal = new LegalProfile() });
                    }
                    PeopleDynamics.Hydrate(w.Data); JusticeRules.EnsureProfiles(w.Data);
                    var people = w.Data.Residents.Where(w.IsPresent).Take(16).ToList();
                    for (int i = 0; i < people.Count; i++) people[i].Position = new Point(-2 + (i % 8) * 1.0f, -15.5f - (i / 8) * 1.6f);
                    var staff = w.Data.Staff.Where(s => s.Arrived).Take(4).ToList();
                    for (int i = 0; i < staff.Count; i++) staff[i].Position = new Point(-2 + i * 1.0f, -18.9f);
                    notes.Add(people.Count + " residents, " + staff.Count + " staff in the line-up; gangs: " + string.Join(", ", people.Select(p => p.Gang).Distinct()));
                    w.Data.Speed = 0; game.PreviewAdvance(0); Next(2.5f); return;
                }
                if (stage == 2)
                {
                    Vector3 middle = new Vector3(1.5f, .95f, -17.2f);
                    game.FrameArt(middle, 9.5f, 180, 6); Render(game.ArtCamera, "lineup-front", 1800, 900);
                    game.FrameArt(middle + new Vector3(-2.2f, 0, 1.7f), 4.2f, 180, 6); Render(game.ArtCamera, "lineup-left", 1600, 900);
                    game.FrameArt(middle + new Vector3(2.2f, 0, 1.7f), 4.2f, 180, 6); Render(game.ArtCamera, "lineup-right", 1600, 900);
                    game.FrameArt(middle + new Vector3(0, 0, -.1f), 4.6f, 180, 6); Render(game.ArtCamera, "lineup-back-row", 1600, 900);
                    int walkers = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "Walker frame");
                    notes.Add("walking frames on site: " + walkers);
                    var bodies = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None).Where(r => r.name == "Body").ToList();
                    notes.Add("body meshes: " + string.Join(", ", bodies.GroupBy(r => r.sharedMesh == null ? "none" : r.sharedMesh.name).Select(g => g.Key + " ×" + g.Count())));
                    notes.Add("skin materials: " + string.Join(", ", bodies.Select(r => r.sharedMaterials.Last().name).Distinct().OrderBy(x => x)));
                    Finish(); return;
                }
            }
            catch (Exception e) { errors.Add(e.ToString()); Finish(); }
        }
        static void Render(Camera camera, string name, int width, int height)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; target.Create();
            float aspect = camera.aspect; camera.aspect = (float)width / height;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; var img = new Texture2D(width, height, TextureFormat.RGB24, false); img.ReadPixels(new Rect(0, 0, width, height), 0, 0); img.Apply(); RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(Output, name + ".png"), img.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(img);
            }
            finally { camera.aspect = aspect; camera.ResetAspect(); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
        static void Finish()
        {
            bool passed = errors.Count == 0;
            File.WriteAllText(Path.Combine(Output, "report.txt"), (passed ? "PASSED" : "FAILED") + "\n\nNotes:\n" + string.Join("\n", notes) + "\n\nErrors:\n" + string.Join("\n", errors));
            if (passed) Debug.Log("CELLBLOCK_PEOPLE_CAPTURE_PASSED"); else Debug.LogError("CELLBLOCK_PEOPLE_CAPTURE_FAILED " + string.Join(" | ", errors.Take(4)));
            SessionState.SetBool(ActiveKey, false); EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
