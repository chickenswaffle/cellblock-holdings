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
    // Close-up review of the skinned human models (Claude). An officer and a resident in open ground from the front,
    // side and back; both hands at open / relaxed / grip / fist; walking frames; the visible-joint contact statistics;
    // and a view destroy/recreate check that generated meshes are released. Builds/HumanReview/. Batch only:
    //   Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.HumanCapture.Run
    [InitializeOnLoad]
    public static class HumanCapture
    {
        const string ActiveKey = "Cellblock.HumanCapture.Active", StageKey = "Cellblock.HumanCapture.Stage", WaitKey = "Cellblock.HumanCapture.Wait";
        const string Output = "Builds/HumanReview";
        static readonly List<string> notes = new List<string>(), errors = new List<string>();
        static int meshesBefore; static GameObject spare;
        static HumanCapture() { EditorApplication.update += Tick; }
        public static void Run()
        {
            // Claude's clone only: never open scenes, delete output or enter Play in an interactive (shared) editor.
            if (!Application.isBatchMode) { Debug.LogError("CELLBLOCK_HUMAN_CAPTURE refused: batch mode only (run from Claude's clone)."); return; }
            ProjectSetup.Validate(); SurfaceLibrary.Ensure(); LightingSetup.Ensure();
            EditorSceneManager.OpenScene("Assets/Cellblock/Scenes/Main.unity");
            Directory.CreateDirectory(Output); foreach (string f in Directory.GetFiles(Output, "*.png")) File.Delete(f);
            notes.Clear(); errors.Clear();
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
                var w = game.World;
                if (stage == 0) { HumanContact.Reset(); game.StartArtPreview("county-maximum", "briar-county", 15 * 60); Next(2); return; }
                var officer = w.Data.Staff.First(s => s.Arrived && s.Role == "Officer"); var resident = w.Data.Residents.First(r => w.IsPresent(r));
                if (stage == 1)
                {
                    // Open ground beside the entrance walk: nothing between camera and person.
                    officer.Position = new Point(6, -14); resident.Position = new Point(9, -14);
                    w.Data.Speed = 0; game.PreviewAdvance(0); Next(1.5f); return;
                }
                if (stage == 2)
                {
                    foreach (var (who, id) in new[] { ("officer", officer.Id), ("resident", resident.Id) })
                    {
                        Vector3 at; if (!game.PreviewPersonPosition(id, out at)) { errors.Add("no actor " + id); continue; }
                        var root = GameObject.Find((who == "officer" ? "Officer · " : "Resident · ") + id);
                        float heading = root == null ? 0 : root.transform.eulerAngles.y;
                        foreach (var (view, yaw) in new[] { ("front", 0f), ("side", 90f), ("back", 180f) })
                        { game.FrameArt(at + Vector3.up * .95f, 1.9f, heading + 180 + yaw, 6); Render(game.ArtCamera, who + "-" + view); }
                    }
                    Next(.2f); return;
                }
                if (stage >= 3 && stage <= 6)
                {
                    float[] curls = { 0, .25f, .82f, 1 }; string[] label = { "open", "relaxed", "grip", "fist" };
                    FacilityView.PreviewCurl = curls[stage - 3];
                    Vector3 at; game.PreviewPersonPosition(officer.Id, out at);
                    var root = GameObject.Find("Officer · " + officer.Id); float heading = root == null ? 0 : root.transform.eulerAngles.y;
                    // Hands hang about 0.8 m up at the sides; one shot per side, close.
                    Vector3 right = Quaternion.Euler(0, heading, 0) * Vector3.right;
                    game.FrameArt(at + Vector3.up * .82f - right * .25f, .42f, heading + 180 - 35, 4); Render(game.ArtCamera, "hand-left-" + label[stage - 3]);
                    game.FrameArt(at + Vector3.up * .82f + right * .25f, .42f, heading + 180 + 35, 4); Render(game.ArtCamera, "hand-right-" + label[stage - 3]);
                    Next(.2f); return;
                }
                if (stage == 7) { FacilityView.PreviewCurl = -1; HumanContact.Reset(); w.Data.Speed = 1; Next(.1f); return; }
                if (stage >= 8 && stage < 14)
                {
                    Vector3 at; if (game.PreviewPersonPosition(officer.Id, out at)) { game.FrameArt(at + Vector3.up * .9f, 2.6f, 200, 10); Render(game.ArtCamera, "officer-walk-" + (stage - 7)); }
                    if (game.PreviewPersonPosition(resident.Id, out at)) { game.FrameArt(at + Vector3.up * .9f, 2.6f, 20, 10); Render(game.ArtCamera, "resident-walk-" + (stage - 7)); }
                    Next(.45f); return;
                }
                if (stage == 14)
                {
                    w.Data.Speed = 0;
                    notes.Add("walking contact: " + HumanContact.Report);
                    Check(HumanContact.Frames > 50 && HumanContact.Knee < .002f && HumanContact.Ankle < .002f && HumanContact.Hand < .002f, "Visible joints drift from the rig: " + HumanContact.Report);
                    Check(HumanContact.Soles > 10 && HumanContact.SoleMin > -.02f && HumanContact.SoleMax < .03f, "Planted shoes float or sink: " + HumanContact.Report);
                    // A second view builds this world's people, then is destroyed: its generated meshes must go with it.
                    meshesBefore = FacilityView.GeneratedHumanMeshes;
                    spare = new GameObject("Spare facility view"); var view = spare.AddComponent<FacilityView>(); view.Initialize(); view.Refresh(w, true);
                    Next(.5f); return;
                }
                if (stage == 15)
                {
                    int built = FacilityView.GeneratedHumanMeshes; notes.Add("generated meshes: before " + meshesBefore + ", with spare view " + built);
                    Check(built > meshesBefore, "The spare view built no human meshes (check is not exercising disposal).");
                    UnityEngine.Object.Destroy(spare); Next(.5f); return;
                }
                if (stage == 16)
                {
                    int after = FacilityView.GeneratedHumanMeshes; notes.Add("after destroying the spare view: " + after);
                    Check(after == meshesBefore, "Destroying a view left " + (after - meshesBefore) + " generated human meshes behind.");
                    Finish(); return;
                }
            }
            catch (Exception e) { errors.Add(e.ToString()); Finish(); }
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
        static void Finish()
        {
            FacilityView.PreviewCurl = -1; if (spare != null) UnityEngine.Object.Destroy(spare);
            bool passed = errors.Count == 0;
            File.WriteAllText(Path.Combine(Output, "report.txt"), (passed ? "PASSED" : "FAILED") + "\n\nNotes:\n" + string.Join("\n", notes) + "\n\nErrors:\n" + string.Join("\n", errors));
            if (passed) Debug.Log("CELLBLOCK_HUMAN_CAPTURE_PASSED"); else Debug.LogError("CELLBLOCK_HUMAN_CAPTURE_FAILED " + string.Join(" | ", errors.Take(4)));
            SessionState.SetBool(ActiveKey, false); EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
