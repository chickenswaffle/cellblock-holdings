using System;
using System.Collections.Generic;

namespace Cellblock.Core
{
    public enum ChoreoAction { PatDown, PocketSearch, StandingCuff, KneelingCuff, ProneCuff, Takedown, Escort, Strike, Counter, Shove, Grab, GetUp, Guard, Compliance, RecoverFromFall }
    public enum ChoreoPhase { Approach, Setup, Windup, Contact, Control, Apply, Sweep, DoubleLock, Release, Recover, Complete }
    public enum ResidentActionPhase { Brace, Flinch, Stagger, Fall, Prone, Kneel, HandsOnHead, Interlace, Cuffed, Rise, Escorted, Resist, PresentWrists, SideLying, Seated }
    public enum ChoreoPoint { None, WristL, WristR, ElbowL, ElbowR, ShoulderBlade, SmallOfBack, WaistL, WaistR, PocketL, PocketR, AnkleL, AnkleR, Head, Jaw, Chest, CollarL, CollarR, UpperArmL, UpperArmR, ArmpitL, ArmpitR, GroinL, GroinR, ThighL, ThighR, KneeL, KneeR, InterlacedHands }
    public enum ChoreoEventKind { Impact, CuffClick, Lift, Fall }
    public enum ChoreoSide { None, Left, Right }
    public enum ChoreoReaction { Flinch, Stagger, Fall }
    public enum ChoreoHandOrientation { Neutral, PalmOutThumbUp, Interlaced, PalmDown }

    public struct ChoreoVector
    {
        public float X, Y, Z;
        public ChoreoVector(float x, float y, float z) { X = x; Y = y; Z = z; }
        public float Length { get { return (float)Math.Sqrt(X * X + Y * Y + Z * Z); } }
    }
    public struct ChoreoTarget
    {
        public string ActorId;
        public ChoreoPoint Point;
        public ChoreoVector Offset, World;
        public bool IsWorld;
        public float Grip, Weight;
        public ChoreoHandOrientation Orientation;
        public bool Active { get { return Weight > 0 && (IsWorld || Point != ChoreoPoint.None); } }
        public string Name { get { return ActionChoreo.AnchorName(Point); } }
    }
    public struct ChoreoRoot
    {
        // Ground is the desired world ground point. PartnerOffset uses partner-right/partner-forward metres.
        // Consumers navigate/blend to this goal; this contract never writes transforms or teleports a body.
        public Point Ground, PartnerOffset;
        public string PartnerId;
        public float Yaw, RelativeYaw, Weight, MaxStepIn;
        public bool ConstrainToNavigation, HoldFeet;
    }
    public struct ChoreoEvent
    {
        public string Id, ActorId, PartnerId;
        public ChoreoEventKind Kind;
        public ChoreoSide Side;
        public ChoreoPoint Part;
        public ChoreoVector Direction;
        public float Time, Strength;
        public int Index;
    }
    public sealed class ActionChoreoFrame
    {
        public string ActionId = "", ActorId = "", PartnerId = "", StepName = "";
        public ChoreoAction Action;
        public ChoreoPhase Phase;
        public ResidentActionPhase ResidentPhase;
        public float Progress, OverallProgress, ElapsedSeconds, DurationSeconds, Lean, Crouch;
        public int CuffsApplied;
        public ChoreoVector ReactionDirection; public ChoreoPoint ReactionPart; public float ReactionStrength;
        public ChoreoRoot Root;
        public readonly ChoreoTarget[] HandTargets = new ChoreoTarget[2];
        public ChoreoTarget KneeTarget, LookTarget;
        public bool IsResident, CanCancel, CrossAnkles;
        public readonly List<ChoreoEvent> Events = new List<ChoreoEvent>(8);
    }
    public sealed class ActionChoreoPair
    {
        public readonly ActionChoreoFrame Officer = new ActionChoreoFrame();
        public readonly ActionChoreoFrame Resident = new ActionChoreoFrame();
        public ActionChoreoFrame Actor { get { return Officer; } }
        public ActionChoreoFrame Partner { get { return Resident; } }
    }
    public struct ActionChoreoContext
    {
        public string ActionId, OfficerId, ResidentId;
        public ChoreoAction Action;
        public Point OfficerGround, ResidentGround;
        public float OfficerYaw, ResidentYaw;
        public bool Resisting, Landed, ActorIsResident, PartnerGrounded, PartnerCuffed, PartnerHeadingKnown, Holding, NarrowEscort, EscortContact, ContactPending, PartnerGuarding, PartnerFreeMovement, ActorFreeMovement, RootAlreadyAligned;
        public ChoreoReaction Reaction;
        public ChoreoVector HitDirection;
        public ChoreoPoint HitPart;
        public float Strength;
        public int LeadHand;
    }
    public sealed class ChoreoStep
    {
        public string Name;
        public float Start, End;
        public ChoreoPhase Phase;
        public ResidentActionPhase Resident;
        public ChoreoPoint Left, Right;
        public float LeftGrip, RightGrip, Lean, Crouch;
        public bool Knee, CrossAnkles;
        public float Duration { get { return End - Start; } }
    }
    public sealed class ChoreoTrack
    {
        public readonly ChoreoAction Action;
        public readonly ChoreoStep[] Steps;
        public readonly ChoreoEvent[] Cues;
        public readonly float Duration;
        internal ChoreoTrack(ChoreoAction action, ChoreoStep[] steps, ChoreoEvent[] cues)
        { Action = action; Steps = steps; Cues = cues; Duration = steps[steps.Length - 1].End; }
    }

