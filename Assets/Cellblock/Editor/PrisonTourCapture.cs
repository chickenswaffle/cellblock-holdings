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
    // Tour of the complete county prison (Claude, 2026-10-19): an overview, a three-quarter view and every module up
    // close, at a set hour, plus a note of where everybody is. Builds/PrisonTour/. Batch only:
    //   Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.PrisonTourCapture.Run [-cellblockHour 12]
    [InitializeOnLoad]
    public static class PrisonTourCapture
    {
        const string ActiveKey = "Cellblock.PrisonTour.Active", StageKey = "Cellblock.PrisonTour.Stage", WaitKey = "Cellblock.PrisonTour.Wait";
        const string Output = "Builds/PrisonTour";
        static readonly List<string> notes = new List<string>(), errors = new List<string>();
        // Module, centre (x, y), camera distance.
        static readonly (string name, float x, float y, float d)[] Stops = {
            ("holding-booking", -10, -5, 9), ("lobby-visitation", -2, -5, 8), ("admin-control", 10, -5, 11), ("staff-infirmary", 22, -5, 11),
            ("unit-a", -4.5f, 16, 14), ("unit-b", -4.5f, 6.5f, 14), ("kitchen-workshop", 14, 6.5f, 12), ("classroom-chapel", 24, 6.5f, 10),
            ("segregation", 11.5f, 17, 9), ("gym", 21.5f, 17, 11), ("yard", -20, 11.5f, 14) };
        static PrisonTourCapture() { EditorApplication.update += Tick; }
        public static void Run()
        {
            if (!Application.isBatchMode) { Debug.LogError("CELLBLOCK_PRISON_TOUR refused: batch mode only."); return; }
            ProjectSetup.Validate(); SurfaceLibrary.Ensure(); LightingSetup.Ensure();
            EditorSceneManager.OpenScene("Assets/Cellblock/Scenes/Main.unity");
            Directory.CreateDirectory(Output); foreach (string f in Directory.GetFiles(Output, "*.png")) File.Delete(f);
            notes.Clear(); errors.Clear();
            SessionState.SetBool(ActiveKey, true); SessionState.SetInt(StageKey, 0); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 4);
            EditorApplication.EnterPlaymode();
        }
        static int Hour { get { string[] a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, "-cellblockHour"); int h; return i >= 0 && i + 1 < a.Length && int.TryParse(a[i + 1], out h) ? h : 11; } }
        // -cellblockClock 06:45 picks the exact minute (the timetable's calls and counts are minutes long).
        static int StartMinute { get { string[] a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, "-cellblockClock"); TimeSpan t; return i >= 0 && i + 1 < a.Length && TimeSpan.TryParse(a[i + 1], out t) ? (int)t.TotalMinutes : Hour * 60; } }
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
                if (stage == 0) { game.StartArtPreview(DemoPrison.TypeId, GameModes.Tier("county").Locations[0], StartMinute); Next(3); return; }
                if (stage == 1)
                {
                    notes.Add("day " + (w.Data.Minute / 1440 + 1) + " " + (w.Data.Minute % 1440 / 60).ToString("00") + ":" + (w.Data.Minute % 60).ToString("00") + " · " + w.Rooms.Count + " rooms · " + w.Data.Residents.Count + " residents · " + w.Data.Staff.Count(s => s.Arrived) + " staff on site");
                    notes.Add("timetable: unit A " + w.UnitBlockNow("A").Title + ", unit B " + w.UnitBlockNow("B").Title + (w.Counts.Count > 0 ? " · last count " + w.Counts.Last().Title + " " + w.Counts.Last().Unit + " " + w.Counts.Last().Present + "/" + w.Counts.Last().Expected : ""));
                    notes.Add("doing: " + string.Join(", ", w.Data.Residents.GroupBy(r => w.DescribeRoutine(r).Goal).OrderByDescending(g => g.Count()).Select(g => g.Key + " " + g.Count())));
                    notes.Add("where residents are: " + string.Join(", ", w.Data.Residents.GroupBy(r => Where(w, r.Position)).OrderByDescending(g => g.Count()).Select(g => g.Key + " " + g.Count())));
                    Vector3 centre = new Vector3(7, 0, 7);
                    game.FrameArt(centre, 46, 0, 72); Render(game.ArtCamera, "00-overview", 1800, 1100);
                    game.FrameArt(centre, 44, 215, 42); Render(game.ArtCamera, "01-three-quarter", 1800, 1100);
                    for (int i = 0; i < Stops.Length; i++)
                    {
                        var s = Stops[i];
                        game.FrameArt(new Vector3(s.x, 0, s.y), s.d, 200, 58); Render(game.ArtCamera, (i + 2).ToString("00") + "-" + s.name, 1400, 900);
                    }
                    Finish(); return;
                }
            }
            catch (Exception e) { errors.Add(e.ToString()); Finish(); }
        }
        // Which module a point is in, by the layout's coordinates.
        static string Where(World w, Point p)
        {
            float x = p.X, y = p.Y;
            if (x < -14) return "yard/outside";
            if (y < -2) return x < -10 ? "holding" : x < -6 ? "booking" : x < 2 ? "lobby" : x < 10 ? "admin" : x < 16 ? "control" : x < 22 ? "staff room" : "infirmary";
            if (y < 1) return "front corridor";
            if (x >= 5 && x <= 8) return "spine corridor";
            if (x < 5) return y < 4.5f ? "unit B cells" : y < 12 ? "unit B dayroom" : y < 18.5f ? "unit A dayroom" : "unit A cells";
            if (y < 12) return x < 14 ? "kitchen" : x < 20 ? "workshop" : y < 6.5f ? "classroom" : "chapel";
            return x < 15 ? "segregation" : "gym";
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
            if (passed) Debug.Log("CELLBLOCK_PRISON_TOUR_PASSED"); else Debug.LogError("CELLBLOCK_PRISON_TOUR_FAILED " + string.Join(" | ", errors.Take(4)));
            SessionState.SetBool(ActiveKey, false); EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
