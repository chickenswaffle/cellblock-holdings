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
    // Standing-turn film (Claude, 2026-10-19): an officer standing still is turned 130°; 12 frames 80 ms apart from the
    // side plus the hip and head yaw each frame. Checks the head leads, the hips come round smoothly (no snap) within a
    // second, and the feet step. Builds/TurnReview/. Batch only:
    //   Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.TurnCapture.Run
    [InitializeOnLoad]
    public static class TurnCapture
    {
        const string ActiveKey = "Cellblock.TurnCapture.Active", StageKey = "Cellblock.TurnCapture.Stage", WaitKey = "Cellblock.TurnCapture.Wait";
        const string Output = "Builds/TurnReview";
        const int Frames = 12;
        static readonly List<string> notes = new List<string>(), errors = new List<string>();
        static string officerId, punchTarget; static float startYaw, targetYaw, firstHead, firstHips; static int stepsSeen;
        static Vector3 footLeft, footRight;
        static TurnCapture() { EditorApplication.update += Tick; }
        public static void Run()
        {
            if (!Application.isBatchMode) { Debug.LogError("CELLBLOCK_TURN_CAPTURE refused: batch mode only."); return; }
            ProjectSetup.Validate(); SurfaceLibrary.Ensure(); LightingSetup.Ensure();
            EditorSceneManager.OpenScene("Assets/Cellblock/Scenes/Main.unity");
            Directory.CreateDirectory(Output); foreach (string f in Directory.GetFiles(Output, "*.png")) File.Delete(f);
            notes.Clear(); errors.Clear(); stepsSeen = 0;
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
                FacilityView view = UnityEngine.Object.FindFirstObjectByType<FacilityView>();
                if (stage == 1)
                {
                    StaffMember officer = w.Data.Staff.First(s => s.Role == "Officer" && s.Arrived); officerId = officer.Id;
                    officer.Position = new Point(6, -14);
                    if (!game.StartCharacterPlay(officerId)) throw new InvalidOperationException("Could not play as the officer: " + w.Notice);
                    game.SetCharacterPerspective(false); w.Data.Speed = 1; w.SetCharacterMoveIntent(default(Point));
                    Next(2.5f); return;
                }
                Transform root = Root(), head = Bone(root, "Head"), pelvis = Bone(root, "pelvis");
                if (stage == 2)
                {
                    startYaw = root.eulerAngles.y; targetYaw = startYaw + 130;
                    firstHead = head.eulerAngles.y; firstHips = pelvis.eulerAngles.y;
                    footLeft = Bone(root, "foot_l").position; footRight = Bone(root, "foot_r").position;
                    view.FaceCharacter(officerId, targetYaw);
                    Next(.03f); return;
                }
                int frame = stage - 3;
                if (frame < Frames)
                {
                    float hips = Mathf.DeltaAngle(startYaw, pelvis.eulerAngles.y - (firstHips - startYaw)), headTurn = Mathf.DeltaAngle(startYaw, head.eulerAngles.y - (firstHead - startYaw));
                    Vector3 l = Bone(root, "foot_l").position, r = Bone(root, "foot_r").position;
                    if (Vector3.Distance(l, footLeft) > .03f) { stepsSeen++; footLeft = l; }
                    if (Vector3.Distance(r, footRight) > .03f) { stepsSeen++; footRight = r; }
                    notes.Add("t=" + (frame * 80) + " ms · hips " + hips.ToString("0") + "° · head " + headTurn.ToString("0") + "°");
                    if (frame == 2) Check(headTurn > hips + 8, "The head did not lead the hips into the turn (head " + headTurn.ToString("0") + "°, hips " + hips.ToString("0") + "°).");
                    if (frame == 0) Check(hips < 60, "The hips snapped " + hips.ToString("0") + "° at once.");
                    if (frame == Frames - 1) Check(Mathf.Abs(Mathf.DeltaAngle(hips, 130)) < 12, "The hips had not come round after a second (" + hips.ToString("0") + "° of 130°).");
                    Vector3 at = root.position + Vector3.up * .9f;
                    game.FrameArt(at, 3.2f, startYaw + 90, 8); Render(game.ArtCamera, "turn-" + frame.ToString("00"));
                    Next(.08f); return;
                }
                if (frame == Frames)
                {
                    notes.Add("feet moved " + stepsSeen + " times during the turn");
                    Check(stepsSeen >= 2, "The feet did not step during a 130° standing turn.");
                    // Punch film: a resident squared up in front, then a real landed Strike beat through the simulation.
                    Resident resident = w.Data.Residents.First(r => w.IsPresent(r) && !w.InIntake(r)); punchTarget = resident.Id;
                    Vector3 f = root.forward; resident.Position = new Point(root.position.x + f.x * .85f, root.position.z + f.z * .85f);
                    resident.Impulsivity = 0; resident.Stress = 5; game.PreviewAdvance(0);
                    Next(.6f); return;
                }
                int punch = frame - Frames - 1;
                if (punch == 0)
                {
                    var beats = (System.Collections.IList)typeof(World).GetField("beats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(w);
                    beats.Add(new ActionBeat { Kind = "Strike", ActorId = officerId, TargetId = punchTarget, Minute = w.Data.Minute, Variant = 1, Landed = true });
                }
                if (punch < 10)
                {
                    game.FrameArt(root.position + root.forward * .42f + Vector3.up * 1.2f, 2.4f, root.eulerAngles.y + 90, 6); Render(game.ArtCamera, "punch-" + punch.ToString("00"));
                    Next(.05f); return;
                }
                // Walk film: stick held forward; side view then front view, following the walker.
                int walk = punch - 10; GameHud hud = UnityEngine.Object.FindFirstObjectByType<GameHud>();
                if (walk == 0) { w.Data.Residents.ForEach(r => { if (r.Id == punchTarget) r.Position = new Point(20, 20); }); officer0().Position = w.Data.Yard + new Point(-3.5f, 0); game.PreviewAdvance(0); hud.PreviewMoveStick(new Vector2(0, 1)); Next(.9f); return; }
                if (walk <= 16) { game.FrameArt(root.position + Vector3.up * .9f, 3.0f, root.eulerAngles.y + 90, 4); Render(game.ArtCamera, "walk-side-" + walk.ToString("00")); Next(.07f); return; }
                if (walk <= 24) { game.FrameArt(root.position + Vector3.up * .9f, 3.4f, root.eulerAngles.y + 180, 4); Render(game.ArtCamera, "walk-front-" + (walk - 16).ToString("00")); Next(.07f); return; }
                hud.PreviewMoveStick(Vector2.zero);
                Finish();
            }
            catch (Exception e) { errors.Add(e.ToString()); Finish(); }
        }
        static StaffMember officer0() { return UnityEngine.Object.FindFirstObjectByType<GameController>().World.Data.Staff.First(s => s.Id == officerId); }
        static Transform Root() { var go = GameObject.Find("Officer · " + officerId); if (go == null) throw new InvalidOperationException("No officer actor."); return go.transform; }
        static Transform Bone(Transform root, string name)
        {
            // Bones of the skinned body only (the procedural rig has its own "Head" pivot).
            Transform model = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(b => b.name == "Human model") ?? root;
            Transform t = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(b => b.name == name); if (t == null) throw new InvalidOperationException("No bone " + name); return t;
        }
        static void Check(bool ok, string what) { if (!ok) errors.Add(what); }
        static void Render(Camera camera, string name)
        {
            var target = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; target.Create();
            float aspect = camera.aspect; camera.aspect = 1;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; var img = new Texture2D(640, 640, TextureFormat.RGB24, false); img.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); img.Apply(); RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(Output, name + ".png"), img.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(img);
            }
            finally { camera.aspect = aspect; camera.ResetAspect(); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
        static void Finish()
        {
            bool passed = errors.Count == 0;
            File.WriteAllText(Path.Combine(Output, "report.txt"), (passed ? "PASSED" : "FAILED") + "\n\nNotes:\n" + string.Join("\n", notes) + "\n\nErrors:\n" + string.Join("\n", errors));
            if (passed) Debug.Log("CELLBLOCK_TURN_CAPTURE_PASSED"); else Debug.LogError("CELLBLOCK_TURN_CAPTURE_FAILED " + string.Join(" | ", errors.Take(4)));
            SessionState.SetBool(ActiveKey, false);
            GameController game = UnityEngine.Object.FindFirstObjectByType<GameController>(); if (game != null && game.CharacterViewActive) game.EndCharacterView(false);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