    // One cursor per live action/consumer. Frames are reused; event IDs allocate only when cues cross. No wall-clock reads.
    // Pause: call with the SAME elapsed value. Rewind never replays feedback; Reset starts a new action.
    public sealed class ActionChoreoCursor
    {
        string actionId = ""; ChoreoAction action; ulong fired;
        float previous = -1;
        public void Reset() { actionId = ""; fired = 0; previous = -1; }
        public void Sample(ActionChoreoContext context, float elapsed, ActionChoreoPair output)
        {
            if (actionId != context.ActionId || action != context.Action)
            { actionId = context.ActionId; action = context.Action; fired = 0; previous = -1; }
            float sampleTime = ActionChoreo.PresentationSeconds(context, elapsed);
            ActionChoreo.Sample(context, sampleTime, previous, output);
            Filter(output.Officer.Events); Filter(output.Resident.Events);
            previous = Math.Max(previous, Math.Max(0, sampleTime));
        }
        void Filter(List<ChoreoEvent> events)
        {
            for (int i = events.Count - 1; i >= 0; i--)
            {
                ulong bit = 1UL << events[i].Index;
                if ((fired & bit) != 0) events.RemoveAt(i); else fired |= bit;
            }
        }
    }

    public static partial class ActionChoreo
    {
        public const int Version = 1;
        public const float SecondsPerGameMinute = .2f;
        public const float HandContactTolerance = .03f, KneeContactTolerance = .05f, RootContactTolerance = .05f;
        public static int GameMinutes(ChoreoAction action) { return (int)Math.Ceiling(Track(action).Duration / SecondsPerGameMinute - .0001f); }
        static string[] anchorNames;
        public static string AnchorName(ChoreoPoint p)
        {
            if (anchorNames == null)
            {
                string[] values = Enum.GetNames(typeof(ChoreoPoint)); anchorNames = new string[values.Length];
                for (int n = 0; n < values.Length; n++)
                {
                    var b = new System.Text.StringBuilder(); string value = values[n];
                    for (int i = 0; i < value.Length; i++) { if (i > 0 && char.IsUpper(value[i])) b.Append('_'); b.Append(char.ToLowerInvariant(value[i])); }
                    anchorNames[n] = n == 0 ? "" : b.ToString();
                }
            }
            int index = (int)p; return index < 0 || index >= anchorNames.Length ? "" : anchorNames[index];
        }
        public static float PresentationSeconds(ActionChoreoContext context, float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) return 0;
            return context.ContactPending ? Math.Min(Math.Max(0, seconds), Track(context.Action).Steps[0].End - .0001f) : Math.Max(0, seconds);
        }
        public static void Sample(ActionChoreoContext c, float seconds, float previousSeconds, ActionChoreoPair output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            seconds = PresentationSeconds(c, seconds);
            ChoreoTrack track = Track(c.Action); float t = Math.Max(0, Math.Min(track.Duration, seconds));
            ChoreoStep step = track.Steps[track.Steps.Length - 1];
            foreach (ChoreoStep candidate in track.Steps) if (t < candidate.End) { step = candidate; break; }
            Init(output.Officer, c, step, t, track.Duration, false); Init(output.Resident, c, step, t, track.Duration, true);
            bool rear = c.Action == ChoreoAction.PatDown || c.Action == ChoreoAction.PocketSearch || c.Action == ChoreoAction.StandingCuff || c.Action == ChoreoAction.KneelingCuff || c.Action == ChoreoAction.ProneCuff || c.Action == ChoreoAction.Escort || c.Action == ChoreoAction.GetUp;
            Point offset = rear ? new Point(.15f, -.45f) : new Point(.18f, .70f);
            if (c.Action == ChoreoAction.Escort) offset = new Point(c.NarrowEscort ? 0 : .35f, -.55f);
            if (c.Action == ChoreoAction.ProneCuff) offset = new Point(.35f, -.40f);
            if (c.Action == ChoreoAction.PatDown || c.Action == ChoreoAction.PocketSearch)
            {
                if (step.Name.StartsWith("switch sides")) offset.X = .15f - .30f * output.Officer.Progress;
                else if (step.Right == ChoreoPoint.InterlacedHands && step.Left != ChoreoPoint.InterlacedHands) offset.X = -.15f;
            }
            bool strike = c.Action == ChoreoAction.Strike || c.Action == ChoreoAction.Counter || c.Action == ChoreoAction.Shove;
            Point goal = c.ResidentGround + Rotate(offset, c.ResidentYaw);
            if (strike || c.Action == ChoreoAction.Grab)
            {
                Point toward = c.ResidentGround - c.OfficerGround; float distance = toward.Length;
                goal = c.OfficerGround + toward * (Math.Min(.35f, Math.Max(0, distance - .60f)) / Math.Max(.0001f, distance));
            }
            // Core owns the swept two-body escort formation, including safe doorway compression.
            if (c.RootAlreadyAligned || c.Action == ChoreoAction.RecoverFromFall || c.Action == ChoreoAction.Compliance || c.Action == ChoreoAction.Guard || c.ActorFreeMovement || c.Action == ChoreoAction.Escort && c.PartnerHeadingKnown) goal = c.OfficerGround;
            offset = Rotate(goal - c.ResidentGround, -c.ResidentYaw);
            output.Officer.Root = new ChoreoRoot { Ground = goal, PartnerId = c.ResidentId, PartnerOffset = offset, RelativeYaw = rear ? 0 : Bearing(c.ResidentGround - goal) - c.ResidentYaw,
                Yaw = rear ? c.ResidentYaw : Bearing(c.ResidentGround - goal), Weight = 1, MaxStepIn = strike || c.Action == ChoreoAction.Grab ? .35f : 0, ConstrainToNavigation = true, HoldFeet = c.Action != ChoreoAction.Escort && step.Phase != ChoreoPhase.Approach && step.Phase != ChoreoPhase.Setup };
            output.Resident.Root = new ChoreoRoot { Ground = c.ResidentGround, Yaw = c.ResidentYaw, PartnerId = c.OfficerId, PartnerOffset = Rotate(c.ResidentGround - goal, -output.Officer.Root.Yaw), RelativeYaw = c.ResidentYaw - output.Officer.Root.Yaw, Weight = 1, ConstrainToNavigation = true, HoldFeet = c.Action != ChoreoAction.Escort && step.Phase != ChoreoPhase.Approach && step.Phase != ChoreoPhase.Setup };
            if (c.ActorFreeMovement) { output.Officer.Root.Weight = 0; output.Officer.Root.HoldFeet = false; }
            if (c.PartnerFreeMovement) { output.Resident.Root.Weight = 0; output.Resident.Root.HoldFeet = false; }
            if (c.Action == ChoreoAction.Guard) { output.Resident.Root.PartnerOffset = Rotate(c.ResidentGround - goal, -c.OfficerYaw); output.Resident.Root.RelativeYaw = c.ResidentYaw - c.OfficerYaw; output.Officer.Root.Yaw = c.OfficerYaw; output.Officer.Root.RelativeYaw = c.OfficerYaw - c.ResidentYaw; output.Officer.Root.HoldFeet = false; }
            if (c.Action == ChoreoAction.RecoverFromFall || c.Action == ChoreoAction.Compliance) { output.Officer.Root.Weight = 0; output.Officer.Root.HoldFeet = false; }
            output.Officer.HandTargets[0] = Target(c.ResidentId, step.Left, step.LeftGrip);
            output.Officer.HandTargets[1] = Target(c.ResidentId, step.Right, step.RightGrip);
            output.Officer.KneeTarget = step.Knee ? Target(c.ResidentId, ChoreoPoint.ShoulderBlade, 0) : default(ChoreoTarget);
            output.Officer.LookTarget = Target(c.ResidentId, step.Knee ? ChoreoPoint.SmallOfBack : ChoreoPoint.Chest, 0);
            output.Resident.LookTarget = Target(c.OfficerId, ChoreoPoint.Head, 0);
            if (c.Action == ChoreoAction.RecoverFromFall || c.Action == ChoreoAction.Compliance || c.Action == ChoreoAction.Guard || c.ActorFreeMovement) output.Officer.LookTarget.Weight = 0;
            int cuffs = 0; foreach (ChoreoEvent cue in track.Cues) if (cue.Kind == ChoreoEventKind.CuffClick && cue.Time <= t) cuffs++;
            output.Officer.CuffsApplied = output.Resident.CuffsApplied = cuffs;
            if (cuffs < 2 && (c.Action == ChoreoAction.StandingCuff || c.Action == ChoreoAction.KneelingCuff || c.Action == ChoreoAction.ProneCuff) && output.Resident.ResidentPhase == ResidentActionPhase.Cuffed) output.Resident.ResidentPhase = ResidentActionPhase.PresentWrists;
            bool search = c.Action == ChoreoAction.PatDown || c.Action == ChoreoAction.PocketSearch;
            bool custodyHold = c.Action == ChoreoAction.Escort || c.Holding;
            if ((search || custodyHold) && (c.PartnerGrounded || c.PartnerCuffed))
            {
                output.Resident.ResidentPhase = c.PartnerGrounded ? (c.PartnerCuffed ? ResidentActionPhase.Seated : ResidentActionPhase.Prone) : ResidentActionPhase.Cuffed;
                output.Resident.CrossAnkles = false;
                if (c.PartnerGrounded) { output.Officer.Crouch = .65f; output.Resident.Root.HoldFeet = output.Officer.Root.HoldFeet = true; }
                for (int arm = 0; arm < 2; arm++)
                    if (output.Officer.HandTargets[arm].Point == ChoreoPoint.InterlacedHands)
                        output.Officer.HandTargets[arm] = Target(c.ResidentId, arm == 0 ? ChoreoPoint.WristL : ChoreoPoint.WristR, .3f);
            }
            ResidentHands(output.Resident, c.ResidentId);
            if (c.Action == ChoreoAction.RecoverFromFall)
            {
                for (int arm = 0; arm < 2; arm++)
                    output.Resident.HandTargets[arm] = step.Resident == ResidentActionPhase.Rise || step.Resident == ResidentActionPhase.Brace ? default(ChoreoTarget) :
                        new ChoreoTarget { IsWorld = true, World = new ChoreoVector(c.ResidentGround.X + (arm == 0 ? -.20f : .20f), .05f, c.ResidentGround.Y + .10f), Weight = 1, Grip = 0 };
            }
            if (c.Action == ChoreoAction.KneelingCuff && t >= 1.1f)
            {
                SetRearHands(output.Resident, c.ResidentId);
                if (t < 1.5f)
                {
                    output.Resident.HandTargets[1] = Target(c.ResidentId, ChoreoPoint.Head, .15f);
                    output.Resident.HandTargets[1].Orientation = ChoreoHandOrientation.Interlaced;
                }
            }
            if (c.PartnerCuffed || cuffs >= 2)
            {
                output.Officer.CuffsApplied = output.Resident.CuffsApplied = 2;
                SetRearHands(output.Resident, c.ResidentId);
            }
            if (strike || c.Action == ChoreoAction.Grab)
            {
                bool fist = c.Action == ChoreoAction.Strike || c.Action == ChoreoAction.Counter;
                int lead = c.LeadHand == 1 ? 1 : 0;
                if (step.Phase == ChoreoPhase.Windup || step.Phase == ChoreoPhase.Recover)
                {
                    for (int arm = 0; arm < 2; arm++)
                    {
                        output.Officer.HandTargets[arm] = Target(c.OfficerId, ChoreoPoint.Chest, fist ? 1 : .1f);
                        output.Officer.HandTargets[arm].Offset = new ChoreoVector(arm == 0 ? -.18f : .18f, .2f, arm == lead && step.Phase == ChoreoPhase.Windup ? -.12f : .08f);
                    }
                }
                else
                {
                    output.Officer.HandTargets[lead] = Target(c.ResidentId, c.HitPart != ChoreoPoint.None ? c.HitPart : step.Left, fist ? 1 : .1f);
                    if (c.Action != ChoreoAction.Shove) output.Officer.HandTargets[1 - lead] = Target(c.OfficerId, ChoreoPoint.Chest, fist ? 1 : .1f);
                }
            }
            if (c.Action == ChoreoAction.Escort && c.PartnerHeadingKnown && !c.EscortContact)
            {
                output.Officer.Phase = ChoreoPhase.Setup; output.Officer.StepName = "reposition before elbow contact";
                output.Officer.HandTargets[0].Weight = output.Officer.HandTargets[1].Weight = 0;
            }
            if (c.Action == ChoreoAction.Guard)
            {
                for (int arm = 0; arm < 2; arm++)
                {
                    output.Officer.HandTargets[arm] = Target(c.OfficerId, ChoreoPoint.Chest, .5f);
                    output.Officer.HandTargets[arm].Offset = new ChoreoVector(arm == 0 ? -.18f : .18f, .25f, .15f);
                }
            }
            if (strike && !c.Landed && !c.ContactPending && step.Phase == ChoreoPhase.Contact)
            {
                int lead = c.LeadHand == 1 ? 1 : 0;
                output.Officer.HandTargets[lead].IsWorld = true;
                output.Officer.HandTargets[lead].World = new ChoreoVector(c.ResidentGround.X, 1.5f, c.ResidentGround.Y);
            }
            if (c.PartnerGuarding && !c.PartnerGrounded && !c.PartnerCuffed)
            {
                for (int arm = 0; arm < 2; arm++)
                {
                    output.Resident.HandTargets[arm] = Target(c.ResidentId, ChoreoPoint.Chest, .5f);
                    output.Resident.HandTargets[arm].Offset = new ChoreoVector(arm == 0 ? -.15f : .15f, .25f, .15f);
                }
            }
            if (c.ResidentId == "")
                for (int arm = 0; arm < 2; arm++)
                    if (output.Officer.HandTargets[arm].Active && output.Officer.HandTargets[arm].ActorId == "")
                    {
                        Point aim = c.OfficerGround + Rotate(new Point(arm == 0 ? -.1f : .1f, .7f), c.OfficerYaw);
                        output.Officer.HandTargets[arm].IsWorld = true; output.Officer.HandTargets[arm].World = new ChoreoVector(aim.X, 1.5f, aim.Y);
                    }
            if (c.Resisting && !c.PartnerCuffed && !c.PartnerGrounded && output.Resident.ResidentPhase != ResidentActionPhase.Prone && output.Resident.ResidentPhase != ResidentActionPhase.Cuffed && output.Resident.ResidentPhase != ResidentActionPhase.Rise && output.Resident.ResidentPhase != ResidentActionPhase.Fall)
                output.Resident.ResidentPhase = ResidentActionPhase.Resist;
            if (!c.Holding && (strike || c.Action == ChoreoAction.Grab)) Reaction(output.Resident, c, t, step);
            foreach (ChoreoEvent cue in track.Cues)
            {
                if (cue.Time <= previousSeconds || cue.Time > t) continue;
                if ((cue.Kind == ChoreoEventKind.Impact || cue.Kind == ChoreoEventKind.Fall) && !c.Landed) continue;
                bool subjectEvent = cue.Kind == ChoreoEventKind.Fall || cue.Kind == ChoreoEventKind.Lift;
                ChoreoEvent e = cue; e.ActorId = subjectEvent ? c.ResidentId : c.OfficerId;
                e.PartnerId = subjectEvent ? c.OfficerId : c.ResidentId;
                e.Id = (c.ActionId ?? "") + ":" + cue.Index;
                e.Direction = Direction(c);
                if (cue.Kind == ChoreoEventKind.Impact) { if (c.Action == ChoreoAction.Strike || c.Action == ChoreoAction.Counter || c.Action == ChoreoAction.Grab) e.Side = c.LeadHand == 1 ? ChoreoSide.Right : ChoreoSide.Left; e.Strength = c.Strength > 0 ? c.Strength : cue.Strength; if (c.HitPart != ChoreoPoint.None) e.Part = c.HitPart; }
                (subjectEvent ? output.Resident.Events : output.Officer.Events).Add(e);
            }
            if (strike && c.Landed && c.Reaction == ChoreoReaction.Fall)
            {
                float fallAt = track.Steps[0].End + .04f;
                if (fallAt > previousSeconds && fallAt <= t) output.Resident.Events.Add(new ChoreoEvent { Id = (c.ActionId ?? "") + ":32", Index = 32, ActorId = c.ResidentId, PartnerId = c.OfficerId, Kind = ChoreoEventKind.Fall, Time = fallAt, Direction = Direction(c), Part = c.HitPart, Strength = c.Strength });
            }
        }
        static ChoreoVector Direction(ActionChoreoContext c)
        {
            ChoreoVector d = c.HitDirection.Length > .0001f ? c.HitDirection : new ChoreoVector(c.ResidentGround.X - c.OfficerGround.X, 0, c.ResidentGround.Y - c.OfficerGround.Y);
            float length = d.Length; return length > .0001f ? new ChoreoVector(d.X / length, d.Y / length, d.Z / length) : new ChoreoVector(0, 0, 1);
        }
        static void Init(ActionChoreoFrame f, ActionChoreoContext c, ChoreoStep s, float t, float duration, bool resident)
        {
            f.ActionId = c.ActionId ?? ""; f.ActorId = resident ? c.ResidentId : c.OfficerId; f.PartnerId = resident ? c.OfficerId : c.ResidentId;
            f.Action = c.Action; f.StepName = s.Name; f.IsResident = resident != c.ActorIsResident; f.Phase = t >= duration && c.Action != ChoreoAction.Escort ? ChoreoPhase.Complete : s.Phase;
            f.ResidentPhase = resident ? s.Resident : ResidentActionPhase.Brace; f.Progress = Clamp((t - s.Start) / Math.Max(.0001f, s.Duration)); f.OverallProgress = Clamp(t / duration);
            f.ElapsedSeconds = t; f.DurationSeconds = duration; f.Lean = resident ? 0 : s.Lean; f.Crouch = resident && s.Resident == ResidentActionPhase.Kneel ? 1 : s.Crouch;
            f.ReactionDirection = Direction(c); f.ReactionPart = c.HitPart == ChoreoPoint.None ? ChoreoPoint.Chest : c.HitPart; f.ReactionStrength = c.Strength;
            f.CanCancel = CancelWindow(c.Action, s.Phase);
            f.CrossAnkles = resident && s.CrossAnkles; f.KneeTarget = default(ChoreoTarget); f.Events.Clear(); f.HandTargets[0] = f.HandTargets[1] = default(ChoreoTarget);
        }
        static void ResidentHands(ActionChoreoFrame f, string id)
        {
            if (f.ResidentPhase == ResidentActionPhase.HandsOnHead || f.ResidentPhase == ResidentActionPhase.Interlace || f.ResidentPhase == ResidentActionPhase.Kneel)
            {
                f.HandTargets[0] = f.HandTargets[1] = Target(id, ChoreoPoint.Head, .15f);
                f.HandTargets[0].Offset = new ChoreoVector(-.06f, .06f, -.04f); f.HandTargets[1].Offset = new ChoreoVector(.06f, .06f, -.04f);
                f.HandTargets[0].Orientation = f.HandTargets[1].Orientation = ChoreoHandOrientation.Interlaced;
            }
            else if (f.ResidentPhase == ResidentActionPhase.Cuffed || f.ResidentPhase == ResidentActionPhase.Rise || f.ResidentPhase == ResidentActionPhase.Escorted || f.ResidentPhase == ResidentActionPhase.Prone || f.ResidentPhase == ResidentActionPhase.PresentWrists || f.ResidentPhase == ResidentActionPhase.SideLying || f.ResidentPhase == ResidentActionPhase.Seated)
            {
                SetRearHands(f, id);
            }
        }
        public static bool CancelWindow(ChoreoAction action, ChoreoPhase phase)
        {
            bool search = action == ChoreoAction.PatDown || action == ChoreoAction.PocketSearch;
            bool cuff = action == ChoreoAction.StandingCuff || action == ChoreoAction.KneelingCuff || action == ChoreoAction.ProneCuff;
            return (search || cuff) && (phase == ChoreoPhase.Approach || phase == ChoreoPhase.Setup);
        }
        static void SetRearHands(ActionChoreoFrame f, string id)
        {
            f.HandTargets[0] = f.HandTargets[1] = Target(id, ChoreoPoint.SmallOfBack, .1f);
            f.HandTargets[0].Offset = new ChoreoVector(-.07f, 0, -.04f); f.HandTargets[1].Offset = new ChoreoVector(.07f, 0, -.04f);
            f.HandTargets[0].Orientation = f.HandTargets[1].Orientation = ChoreoHandOrientation.PalmOutThumbUp;
        }
        static void Reaction(ActionChoreoFrame f, ActionChoreoContext c, float t, ChoreoStep s)
        {
            if (s.Phase == ChoreoPhase.Windup || s.Phase == ChoreoPhase.Approach) { f.ResidentPhase = c.Resisting ? ResidentActionPhase.Resist : ResidentActionPhase.Brace; return; }
            if (!c.Landed) { f.ResidentPhase = ResidentActionPhase.Brace; return; }
            f.ResidentPhase = c.Reaction == ChoreoReaction.Fall ? (t < Track(c.Action).Duration ? ResidentActionPhase.Fall : ResidentActionPhase.Prone) : c.Reaction == ChoreoReaction.Stagger ? ResidentActionPhase.Stagger : ResidentActionPhase.Flinch;
        }
        static ChoreoTarget Target(string id, ChoreoPoint point, float grip) { return new ChoreoTarget { ActorId = id ?? "", Point = point, Weight = point == ChoreoPoint.None ? 0 : 1, Grip = Clamp(grip) }; }
        static Point Rotate(Point local, float yaw) { float a = yaw * (float)Math.PI / 180; return new Point(local.X * (float)Math.Cos(a) + local.Y * (float)Math.Sin(a), -local.X * (float)Math.Sin(a) + local.Y * (float)Math.Cos(a)); }
        static float Bearing(Point d) { return (float)(Math.Atan2(d.X, d.Y) * 180 / Math.PI); }
        static float Clamp(float x) { return Math.Max(0, Math.Min(1, x)); }
    }
}
