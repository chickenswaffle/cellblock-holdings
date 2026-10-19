using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cellblock.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cellblock.Editor
{
    // Records what the soundscape actually outputs (Claude, 2026-10-18): the mixed AudioListener signal for a few
    // staged moments, written as WAVs to Builds/SoundReview/ with levels and the live mix in report.txt. Run from
    // Claude's clone like ArtCapture: Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.SoundCapture.Run
    [InitializeOnLoad]
    public static class SoundCapture
    {
        const string ActiveKey = "Cellblock.SoundCapture.Active", StageKey = "Cellblock.SoundCapture.Stage", WaitKey = "Cellblock.SoundCapture.Wait";
        const string Output = "Builds/SoundReview";
        struct Scene { public string Name, Type, Location; public int Minute; public float Seconds; public string Mode; }
        static readonly Scene[] Scenes =
        {
            new Scene { Name = "01-overview-morning", Type = "county-maximum", Location = "briar-county", Minute = 6 * 60 + 40, Seconds = 12, Mode = "overview" },
            new Scene { Name = "02-officer-midday", Type = "county-maximum", Location = "briar-county", Minute = 11 * 60 + 50, Seconds = 14, Mode = "officer" },
            new Scene { Name = "03-officer-lights-out", Type = "county-maximum", Location = "briar-county", Minute = 21 * 60 + 58, Seconds = 12, Mode = "officer" },
            new Scene { Name = "04-alarm", Type = "county-maximum", Location = "briar-county", Minute = 14 * 60, Seconds = 9, Mode = "alarm" },
            new Scene { Name = "05-close-night", Type = "county-maximum", Location = "briar-county", Minute = 2 * 60, Seconds = 10, Mode = "close" },
        };
        static readonly List<string> report = new List<string>();
        static Recorder recorder; static double recordUntil;

        sealed class Recorder : MonoBehaviour
        {
            public readonly List<float> Samples = new List<float>(); public int Channels = 2; public bool On;
            void OnAudioFilterRead(float[] data, int channels) { if (!On) return; Channels = channels; lock (Samples) Samples.AddRange(data); }
        }

        static SoundCapture() { EditorApplication.update += Tick; }
        public static void Run()
        {
            ProjectSetup.Validate(); SurfaceLibrary.Ensure(); LightingSetup.Ensure();
            EditorSceneManager.OpenScene("Assets/Cellblock/Scenes/Main.unity");
            Directory.CreateDirectory(Output); report.Clear();
            SessionState.SetBool(ActiveKey, true); SessionState.SetInt(StageKey, 0);
            SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 4);
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || EditorApplication.isCompiling || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < SessionState.GetFloat(WaitKey, 0)) return;
            int stage = SessionState.GetInt(StageKey, 0);
            try
            {
                GameController game = UnityEngine.Object.FindFirstObjectByType<GameController>();
                if (game == null) throw new InvalidOperationException("No GameController in Play mode.");
                int index = stage / 2;
                if (index >= Scenes.Length) { Finish(0); return; }
                Scene s = Scenes[index];
                if (stage % 2 == 0)
                {
                    game.StartArtPreview(s.Type, s.Location, s.Minute);
                    var w = game.World; w.Data.Speed = 1;
                    if (s.Mode == "officer")
                    {
                        var officer = w.Data.Staff.FirstOrDefault(x => x.Arrived && x.Role == "Officer");
                        if (officer == null || !game.StartCharacterPlay(officer.Id)) throw new InvalidOperationException("No officer to follow: " + w.Notice);
                    }
                    if (s.Mode == "alarm") w.SetAlarm(true);
                    if (s.Mode == "close" || s.Mode == "alarm") game.FocusPoint(w.Data.Residents.Count > 0 ? w.Data.Residents[0].Home : new Cellblock.Core.Point(-5, 0), 10);
                    if (recorder == null) recorder = game.ArtCamera.gameObject.AddComponent<Recorder>();
                    lock (recorder.Samples) recorder.Samples.Clear();
                    recorder.On = true; recordUntil = EditorApplication.timeSinceStartup + s.Seconds;
                    SessionState.SetInt(StageKey, stage + 1); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + s.Seconds * .5f);
                    return;
                }
                if (EditorApplication.timeSinceStartup < recordUntil) { report.Add(s.Name + " mid: " + FacilitySoundscape.PreviewMix); SessionState.SetFloat(WaitKey, (float)recordUntil); return; }
                recorder.On = false;
                float[] data; lock (recorder.Samples) data = recorder.Samples.ToArray();
                Write(Path.Combine(Output, s.Name + ".wav"), data, recorder.Channels, AudioSettings.outputSampleRate);
                double sum = 0; float peak = 0; foreach (float v in data) { sum += v * v; peak = Mathf.Max(peak, Mathf.Abs(v)); }
                double rms = data.Length == 0 ? 0 : Math.Sqrt(sum / data.Length);
                report.Add(s.Name + ": " + (data.Length / Math.Max(1, recorder.Channels) / (double)AudioSettings.outputSampleRate).ToString("0.0") + " s, rms " +
                    (20 * Math.Log10(rms + 1e-9)).ToString("0.0") + " dBFS, peak " + (20 * Math.Log10(peak + 1e-9)).ToString("0.0") + " dBFS, clock " + game.World.Data.Minute % 1440 / 60 + ":" + (game.World.Data.Minute % 60).ToString("00") + "  mix: " + FacilitySoundscape.PreviewMix);
                if (s.Mode == "alarm") game.World.SetAlarm(false);
                if (game.CharacterViewActive) game.EndCharacterView(false);
                SessionState.SetInt(StageKey, stage + 1); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 1);
            }
            catch (Exception error) { report.Add("ERROR " + error); Finish(1); }
        }

        static void Write(string path, float[] data, int channels, int rate)
        {
            using (var stream = new BinaryWriter(File.Create(path)))
            {
                int bytes = data.Length * 2;
                stream.Write("RIFF".ToCharArray()); stream.Write(36 + bytes); stream.Write("WAVEfmt ".ToCharArray()); stream.Write(16); stream.Write((short)1);
                stream.Write((short)channels); stream.Write(rate); stream.Write(rate * channels * 2); stream.Write((short)(channels * 2)); stream.Write((short)16);
                stream.Write("data".ToCharArray()); stream.Write(bytes);
                foreach (float v in data) stream.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767), -32768, 32767));
            }
        }
        static void Finish(int code)
        {
            SessionState.SetBool(ActiveKey, false);
            File.WriteAllText(Path.Combine(Output, "report.txt"), string.Join("\n", report));
            if (code == 0) Debug.Log("CELLBLOCK_SOUND_CAPTURE_PASSED " + Path.GetFullPath(Output)); else Debug.LogError("CELLBLOCK_SOUND_CAPTURE_FAILED " + string.Join(" | ", report.Where(r => r.StartsWith("ERROR"))));
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }
}
