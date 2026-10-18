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
    // The20 original sequences deliberately stage poses/outcomes. A fifth fixture lets core-selected
    // exchanges complete after initial contact placement. PresentationPreview owns the sole editor,
    // scene/save checks, refresh freeze, timeout and Play lifecycle; this class never launches Unity.
    public static class EscapeCapture
    {
        const string PathKey = "Cellblock.Escape.Output", StartedKey = "Cellblock.Escape.Started";
        const string OutputRoot = "Temp/CellblockEscape";
        static string Output { get { return SessionState.GetString(PathKey, ""); } }
        static int step, frames, shotFrame, lastFrame = -1;
        static string shot = "", escapeeId = "", leadId = "";
        static StreamWriter log;
        static readonly List<string> failures = new List<string>();
        static readonly List<string> captures = new List<string>();
        static readonly HashSet<string> sequences = new HashSet<string>();
        static bool running, completed, naturalContact, naturalCuffs, naturalEscort;
        static int scenarios, naturalStartMinute, naturalInterceptions;
        static Point naturalEscortStart;

        [MenuItem("Cellblock/Capture staged escape review")]
        public static void Run()
        {
            PresentationPreview.RequestEscapeReview();
        }
        internal static void PrepareRequest()
        {
            Reset();
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            SessionState.SetString(PathKey, Path.GetFullPath(Path.Combine(OutputRoot, stamp)));
            SessionState.SetString(StartedKey, DateTime.UtcNow.ToString("O"));
            Directory.CreateDirectory(Output); WriteReport(false, "Preparing safe editor review.", new string[0], "preparing");
        }
        static void Reset()
        {
            Time.captureDeltaTime = 0;
            if (log != null) { log.Dispose(); log = null; }
            step = frames = shotFrame = scenarios = 0; lastFrame = doneSince = -1; shot = escapeeId = leadId = "";
            running = completed = naturalContact = naturalCuffs = naturalEscort = false;
            naturalStartMinute = naturalInterceptions = 0; naturalEscortStart = new Point();
            failures.Clear(); captures.Clear(); sequences.Clear();
        }
        internal static void Begin(GameController game)
        {
            Reset();
            if (!EditorApplication.isPlaying || game == null || Output == "") throw new InvalidOperationException("Escape review requires the prepared existing Play session.");
            Directory.CreateDirectory(Output);
            log = new StreamWriter(Path.Combine(Output, "timeline.csv")) { AutoFlush = true };
            log.WriteLine("sequence,frame,minute,phase,move,renderBeat,staggered,attempt,stamina,puffs,impulse,fight");
            running = true; WriteReport(false, "Running20 staged pose sequences plus an unforced core-outcome fixture.", new string[0], "running");
        }
        internal static bool TickReview(GameController game, string[] runtimeErrors)
        {
            if (completed) return true;
            if (!running) throw new InvalidOperationException("Escape review state was interrupted. Request a fresh run.");
            if (Time.frameCount == lastFrame) return false;
            lastFrame = Time.frameCount;
            FacilityView view = UnityEngine.Object.FindFirstObjectByType<FacilityView>();
            if (game == null || view == null) throw new InvalidOperationException("No game renderer in the existing Play session.");
            Step(game, view);
            runtimeErrors = PresentationPreview.ReviewErrors();
            if (completed)
            {
                bool passed = failures.Count == 0 && runtimeErrors.Length == 0;
                WriteReport(passed, "20 staged pose sequences; unforced contact, cuffing, one arrest and physical escort after initial fixture placement. Not a device/performance test.", runtimeErrors, "completed");
                Debug.Log(passed ? "CELLBLOCK_ESCAPE_CAPTURE_PASSED " + Output : "CELLBLOCK_ESCAPE_CAPTURE_FAILED " + Output);
            }
            return completed;
        }

        static void Step(GameController game, FacilityView view)
        {
            World w = game.World; frames++;
            EscapeStruggle s = w == null ? new EscapeStruggle() : w.Struggle;
            switch (step)
            {
                case 0: if (frames > 30) Next(); return;
                // Scenario one: a sprint, a clinch, a punch that drops the officer, a flying tackle from behind, the pin, cuffs,
                // the lift and the walk back.
                case 1: Company(game); Next(); return;
                case 2: if (frames > 20) { StartRun(game); Next(); } return;
                case 3: Capture(game, view, "01-sprint", () => false, 0, 60, 1, () => Side(view, escapeeId, null, 4.5f)); return;
                case 4: Engage(game, "a", false); Next(); return;
                case 5: if (s.Active || frames > 90) { Require(s.Active, "the first officer never made contact"); leadId = s.Officer; Force(w, "Grappling", "Hold", 3); Next(); } return;
                case 6: Capture(game, view, "02-clinch", () => false, 0, 18, 1, () => Side(view, escapeeId, leadId, 3.2f)); return;
                case 7: Force(w, "Grappling", "Strike", 5); Next(); return;
                case 8: Capture(game, view, "03-strike", () => s.Staggered == leadId, 14, 90, 1, () => Side(view, escapeeId, leadId, 3.2f)); return;
                case 9: Require(s.Staggered == leadId && s.StaggerMove == "Strike", "the strike did not knock the officer down"); Next(); return;
                case 10: Capture(game, view, "04-knocked-down", () => false, 0, 24, 2, () => Side(view, leadId, null, 3.4f)); return;
                case 11:
                    if (w.Escape.Phase == "None") { Require(false, "arrested before the tackle"); step = 19; frames = 0; shot = ""; return; }
                    // Send the first officer away and let go of any hold, so the next contact is a clean run-down from behind.
                    foreach (StaffMember first in w.Data.Staff.Where(x => x.Id == leadId)) { first.Position = first.Station = first.Destination = new Point(24, 20); first.NextDutyMinute = int.MaxValue / 2; }
                    if (s.Active) { s.Phase = "None"; s.Officer = s.Helper = s.Move = ""; s.SlipUntil = w.Data.Minute + 2; }
                    Engage(game, "b", true); Next(); return;
                case 12: if (s.Active || frames > 120) { Require(s.Active, "the second officer never made contact"); leadId = s.Officer; if (s.Move != "Tackle") Force(w, "Grappling", "Tackle", 4); Next(); } return;
                case 13: Capture(game, view, "05-tackle", () => s.Phase == "Down", 12, 90, 1, () => Side(view, escapeeId, leadId, 3.6f)); return;
                case 14: Require(s.Phase == "Down" && s.Prone, "the tackle did not pin: " + s.Phase); if (s.Phase == "Down") Force(w, "Down", "Thrash", 5); Next(); return;
                case 15: Capture(game, view, "06-pinned", () => s.Phase != "Down", 0, 120, 1, () => Close(view, escapeeId, leadId)); return;
                case 16: Require(s.Phase == "Cuffing", "no cuffing after the pin: " + s.Phase); Next(); return;
                case 17: Capture(game, view, "07-cuffing", () => s.Phase == "Rising", 2, 160, 2, () => Close(view, escapeeId, leadId)); return;
                case 18: Capture(game, view, "08-rising", () => w.Escape.Phase == "None", 10, 160, 1, () => Side(view, escapeeId, leadId, 3.2f)); return;
                case 19: Require(w.Escape.Phase == "None" && s.Escort != "", "the cuffs did not end the attempt with an escort"); leadId = s.Escort; Next(); return;
                case 20: Capture(game, view, "09-escort", () => s.Escort == "", 0, 90, 3, () => Side(view, escapeeId, leadId, 3.4f)); return;
                // Scenario two: cornered at the closed gate, a shove free, the flight, then a frontal takedown.
                case 21: Company(game); Next(); return;
                case 22: if (frames > 20) { StartRun(game, true); Next(); } return;
                case 23: if (w.Escape.Phase == "Held at gate" || frames > 900) { Require(w.Escape.Phase == "Held at gate", "never reached the closed gate"); Next(); } return;
                case 24: Capture(game, view, "10-gate", () => false, 0, 40, 2, () => Side(view, escapeeId, null, 3.4f)); return;
                case 25: Engage(game, "c", false); Next(); return;
                case 26: if (s.Active || frames > 90) { Require(s.Active, "no contact at the gate"); leadId = s.Officer; Force(w, "Grappling", "Shove", 5); Next(); } return;
                case 27: Capture(game, view, "11-shove", () => s.Staggered == leadId, 30, 120, 1, () => Side(view, escapeeId, leadId, 3.2f)); return;
                case 28: Capture(game, view, "12-flee", () => false, 0, 40, 2, () => Side(view, escapeeId, null, 4.2f)); return;
                case 29: if (w.Escape.Phase == "None") { Require(false, "arrested before the takedown"); step = 33; frames = 0; shot = ""; return; } if (!s.Active) Engage(game, "d", false); Next(); return;
                case 30: if (s.Active || frames > 120) { Require(s.Active, "no contact for the takedown"); leadId = s.Officer; Force(w, "Grappling", "Takedown", 5); Next(); } return;
                case 31: Capture(game, view, "13-takedown", () => s.Phase == "Down", 14, 90, 1, () => Side(view, escapeeId, leadId, 3.2f)); return;
                case 32: Capture(game, view, "14-takedown-pin", () => s.Phase != "Down", 0, 30, 2, () => Close(view, escapeeId, leadId)); return;
                // Scenario three: spent at the gate, hands on the knees; a surrender, standing cuffs and the lift.
                case 33: Company(game); Next(); return;
                case 34: if (frames > 20) { StartRun(game, true); Next(); } return;
                case 35: if (w.Escape.Phase == "Held at gate" || frames > 900) { Require(w.Escape.Phase == "Held at gate", "never reached the closed gate (spent)"); w.Struggle.Stamina = 8; Next(); } return;
                case 36: w.Struggle.Stamina = 8; Capture(game, view, "15-exhausted", () => false, 0, 30, 2, () => Side(view, escapeeId, null, 3.2f)); return;
                case 37: Engage(game, "e", false); Next(); return;
                case 38: if (s.Active || frames > 90) { Require(s.Active, "no contact for the surrender"); leadId = s.Officer; Force(w, "Surrendered", "Kneel", 4); Next(); } return;
                case 39: Capture(game, view, "16-surrender", () => s.Phase == "Cuffing", 6, 90, 1, () => Side(view, escapeeId, leadId, 3.2f)); return;
                case 40: Capture(game, view, "17-standing-cuffs", () => w.Escape.Phase == "None", 6, 200, 2, () => Side(view, escapeeId, leadId, 3.2f)); return;
                // Scenario four: a resident fight squaring up, trading blows, then held apart by an officer.
                case 41: Company(game); Next(); return;
                case 42: if (frames > 20) { StartFight(game); Next(); } return;
                case 43: if (w.Fight.Phase == "Tension" || frames > 900) { Require(w.Fight.Phase == "Tension", "the fight never squared up: " + w.Fight.Phase); Next(); } return;
                case 44: Capture(game, view, "18-squaring-up", () => w.Fight.Phase != "Tension", 0, 120, 2, () => High(Side(view, w.Fight.A, w.Fight.B, 3.6f))); return;
                case 45: Capture(game, view, "19-brawl", () => false, 0, 120, 2, () => High(Side(view, w.Fight.A, w.Fight.B, 3.6f))); return;
                case 46: Separate(game); Next(); return;
                case 47: if (w.Fight.Phase == "Separating" || frames > 300) { Require(w.Fight.Phase == "Separating", "no officer reached the fight: " + w.Fight.Phase); Next(); } return;
                case 48: Capture(game, view, "20-separating", () => false, 0, 40, 2, () => Side(view, w.Fight.A, w.Fight.B, 3.8f)); return;
                // A fifth fixture: initial positions/traits only. No forced phase/move, teleport,
                // outcome or clock jump after contact; the ordinary core completes the arrest.
                case 49: Company(game); Next(); return;
                case 50:
                    if (frames <= 20) return;
                    StartRun(game); Engage(game, "unforced", false);
                    Resident natural = w.Escapee; natural.Discipline = 95; natural.Impulsivity = 0; natural.Stress = 20;
                    StaffMember responder = w.Data.Staff.First(o => o.Id == leadId); responder.Morale = 95; responder.Integrity = 100; responder.Favoritism = 0; responder.FavoredGang = "";
                    PersonnelFile file = w.FileFor(responder); if (file != null) { file.Experience = 15; file.RankLevel = 2; }
                    naturalStartMinute = w.Data.Minute; naturalInterceptions = w.Data.Justice.Interceptions;
                    Next(); return;
                case 51:
                    naturalContact |= s.Active && s.Resident == escapeeId;
                    naturalCuffs |= s.Phase == "Cuffing" && s.Resident == escapeeId;
                    if (w.Escape.Phase == "None")
                    {
                        Resident arrested = w.Data.Residents.First(r => r.Id == escapeeId);
                        naturalEscort = s.Escort != "" && s.Escorted == escapeeId && arrested.Legal.Custody == "Escorted";
                        Require(naturalContact && naturalCuffs, "unforced contact did not visibly pass through cuffing");
                        Require(w.Data.Justice.Interceptions == naturalInterceptions + 1 && naturalEscort, "unforced arrest did not create exactly one interception and an escort");
                        naturalEscortStart = arrested.Position; leadId = s.Escort; Next(); return;
                    }
                    if (frames > 2400 || w.Data.Minute - naturalStartMinute > 150) throw new InvalidOperationException("Unforced contact did not finish within its bounded window.");
                    CaptureNaturalFrame(game, view, "21-unforced-contact", escapeeId, s.Officer == "" ? leadId : s.Officer); return;
                case 52:
                    CaptureNaturalFrame(game, view, "22-unforced-escort", escapeeId, leadId);
                    Resident escorted = w.Data.Residents.First(r => r.Id == escapeeId);
                    if (frames >= 90 || s.Escort == "")
                    {
                        Require(naturalEscort && Geometry.Distance(naturalEscortStart, escorted.Position) > .15f, "unforced arrest escort did not physically move");
                        Require(w.Data.Justice.Interceptions == naturalInterceptions + 1, "unforced escort credited the interception twice");
                        Next();
                    }
                    return;
                default:
                    Require(sequences.Count == 22 && scenarios == 5, "escape review did not produce20 staged and2 unforced sequences across5 scenarios");
                    Finish(); return;
            }
        }
        static void Next() { step++; frames = 0; shotFrame = 0; shot = ""; }
        static void Require(bool ok, string what) { if (!ok) { failures.Add(what); Debug.LogWarning("CELLBLOCK_ESCAPE_CHECK " + what); } }

        static void Company(GameController game)
        {
            Time.captureDeltaTime = 0;
            game.StartArtPreview("county-maximum", "briar-county", 14 * 60 + 30);
            World w = game.World; scenarios++;
            w.Data.Incidents = new IncidentState(); w.Data.Justice.Attempt = new EscapeAttempt(); w.Data.Struggle = new EscapeStruggle();
            w.Population.MoratoriumUntil = int.MaxValue / 2; w.Population.Backlog = 0; w.Population.NextSurgeMinute = int.MaxValue / 2;
            w.Data.Justice.NextAttemptMinute = int.MaxValue / 2;
            w.Population.Audit.Phase = "Scheduled"; w.Population.Audit.NextMinute = int.MaxValue / 2;
            w.Population.Riot.Phase = "None"; w.Population.Riot.Rioters.Clear();
            w.Stories.NextIncidentMinute = int.MaxValue / 2; w.Stories.Incidents.Clear();
            foreach (NotableResident notable in w.Stories.Notables) notable.NextPressureMinute = int.MaxValue / 2;
            w.IntelWork.Tips.Clear(); w.IntelWork.Intel.Clear(); w.Contraband.Drones.Clear(); w.Contraband.Items.Clear();
            foreach (Resident r in w.Data.Residents) { r.LastFightMinute = w.Data.Minute; r.NextPlanMinute = int.MaxValue / 2; r.Legal.NextCourtMinute = -1; }
            escapeeId = leadId = "";
        }
        // The demo's pick, moved onto open ground first so the run is visible, with every other officer held far away.
        static void StartRun(GameController game, bool toTheGate = false)
        {
            World w = game.World;
            Resident pick = w.Data.Residents.Where(r => r.Legal.Ready && r.Legal.Custody == "In facility" && r.Legal.RestrictedUntil <= w.Data.Minute)
                .OrderByDescending(r => w.EscapeRisk(r)).ThenBy(r => r.Id, StringComparer.Ordinal).First();
            Point exit = JusticeRules.GateInside;
            Point open = new[] { new Point(-8, -14), new Point(-6, -16), new Point(-10, -12), new Point(-4, -17), new Point(-12, -16) }
                .Select(p => toTheGate ? exit + (p - exit) * 0.45f : p).First(p => w.Routes.Clear(p, p) && w.Routes.Find(p, exit) != null);
            pick.Position = open;
            foreach (StaffMember officer in w.Data.Staff.Where(s => s.Role == "Officer"))
            { officer.Position = officer.Station = officer.Destination = new Point(24, 20); officer.NextDutyMinute = int.MaxValue / 2; }
            if (toTheGate) w.SetGate(false);
            Require(game.RunIncidentDemo(true) && w.Escapee == pick, "the escape demo did not start for the expected resident");
            // A closed gate keeps the resident on site for every beat; breaking free turns them back into the yard.
            w.SetGate(false);
            escapeeId = pick.Id;
            // Already spotted: sprint from the first frame.
            w.Escape.DetectedMinute = w.Data.Minute - 10; w.Escape.DetectedBy = "the capture";
            Time.captureDeltaTime = 1f / 30;
        }
        // An available officer placed a stride ahead of the resident, on the way to the gate, to make contact.
        static void Engage(GameController game, string salt, bool behind)
        {
            World w = game.World; Resident r = w.Escapee;
            if (r == null) { Require(false, "no escapee left to engage (" + salt + "): " + w.IncidentReport.Outcome); return; }
            StaffMember officer = w.Data.Staff.Where(s => s.Role == "Officer" && s.SecurityRole == "" && !w.IsWarden(s) && s.Id != w.Struggle.Staggered && s.Id != leadId &&
                (w.FileFor(s) == null || (w.FileFor(s).Duty != "Gate post" && w.FileFor(s).Duty != "Court escort" && w.FileFor(s).Duty != "CCTV monitoring" && w.FileFor(s).Duty != "Intake")))
                .OrderBy(s => s.Id, StringComparer.Ordinal).First();
            officer.ShiftStart = w.Data.Minute / 60 % 24;
            Point heading = Direction(r.Position, w.Struggle.FleeUntil > w.Data.Minute ? w.Struggle.FleeTarget : w.Escape.Exit);
            Point spot = r.Position + heading * (behind ? -1.5f : 1.3f);
            officer.Position = officer.Station = officer.Destination = w.Routes.Clear(spot, spot) && w.SightLines.Clear(spot, r.Position) ? spot : r.Position + new Point(1.1f, 0);
            officer.NextDutyMinute = int.MaxValue / 2;
            leadId = officer.Id;
            Debug.Log("CELLBLOCK_ESCAPE engage " + salt + " " + officer.Name);
        }
        static Point Direction(Point from, Point to) { Point d = to - from; float l = d.Length; return l > 0.01f ? d * (1 / l) : new Point(1, 0); }
        // The next exchange starts now and lands after the given minutes with the given result.
        static void Force(World w, string phase, string move, int minutes)
        {
            EscapeStruggle s = w.Struggle; s.Phase = phase; s.Move = move; s.MoveMinute = w.Data.Minute; s.MoveEnd = w.Data.Minute + minutes;
            if (move == "Takedown" || move == "Hold") s.BreakFrees = Math.Min(s.BreakFrees, 1);
        }

        struct Shot { public Vector3 Focus; public float Distance, Yaw, Pitch; }
        // A three-quarter side view across the line between the people involved, close enough to read limbs.
        static Shot Side(FacilityView view, string a, string b, float distance)
        {
            Vector3 pa, pb = Vector3.zero; bool hasA = view.TryGetPersonPosition(a, out pa), hasB = b != null && view.TryGetPersonPosition(b, out pb);
            if (!hasA) pa = Vector3.zero;
            if (!hasB) pb = pa;
            Vector3 axis = pb - pa; axis.y = 0;
            float yaw = axis.sqrMagnitude > 0.01f ? Mathf.Atan2(axis.x, axis.z) * Mathf.Rad2Deg + 90 : 30;
            Vector3 focus = (pa + pb) * 0.5f; focus.y = 0.55f;
            return new Shot { Focus = focus, Distance = distance, Yaw = yaw + 20, Pitch = 24 };
        }
        // Low and close on someone on the ground, from the side away from the officer.
        static Shot Close(FacilityView view, string a, string b)
        {
            Vector3 pa, pb = Vector3.zero; view.TryGetPersonPosition(a, out pa); bool hasB = view.TryGetPersonPosition(b, out pb);
            Vector3 away = hasB ? pa - pb : Vector3.forward; away.y = 0;
            float yaw = away.sqrMagnitude > 0.01f ? Mathf.Atan2(-away.x, -away.z) * Mathf.Rad2Deg + 35 : 30;
            Vector3 focus = hasB ? Vector3.Lerp(pa, pb, 0.35f) : pa; focus.y = 0.35f;
            return new Shot { Focus = focus, Distance = 2.6f, Yaw = yaw, Pitch = 34 };
        }
        static Shot High(Shot shot) { shot.Pitch = 40; return shot; }
        static void StartFight(GameController game)
        {
            World w = game.World;
            foreach (StaffMember officer in w.Data.Staff.Where(s => s.Role == "Officer"))
            { officer.Position = officer.Station = officer.Destination = new Point(24, 20); officer.NextDutyMinute = int.MaxValue / 2; }
            // The demo picks the most tense pair; stand them a few steps apart in the yard so the scene forms quickly.
            Resident a = null, b = null; float best = -1;
            foreach (Resident x in w.Data.Residents.Where(r => w.IsPresent(r) && r.Legal.Ready && r.Legal.Custody == "In facility"))
                foreach (Relationship link in x.Relationships)
                {
                    Resident y = w.Data.Residents.FirstOrDefault(r => r.Id == link.OtherId && w.IsPresent(r) && r.Legal.Ready && r.Legal.Custody == "In facility");
                    if (y != null && link.Tension > best) { a = x; b = y; best = link.Tension; }
                }
            if (a != null)
            {
                Point yard = w.Data.Yard + new Point(1.5f, 1.5f);
                foreach (var pair in new[] { (a, new Point(-1.6f, 0)), (b, new Point(1.6f, 0)) })
                { Point p = yard + pair.Item2; if (w.Routes.Clear(p, p)) pair.Item1.Position = p; }
            }
            Require(game.RunIncidentDemo(false) && w.Fight.Active, "the fight demo did not start: " + w.Notice);
            Time.captureDeltaTime = 1f / 30;
        }
        // Bring an available officer close enough to see the fight and step in.
        static void Separate(GameController game)
        {
            World w = game.World;
            StaffMember officer = w.Data.Staff.Where(s => s.Role == "Officer" && s.SecurityRole == "" && !w.IsWarden(s) && (w.FileFor(s) == null || w.FileFor(s).Duty == "" || w.FileFor(s).Duty == "Patrol"))
                .OrderBy(s => s.Id, StringComparer.Ordinal).FirstOrDefault() ?? w.Data.Staff.First(s => s.Role == "Officer" && !w.IsWarden(s));
            officer.ShiftStart = w.Data.Minute / 60 % 24;
            Point near = w.Fight.Center + new Point(0, -3);
            if (!w.Routes.Clear(near, near)) near = w.Fight.Center + new Point(3, 0);
            officer.Position = officer.Station = officer.Destination = near; officer.NextDutyMinute = 0;
            Debug.Log("CELLBLOCK_ESCAPE separate " + officer.Name);
        }
        static int doneSince = -1;
        // Renders every few frames until the condition has held for the given tail of frames, or the cap is reached.
        static void Capture(GameController game, FacilityView view, string name, Func<bool> done, int tail, int max, int every, Func<Shot> at)
        {
            if (shot != name) { shot = name; shotFrame = 0; doneSince = -1; sequences.Add(name); Directory.CreateDirectory(Path.Combine(Output, name)); }
            if ((frames - 1) % every == 0)
            {
                World w = game.World; EscapeStruggle s = w.Struggle; Shot frame = at();
                game.FrameArt(frame.Focus, frame.Distance, frame.Yaw, frame.Pitch);
                string path = Path.Combine(Output, name, name + "-" + shotFrame.ToString("000") + ".png");
                Render(game.ArtCamera, 960, 540, path); captures.Add(Path.GetFullPath(path));
                log.WriteLine(name + "," + shotFrame + "," + w.Data.Minute + "," + s.Phase + "," + s.Move + "," + view.PreviewEscapeBeat + "," + s.Staggered + "," + w.Escape.Phase + "," +
                    Math.Round(s.Stamina) + "," + view.PreviewActivePuffs + "," + Math.Round(view.CameraImpulse, 2) + "," + w.Fight.Phase);
                shotFrame++;
            }
            if (doneSince < 0 && done()) doneSince = frames;
            if (frames >= max * every || (doneSince >= 0 && frames - doneSince >= tail)) Next();
        }
        static void CaptureNaturalFrame(GameController game, FacilityView view, string name, string resident, string officer)
        {
            if (shot != name) { shot = name; shotFrame = 0; sequences.Add(name); Directory.CreateDirectory(Path.Combine(Output, name)); }
            if ((frames - 1) % 3 != 0) return;
            Shot frame = Side(view, resident, officer, 3.8f); game.FrameArt(frame.Focus, frame.Distance, frame.Yaw, frame.Pitch);
            string path = Path.Combine(Output, name, name + "-" + shotFrame.ToString("000") + ".png"); Render(game.ArtCamera, 960, 540, path); captures.Add(Path.GetFullPath(path));
            World w = game.World; EscapeStruggle s = w.Struggle;
            log.WriteLine(name + "," + shotFrame++ + "," + w.Data.Minute + "," + s.Phase + "," + s.Move + "," + view.PreviewEscapeBeat + "," + s.Staggered + "," + w.Escape.Phase + "," + Math.Round(s.Stamina) + "," + view.PreviewActivePuffs + "," + Math.Round(view.CameraImpulse, 2) + "," + w.Fight.Phase);
        }
        static void Render(Camera camera, int width, int height, string path)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Escape capture" };
            target.Create();
            float aspect = camera.aspect; RenderTexture previous = RenderTexture.active; Texture2D image = null; camera.aspect = (float)width / height;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; if (image != null) UnityEngine.Object.DestroyImmediate(image); camera.aspect = aspect; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
        [Serializable] sealed class EscapeReport
        {
            public bool passed, staged = true, unforcedContact, unforcedCuffing, unforcedEscort;
            public string status, editor, message, fullPath, timeline, startedUtc;
            public int captureCount, sequenceCount, scenarioCount, stagedScenarioCount, unforcedScenarioCount;
            public string[] errors, runtimeErrors, captures;
        }
        static void WriteReport(bool passed, string message, string[] runtimeErrors, string status)
        {
            if (Output == "") return;
            Directory.CreateDirectory(OutputRoot); Directory.CreateDirectory(Output);
            var report = new EscapeReport { passed = passed, status = status, editor = Application.unityVersion, message = message,
                fullPath = Path.GetFullPath(Output), timeline = Path.Combine(Output, "timeline.csv"), startedUtc = SessionState.GetString(StartedKey, ""),
                captureCount = captures.Count, sequenceCount = sequences.Count, scenarioCount = scenarios, stagedScenarioCount = Math.Min(4, scenarios), unforcedScenarioCount = scenarios >= 5 ? 1 : 0,
                unforcedContact = naturalContact, unforcedCuffing = naturalCuffs, unforcedEscort = naturalEscort,
                errors = failures.Concat(runtimeErrors).Distinct().ToArray(), runtimeErrors = runtimeErrors, captures = captures.ToArray() };
            string json = JsonUtility.ToJson(report, true); File.WriteAllText(Path.Combine(Output, "report.json"), json);
            string stable = Path.Combine(OutputRoot, "report.json"), temporary = stable + ".tmp";
            File.WriteAllText(temporary, json); if (File.Exists(stable)) File.Replace(temporary, stable, null); else File.Move(temporary, stable);
        }
        internal static void Abort(string reason, string[] runtimeErrors)
        {
            failures.Add(reason); Finish(); WriteReport(false, reason, runtimeErrors, "failed");
        }
        static void Finish()
        {
            running = false; completed = true; Time.captureDeltaTime = 0;
            if (log != null) { log.Dispose(); log = null; }
        }
    }
}
