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
using UnityEngine.UIElements;

namespace Cellblock.Editor
{
    // HUD audit (Claude). Every main HUD state at device sizes with their safe-area insets: a composite screenshot and
    // measurements — how much of the screen and of its centre the HUD covers, touch targets under 44 pt, buttons that
    // overlap, and buttons outside the safe area. Builds/HudAudit/ (report.txt + audit.json for before/after diffs).
    // Batch only: Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.HudAudit.Run
    [InitializeOnLoad]
    public static class HudAudit
    {
        const string ActiveKey = "Cellblock.HudAudit.Active", StageKey = "Cellblock.HudAudit.Stage", WaitKey = "Cellblock.HudAudit.Wait";
        const string Output = "Builds/HudAudit";
        // Panel points. iPhone 17 Pro: 874 × 402 landscape, Dynamic Island / corner insets 62 pt each side, home bar 21.
        sealed class Device { public string Name; public int Width, Height; public Vector4 Insets; }
        static readonly Device[] Devices = {
            new Device { Name = "phone-landscape", Width = 874, Height = 402, Insets = new Vector4(62, 62, 0, 21) },
            // iPhone runs landscape only; narrow/portrait layouts matter for iPad portrait and Split View.
            new Device { Name = "ipad-portrait", Width = 1032, Height = 1376, Insets = new Vector4(0, 0, 24, 20) },
            new Device { Name = "ipad-landscape", Width = 1376, Height = 1032, Insets = new Vector4(0, 0, 24, 20) },
            new Device { Name = "desktop", Width = 1600, Height = 900, Insets = Vector4.zero },
        };
        static readonly string[] States = { "idle", "facility", "build", "people", "inspect", "company", "control", "operations", "officer-third", "officer-first", "officer-phone", "officer-actions" };
        static readonly List<string> notes = new List<string>(), errors = new List<string>(), json = new List<string>();
        static RenderTexture hud; static string officerId, targetId;
        static HudAudit() { EditorApplication.update += Tick; }
        // -cellblockType county-complete audits the complete prison (its timetable chip) instead of the default start.
        static string AuditType { get { string[] a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, "-cellblockType"); return i >= 0 && i + 1 < a.Length ? a[i + 1] : "county-maximum"; } }

