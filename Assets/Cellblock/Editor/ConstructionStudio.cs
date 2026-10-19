using System;
using System.IO;
using Cellblock.Core;
using Cellblock.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cellblock.Editor
{
    // An authored art contact sheet, not a simulation test. Uses the shared disposable-company runner.
    [InitializeOnLoad]
    public static class ConstructionStudio
    {
        static bool active;
        static int stage, frame;
        static string output;
        static GameController game;
        static FacilityView view;
        static Crew crew;
        static Project project;
        public static bool IsRunning { get { return active; } }
        static ConstructionStudio() { EditorApplication.update += Tick; }
        [MenuItem("Cellblock/Art/Construction contact sheet")]
        public static void Run() { PresentationPreview.RequestFeatureReview("workshop"); }
        internal static void BeginExisting()
        {
            output = Path.Combine("Temp/CellblockConstructionArt", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(output);
            active = true; stage = 0; frame = Time.frameCount + 45;
            game = UnityEngine.Object.FindFirstObjectByType<GameController>();
            game.StartArtPreview("county-maximum", "briar-county", 10 * 60);
            view = UnityEngine.Object.FindFirstObjectByType<FacilityView>();
            crew = new Crew { Id = "art-crew", Arrived = true, Working = true, WorkPhase = "Working", Position = new Point(-16, -10.8f), HelperPosition = new Point(-16.7f, -10.8f), WorkFocus = new Point(-16, -10) };
            game.World.Data.Crews.Clear(); game.World.Data.Crews.Add(crew);
            project = new Project { Id = "art-project", Name = "Work pose study", Preparation = 0 };
            game.World.Data.Projects.Clear(); game.World.Data.Projects.Add(project);
            game.World.Data.Walls.Add(new Wall { Id = "art-removal-wall", A = new Point(-18, -10), B = new Point(-14, -10) });
            game.World.Data.GeometryGeneration++;
            File.WriteAllText(Path.Combine(output, "scope.txt"), "Authored renderer poses. No gameplay assertions, construction completion, save roundtrip or native-device proof.\n");
        }
        internal static void Abort(string reason)
        {
            if (!active) return;
            File.AppendAllText(Path.Combine(output, "scope.txt"), "Aborted: " + reason + "\n"); Finish();
        }
        static void Tick()
        {
            if (!active || !EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < frame) return;
            try
            {
                string[] tasks = { "Saw", "Hammer", "Drill", "Demolish" };
                if (stage >= 12) { Finish(); return; }
                int task = stage / 3, pose = stage % 3;
                crew.WorkKind = tasks[task]; crew.WorkTarget = crew.Position; crew.HelperWorkTarget = crew.HelperPosition;
                var work = new WorkItem { TaskId = "art-task", Kind = task == 3 ? "Removal" : "Wall", Required = 100, Remaining = task == 3 ? 35 : task == 0 ? 85 : task == 1 ? 50 : 15, Site = new Point(-16, -10), HasSite = true };
                if (task == 3) work.RemovedWallId = "art-removal-wall";
                else work.Wall = new Wall { Id = "art-new-wall", A = new Point(-18, -10), B = new Point(-14, -10) };
                project.Work.Clear(); project.Work.Add(work); crew.ProjectId = project.Id; crew.WorkId = project.Id + "/" + work.TaskId;
                // Only the last three images depict existing fabric being dismantled.
                game.World.Data.Walls.RemoveAll(w => w.Id == "art-removal-wall");
                if (task == 3) game.World.Data.Walls.Add(new Wall { Id = "art-removal-wall", A = new Point(-18, -10), B = new Point(-14, -10) });
                view.StudioBuilderClock = new[] { .12f, .54f, .72f }[pose] / (task == 1 ? 1.15f : task == 3 ? .48f : .85f);
                game.PreviewAdvance(0);
                game.FrameArt(new Vector3(-16.2f, 1.15f, -10.35f), 3.0f, pose == 0 ? 23 : pose == 1 ? 120 : 270, pose == 2 ? 18 : 26);
                Capture(tasks[task] + "-" + pose);
                stage++; frame = Time.frameCount + 3;
            }
            catch (Exception error) { Abort(error.ToString()); Debug.LogException(error); }
        }
        static void Capture(string name)
        {
            var target = new RenderTexture(1400, 1000, 24) { antiAliasing = 4 }; target.Create();
            RenderTexture previous = RenderTexture.active; float aspect = game.ArtCamera.aspect;
            Texture2D image = null;
            try
            {
                game.ArtCamera.aspect = 1.4f;
                RenderPipeline.SubmitRenderRequest(game.ArtCamera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; image = new Texture2D(1400, 1000, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1400, 1000), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                game.ArtCamera.aspect = aspect; RenderTexture.active = previous;
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                target.Release(); UnityEngine.Object.DestroyImmediate(target);
            }
        }
        static void Finish()
        {
            active = false;
            if (view != null) view.StudioBuilderClock = -1;
            Debug.Log("CELLBLOCK_CONSTRUCTION_ART " + Path.GetFullPath(output));
        }
    }
}
