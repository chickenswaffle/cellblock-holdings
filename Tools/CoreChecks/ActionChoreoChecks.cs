using System;
using System.Linq;
using Cellblock.Core;

static partial class Program
{
    static ActionChoreoContext ChoreoFixture(ChoreoAction action)
    { return new ActionChoreoContext { ActionId = "test:1", OfficerId = "officer", ResidentId = "subject", Action = action, OfficerGround = new Point(0, 1), ResidentGround = new Point(0, 0), ResidentYaw = 0, Landed = true, HitDirection = new ChoreoVector(1, 0, 0), Strength = .6f }; }
    static void RunActionChoreoChecks()
    {
        Test("action choreo · every action publishes finite continuous phases and named targets", () => {
            var pair = new ActionChoreoPair();
            foreach (ChoreoAction action in Enum.GetValues(typeof(ChoreoAction)))
            {
                ChoreoTrack track = ActionChoreo.Track(action); float end = 0;
                foreach (ChoreoStep step in track.Steps) { Check(Math.Abs(step.Start - end) < .001f && step.Duration > 0, "Timeline gap/overlap: " + action); end = step.End; }
                for (float t = 0; t < track.Duration + .05f; t += .04f)
                {
                    ActionChoreo.Sample(ChoreoFixture(action), t, t, pair);
                    foreach (var frame in new[] { pair.Officer, pair.Resident })
                    {
                        Check(frame.Progress >= 0 && frame.Progress <= 1 && !float.IsNaN(frame.Root.Ground.X), "Invalid frame");
                        Check(frame.HandTargets.Length == 2 && frame.Events.Count == 0, "Bad hands or repeated-time events");
                        var partner = frame == pair.Actor ? pair.Partner : pair.Actor; double yaw = partner.Root.Yaw * Math.PI / 180;
                        Point relative = frame.Root.PartnerOffset;
                        Point rebuilt = partner.Root.Ground + new Point((float)(relative.X * Math.Cos(yaw) + relative.Y * Math.Sin(yaw)), (float)(-relative.X * Math.Sin(yaw) + relative.Y * Math.Cos(yaw)));
                        Check(Geometry.Distance(frame.Root.Ground, rebuilt) < .001f, "Relative root contradicts world root");
                        foreach (var hand in frame.HandTargets) if (hand.Active) Check((hand.IsWorld ? !float.IsNaN(hand.World.Y) : hand.Name != "" && hand.ActorId != "") && hand.Weight <= 1 && hand.Grip <= 1, "Unresolved named target");
                    }
                }
            }
        });
        Test("action choreo · both-side pat-down order maintains interlaced control", () => {
            var sweeps = ActionChoreo.Track(ChoreoAction.PatDown).Steps.Where(s => s.Phase == ChoreoPhase.Sweep).ToArray();
            Check(sweeps.Length == 22 && sweeps[0].Right == ChoreoPoint.CollarL && sweeps[10].Right == ChoreoPoint.AnkleL && sweeps[11].Left == ChoreoPoint.CollarR && sweeps[21].Left == ChoreoPoint.AnkleR, "Sweep order incomplete");
            Check(sweeps.All(s => s.Left == ChoreoPoint.InterlacedHands || s.Right == ChoreoPoint.InterlacedHands), "Control hand lost");
        });
        Test("action choreo · cuff clicks are sequential and hands target matching wrists", () => {
            var pair = new ActionChoreoPair();
            foreach (ChoreoAction a in new[] { ChoreoAction.StandingCuff, ChoreoAction.KneelingCuff, ChoreoAction.ProneCuff })
            {
                var cues = ActionChoreo.Track(a).Cues.Where(e => e.Kind == ChoreoEventKind.CuffClick).ToArray();
                Check(cues.Length == 2 && cues[0].Side == ChoreoSide.Left && cues[1].Side == ChoreoSide.Right && cues[0].Time < cues[1].Time, "Cuff sequence wrong");
                foreach (var cue in cues) { ActionChoreo.Sample(ChoreoFixture(a), cue.Time, cue.Time - .01f, pair); int hand = cue.Side == ChoreoSide.Left ? 0 : 1; Check(pair.Officer.HandTargets[hand].Point == cue.Part && pair.Officer.Events.Any(e => e.Kind == ChoreoEventKind.CuffClick), "Click lost wrist target"); }
            }
        });
        Test("action choreo · prone knee stays on shoulder blade and officer never falls", () => {
            var pair = new ActionChoreoPair(); var c = ChoreoFixture(ChoreoAction.ProneCuff);
            foreach (var s in ActionChoreo.Track(c.Action).Steps.Where(s => s.Knee)) { ActionChoreo.Sample(c, (s.Start + s.End) / 2, -1, pair); Check(pair.Officer.KneeTarget.Name == "shoulder_blade" && pair.Officer.KneeTarget.ActorId == c.ResidentId, "Unsafe knee anchor"); }
            c.Action = ChoreoAction.Takedown; ActionChoreo.Sample(c, .6f, 0, pair);
            Check(pair.Resident.Events.Any(e => e.Kind == ChoreoEventKind.Fall) && !pair.Officer.Events.Any(e => e.Kind == ChoreoEventKind.Fall), "Officer falls with subject");
        });
        Test("action choreo · windup recovery step-in and directional reactions are bounded", () => {
            var pair = new ActionChoreoPair();
            foreach (ChoreoAction a in new[] { ChoreoAction.Strike, ChoreoAction.Counter, ChoreoAction.Shove, ChoreoAction.Grab })
            {
                var track = ActionChoreo.Track(a); Check(track.Steps[0].Duration >= .12f && track.Steps[0].Duration <= .2f && track.Steps.Last().Duration >= .25f && track.Steps.Last().Duration <= .35f, "Timing outside design");
                var c = ChoreoFixture(a); c.OfficerGround = new Point(0, 2); c.Reaction = ChoreoReaction.Fall; ActionChoreo.Sample(c, .3f, 0, pair);
                Check(Geometry.Distance(c.OfficerGround, pair.Officer.Root.Ground) <= .35001f && pair.Resident.ReactionDirection.X == 1, "Step/direction invalid");
                c.Landed = false; ActionChoreo.Sample(c, .3f, 0, pair); Check(pair.Officer.Events.Count == 0 && pair.Resident.Events.Count == 0, "Miss fired impact/fall");
            }
        });
        Test("action choreo · grounded custody and searches preserve posture and cuffs", () => {
            var pair = new ActionChoreoPair();
            foreach (ChoreoAction action in new[] { ChoreoAction.Escort, ChoreoAction.PatDown, ChoreoAction.PocketSearch, ChoreoAction.GetUp })
            {
                var c = ChoreoFixture(action); c.PartnerCuffed = c.PartnerGrounded = true;
                ActionChoreo.Sample(c, .9f, .9f, pair);
                Check(pair.Resident.HandTargets.All(h => h.Point == ChoreoPoint.SmallOfBack) && pair.Resident.CuffsApplied == 2, "Cuffs broken: " + action);
                if (action != ChoreoAction.GetUp) Check(pair.Resident.ResidentPhase == ResidentActionPhase.Seated && pair.Resident.Root.HoldFeet, "Grounded subject stood: " + action);
            }
        });
        Test("action choreo · impact names the actual lead fist", () => {
            var pair = new ActionChoreoPair(); var c = ChoreoFixture(ChoreoAction.Strike); c.LeadHand = 1;
            ActionChoreo.Sample(c, .18f, 0, pair);
            Check(pair.Officer.Events.Single(e => e.Kind == ChoreoEventKind.Impact).Side == ChoreoSide.Right && pair.Officer.HandTargets[1].Point == ChoreoPoint.Jaw, "Wrong impact fist");
        });
        Test("action choreo · world snapshot is pure and pending cuffs settle once", () => {
            StaffMember officer; Resident subject; World w = ActionWorld(out officer, out subject);
            w.Data.Speed = 0;
            Check(w.ActStaffInteraction(subject.Id, StaffInteraction.Grab), w.Notice);
            Check(w.ActStaffInteraction(subject.Id, StaffInteraction.Cuff), w.Notice);
            var states = new System.Collections.Generic.List<WorldActionChoreoState>(); int minute = w.Data.Minute;
            w.GetActionChoreoStates(states); string id = states[0].Context.ActionId;
            Check(states[0].Context.Action == ChoreoAction.StandingCuff && w.StaffBusyMinutes == ActionChoreo.GameMinutes(ChoreoAction.StandingCuff), "Wrong cuff duration/variant");
            w.GetActionChoreoStates(states);
            Check(states[0].Context.ActionId == id && w.Data.Minute == minute && w.StaffActions.Cuffed == 0, "Query mutated work");
            FinishStaffWork(w); w.GetActionChoreoStates(states);
            Check(w.StaffActions.Cuffed == 1 && states[0].Context.PartnerCuffed, "Cuffs never settled");
            ActionMinute(w); Check(w.StaffActions.Cuffed == 1, "Cuffs settled twice");
        });
        Test("action choreo · escorted subject walks ahead and beside the officer without warping", () => {
            StaffMember officer; Resident subject; World w = ActionWorld(out officer, out subject);
            Check(w.ActStaffInteraction(subject.Id, StaffInteraction.Grab) && w.ActStaffInteraction(subject.Id, StaffInteraction.Cuff), w.Notice); FinishStaffWork(w);
            w.SetCharacterMoveIntent(new Point(1, 0)); Point beforeOfficer = officer.Position;
            for (int i = 0; i < 12; i++) { Point before = subject.Position; ActionMinute(w); Check(Geometry.Distance(before, subject.Position) <= .3701f, "Subject teleported into formation"); }
            Check(officer.Position.X > beforeOfficer.X + 1, "Escort never advanced");
            Point separation = subject.Position - officer.Position;
            Check(Math.Abs(separation.X - .55f) < .001f && Math.Abs(separation.Y - .35f) < .001f, "Subject still trails the officer");
            var states = new System.Collections.Generic.List<WorldActionChoreoState>(); w.GetActionChoreoStates(states);
            var pair = new ActionChoreoPair(); ActionChoreo.Sample(states[0].Context, 0, 0, pair);
            Check(Geometry.Distance(pair.Officer.Root.Ground, officer.Position) < .001f, "Choreography fights Core formation");
            var reposition = states[0].Context; reposition.EscortContact = false;
            ActionChoreo.Sample(reposition, 0, 0, pair);
            Check(pair.Officer.Phase == ChoreoPhase.Setup && pair.Officer.HandTargets.All(h => !h.Active), "Elbow grip reaches through a corner");
            w.Data.Speed = 0; Point paused = subject.Position; w.AdvanceMinutes(4); Check(Geometry.Distance(paused, subject.Position) == 0, "Paused escort moved");
        });
        Test("action choreo · preparation cancels safely and release clears pending cuff publication", () => {
            StaffMember officer; Resident subject; World w = ActionWorld(out officer, out subject);
            Check(w.ActStaffInteraction(subject.Id, StaffInteraction.PatDown) && w.CanCancelStaffChoreo && w.CancelStaffChoreo(), "Search preparation cannot cancel");
            Check(w.StaffActions.Searches == 0 && w.BusyAction == "", "Cancelled search settled");
            Check(w.ActStaffInteraction(subject.Id, StaffInteraction.Grab) && w.ActStaffInteraction(subject.Id, StaffInteraction.Cuff), w.Notice);
            Check(w.CancelStaffChoreo() && w.HoldPhase == "Holding" && w.StaffActions.Cuffed == 0, "Cancelled cuff settled");
            Check(w.ActStaffInteraction(subject.Id, StaffInteraction.Cuff), w.Notice);
            w.AdvanceMinutes(4); Check(!w.CanCancelStaffChoreo && !w.CancelStaffChoreo(), "Contact was undone");
            Check(w.ActStaffInteraction(subject.Id, StaffInteraction.Release), w.Notice);
            var states = new System.Collections.Generic.List<WorldActionChoreoState>(); w.GetActionChoreoStates(states);
            Check(w.BusyAction == "" && states.All(s => s.Context.Action != ChoreoAction.StandingCuff), "Released cuff kept publishing");
            w.AdvanceMinutes(20); Check(w.StaffActions.Cuffed == 0, "Released pending cuff later counted");
        });
        Test("action choreo · blocked escort formation stops the officer at a wall", () => {
            StaffMember officer; Resident subject; World initial = ActionWorld(out officer, out subject);
            string officerId = officer.Id, subjectId = subject.Id; Point start = officer.Position; State state = RoundTrip(initial.Data);
            var wall = Facility.Line(start.X + .9f, start.Y - .5f, start.X + .9f, start.Y + 1.4f); wall.Id = "escort-wall"; state.Walls.Add(wall);
            World w = new World(state); officer = w.Data.Staff.First(s => s.Id == officerId); subject = w.Data.Residents.First(r => r.Id == subjectId);
            Check(w.BeginCharacterControl(officerId), w.Notice);
            Check(w.ActStaffInteraction(subjectId, StaffInteraction.Grab) && w.ActStaffInteraction(subjectId, StaffInteraction.Cuff), w.Notice); FinishStaffWork(w);
            w.SetCharacterMoveIntent(new Point(1, 0));
            for (int i = 0; i < 20; i++) ActionMinute(w);
            Check(subject.Position.X < start.X + .9f && officer.Position.X < start.X + .9f, "Escort crossed a blocking wall");
            Point stopped = officer.Position; ActionMinute(w);
            Check(Geometry.Distance(stopped, officer.Position) < .001f && w.CharacterControlBlocker.Contains("Escort"), "Officer kept moving without subject");
        });
        Test("action choreo · pause replay skipped frames and rewind never duplicate events", () => {
            var cursor = new ActionChoreoCursor(); var pair = new ActionChoreoPair(); var c = ChoreoFixture(ChoreoAction.StandingCuff);
            cursor.Sample(c, 0, pair); cursor.Sample(c, 2, pair); Check(pair.Officer.Events.Count(e => e.Kind == ChoreoEventKind.CuffClick) == 2, "Skipped frame lost clicks");
            cursor.Sample(c, 2, pair); Check(pair.Officer.Events.Count == 0, "Pause replays cues"); cursor.Sample(c, 0, pair); cursor.Sample(c, 2, pair); Check(pair.Officer.Events.Count == 0, "Rewind replays cues");
            c.ActionId = "test:2"; cursor.Sample(c, 2, pair); Check(pair.Officer.Events.Count == 2, "New action cannot emit");
        });
    }
}
