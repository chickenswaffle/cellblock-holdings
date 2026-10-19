using System;
using System.Collections.Generic;
using Cellblock.Core;
using Cellblock.Unity;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cellblock.Editor
{
    // Actual observed positions after the game camera renders. Never substitute a requested IK target for a hand.
    internal sealed class ActionChoreoContactChecks : IDisposable
    {
        readonly GameController game;
        readonly List<string> errors, notes;
        readonly HashSet<string> seen = new HashSet<string>();
        readonly bool required;
        int clicks, knees, roots; float worstHand, worstKnee, worstRoot;
        bool missing, disposed;
        public ActionChoreoContactChecks(GameController game, List<string> errors, List<string> notes, bool required)
        {
            this.game = game; this.errors = errors; this.notes = notes; this.required = required;
            RenderPipelineManager.endCameraRendering += Observe;
        }
        void Observe(ScriptableRenderContext context, Camera camera)
        {
            if (game == null || camera != game.ArtCamera) return;
            FacilityView view = game.GetComponentInChildren<FacilityView>();
            if (view == null || view.ActionChoreoConsumerVersion < ActionChoreo.Version) { missing = true; return; }
            World world = game.World;
            if (world == null || world.CharacterId == "") return;
            ActionChoreoFrame frame;
            if (!view.TryGetActionChoreoFrame(world.CharacterId, out frame)) return;
            Vector3 ground, left, right, knee;
            if (!view.TryGetActionChoreoObservation(frame.ActorId, out ground, out left, out right, out knee)) { Fail("No rendered actor observation."); return; }
            foreach (ChoreoEvent e in frame.Events)
            {
                if (e.Kind != ChoreoEventKind.CuffClick || !seen.Add(e.Id)) continue;
                int side = e.Side == ChoreoSide.Left ? 0 : 1; ChoreoTarget target = frame.HandTargets[side];
                Vector3 wrist;
                if (target.Point != e.Part || !view.TryResolveActionChoreoTarget(target, out wrist)) { Fail("CuffClick has no matching wrist target: " + e.Id); continue; }
                float error = Vector3.Distance(side == 0 ? left : right, wrist); worstHand = Mathf.Max(worstHand, error); clicks++;
                if (error > ActionChoreo.HandContactTolerance) Fail("CuffClick hand error " + error.ToString("0.000") + " m > 0.030 m: " + e.Id);
            }
            if (frame.KneeTarget.Active)
            {
                Vector3 blade;
                if (frame.KneeTarget.Point != ChoreoPoint.ShoulderBlade || !view.TryResolveActionChoreoTarget(frame.KneeTarget, out blade)) Fail("Prone cuff knee target is not the shoulder blade.");
                else { float error = Vector3.Distance(knee, blade); worstKnee = Mathf.Max(worstKnee, error); knees++; if (error > ActionChoreo.KneeContactTolerance) Fail("Prone cuff knee error " + error.ToString("0.000") + " m > 0.050 m."); }
            }
            if (frame.Phase == ChoreoPhase.Contact)
            {
                float error = Vector2.Distance(new Vector2(ground.x, ground.z), new Vector2(frame.Root.Ground.X, frame.Root.Ground.Y));
                roots++; worstRoot = Mathf.Max(worstRoot, error); if (error > ActionChoreo.RootContactTolerance) Fail("Contact root error " + error.ToString("0.000") + " m > 0.050 m.");
            }
        }
        void Fail(string message) { if (!errors.Contains(message)) errors.Add(message); }
        public void Finish(bool expectKnee)
        {
            FacilityView view = game == null ? null : game.GetComponentInChildren<FacilityView>();
            if (missing || view == null || view.ActionChoreoConsumerVersion < ActionChoreo.Version)
            {
                notes.Add("ActionChoreo contact gates NOT EXECUTED: Claude consumer version1 is not registered. No contact pass is claimed.");
                if (required) Fail("Required ActionChoreo contact gates blocked: no registered consuming rig adapter.");
            }
            else
            {
                notes.Add("Observed ActionChoreo contact samples: cuffs=" + clicks + " knees=" + knees + " roots=" + roots + "; max metres=" + worstHand + "/" + worstKnee + "/" + worstRoot);
                if (clicks < 2 || roots == 0 || expectKnee && knees == 0) Fail("ActionChoreo contact coverage incomplete; actual clicks/contact frames must render without clock jumps.");
            }
            Dispose();
        }
        public void Dispose()
        { if (disposed) return; disposed = true; RenderPipelineManager.endCameraRendering -= Observe; }
    }
}
