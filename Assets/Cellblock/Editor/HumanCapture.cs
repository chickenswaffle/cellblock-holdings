using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cellblock.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cellblock.Editor
{
    // Close-up review of the skinned human models (Claude, 2026-10-18): an officer and a resident from the front, side
    // and back at rest, then the same people over several frames of ordinary walking. Builds/HumanReview/.
    //   Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.HumanCapture.Run
    [InitializeOnLoad]
    public static class HumanCapture
    {
        const string ActiveKey = "Cellblock.HumanCapture.Active", StageKey = "Cellblock.HumanCapture.Stage", WaitKey = "Cellblock.HumanCapture.Wait";
        const string Output = "Builds/HumanReview";
        static readonly List<string> notes = new List<string>();
        static HumanCapture() { EditorApplication.update += Tick; }
        public static void Run()
        {
            ProjectSetup.Validate(); SurfaceLibrary.Ensure(); LightingSetup.Ensure();
            EditorSceneManager.OpenScene("Assets/Cellblock/Scenes/Main.unity");
            Directory.CreateDirectory(Output); foreach (string f in Directory.GetFiles(Output, "*.png")) File.Delete(f);
            SessionState.SetBool(ActiveKey, true); SessionState.SetInt(StageKey, 0); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 4);
            EditorApplication.EnterPlaymode();
        }
        static void Next(float s) { SessionState.SetInt(StageKey, SessionState.GetInt(StageKey, 0) + 1); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + s); }
        static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || EditorApplication.isCompiling || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < SessionState.GetFloat(WaitKey, 0)) return;
            int stage = SessionState.GetInt(StageKey, 0);
            try
            {
                GameController game = UnityEngine.Object.FindFirstObjectByType<GameController>();
                var w = game.World;
                if (stage == 0) { game.StartArtPreview("county-maximum", "briar-county", 15 * 60); Next(2); return; }
                var officer = w.Data.Staff.First(s => s.Arrived && s.Role == "Officer"); var resident = w.Data.Residents.First(r => w.IsPresent(r));
                if (stage == 1)
                {
                    foreach (var (who, id) in new[] { ("officer", officer.Id), ("resident", resident.Id) })
                    {
                        Vector3 at; if (!game.PreviewPersonPosition(id, out at)) { notes.Add("no actor " + id); continue; }
                        var root = GameObject.Find(who == "officer" ? "Officer · " + id : "Resident · " + id);
                        float heading = root == null ? 0 : root.transform.eulerAngles.y;
                        foreach (var (view, yaw) in new[] { ("front", 0f), ("side", 90f), ("back", 180f) })
                        {
                            game.FrameArt(at + Vector3.up * .95f, 1.75f, heading + 180 + yaw, 8);
                            Render(game.ArtCamera, who + "-" + view);
                        }
                    }
                    w.Data.Speed = 1; Next(.1f); return;
                }
                if (stage >= 2 && stage < 8)
                {
                    Vector3 at; if (game.PreviewPersonPosition(officer.Id, out at)) { game.FrameArt(at + Vector3.up * .9f, 2.6f, 200, 10); Render(game.ArtCamera, "officer-walk-" + (stage - 1)); }
                    if (game.PreviewPersonPosition(resident.Id, out at)) { game.FrameArt(at + Vector3.up * .9f, 2.6f, 20, 10); Render(game.ArtCamera, "resident-walk-" + (stage - 1)); }
                    Next(.45f); return;
                }
                File.WriteAllText(Path.Combine(Output, "report.txt"), string.Join("\n", notes));
                Debug.Log("CELLBLOCK_HUMAN_CAPTURE_PASSED"); Finish(0);
            }
            catch (Exception e) { Debug.LogError("CELLBLOCK_HUMAN_CAPTURE_FAILED " + e); Finish(1); }
        }
        static void Render(Camera camera, string name)
        {
            var target = new RenderTexture(900, 1100, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; target.Create();
            float aspect = camera.aspect; camera.aspect = 900f / 1100;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; var img = new Texture2D(900, 1100, TextureFormat.RGB24, false); img.ReadPixels(new Rect(0, 0, 900, 1100), 0, 0); img.Apply(); RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(Output, name + ".png"), img.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(img);
            }
            finally { camera.aspect = aspect; camera.ResetAspect(); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
        static void Finish(int code) { SessionState.SetBool(ActiveKey, false); EditorApplication.isPlaying = false; if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(code); }
    }
}
