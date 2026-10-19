using System;
using System.Collections.Generic;

namespace Cellblock.Core
{
    public static partial class ActionChoreo
    {
        static readonly Dictionary<ChoreoAction, ChoreoTrack> tracks = BuildTracks();
        public static ChoreoTrack Track(ChoreoAction action) { return tracks[action]; }
        static ChoreoStep Step(string name, float duration, ChoreoPhase phase, ResidentActionPhase resident, ChoreoPoint left = ChoreoPoint.None, ChoreoPoint right = ChoreoPoint.None, bool knee = false, float crouch = 0, bool crossed = false)
        { return new ChoreoStep { Name = name, End = duration, Phase = phase, Resident = resident, Left = left, Right = right, LeftGrip = .3f, RightGrip = .3f, Knee = knee, Crouch = crouch, CrossAnkles = crossed, Lean = crouch * 15 }; }
        static ChoreoTrack Make(ChoreoAction action, ChoreoStep[] steps, params ChoreoEvent[] cues)
        {
            float at = 0; foreach (ChoreoStep s in steps) { float duration = s.End; s.Start = at; s.End = at += duration; }
            for (int i = 0; i < cues.Length; i++) cues[i].Index = i;
            return new ChoreoTrack(action, steps, cues);
        }
        static ChoreoEvent Cue(ChoreoEventKind kind, float time, ChoreoPoint part = ChoreoPoint.None, ChoreoSide side = ChoreoSide.None, float strength = 0)
        { return new ChoreoEvent { Kind = kind, Time = time, Part = part, Side = side, Strength = strength }; }
        static Dictionary<ChoreoAction, ChoreoTrack> BuildTracks()
        {
            var result = new Dictionary<ChoreoAction, ChoreoTrack>();
            foreach (ChoreoAction a in new[] { ChoreoAction.PatDown, ChoreoAction.PocketSearch })
            {
                var steps = new List<ChoreoStep> {
                    Step("rear approach", .4f, ChoreoPhase.Approach, ResidentActionPhase.Brace),
                    Step("hands on head", .3f, ChoreoPhase.Setup, ResidentActionPhase.HandsOnHead),
                    Step("control interlaced fingers", .3f, ChoreoPhase.Control, ResidentActionPhase.Interlace, ChoreoPoint.InterlacedHands, ChoreoPoint.CollarL)
                };
                // Hold the interlaced hands with one hand; sweep in a deterministic order with the other.
                ChoreoPoint[] left = { ChoreoPoint.CollarL, ChoreoPoint.UpperArmL, ChoreoPoint.ArmpitL, ChoreoPoint.Chest, ChoreoPoint.WaistL, ChoreoPoint.SmallOfBack, ChoreoPoint.PocketL, ChoreoPoint.GroinL, ChoreoPoint.ThighL, ChoreoPoint.KneeL, ChoreoPoint.AnkleL };
                ChoreoPoint[] right = { ChoreoPoint.CollarR, ChoreoPoint.UpperArmR, ChoreoPoint.ArmpitR, ChoreoPoint.Chest, ChoreoPoint.WaistR, ChoreoPoint.SmallOfBack, ChoreoPoint.PocketR, ChoreoPoint.GroinR, ChoreoPoint.ThighR, ChoreoPoint.KneeR, ChoreoPoint.AnkleR };
                if (a == ChoreoAction.PocketSearch) { left = new[] { ChoreoPoint.WaistL, ChoreoPoint.PocketL, ChoreoPoint.SmallOfBack }; right = new[] { ChoreoPoint.WaistR, ChoreoPoint.PocketR, ChoreoPoint.SmallOfBack }; }
                foreach (ChoreoPoint p in left) steps.Add(Step("sweep left " + AnchorName(p), a == ChoreoAction.PatDown ? .25f : .55f, ChoreoPhase.Sweep, ResidentActionPhase.Interlace, ChoreoPoint.InterlacedHands, p, false, p == ChoreoPoint.AnkleL ? .9f : p == ChoreoPoint.KneeL ? .55f : .1f));
                steps.Add(Step("switch sides while maintaining control", .25f, ChoreoPhase.Control, ResidentActionPhase.Interlace, ChoreoPoint.InterlacedHands, ChoreoPoint.WristR));
                foreach (ChoreoPoint p in right) steps.Add(Step("sweep right " + AnchorName(p), a == ChoreoAction.PatDown ? .25f : .55f, ChoreoPhase.Sweep, ResidentActionPhase.Interlace, p, ChoreoPoint.InterlacedHands, false, p == ChoreoPoint.AnkleR ? .9f : p == ChoreoPoint.KneeR ? .55f : .1f));
                steps.Add(Step("release search control", .25f, ChoreoPhase.Release, ResidentActionPhase.Brace)); steps.Add(Step("recover", .3f, ChoreoPhase.Recover, ResidentActionPhase.Brace));
                result[a] = Make(a, steps.ToArray());
            }
            result[ChoreoAction.StandingCuff] = Make(ChoreoAction.StandingCuff, new[] {
                Step("rear approach", .3f, ChoreoPhase.Approach, ResidentActionPhase.Brace),
                Step("palms out thumbs up", .3f, ChoreoPhase.Setup, ResidentActionPhase.Cuffed, ChoreoPoint.ElbowL, ChoreoPoint.WristL),
                Step("elbow and first wrist control", .3f, ChoreoPhase.Control, ResidentActionPhase.Cuffed, ChoreoPoint.WristL, ChoreoPoint.ElbowL),
                Step("first cuff", .35f, ChoreoPhase.Apply, ResidentActionPhase.Cuffed, ChoreoPoint.WristL, ChoreoPoint.ElbowL),
                Step("control second wrist", .35f, ChoreoPhase.Control, ResidentActionPhase.Cuffed, ChoreoPoint.WristL, ChoreoPoint.WristR),
                Step("second cuff", .35f, ChoreoPhase.Apply, ResidentActionPhase.Cuffed, ChoreoPoint.WristL, ChoreoPoint.WristR),
                Step("double lock", .2f, ChoreoPhase.DoubleLock, ResidentActionPhase.Cuffed, ChoreoPoint.WristL, ChoreoPoint.WristR),
                Step("recover", .3f, ChoreoPhase.Recover, ResidentActionPhase.Cuffed)
            }, Cue(ChoreoEventKind.CuffClick, 1.10f, ChoreoPoint.WristL, ChoreoSide.Left), Cue(ChoreoEventKind.CuffClick, 1.80f, ChoreoPoint.WristR, ChoreoSide.Right));
            result[ChoreoAction.KneelingCuff] = Make(ChoreoAction.KneelingCuff, new[] {
                Step("rear approach", .35f, ChoreoPhase.Approach, ResidentActionPhase.HandsOnHead),
                Step("kneel and cross ankles", .45f, ChoreoPhase.Setup, ResidentActionPhase.Kneel, ChoreoPoint.InterlacedHands, ChoreoPoint.ElbowL, false, .3f, true),
                Step("control interlaced fingers", .3f, ChoreoPhase.Control, ResidentActionPhase.Kneel, ChoreoPoint.InterlacedHands, ChoreoPoint.WristL, false, .45f, true),
                Step("first cuff", .4f, ChoreoPhase.Apply, ResidentActionPhase.Kneel, ChoreoPoint.WristL, ChoreoPoint.ElbowL, false, .45f, true),
                Step("second wrist", .3f, ChoreoPhase.Control, ResidentActionPhase.Kneel, ChoreoPoint.WristL, ChoreoPoint.WristR, false, .45f, true),
                Step("second cuff", .4f, ChoreoPhase.Apply, ResidentActionPhase.Kneel, ChoreoPoint.WristL, ChoreoPoint.WristR, false, .45f, true),
                Step("double lock", .2f, ChoreoPhase.DoubleLock, ResidentActionPhase.Kneel, ChoreoPoint.WristL, ChoreoPoint.WristR, false, .45f),
                Step("recover", .3f, ChoreoPhase.Recover, ResidentActionPhase.Kneel, ChoreoPoint.None, ChoreoPoint.None, false, .3f, true)
            }, Cue(ChoreoEventKind.CuffClick, 1.3f, ChoreoPoint.WristL, ChoreoSide.Left), Cue(ChoreoEventKind.CuffClick, 2f, ChoreoPoint.WristR, ChoreoSide.Right));
            result[ChoreoAction.ProneCuff] = Make(ChoreoAction.ProneCuff, new[] {
                Step("follow to shoulder side", .25f, ChoreoPhase.Approach, ResidentActionPhase.Prone),
                Step("light shoulder blade control", .4f, ChoreoPhase.Control, ResidentActionPhase.Prone, ChoreoPoint.WristL, ChoreoPoint.ElbowL, true, .9f),
                Step("secure first hand", .4f, ChoreoPhase.Apply, ResidentActionPhase.Prone, ChoreoPoint.WristL, ChoreoPoint.ElbowL, true, .9f),
                Step("sweep lower back", .5f, ChoreoPhase.Sweep, ResidentActionPhase.Prone, ChoreoPoint.WristL, ChoreoPoint.SmallOfBack, true, .9f),
                Step("secure second hand", .4f, ChoreoPhase.Control, ResidentActionPhase.Prone, ChoreoPoint.WristL, ChoreoPoint.WristR, true, .9f),
                Step("second cuff", .4f, ChoreoPhase.Apply, ResidentActionPhase.Prone, ChoreoPoint.WristL, ChoreoPoint.WristR, true, .9f),
                Step("double lock", .25f, ChoreoPhase.DoubleLock, ResidentActionPhase.Prone, ChoreoPoint.WristL, ChoreoPoint.WristR, false, .8f),
                Step("roll to side", .4f, ChoreoPhase.Release, ResidentActionPhase.SideLying, ChoreoPoint.ElbowL, ChoreoPoint.ShoulderBlade, false, .8f),
                Step("assist to sitting", .5f, ChoreoPhase.Recover, ResidentActionPhase.Seated, ChoreoPoint.ElbowL, ChoreoPoint.SmallOfBack, false, .6f)
            }, Cue(ChoreoEventKind.CuffClick, .85f, ChoreoPoint.WristL, ChoreoSide.Left), Cue(ChoreoEventKind.CuffClick, 2.15f, ChoreoPoint.WristR, ChoreoSide.Right), Cue(ChoreoEventKind.Lift, 3f));
            result[ChoreoAction.Takedown] = Make(ChoreoAction.Takedown, new[] {
                Step("control shoulder and elbow", .16f, ChoreoPhase.Windup, ResidentActionPhase.Brace, ChoreoPoint.ElbowL, ChoreoPoint.ShoulderBlade),
                Step("break balance", .24f, ChoreoPhase.Control, ResidentActionPhase.Stagger, ChoreoPoint.ElbowL, ChoreoPoint.Chest, false, .35f),
                Step("guide down", .32f, ChoreoPhase.Contact, ResidentActionPhase.Fall, ChoreoPoint.ElbowL, ChoreoPoint.ShoulderBlade, false, .55f),
                Step("follow without falling", .28f, ChoreoPhase.Control, ResidentActionPhase.Prone, ChoreoPoint.WristL, ChoreoPoint.ElbowL, false, .85f),
                Step("recover in control", .3f, ChoreoPhase.Recover, ResidentActionPhase.Prone, ChoreoPoint.WristL, ChoreoPoint.ElbowL, false, .8f)
            }, Cue(ChoreoEventKind.Fall, .45f, ChoreoPoint.Chest), Cue(ChoreoEventKind.Impact, .65f, ChoreoPoint.Chest, ChoreoSide.None, .45f));
            foreach (ChoreoAction a in new[] { ChoreoAction.Strike, ChoreoAction.Counter, ChoreoAction.Shove, ChoreoAction.Grab })
            {
                float wind = a == ChoreoAction.Counter ? .12f : a == ChoreoAction.Shove ? .18f : .16f;
                float recovery = a == ChoreoAction.Counter ? .28f : .30f;
                ChoreoPoint part = a == ChoreoAction.Shove ? ChoreoPoint.Chest : a == ChoreoAction.Grab ? ChoreoPoint.ElbowL : ChoreoPoint.Jaw;
                result[a] = Make(a, new[] { Step("anticipation", wind, ChoreoPhase.Windup, ResidentActionPhase.Brace, part, a == ChoreoAction.Shove ? part : ChoreoPoint.None),
                    Step("contact", .08f, ChoreoPhase.Contact, ResidentActionPhase.Flinch, part, a == ChoreoAction.Shove ? part : ChoreoPoint.None),
                    Step("recover", recovery, ChoreoPhase.Recover, ResidentActionPhase.Flinch) }, Cue(ChoreoEventKind.Impact, wind, part, ChoreoSide.Left, a == ChoreoAction.Shove ? .35f : .6f));
            }
            result[ChoreoAction.Escort] = Make(ChoreoAction.Escort, new[] { Step("behind and beside, elbow and wrist", 1, ChoreoPhase.Control, ResidentActionPhase.Escorted, ChoreoPoint.ElbowL, ChoreoPoint.WristL) });
            result[ChoreoAction.GetUp] = Make(ChoreoAction.GetUp, new[] {
                Step("roll to side", .2f, ChoreoPhase.Setup, ResidentActionPhase.SideLying, ChoreoPoint.ElbowL, ChoreoPoint.ShoulderBlade, false, .8f),
                Step("assist to sitting", .25f, ChoreoPhase.Control, ResidentActionPhase.Seated, ChoreoPoint.ElbowL, ChoreoPoint.SmallOfBack, false, .7f),
                Step("gather legs", .25f, ChoreoPhase.Control, ResidentActionPhase.Kneel, ChoreoPoint.ElbowL, ChoreoPoint.ShoulderBlade, false, .5f),
                Step("assist upright", .35f, ChoreoPhase.Apply, ResidentActionPhase.Rise, ChoreoPoint.ElbowL, ChoreoPoint.SmallOfBack, false, .25f),
                Step("escort ready", .15f, ChoreoPhase.Recover, ResidentActionPhase.Escorted, ChoreoPoint.ElbowL, ChoreoPoint.WristL)
            }, Cue(ChoreoEventKind.Lift, .45f));
            return result;
        }
    }
}
