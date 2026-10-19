using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Cellblock.Editor
{
    // Claude's batch wrapper for the shared PresentationPreview runner, for Claude's own clone only (never the Mac
    // checkout's open editor): Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.ClaudeBatch.Run
    //   -cellblockFeature <look|pad|inmate|yard|actions|sanitation> -logFile <log>
    // Files the same request the menu does, waits (across the Play domain reload) for this run's report and for Play to
    // stop, then exits 0 on a passing report.
    [InitializeOnLoad]
    public static class ClaudeBatch
    {
        const string ModeKey = "Cellblock.ClaudeBatch.Mode", LaunchKey = "Cellblock.ClaudeBatch.Launch", PlayedKey = "Cellblock.ClaudeBatch.Played";
        static ClaudeBatch() { if (Application.isBatchMode && SessionState.GetString(ModeKey, "") != "") EditorApplication.update += Wait; }
        public static void Run()
        {
            // Enforced, not just documented: an interactive editor (the shared Mac checkout) must never get a scheduled exit.
            if (!Application.isBatchMode) { Debug.LogError("CELLBLOCK_BATCH refused: ClaudeBatch.Run is batch-mode only."); return; }
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-cellblockFeature");
            string mode = i >= 0 && i + 1 < args.Length ? args[i + 1] : "";
            if (mode == "") { Debug.LogError("CELLBLOCK_BATCH missing -cellblockFeature"); EditorApplication.Exit(2); return; }
            SessionState.SetString(ModeKey, mode); SessionState.SetString(LaunchKey, DateTime.UtcNow.Ticks.ToString()); SessionState.SetBool(PlayedKey, false);
            Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/CellblockPresentation.request", mode);
            EditorApplication.update -= Wait; EditorApplication.update += Wait;
        }
        static string Fresh(string mode)
        {
            if (mode == "workshop") return WorkshopReport();
            string root = Path.Combine("Temp/CellblockFeatureReviews", mode);
            if (!Directory.Exists(root)) return null;
            var launched = new DateTime(long.Parse(SessionState.GetString(LaunchKey, "0")), DateTimeKind.Utc);
            return Directory.GetDirectories(root).Select(d => Path.Combine(d, "report.json")).Where(File.Exists)
                .Where(f => File.GetLastWriteTimeUtc(f) >= launched).OrderBy(f => File.GetLastWriteTimeUtc(f)).LastOrDefault();
        }
        // Codex's workshop is an art studio: it writes authored pose images (no pass/fail report) to its own folder.
        // Once it has stopped, summarise that run: passed = images present and no abort recorded.
        static string WorkshopReport()
        {
            const string root = "Temp/CellblockConstructionArt";
            if (!Directory.Exists(root) || ConstructionStudio.IsRunning) return null;
            var launched = new DateTime(long.Parse(SessionState.GetString(LaunchKey, "0")), DateTimeKind.Utc);
            string run = Directory.GetDirectories(root).Where(d => Directory.GetLastWriteTimeUtc(d) >= launched).OrderBy(Directory.GetLastWriteTimeUtc).LastOrDefault();
            if (run == null) return null;
            int images = Directory.GetFiles(run, "*.png").Length;
            string scope = Path.Combine(run, "scope.txt");
            bool aborted = File.Exists(scope) && File.ReadAllText(scope).Contains("Aborted");
            if (images == 0 && !aborted) return null;
            string report = Path.Combine(run, "report.json");
            File.WriteAllText(report, "{\"passed\": " + (images >= 12 && !aborted ? "true" : "false") + ", \"images\": " + images + "}");
            File.WriteAllText(Path.Combine(run, "report.txt"), (images >= 12 && !aborted ? "PASSED" : "FAILED") + "\n" + images + " construction pose images" + (aborted ? "\n" + File.ReadAllText(scope) : ""));
            return report;
        }
        static void Wait()
        {
            string mode = SessionState.GetString(ModeKey, "");
            if (mode == "") { EditorApplication.update -= Wait; return; }
            if (EditorApplication.isPlaying) SessionState.SetBool(PlayedKey, true);
            var launched = new DateTime(long.Parse(SessionState.GetString(LaunchKey, "0")), DateTimeKind.Utc);
            bool late = DateTime.UtcNow - launched > TimeSpan.FromMinutes(15);
            string report = Fresh(mode);
            if (!late && (report == null || !SessionState.GetBool(PlayedKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)) return;
            EditorApplication.update -= Wait; SessionState.SetString(ModeKey, "");
            bool passed = report != null && File.ReadAllText(report).Replace(" ", "").Contains("\"passed\":true");
            // Unity clears the project's Temp on quit: print the report and copy the run out first (-cellblockKeep <dir>).
            if (report != null)
            {
                string text = Path.Combine(Path.GetDirectoryName(report), "report.txt");
                Debug.Log("CELLBLOCK_BATCH_REPORT\n" + (File.Exists(text) ? File.ReadAllText(text) : File.ReadAllText(report)));
                string[] args = Environment.GetCommandLineArgs(); int k = Array.IndexOf(args, "-cellblockKeep");
                if (k >= 0 && k + 1 < args.Length)
                {
                    string keep = Path.Combine(args[k + 1], mode); Directory.CreateDirectory(keep);
                    foreach (string file in Directory.GetFiles(Path.GetDirectoryName(report))) File.Copy(file, Path.Combine(keep, Path.GetFileName(file)), true);
                }
            }
            Debug.Log("CELLBLOCK_BATCH " + mode + " " + (passed ? "PASSED" : "FAILED") + " " + (report ?? "(no report)"));
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
