using System;
using System.IO;
using System.Linq;
using Cellblock.Core;
using Cellblock.Unity;
using UnityEditor;
using UnityEngine;

namespace Cellblock.Editor
{
    public static partial class PresentationPreview
    {
        // Authored UI gallery, not a simulation test. Only touches the shared disposable preview world.
        static bool constructionDeskArtActive;
        static string constructionDeskArtOutput, constructionDeskArtPending;
        static int constructionDeskArtStage;
        static double constructionDeskArtNext;
        static GameController constructionDeskArtGame;
        static readonly string[] ConstructionDeskArtNames = {
            "01-desktop-no-crew", "02-desktop-build-and-crew", "03-desktop-attention", "04-desktop-completed",
            "05-phone-projects", "06-phone-attention", "07-portrait-projects", "08-portrait-completed", "09-desktop-handover-prerequisite"
        };

        static void BeginConstructionDeskArt()
        {
            constructionDeskArtOutput = Path.Combine("Temp/CellblockConstructionDeskArt", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(constructionDeskArtOutput);
            File.WriteAllText(Path.Combine(constructionDeskArtOutput, "scope.txt"), "Authored project/crew data for layout inspection. No scheduler, project completion, device, controller or save-roundtrip assertions.\n");
            constructionDeskArtGame = UnityEngine.Object.FindFirstObjectByType<GameController>();
            constructionDeskArtGame.StartArtPreview("county-maximum", "briar-county", 10 * 60);
            World world = constructionDeskArtGame.World; world.Data.Speed = 0; world.Data.Projects.Clear(); world.Data.Crews.Clear();
            for (int i = 0; i < 7; i++)
            {
                var project = new Project { Id = "desk-project-" + i, Name = new[] {
                    "East wing · new intake cells", "Kitchen service & secure dining connection", "Perimeter lighting · south access",
                    "Casework rooms · phase two", "Yard shelter & sanitation", "Education fitout", "West wing repair"
                }[i], MaterialCents = 275000 + i * 13100, Preparation = i == 0 ? 15 : 0, Handover = i == 6 ? 0 : 10,
                    Completed = i == 6, CompletedMinute = i == 6 ? world.Data.Minute - 60 : 0,
                    Status = i == 2 ? "Worksite blocked — change access to south wall" : i == 3 ? "Handover waits for earlier project" : "Construction" };
                project.Work.Add(new WorkItem { TaskId = "part", Kind = "Fixture", Required = 90, Remaining = i == 3 || i == 6 ? 0 : i == 1 ? 48 : 90,
                    HasSite = true, Site = new Point(-16 + i, -10), Fixture = new Fixture { Id = "desk-fixture-" + i, Kind = "Bed", Position = new Point(-16 + i, -10) } });
                if (i == 4) project.Work.Add(new WorkItem { TaskId = "removal", Kind = "Removal", Required = 40, Remaining = 24, HasSite = true, Site = new Point(-12, -10) });
                if (i == 3) project.DependsOn.Add("desk-project-0");
                world.Data.Projects.Add(project);
            }
            constructionDeskArtActive = true; constructionDeskArtStage = 0; constructionDeskArtPending = "";
            constructionDeskArtNext = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update += TickConstructionDeskArt;
        }

        static void TickConstructionDeskArt()
        {
            if (!constructionDeskArtActive || EditorApplication.isCompiling || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < constructionDeskArtNext) return;
            try
            {
                if (constructionDeskArtPending != "")
                {
                    if (!File.Exists(constructionDeskArtPending)) return;
                    constructionDeskArtPending = ""; constructionDeskArtStage++;
                    constructionDeskArtNext = EditorApplication.timeSinceStartup + .2;
                    return;
                }
                if (constructionDeskArtStage >= ConstructionDeskArtNames.Length) { FinishConstructionDeskArt(""); return; }
                World world = constructionDeskArtGame.World;
                int stage = constructionDeskArtStage;
                if (stage == 1) world.Data.Crews.Add(new Crew { Id = "desk-crew", Name = "Northline construction · Crew A", Arrived = true,
                    ProjectId = "desk-project-1", Position = new Point(-17, -11), HelperPosition = new Point(-17.6f, -11), Activity = "Carrying materials to kitchen service", WorkPhase = "Carrying" });
                SetGameSize(stage < 4 || stage == 8 ? 1600 : stage < 6 ? 900 : 480, stage < 4 || stage == 8 ? 900 : stage < 6 ? 420 : 850);
                GameHud hud = constructionDeskArtGame.GetComponentInChildren<GameHud>();
                hud.PreviewConstructionDesk(stage == 2 || stage == 5 ? 1 : stage == 3 || stage == 7 ? 2 : 0,
                    "desk-project-" + (stage == 8 ? 3 : stage == 2 || stage == 5 ? 2 : stage == 3 || stage == 7 ? 6 : stage == 1 || stage == 4 || stage == 6 ? 1 : 0));
                // Let UI Toolkit settle after the Game view/layout change, then capture its actual composition.
                constructionDeskArtNext = EditorApplication.timeSinceStartup + 2;
                constructionDeskArtPending = Path.Combine(constructionDeskArtOutput, ConstructionDeskArtNames[stage] + ".png");
                constructionDeskArtGame.StartCoroutine(CaptureConstructionDeskAfterLayout(constructionDeskArtPending));
            }
            catch (Exception error) { FinishConstructionDeskArt(error.ToString()); Debug.LogException(error); }
        }

        static System.Collections.IEnumerator CaptureConstructionDeskAfterLayout(string path)
        {
            yield return new WaitForSecondsRealtime(1.5f);
            if (constructionDeskArtActive) constructionDeskArtGame.CapturePresentationFrame(path);
        }

        static void FinishConstructionDeskArt(string reason)
        {
            if (!constructionDeskArtActive) return;
            constructionDeskArtActive = false; EditorApplication.update -= TickConstructionDeskArt;
            var errors = ReviewErrors().ToList(); if (reason != "") errors.Add(reason);
            WriteFeatureEvidence(constructionDeskArtOutput, errors.Count == 0, errors.ToArray(), new[] {
                "Authored construction desk gallery. Inspect actual PNGs; no automated gameplay assertions.",
                "Runtime project data and crews are staged in a disposable preview company; simulation is paused."
            });
            Debug.Log("CELLBLOCK_CONSTRUCTION_DESK_ART " + Path.GetFullPath(constructionDeskArtOutput));
        }
    }
}