        public static void Run()
        {
            if (!Application.isBatchMode) { Debug.LogError("CELLBLOCK_HUD_AUDIT refused: batch mode only (run from Claude's clone)."); return; }
            ProjectSetup.Validate(); SurfaceLibrary.Ensure(); LightingSetup.Ensure();
            EditorSceneManager.OpenScene("Assets/Cellblock/Scenes/Main.unity");
            Directory.CreateDirectory(Output); foreach (string f in Directory.GetFiles(Output, "*.png")) File.Delete(f);
            notes.Clear(); errors.Clear(); json.Clear();
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
                if (stage == 0)
                {
                    game.StartArtPreview(AuditType, "briar-county", 10 * 60 + 30);
                    World w = game.World; w.Data.Speed = 0;
                    StaffMember officer = w.Data.Staff.First(s => s.Role == "Officer" && s.Arrived); officerId = officer.Id;
                    officer.Position = w.Data.Yard + new Point(0, -3);
                    Resident target = w.Data.Residents.First(r => w.IsPresent(r) && !w.InIntake(r)); targetId = target.Id;
                    target.Impulsivity = 0; target.Stress = 10; target.Position = officer.Position + new Point(0, 1.3f);
                    game.PreviewAdvance(0); Next(2); return;
                }
                // Two ticks per shot: set up (then let layout settle), capture.
                int shot = (stage - 1) / 2, deviceIndex = shot / States.Length;
                if (deviceIndex >= Devices.Length) { Finish(game); return; }
                Device device = Devices[deviceIndex]; string state = States[shot % States.Length];
                GameHud view = UnityEngine.Object.FindFirstObjectByType<GameHud>();
                if ((stage - 1) % 2 == 0) { SetUp(game, view, device, state); Next(.8f); return; }
                Capture(game, view, device, state);
                Next(.1f);
            }
            catch (Exception e) { errors.Add(e.ToString()); Finish(game); }
        }

        static void SetUp(GameController game, GameHud view, Device device, string state)
        {
            int index = Array.IndexOf(States, state);
            if (index == 0)
            {
                // New device: fresh UI target at its size, its insets, touch controls; back to management.
                if (game.CharacterViewActive) game.EndCharacterView(false);
                game.PreviewUiTarget(null); if (hud != null) { hud.Release(); UnityEngine.Object.DestroyImmediate(hud); }
                hud = new RenderTexture(device.Width, device.Height, 24, RenderTextureFormat.ARGB32) { name = "HUD audit " + device.Name }; hud.Create();
                GameHud.PreviewSafeInsets = device.Insets;
                game.PreviewControlDevice(device.Name == "desktop" ? ControlDevice.KeyboardMouse : ControlDevice.Touch);
                game.PreviewUiTarget(hud);
            }
            view.CloseOverlays();
            if (state == "idle") { view.PreviewCommandWorkspace(0); view.PreviewCloseWorkspace(); }
            else if (index < 8)
            {
                if (state == "inspect") { view.PreviewCommandWorkspace(2); game.InspectResident(targetId); }
                else view.PreviewCommandWorkspace(new[] { -1, 0, 1, 2, -1, 3, 4, 5 }[index]);
            }
            else
            {
                if (!game.CharacterViewActive && !game.StartCharacterPlay(officerId)) { errors.Add(device.Name + ": could not play as the officer: " + game.World.Notice); return; }
                game.World.Data.Speed = 0;
                if (view.CharacterPhoneOpen) view.CloseCharacterPhone();
                game.SetCharacterPerspective(state == "officer-first");
                if (state == "officer-phone") view.ToggleCharacterPhone();
                if (state == "officer-actions") { game.SetCharacterPerspective(true); game.OpenStaffInteractions(); }
            }
            game.PreviewRelayout();
        }

        static void Capture(GameController game, GameHud view, Device device, string state)
        {
            game.PreviewRelayout();
            string name = device.Name + "-" + (Array.IndexOf(States, state) + 1).ToString("00") + "-" + state;
            Texture2D world = RenderWorld(game, device), ui = Read(hud);
            Color32[] a = world.GetPixels32(), b = ui.GetPixels32();
            int covered = 0, centre = 0, centreCount = 0;
            for (int i = 0; i < a.Length; i++)
            {
                int x = i % device.Width, y = i / device.Width;
                bool hit = b[i].a > 128; if (hit) covered++;
                if (x > device.Width * .2f && x < device.Width * .8f && y > device.Height * .2f && y < device.Height * .8f) { centreCount++; if (hit) centre++; }
                float t = b[i].a / 255f;
                a[i] = new Color32((byte)(a[i].r + (b[i].r - a[i].r) * t), (byte)(a[i].g + (b[i].g - a[i].g) * t), (byte)(a[i].b + (b[i].b - a[i].b) * t), 255);
            }
            world.SetPixels32(a); world.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), world.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(world); UnityEngine.Object.DestroyImmediate(ui);

            // Interactive elements actually shown.
            VisualElement root = view.PreviewRoot;
            var buttons = root.Query<Button>().ToList().Where(Shown).ToList();
            Rect safe = new Rect(device.Insets.x, device.Insets.z, device.Width - device.Insets.x - device.Insets.y, device.Height - device.Insets.z - device.Insets.w);
            var small = buttons.Where(x => x.worldBound.width < 44 || x.worldBound.height < 44).Select(Label).ToList();
            var outside = buttons.Where(x => !Inside(safe, x.worldBound)).Select(Label).ToList();
            var overlaps = new List<string>();
            for (int i = 0; i < buttons.Count; i++) for (int j = i + 1; j < buttons.Count; j++)
            {
                if (buttons[i].Contains(buttons[j]) || buttons[j].Contains(buttons[i])) continue;
                Rect r = Intersect(buttons[i].worldBound, buttons[j].worldBound);
                if (r.width > 4 && r.height > 4) overlaps.Add(Label(buttons[i]) + " × " + Label(buttons[j]));
            }
            // The phone status chip at its longest texts (timetable, counts, meters): inside the safe area and clear of the
            // pressure alert, measured rather than eyeballed.
            Label chip = root.Q<Label>(className: "phone-stat");
            if (chip != null && Shown(chip))
            {
                VisualElement alert = root.Q<VisualElement>(className: "pressure-alert");
                float pad = chip.resolvedStyle.paddingLeft + chip.resolvedStyle.paddingRight + chip.resolvedStyle.borderLeftWidth + chip.resolvedStyle.borderRightWidth;
                foreach (string text in new[] { chip.text, "Count A 12/12 · B 12/12", "A Lock-in · B Showers", "All units · Breakfast", "A Dayroom · B Dinner", "Escape risk 100", "Beds 36/24" })
                {
                    float width = chip.MeasureTextSize(text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x + pad;
                    Rect r = new Rect(chip.worldBound.x, chip.worldBound.y, width, chip.worldBound.height);
                    if (!Inside(safe, r)) outside.Add("chip '" + text + "' " + width.ToString("0") + "×" + r.height.ToString("0"));
                    if (alert != null && Shown(alert) && Intersect(r, alert.worldBound).width > 2) overlaps.Add("chip '" + text + "' × pressure alert");
                }
                notes.Add(name + ": chip '" + chip.text + "' at x " + chip.worldBound.x.ToString("0") + "–" + chip.worldBound.xMax.ToString("0") + " (safe area to " + safe.xMax.ToString("0") + ")");
            }
            // Where a world drag or pinch would be taken by the HUD instead (any picked element counts, as IsOverUI does).
            int pickable = 0, samples = 0;
            for (float y = 2; y < device.Height; y += 6) for (float x = 2; x < device.Width; x += 6)
            { samples++; VisualElement picked = root.panel.Pick(new Vector2(x, y)); if (picked != null && picked != root) pickable++; }
            float blocks = 100f * pickable / Mathf.Max(1, samples);
            float cover = 100f * covered / a.Length, middle = 100f * centre / Mathf.Max(1, centreCount);
            string line = name + ": HUD covers " + cover.ToString("0") + "% of the screen, " + middle.ToString("0") + "% of its centre, takes touches over " + blocks.ToString("0") + "% · " + buttons.Count + " buttons, " +
                small.Count + " under 44 pt" + (small.Count > 0 ? " (" + string.Join(", ", small.Take(8)) + (small.Count > 8 ? ", …" : "") + ")" : "") +
                (overlaps.Count > 0 ? " · overlaps: " + string.Join("; ", overlaps.Take(4)) : "") + (outside.Count > 0 ? " · outside safe area: " + string.Join(", ", outside.Take(6)) : "");
            notes.Add(line); Debug.Log("CELLBLOCK_HUD_AUDIT " + line);
            json.Add("{\"shot\":\"" + name + "\",\"cover\":" + cover.ToString("0.0") + ",\"centre\":" + middle.ToString("0.0") + ",\"blocks\":" + blocks.ToString("0.0") + ",\"buttons\":" + buttons.Count + ",\"small\":" + small.Count + ",\"overlaps\":" + overlaps.Count + ",\"outside\":" + outside.Count + "}");
        }

        static bool Shown(VisualElement e)
        {
            if (e.worldBound.width < 1 || e.worldBound.height < 1 || float.IsNaN(e.worldBound.width)) return false;
            for (VisualElement v = e; v != null; v = v.parent)
                if (v.resolvedStyle.display == DisplayStyle.None || v.resolvedStyle.visibility == Visibility.Hidden || v.resolvedStyle.opacity < .05f) return false;
            return true;
        }
        static string Label(Button b) { string t = (b.text ?? "").Replace("\n", " ").Trim(); return (t == "" ? b.name : t) + " " + b.worldBound.width.ToString("0") + "×" + b.worldBound.height.ToString("0"); }
        static bool Inside(Rect outer, Rect r) { return r.xMin >= outer.xMin - 1 && r.xMax <= outer.xMax + 1 && r.yMin >= outer.yMin - 1 && r.yMax <= outer.yMax + 1; }
        static Rect Intersect(Rect a, Rect b) { float x = Mathf.Max(a.xMin, b.xMin), y = Mathf.Max(a.yMin, b.yMin); return new Rect(x, y, Mathf.Max(0, Mathf.Min(a.xMax, b.xMax) - x), Mathf.Max(0, Mathf.Min(a.yMax, b.yMax) - y)); }

        static Texture2D RenderWorld(GameController game, Device device)
        {
            var target = new RenderTexture(device.Width, device.Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; target.Create();
            Camera camera = game.ArtCamera;
            float aspect = camera.aspect; camera.aspect = (float)device.Width / device.Height;
            try { RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target }); return Read(target); }
            finally { camera.aspect = aspect; camera.ResetAspect(); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
        static Texture2D Read(RenderTexture source)
        {
            RenderTexture previous = RenderTexture.active; RenderTexture.active = source;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); image.Apply();
            RenderTexture.active = previous; return image;
        }

        static void Finish(GameController game)
        {
            SessionState.SetBool(ActiveKey, false); GameHud.PreviewSafeInsets = Vector4.zero;
            if (game != null) { if (game.CharacterViewActive) game.EndCharacterView(false); game.PreviewUiTarget(null); }
            if (hud != null) { hud.Release(); UnityEngine.Object.DestroyImmediate(hud); hud = null; }
            bool passed = errors.Count == 0;
            File.WriteAllText(Path.Combine(Output, "report.txt"), (passed ? "COMPLETE" : "FAILED") + "\n\nMeasurements (panel points; touch target floor 44 pt):\n" + string.Join("\n", notes) + "\n\nErrors:\n" + string.Join("\n", errors));
            File.WriteAllText(Path.Combine(Output, "audit.json"), "[\n" + string.Join(",\n", json) + "\n]\n");
            if (passed) Debug.Log("CELLBLOCK_HUD_AUDIT_COMPLETE"); else Debug.LogError("CELLBLOCK_HUD_AUDIT_FAILED " + string.Join(" | ", errors.Take(4)));
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
