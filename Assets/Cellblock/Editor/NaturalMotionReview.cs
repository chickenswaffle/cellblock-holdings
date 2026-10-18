using System;
using System.Collections.Generic;
using System.Globalization;
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
    // Driven exclusively by PresentationPreview's existing editor runner. Measures actual rig transforms;
    // the fixed-step path is presentation instrumentation, never an alternative simulation or saved company.
    public static class NaturalMotionReview
    {
        const float Dt = 1f / 30;
        static readonly string[] Names = { "walk", "stop", "corner", "reverse", "run", "settle", "walk2x", "stop2x", "walk4x", "stop4x", "pause" };
        static readonly int[] Lengths = { 120, 90, 75, 90, 90, 90, 90, 45, 90, 45, 120 };
        static readonly List<string> failures = new List<string>(), captures = new List<string>();
        static readonly List<StageMetrics> metrics = new List<StageMetrics>();
        static readonly List<string> galleryIds = new List<string>();
        static FacilityView view;
        static GameController owner;
        static StreamWriter timeline;
        static string output, actorId, coreSnapshot;
        static int stage, frame, lastFrame, totalFrames, gallery;
        static float previousCaptureDelta;
        static bool running, completed;
        static Point target;
        static FacilityView.NaturalMotionSample previous;
        static FacilityView.NaturalMotionSample pauseBaseline;
        static StageMetrics current;

        [Serializable] sealed class StageMetrics
        {
            public string name;
            public int frames, plantedComparisons, stanceSamples, movingFrames, rate;
            public float maxPlantedDrift, maxAnchorError, maxAnchorJump, maxMidStanceTilt, maxYawStep, maxRootSpeed, maxRootAcceleration, maxPace;
            public float finalPace, finalDistance, travelled;
            public float maxPausedJointDrift, maxPausedJointRotation, maxPausedClockDelta;
            public bool finite = true;
        }
        [Serializable] sealed class MotionReport
        {
            public bool passed, coreUnchanged;
            public string status, editor, message, output;
            public int renderedFrames;
            public float dt;
            public StageMetrics[] stages;
            public string[] errors, captures;
        }

        public static void Begin(GameController game)
        {
            if (running) throw new InvalidOperationException("A natural motion review is already active.");
            if (!EditorApplication.isPlaying || game == null) throw new InvalidOperationException("The existing Unity Play runner is required.");
            owner = game; failures.Clear(); captures.Clear(); metrics.Clear(); galleryIds.Clear();
            stage = frame = totalFrames = gallery = 0; lastFrame = -1; previous = pauseBaseline = null; coreSnapshot = ""; completed = false;
            previousCaptureDelta = Time.captureDeltaTime; Time.captureDeltaTime = Dt;
            output = Path.GetFullPath(Path.Combine("Temp/CellblockNaturalMotion", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6)));
            Directory.CreateDirectory(output);
            timeline = new StreamWriter(Path.Combine(output, "motion.csv")) { AutoFlush = true };
            timeline.WriteLine("stage,frame,x,y,z,pace,rootSpeed,yaw,leftPlanted,leftX,leftY,leftZ,rightPlanted,rightX,rightY,rightZ");
            running = true;
            try { Prepare(game); StartStage(); WriteReport(false, "running", new string[0]); }
            catch (Exception error) { Abort(error.ToString(), new string[0]); throw; }
            Debug.Log("CELLBLOCK_NATURAL_MOTION_STARTED " + output);
        }

        static void Prepare(GameController game)
        {
            game.StartPresentationPreview("county"); World w = game.World; w.Data.Minute = 541; w.Data.Speed = 0;
            w.SetAutoJobs(false); w.Data.Justice.NextAttemptMinute = int.MaxValue / 2;
            w.Population.MoratoriumUntil = w.Population.NextSurgeMinute = int.MaxValue / 2; w.Population.Backlog = 0;
            w.Data.Intake.Cases.Clear();
            foreach (Resident r in w.Data.Residents)
            {
                r.Activity = r.ActivityPartner = r.ActivityFixture = ""; r.NextPlanMinute = int.MaxValue / 2;
                r.Legal.Custody = "In facility"; r.Legal.RestrictedUntil = r.Legal.NextCourtMinute = -1;
            }
            actorId = w.Data.Residents[0].Id; target = new Point(10, 14);
            w.Data.Residents[0].Position = target;
            // Actual rendered staff roles, using a disposable company rather than standalone lookalike meshes.
            string[] roles = { "Officer", "Caseworker", "Technician", "Nurse", "Warden" };
            for (int i = 0; i < roles.Length; i++)
            {
                string id = "natural-review-" + roles[i].ToLowerInvariant();
                Point p = new Point(11 + (i % 4) * 1.8f, 9 - (i / 4) * 2.4f);
                string role = roles[i] == "Nurse" ? "Caseworker" : roles[i] == "Warden" ? "Officer" : roles[i];
                var staff = new StaffMember { Id = id, Name = roles[i] + " review", Role = role, Arrived = true, Duty = "Walking", Position = p, Station = p, Destination = p, ShiftStart = 9, NextDutyMinute = int.MaxValue / 2 };
                w.Data.Staff.Add(staff); PersonnelRules.EnsureFiles(w.Data); PersonnelFile file = w.FileFor(staff);
                if (roles[i] == "Warden" || roles[i] == "Nurse") file.Post = roles[i];
                galleryIds.Add(id);
            }
            var crew = new Crew { Id = "natural-review-construction", Name = "Review construction", Arrived = true, Position = new Point(16, 6.6f), HelperPosition = new Point(17.2f, 6.6f) };
            w.Data.Crews.Add(crew); galleryIds.Add(crew.Id + "/builder-0");
            galleryIds.Insert(0, actorId);
            if (w.Data.Residents.Count > 1) { w.Data.Residents[1].Position = new Point(18, 9); galleryIds.Add(w.Data.Residents[1].Id); }
            game.ReloadPresentationPreview(); view = game.GetComponentInChildren<FacilityView>();
            if (view == null) throw new InvalidOperationException("No facility renderer.");
            view.PreviewFixedFrameDelta = Dt;
            foreach (string id in galleryIds) view.ConfigureNaturalMotionProbe(id, .9f);
            previous = view.ProbeNaturalMotion(actorId, target, Dt, true);
            foreach (string id in galleryIds.Where(id => id != actorId))
            {
                Point p = PositionFor(w = game.World, id); view.ProbeNaturalMotion(id, p, Dt, true);
            }
            Require(game.World.Routes.Clear(new Point(10, 14), new Point(27, 14)) && game.World.Routes.Clear(new Point(15, 10), new Point(15, 18)), "The presentation path is blocked by facility geometry.");
            coreSnapshot = JsonUtility.ToJson(game.World.Data);
        }

        static Point PositionFor(World w, string id)
        {
            Resident resident = w.Data.Residents.FirstOrDefault(r => r.Id == id);
            if (resident != null) return resident.Position;
            StaffMember staff = w.Data.Staff.FirstOrDefault(s => s.Id == id);
            if (staff != null) return staff.Position;
            return w.Data.Crews.First(c => id == c.Id + "/builder-0").Position;
        }
        static void StartStage()
        {
            string name = Names[stage];
            int rate = name == "pause" ? 0 : name.EndsWith("4x", StringComparison.Ordinal) ? 4 : name.EndsWith("2x", StringComparison.Ordinal) ? 2 : 1;
            frame = 0; current = new StageMetrics { name = name, rate = rate }; metrics.Add(current);
            view.ConfigureNaturalMotionProbe(actorId, name == "run" ? 2.4f : .9f, rate);
            // Each accelerated fixture starts at the same clear lane boundary. The reset is outside
            // measurement; comparisons begin with the real snapped pose, not the preceding fixture.
            if (name == "walk2x" || name == "walk4x")
            {
                target = new Point(27, 14); previous = view.ProbeNaturalMotion(actorId, target, Dt, true);
            }
        }

        public static bool TickReview(GameController game, string[] errors)
        {
            if (completed) return true;
            if (!running || owner != game) throw new InvalidOperationException("Natural motion review state was lost. Start a fresh request.");
            if (Time.frameCount == lastFrame) return false; lastFrame = Time.frameCount;
            if (errors.Length > 0) { Abort("Runtime errors occurred during the motion review.", errors); return true; }
            if (stage < Names.Length)
            {
                Step(game); return false;
            }
            if (gallery < galleryIds.Count)
            {
                string id = galleryIds[gallery++]; var sample = view.ReadNaturalMotionProbe(id);
                Require(sample.Finite, "Nonfinite gallery joints for " + id);
                float yaw = sample.Rotation.eulerAngles.y;
                Capture(game, "gallery-" + gallery.ToString("00") + "-" + Safe(id), sample, 1.65f, yaw + 158, 5);
                Capture(game, "face-" + gallery.ToString("00") + "-" + Safe(id), sample, .52f, yaw + 174, 0, true);
                return false;
            }
            Require(metrics.Sum(m => m.plantedComparisons) > 60, "Insufficient actual planted-foot comparisons.");
            Require(metrics.First(m => m.name == "walk").travelled > 3, "The actual walking root did not traverse its path.");
            Require(metrics.First(m => m.name == "run").maxRootSpeed > 2.4f, "The actual actor did not run.");
            int expectedCaptures = Lengths.Sum(length => (length + 9) / 10) + galleryIds.Count * 2;
            Require(captures.Count == expectedCaptures && captures.All(File.Exists), "Missing natural motion or character gallery captures.");
            Require(coreSnapshot == JsonUtility.ToJson(game.World.Data), "Presentation probes changed the paused simulation state.");
            Complete(errors); return true;
        }

        static void Step(GameController game)
        {
            string name = Names[stage]; frame++; totalFrames++;
            if (name == "walk") target += new Point(1.25f * Dt, 0);
            else if (name == "corner") target += new Point(0, 1.25f * Dt);
            else if (name == "reverse") target += new Point(0, -1.25f * Dt);
            else if (name == "run") target += new Point(3.4f * Dt, 0);
            else if (name == "walk2x" || name == "walk4x") target += new Point(-1.25f * current.rate * Dt, 0);
            var sample = view.ProbeNaturalMotion(actorId, target, Dt);
            Measure(sample); previous = sample;
            if (name == "pause") MeasurePause(sample);
            if (frame % 10 == 0 || frame == Lengths[stage]) Capture(game, name + "-" + frame.ToString("000"), sample, 2.15f, 145, 12);
            if (frame < Lengths[stage]) return;
            current.finalPace = sample.Pace; current.finalDistance = Vector2.Distance(new Vector2(sample.Position.x, sample.Position.z), new Vector2(target.X, target.Y));
            Require(current.finite, name + ": nonfinite generated rig transform.");
            Require(current.maxPlantedDrift < .002f, name + ": consecutive planted ankle drift " + current.maxPlantedDrift.ToString("F4") + " m exceeds 2 mm.");
            Require(current.maxAnchorJump < .002f, name + ": consecutive planted anchor moved " + current.maxAnchorJump.ToString("F4") + " m, exceeding 2 mm.");
            Require(current.maxAnchorError < .045f, name + ": world ankle/anchor error " + current.maxAnchorError.ToString("F4") + " m exceeds 4.5 cm.");
            Require(current.maxMidStanceTilt < 10, name + ": mid-stance ankle tilts " + current.maxMidStanceTilt.ToString("F2") + " degrees from the flat surface.");
            Require(current.maxYawStep < 35, name + ": root turn snapped by " + current.maxYawStep.ToString("F2") + " degrees per frame.");
            if (name.StartsWith("stop", StringComparison.Ordinal) || name == "settle" || name == "pause") Require(current.finalPace < .10f && current.finalDistance < .045f, name + ": root failed to settle at the stationary target.");
            if (name == "walk2x" || name == "walk4x")
            {
                Require(current.travelled > 1.25f * current.rate * Dt * Lengths[stage] - .5f, name + ": actual root did not cover the accelerated path.");
                Require(current.maxPace < 1.8f, name + ": the accelerated walk was misclassified as a faster physical gait.");
                Require(current.plantedComparisons > 20, name + ": insufficient actual stance measurements at accelerated time.");
            }
            if (name == "pause")
            {
                Require(pauseBaseline != null && current.maxPausedJointDrift < .0005f && current.maxPausedJointRotation < .1f, "Paused settled joints continued moving.");
                Require(current.maxPausedClockDelta < .000001f, "The natural idle/gaze clock advanced while paused.");
            }
            stage++; if (stage < Names.Length) StartStage();
        }

        static void MeasurePause(FacilityView.NaturalMotionSample sample)
        {
            // Allow existing interpolation to settle for two seconds, then compare one full actual
            // pose to every remaining paused frame. A static counter alone cannot pass this check.
            if (frame == 60) pauseBaseline = sample;
            if (frame <= 60 || pauseBaseline == null) return;
            Require(sample.JointPositions.Length == pauseBaseline.JointPositions.Length, "Paused rig topology changed during measurement.");
            int count = Mathf.Min(sample.JointPositions.Length, pauseBaseline.JointPositions.Length);
            for (int i = 0; i < count; i++)
            {
                current.maxPausedJointDrift = Mathf.Max(current.maxPausedJointDrift, Vector3.Distance(sample.JointPositions[i], pauseBaseline.JointPositions[i]));
                current.maxPausedJointRotation = Mathf.Max(current.maxPausedJointRotation, Quaternion.Angle(sample.JointRotations[i], pauseBaseline.JointRotations[i]));
            }
            current.maxPausedClockDelta = Mathf.Max(current.maxPausedClockDelta, Mathf.Abs(sample.Clock - pauseBaseline.Clock));
        }

        static void Measure(FacilityView.NaturalMotionSample sample)
        {
            current.frames++; current.finite &= sample.Finite;
            current.maxPace = Mathf.Max(current.maxPace, sample.Pace);
            float rootSpeed = 0;
            if (previous != null)
            {
                float distance = Vector3.Distance(previous.Position, sample.Position); rootSpeed = distance / Dt;
                current.travelled += distance; if (distance > .0005f) current.movingFrames++;
                current.maxRootSpeed = Mathf.Max(current.maxRootSpeed, rootSpeed);
                current.maxRootAcceleration = Mathf.Max(current.maxRootAcceleration, Mathf.Abs(sample.Speed - previous.Speed) / Dt);
                current.maxYawStep = Mathf.Max(current.maxYawStep, Quaternion.Angle(previous.Rotation, sample.Rotation));
            }
            for (int leg = 0; leg < 2; leg++)
            {
                var foot = sample.Feet[leg];
                if (!foot.Active || !foot.Planted) continue;
                current.stanceSamples++;
                current.maxAnchorError = Mathf.Max(current.maxAnchorError, Vector3.Distance(foot.Position, foot.Anchor));
                // Contact flags delimit stance; evidence is measured world-space movement of the actual ankle.
                // Do not silently accept a moving anchor: two consecutive planted samples must remain stable.
                if (previous != null && previous.Feet[leg].Active && previous.Feet[leg].Planted)
                {
                    current.plantedComparisons++;
                    current.maxPlantedDrift = Mathf.Max(current.maxPlantedDrift, Vector3.Distance(previous.Feet[leg].Position, foot.Position));
                    current.maxAnchorJump = Mathf.Max(current.maxAnchorJump, Vector3.Distance(previous.Feet[leg].Anchor, foot.Anchor));
                }
                if (foot.Phase > .12f && foot.Phase < .30f) current.maxMidStanceTilt = Mathf.Max(current.maxMidStanceTilt, foot.Tilt);
            }
            var a = sample.Feet[0]; var b = sample.Feet[1];
            timeline.WriteLine(string.Join(",", new[] { Names[stage], frame.ToString(), F(sample.Position.x), F(sample.Position.y), F(sample.Position.z), F(sample.Pace), F(rootSpeed), F(sample.Rotation.eulerAngles.y), a.Planted.ToString(), F(a.Position.x), F(a.Position.y), F(a.Position.z), b.Planted.ToString(), F(b.Position.x), F(b.Position.y), F(b.Position.z) }));
        }

        static string F(float value) { return value.ToString("F6", CultureInfo.InvariantCulture); }
        static string Safe(string value) { return new string(value.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-').ToArray()); }
        static void Require(bool condition, string message) { if (!condition && !failures.Contains(message)) failures.Add(message); }

        static void Capture(GameController game, string name, FacilityView.NaturalMotionSample sample, float distance, float yaw, float pitch, bool face = false)
        {
            Camera camera = game.ArtCamera; Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
            bool orthographic = camera.orthographic; float aspect = camera.aspect;
            var targetTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Natural motion review" };
            RenderTexture priorActive = RenderTexture.active; Texture2D image = null;
            string file = Path.Combine(output, name + ".png");
            try
            {
                game.FrameArt(face ? sample.Head : sample.Position + Vector3.up * .95f, distance, yaw, pitch);
                camera.aspect = 1280f / 720; targetTexture.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = targetTexture });
                RenderTexture.active = targetTexture; image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply(); File.WriteAllBytes(file, image.EncodeToPNG());
                captures.Add(file);
            }
            finally
            {
                RenderTexture.active = priorActive; if (image != null) UnityEngine.Object.DestroyImmediate(image);
                targetTexture.Release(); UnityEngine.Object.DestroyImmediate(targetTexture);
                camera.transform.SetPositionAndRotation(position, rotation); camera.aspect = aspect; camera.orthographic = orthographic;
            }
        }

        static void Complete(string[] errors)
        {
            running = false; completed = true; Cleanup();
            WriteReport(failures.Count == 0 && errors.Length == 0, "completed", errors);
            Debug.Log((failures.Count == 0 && errors.Length == 0 ? "CELLBLOCK_NATURAL_MOTION_PASSED " : "CELLBLOCK_NATURAL_MOTION_FAILED ") + output);
        }
        public static void Abort(string reason = "Motion review cancelled.", string[] errors = null)
        {
            if (!running || completed) return;
            Require(false, reason); running = false; completed = true; Cleanup(); WriteReport(false, "aborted", errors ?? new string[0]);
        }
        static void Cleanup()
        {
            if (view != null) { view.ReleaseNaturalMotionProbes(); view.PreviewFixedFrameDelta = 0; }
            Time.captureDeltaTime = previousCaptureDelta;
            if (timeline != null) { timeline.Dispose(); timeline = null; }
        }
        static void WriteReport(bool passed, string status, string[] errors)
        {
            if (string.IsNullOrEmpty(output)) return;
            var report = new MotionReport { passed = passed, coreUnchanged = owner != null && coreSnapshot == JsonUtility.ToJson(owner.World.Data), status = status, editor = Application.unityVersion,
                output = output, dt = Dt, renderedFrames = totalFrames, stages = metrics.ToArray(), errors = failures.Concat(errors).Distinct().ToArray(), captures = captures.ToArray(),
                message = "Actual generated actor joints at fixed30Hz: walk/start, stationary stop,90-degree corner, reversal, run, settle,2x/4x walks/stops and stable pause. Measured world ankle positions, stance drift/anchor error, root speed/yaw, finite transforms and paused joint/idle-clock stability; actual face/full-body role gallery. Rate logic is exercised synchronously and core speed restored to zero before every return, with no clock advance. Paused disposable simulation remains unchanged. Renderer captures; no native touch, performance, balancing or motion-capture claim." };
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
        }
    }
}
