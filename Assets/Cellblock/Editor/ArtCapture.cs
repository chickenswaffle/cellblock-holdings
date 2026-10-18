using System;
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
    // Renders key art and app-icon candidates with the game's own camera, lighting and models, so marketing images show
    // what the game actually looks like. Claude's (TestFlight/art, 2026-10-17). Run from Claude's clone, with graphics:
    //   Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.ArtCapture.Run -logFile <log>
    // (no -quit and no -nographics: it enters Play mode, renders, then exits). Images land in Builds/Art/.
    [InitializeOnLoad]
    public static class ArtCapture
    {
        const string StageKey = "Cellblock.Art.Stage", ActiveKey = "Cellblock.Art.Active", WaitKey = "Cellblock.Art.Wait";
        const string Output = "Builds/Art";
        struct Scene { public string Type, Location, Name; public int Minute; }
        // Cell angles frame one resident's bunk at a fixed distance in metres; the rest scale with the facility.
        struct Angle { public string Name; public float Distance, Yaw, Pitch; public bool Cell; }
        static readonly Scene[] Scenes =
        {
            new Scene { Type = "federal-fci", Location = "halvern", Minute = 16 * 60 + 55, Name = "federal-golden" },
            new Scene { Type = "state-medium", Location = "mason-ridge", Minute = 16 * 60 + 55, Name = "state-golden" },
            new Scene { Type = "federal-fci", Location = "halvern", Minute = 21 * 60 + 20, Name = "federal-night" },
            new Scene { Type = "county-maximum", Location = "briar-county", Minute = 16 * 60 + 40, Name = "county-golden" },
            new Scene { Type = "state-medium", Location = "mason-ridge", Minute = 21 * 60 + 5, Name = "state-night" },
            new Scene { Type = "county-maximum", Location = "briar-county", Minute = 21 * 60 + 15, Name = "county-night" },
        };
        static readonly Angle[] Angles =
        {
            new Angle { Name = "hero", Distance = 0.30f, Yaw = 222, Pitch = 34 },
            new Angle { Name = "hero2", Distance = 0.34f, Yaw = 312, Pitch = 36 },
            new Angle { Name = "close", Distance = 0.36f, Yaw = 140, Pitch = 38 },
            new Angle { Name = "cell", Distance = 5.2f, Yaw = 45, Pitch = 62, Cell = true },
            new Angle { Name = "cell2", Distance = 6.0f, Yaw = 225, Pitch = 56, Cell = true },
            new Angle { Name = "cell3", Distance = 7.5f, Yaw = 135, Pitch = 66, Cell = true },
        };

        static ArtCapture() { EditorApplication.update += Tick; }

        public static void Run()
        {
            ProjectSetup.Validate();
            EditorSceneManager.OpenScene("Assets/Cellblock/Scenes/Main.unity");
            Directory.CreateDirectory(Output);
            SessionState.SetBool(ActiveKey, true); SessionState.SetInt(StageKey, 0);
            SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 4);
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || EditorApplication.isCompiling) return;
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < SessionState.GetFloat(WaitKey, 0)) return;
            int stage = SessionState.GetInt(StageKey, 0);
            try
            {
                GameController game = UnityEngine.Object.FindFirstObjectByType<GameController>();
                if (game == null) throw new InvalidOperationException("No GameController in Play mode.");
                int scene = stage / 2;
                if (scene >= Scenes.Length) { Finish(0, "CELLBLOCK_ART_CAPTURE_PASSED " + Path.GetFullPath(Output)); return; }
                Scene s = Scenes[scene];
                // Even stages build the company and let lights, people and the sun settle; odd stages render it.
                if (stage % 2 == 0) game.StartArtPreview(s.Type, s.Location, s.Minute);
                else
                {
                    Vector3 center = game.ArtCenter; float span = Mathf.Max(30, game.PreviewCameraDistance);
                    Light sun = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
                    Debug.Log("CELLBLOCK_ART " + s.Name + " minute " + game.World.Data.Minute % 1440 + " sun " + (sun == null ? -1 : sun.intensity) + " ambient " + RenderSettings.ambientIntensity);
                    foreach (Angle a in Angles)
                    {
                        Vector3 focus = a.Cell ? CellCenter(game) : center;
                        game.FrameArt(focus, a.Cell ? a.Distance : span * a.Distance, a.Yaw, a.Pitch);
                        Render(game.ArtCamera, 2400, 1350, s.Name + "-" + a.Name + "-wide.png");
                        Render(game.ArtCamera, 1600, 1600, s.Name + "-" + a.Name + "-square.png");
                    }
                }
                SessionState.SetInt(StageKey, stage + 1);
                SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + (stage % 2 == 0 ? 3 : 0.5f));
            }
            catch (Exception error) { Finish(1, "CELLBLOCK_ART_CAPTURE_FAILED " + error); }
        }

        // The middle of the room where the first resident sleeps: one whole cell, walls on every side.
        static Vector3 CellCenter(GameController game)
        {
            var world = game.World; if (world.Data.Residents.Count == 0) return game.ArtCenter;
            Cellblock.Core.Point home = world.Data.Residents[0].Home;
            foreach (var room in world.Rooms)
                if (Cellblock.Core.Geometry.Contains(room, home))
                {
                    float x = 0, y = 0; foreach (var p in room) { x += p.X; y += p.Y; }
                    return new Vector3(x / room.Count, 0, y / room.Count);
                }
            return FacilityView.V(home);
        }
        static void Render(Camera camera, int width, int height, string name)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Art capture" };
            target.Create();
            float aspect = camera.aspect; camera.aspect = (float)width / height;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(Output, name), image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally { camera.aspect = aspect; camera.ResetAspect(); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }

        static void Finish(int code, string message)
        {
            SessionState.SetBool(ActiveKey, false);
            if (code == 0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }
}
