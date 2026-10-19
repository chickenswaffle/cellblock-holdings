using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cellblock.Unity;
using UnityEditor;
using UnityEngine;

namespace Cellblock.Editor
{
    // Records the actual mixed AudioListener signal through the sole shared Play runner.
    // Each review preserves WAVs and level reports in a unique Temp/CellblockFeatureReviews/sound folder.
    [InitializeOnLoad]
    public static class SoundCapture
    {
        const string ActiveKey = "Cellblock.SoundCapture.Active", StageKey = "Cellblock.SoundCapture.Stage", WaitKey = "Cellblock.SoundCapture.Wait";
        static string Output = "";
        internal static bool IsRunning { get { return SessionState.GetBool(ActiveKey, false); } }
        static readonly List<string> errors = new List<string>();
        static float savedVolume; static bool savedPause, listenerSaved;
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
        public static void Run() { PresentationPreview.RequestFeatureReview("sound"); }
        internal static void BeginExisting()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Sound capture requires the shared Play runner.");
            Output = Path.Combine("Temp/CellblockFeatureReviews", "sound", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(Output); report.Clear(); errors.Clear();
            savedVolume = AudioListener.volume; savedPause = AudioListener.pause; listenerSaved = true;
            AudioListener.volume = .65f; AudioListener.pause = false;
            SessionState.SetBool(ActiveKey, true); SessionState.SetInt(StageKey, 0);
            SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 4);
        }
        internal static void Abort(string reason) { if (IsRunning) { errors.Add(reason); Finish(1); } }

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
                if (data.Length < AudioSettings.outputSampleRate * Math.Max(1, recorder.Channels)) errors.Add(s.Name + ": less than one second of actual listener samples.");
                if (rms < .00001) errors.Add(s.Name + ": listener mix is silent.");
                if (peak >= .999f || data.Any(v => float.IsNaN(v) || float.IsInfinity(v))) errors.Add(s.Name + ": clipped or invalid mix.");
                report.Add(s.Name + ": " + (data.Length / Math.Max(1, recorder.Channels) / (double)AudioSettings.outputSampleRate).ToString("0.0") + " s, rms " +
                    (20 * Math.Log10(rms + 1e-9)).ToString("0.0") + " dBFS, peak " + (20 * Math.Log10(peak + 1e-9)).ToString("0.0") + " dBFS, clock " + game.World.Data.Minute % 1440 / 60 + ":" + (game.World.Data.Minute % 60).ToString("00") + "  mix: " + FacilitySoundscape.PreviewMix);
                if (s.Mode == "alarm") game.World.SetAlarm(false);
                if (game.CharacterViewActive) game.EndCharacterView(false);
                SessionState.SetInt(StageKey, stage + 1); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 1);
            }
            catch (Exception error) { errors.Add(error.ToString()); Finish(1); }
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
            if (recorder != null) { recorder.On = false; UnityEngine.Object.DestroyImmediate(recorder); recorder = null; }
            if (listenerSaved) { AudioListener.volume = savedVolume; AudioListener.pause = savedPause; listenerSaved = false; }
            bool passed = code == 0 && errors.Count == 0;
            File.WriteAllText(Path.Combine(Output, "report.txt"), (passed ? "PASSED" : "FAILED") + "\n" + string.Join("\n", report) + "\nErrors:\n" + string.Join("\n", errors));
            PresentationPreview.WriteFeatureEvidence(Output, passed, errors.ToArray(), report.ToArray());
            if (passed) Debug.Log("CELLBLOCK_SOUND_CAPTURE_PASSED " + Path.GetFullPath(Output));
            else Debug.LogError("CELLBLOCK_SOUND_CAPTURE_FAILED " + string.Join(" | ", errors));
        }
    }
}
