using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cellblock.Core;
using Cellblock.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cellblock.Editor
{
    // Shared-runner disposable company. No second editor, private scene, real input injection or slot writes.
    [InitializeOnLoad]
    public static class StaffCombatCapture
    {
        const string ActiveKey = "Cellblock.StaffCombat.Active", StageKey = "Cellblock.StaffCombat.Stage", WaitKey = "Cellblock.StaffCombat.Wait";
        const int Width = 1600, Height = 900;
        static string Output = "", officerId = "", targetId = "";
        static RenderTexture hud; static int shot, pausedMinute;
        static float initialHealth, guardedHealth;
        static readonly List<string> errors = new List<string>(), notes = new List<string>();
        internal static bool IsRunning { get { return SessionState.GetBool(ActiveKey, false); } }
        static StaffCombatCapture()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += (condition, stack, type) => {
                if (IsRunning && EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !condition.StartsWith("CELLBLOCK_COMBAT")) errors.Add(condition.Replace('\n', ' '));
            };
        }
        internal static void BeginExisting()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Shared Play runner required.");
            Output = Path.Combine("Temp/CellblockFeatureReviews/combat", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(Output); errors.Clear(); notes.Clear(); shot = 0;
            SessionState.SetBool(ActiveKey, true); SessionState.SetInt(StageKey, 0); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 2);
        }
        internal static void Abort(string reason) { if (IsRunning) { errors.Add(reason); Finish(null); } }
        static void Next(float wait = .4f) { SessionState.SetInt(StageKey, SessionState.GetInt(StageKey, 0) + 1); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + wait); }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void Ready(GameController game)
        {
            int limit = 30; while ((game.World.StaffRecoveryMinutes > 0 || game.World.OfficerStaggered) && limit-- > 0) game.PreviewAdvance(1);
            Require(limit > 0 && game.World.CharacterId == officerId, "Officer did not recover or was incapacitated.");
        }
        static void Approach(GameController game, Resident target)
        {
            StaffMember officer = game.World.Data.Staff.First(s => s.Id == officerId);
            int limit = 15;
            while (Geometry.Distance(officer.Position, target.Position) > 1.05f && limit-- > 0)
            {
                Point delta = target.Position - officer.Position; game.World.SetCharacterMoveIntent(delta * (1 / Math.Max(.001f, delta.Length))); game.PreviewAdvance(1);
            }
            game.World.SetCharacterMoveIntent(default(Point));
            Point facing = target.Position - officer.Position; game.World.SetStaffViewFacing(facing);
            Require(limit > 0, "Could not approach the opponent through a real route.");
        }
        static void Tick()
        {
            if (!IsRunning || EditorApplication.isCompiling || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < SessionState.GetFloat(WaitKey, 0)) return;
            try
            {
                GameController game = UnityEngine.Object.FindFirstObjectByType<GameController>(); Require(game != null, "No game.");
                World w = game.World;
                switch (SessionState.GetInt(StageKey, 0))
                {
                    case 0: game.StartArtPreview("county-maximum", "briar-county", 630); Next(2); return;
                    case 1:
                    {
                        StaffMember officer = w.Data.Staff.First(s => s.Role == "Officer" && s.Arrived); officerId = officer.Id; officer.Position = w.Data.Yard + new Point(0, -3);
                        Resident target = w.Data.Residents.First(r => w.IsPresent(r) && !w.InIntake(r)); targetId = target.Id;
                        foreach (Resident other in w.Data.Residents) { other.Position = w.Data.Yard + new Point(8, 8); other.Stress = 5; other.Impulsivity = 0; }
                        target.Position = officer.Position + new Point(0, .8f); target.Impulsivity = 100; target.Stress = 100; target.Patience = target.Discipline = 0; target.Gang = "Unaffiliated";
                        game.PreviewAdvance(0); Require(game.StartCharacterPlay(officer.Id), w.Notice); game.SetCharacterPerspective(false); w.Data.Speed = 0;
                        hud = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "Combat HUD review" }; hud.Create(); game.PreviewUiTarget(hud); Next(1); return;
                    }
                    case 2:
                    {
                        StaffMember officer = w.Data.Staff.First(s => s.Id == officerId); Resident target = w.Data.Residents.First(r => r.Id == targetId);
                        Vector3 look = game.ArtCamera.transform.forward; Point ahead = new Point(look.x, look.z); ahead *= 1 / Math.Max(.001f, ahead.Length);
                        target.Position = officer.Position + ahead * .8f; w.SetStaffViewFacing(ahead); game.PreviewAdvance(0);
                        for (int seed = 1; seed < 10000; seed++)
                            if (JusticeRules.Unit(seed, "staff:" + seed + ":1", "contact", w.Data.Minute + 1) < .5f && JusticeRules.Unit(seed, "staff:" + seed + ":2", "contact", w.Data.Minute + 4) < .5f) { w.Data.Seed = seed; break; }
                        initialHealth = target.Health; Require(game.PerformStaffInteraction(targetId, StaffInteraction.Punch), w.Notice); w.Data.Speed = 0;
                        Require(target.Health == initialHealth && w.ActionBeats.Last().ContactPending, "Strike damaged before contact."); pausedMinute = w.Data.Minute; Next(); return;
                    }
                    case 3:
                        Require(w.Data.Minute == pausedMinute && w.Data.Residents.First(r => r.Id == targetId).Health == initialHealth, "Paused windup advanced.");
                        Composite(game, "paused-player-windup"); game.PreviewAdvance(1); Next(); return;
                    case 4:
                    {
                        Require(w.Data.Residents.First(r => r.Id == targetId).Health < initialHealth, "Staged player contact did not land.");
                        Composite(game, "player-contact"); SideComposite(game, "player-contact-side");
                        int limit = 15; while (!w.StaffCombat.Incoming && limit-- > 0) game.PreviewAdvance(1);
                        Require(w.StaffCombat.Incoming && w.StaffCombatHealth == 100, "No telegraphed retaliation, or it hit early."); Next(); return;
                    }
                    case 5:
                        Composite(game, "resident-windup");
                        game.GetComponentInChildren<GameHud>().ShowStaffInteractions(w, officerId, targetId); Next(); return;
                    case 6:
                        Require(game.GetComponentInChildren<GameHud>().PreviewStaffTrayFits, "Combat actions menu clipped.");
                        Composite(game, "combat-actions"); game.GetComponentInChildren<GameHud>().PreviewStaffCommand(StaffInteraction.Guard); w.Data.Speed = 0;
                        Require(w.StaffCombatGuarding, "Actual menu Guard did not dispatch."); game.PreviewAdvance(1); Next(); return;
                    case 7:
                        guardedHealth = w.StaffCombatHealth;
                        Require(guardedHealth < 100 && guardedHealth > 98 && w.StaffCombat.Blocked > 0, "Facing guard did not mitigate a real counter.");
                        notes.Add("Guarded first counter: health=" + guardedHealth + ", blocks=" + w.StaffCombat.Blocked + ", wind=" + w.ActionWind);
                        Composite(game, "guarded-counter"); SideComposite(game, "guarded-counter-side"); game.SetCharacterPerspective(true); Next(); return;
                    case 8:
                        Composite(game, "guard-first-person"); w.SetStaffCombatGuard(false); game.SetCharacterPerspective(false);
                        Ready(game); Approach(game, w.Data.Residents.First(r => r.Id == targetId)); Require(game.PerformStaffInteraction(targetId, StaffInteraction.Shove), w.Notice); w.Data.Speed = 0; Next(); return;
                    case 9:
                        game.PreviewAdvance(1); Next(); return;
                    case 10:
                    {
                        Composite(game, "shove-balance-break"); SideComposite(game, "shove-side"); Resident target = w.Data.Residents.First(r => r.Id == targetId);
                        int attempts = 0;
                        while (w.StaffCombat.Active && w.StaffCombat.ResidentBalance > 45 && attempts++ < 12)
                        { Ready(game); Approach(game, target); Require(game.PerformStaffInteraction(targetId, StaffInteraction.Shove), w.Notice); w.Data.Speed = 0; game.PreviewAdvance(1); }
                        Ready(game); Approach(game, target);
                        Require(target.Health > 65 && w.StaffCombat.ResidentBalance <= 55, "Could not break balance without beating health away.");
                        notes.Add("Before takedown: resident health=" + target.Health + ", balance=" + w.StaffCombat.ResidentBalance + ", stamina=" + w.StaffCombat.ResidentStamina);
                        Require(game.PerformStaffInteraction(targetId, StaffInteraction.GroundAndCuff), w.Notice); w.Data.Speed = 0; game.PreviewAdvance(2); Next(); return;
                    }
                    case 11:
                    {
                        Composite(game, "controlled-takedown"); SideComposite(game, "controlled-takedown-side");
                        int limit = 100; while (w.BusyAction != "" && limit-- > 0) game.PreviewAdvance(1);
                        Require(limit > 0 && w.HoldPhase == "Cuffed" && !w.StaffCombat.Active, "Cuffs did not finish/end retaliation."); Next(); return;
                    }
                    case 12:
                    {
                        Composite(game, "restrained-after-fight"); SideComposite(game, "restrained-side");
                        float health = w.GetStaffCombatHealth(officerId); game.EndCharacterView(false);
                        Require(w.GetStaffCombatHealth(officerId) == health, "Handback healed an injury.");
                        notes.Add("Live rules/actual menu dispatch, windup pause, guarded repeated retaliation, swept shove/balance, takedown-to-cuffs and saved injury ownership reviewed. Rig/IK contact fidelity and native device feel are separate Claude gates; no contact pass claimed.");
                        Finish(game); return;
                    }
                }
            }
            catch (Exception error) { errors.Add(error.ToString()); Finish(null); }
        }
        static void SideComposite(GameController game, string name)
        {
            StaffMember officer = game.World.Data.Staff.First(s => s.Id == officerId);
            Resident target = game.World.Data.Residents.First(r => r.Id == targetId);
            Camera camera = game.ArtCamera; Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
            Point delta = target.Position - officer.Position;
            float yaw = Mathf.Atan2(delta.X, delta.Y) * Mathf.Rad2Deg + 65;
            try
            {
                game.FrameArt(FacilityView.V((officer.Position + target.Position) * .5f, .9f), 2.5f, yaw, 18);
                Composite(game, name);
            }
            finally { camera.transform.SetPositionAndRotation(position, rotation); }
            notes.Add(name + ": explicit side-camera evidence, alongside the actual character-follow frame.");
        }
        static Texture2D Read(RenderTexture source)
        {
            RenderTexture previous = RenderTexture.active; RenderTexture.active = source;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); image.Apply();
            RenderTexture.active = previous; return image;
        }
        static void Composite(GameController game, string name)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Action camera review" };
            target.Create();
            Camera camera = game.ArtCamera; float aspect = camera.aspect; camera.aspect = (float)Width / Height;
            Texture2D world;
            try { RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target }); world = Read(target); }
            finally { camera.aspect = aspect; camera.ResetAspect(); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            Texture2D ui = Read(hud);
            Color32[] a = world.GetPixels32(), b = ui.GetPixels32();
            for (int i = 0; i < a.Length; i++)
            {
                float t = b[i].a / 255f;
                a[i] = new Color32((byte)(a[i].r + (b[i].r - a[i].r) * t), (byte)(a[i].g + (b[i].g - a[i].g) * t), (byte)(a[i].b + (b[i].b - a[i].b) * t), 255);
            }
            world.SetPixels32(a); world.Apply();
            File.WriteAllBytes(Path.Combine(Output, (++shot).ToString("00") + "-" + name + ".png"), world.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(world); UnityEngine.Object.DestroyImmediate(ui);
        }
        static void Finish(GameController game)
        {
            SessionState.SetBool(ActiveKey, false);
            if (game == null) game = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (game != null) game.PreviewUiTarget(null);
            if (hud != null) { hud.Release(); UnityEngine.Object.DestroyImmediate(hud); hud = null; }
            bool passed = errors.Count == 0;
            File.WriteAllText(Path.Combine(Output, "report.txt"), (passed ? "PASSED" : "FAILED") + "\nNotes:\n" + string.Join("\n", notes) + "\nErrors:\n" + string.Join("\n", errors));
            PresentationPreview.WriteFeatureEvidence(Output, passed, errors.ToArray(), notes.ToArray());
            if (passed) Debug.Log("CELLBLOCK_COMBAT_PASSED " + Path.GetFullPath(Output)); else Debug.LogError("CELLBLOCK_COMBAT_FAILED " + string.Join(" | ", errors.Take(4)));
        }
    }
}
