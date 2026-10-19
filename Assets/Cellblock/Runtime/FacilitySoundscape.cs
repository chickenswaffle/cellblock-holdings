using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // Facility soundscape (Claude, 2026-10-18). Real CC0 recordings (docs/asset-credits.md, Tools/Art/build_sounds.py),
    // mixed from what the simulation is actually doing:
    //   beds   — HVAC and fluorescent hum indoors; a population murmur that follows the head count, waking hours and
    //            unrest; yard voices for people outside; dawn chorus, day, dusk, night and crickets by the clock; a far
    //            road. Indoor/outdoor is the listener's real position against the built rooms, with low-pass muffling.
    //   spots  — 3D one-shots at their source: footsteps on the floor they walk on, electric strikes and steel clanks
    //            as people pass through doorways, cuffs/strikes/takedowns from the staff action log, fight scuffles,
    //            weights at exercise benches, trays at meal service, showers, construction crews, the vehicle gate,
    //            the court van, schedule chimes, lights out and the alarm bell.
    //   ui     — a soft mechanical tick on buttons, a pen signature on approvals.
    // Presentation only: it reads state and never writes it; its randomness never touches the simulation's.
    public sealed class FacilitySoundscape : MonoBehaviour
    {
        static FacilitySoundscape instance;
        // Mix trims measured with Editor/SoundCapture (output RMS around -28 dBFS at the default 65% master).
        const float BedGain = 2.2f, SpotGain = 1.5f;
        GameController game; Camera ear;

        sealed class Bed { public AudioSource Source; public AudioLowPassFilter Filter; public float Level; }
        sealed class Walker { public Point Last; public float Stride; public int NearDoor = -1; public float KeysAt; }
        readonly Dictionary<string, Bed> beds = new Dictionary<string, Bed>();
        readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        readonly Dictionary<string, float> quietUntil = new Dictionary<string, float>();
        readonly Dictionary<string, Walker> walkers = new Dictionary<string, Walker>();
        readonly List<AudioSource> voices = new List<AudioSource>();
        readonly List<Point> doors = new List<Point>();
        readonly Dictionary<int, float> doorQuiet = new Dictionary<int, float>();
        AudioSource showerLoop, gateLoop, drillLoop;
        readonly System.Random random = new System.Random(1789);

        World seen; int doorsRevision = -1, lastMinute = -1, beatsSeen; string vanPhase = "", fightPhase = "None", riotPhase = "None";
        bool alarmWas, lockdownWas, gateWas; float gateUntil, nextFight, nextBuild, nextWeights, nextTray, nextFlush;

        public static void Create(GameController game, Camera ear)
        {
            if (instance != null) return;
            instance = new GameObject("Facility soundscape").AddComponent<FacilitySoundscape>();
            instance.transform.SetParent(game.transform, false); instance.game = game; instance.ear = ear;
        }

        // ---- library ---------------------------------------------------------------------------------------------
        AudioClip[] Clips(string name)
        {
            AudioClip[] found;
            if (clips.TryGetValue(name, out found)) return found;
            var list = new List<AudioClip>();
            AudioClip single = Resources.Load<AudioClip>("Sounds/" + name);
            if (single != null) list.Add(single);
            for (int i = 1; i < 16; i++) { AudioClip c = Resources.Load<AudioClip>("Sounds/" + name + "_" + i); if (c == null) break; list.Add(c); }
            return clips[name] = list.ToArray();
        }
        AudioClip Pick(string name) { AudioClip[] set = Clips(name); return set.Length == 0 ? null : set[random.Next(set.Length)]; }
        float Range(float a, float b) { return a + (float)random.NextDouble() * (b - a); }
        bool Chance(float p) { return random.NextDouble() < p; }

        // ---- voices ----------------------------------------------------------------------------------------------
        AudioSource Voice()
        {
            foreach (AudioSource v in voices) if (!v.isPlaying) return v;
            if (voices.Count >= 24) return voices.OrderBy(v => v.time / Mathf.Max(.01f, v.clip == null ? 1 : v.clip.length)).Last();
            var go = new GameObject("Sound voice"); go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>(); s.playOnAwake = false; s.dopplerLevel = 0; s.spatialBlend = 1;
            s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = 2.5f; s.maxDistance = 90; s.spread = 30;
            voices.Add(s); return s;
        }
        // A 3D one-shot. `gap` keeps a sound from machine-gunning (per name, real seconds).
        bool PlayAt(string name, Vector3 at, float volume, float gap = 0, float pitchSpread = .05f, float minDistance = 2.5f)
        {
            float until;
            if (gap > 0 && quietUntil.TryGetValue(name, out until) && Time.unscaledTime < until) return false;
            AudioClip clip = Pick(name); if (clip == null) return false;
            AudioSource v = Voice(); v.transform.position = at; v.spatialBlend = 1; v.minDistance = minDistance;
            v.clip = clip; v.volume = Mathf.Min(1, volume * SpotGain * Range(.86f, 1f)); v.pitch = Range(1 - pitchSpread, 1 + pitchSpread); v.Play();
            if (gap > 0) quietUntil[name] = Time.unscaledTime + gap;
            return true;
        }
        bool Play2D(string name, float volume, float gap = 0)
        {
            float until;
            if (gap > 0 && quietUntil.TryGetValue(name, out until) && Time.unscaledTime < until) return false;
            AudioClip clip = Pick(name); if (clip == null) return false;
            AudioSource v = Voice(); v.spatialBlend = 0; v.clip = clip; v.volume = Mathf.Min(1, volume * SpotGain); v.pitch = Range(.98f, 1.02f); v.Play();
            if (gap > 0) quietUntil[name] = Time.unscaledTime + gap;
            return true;
        }
        // For other presentation code (e.g. the incident radio): true when a recording played.
        public static bool TryPlay2D(string name, float volume) { return instance != null && instance.Play2D(name, volume, .25f); }
        public static bool ProvidesAlarm { get { return instance != null && Resources.Load<AudioClip>("Sounds/loop_alarm_bell") != null; } }

        // ---- beds ------------------------------------------------------------------------------------------------
        Bed BedFor(string name, bool filtered)
        {
            Bed bed;
            if (beds.TryGetValue(name, out bed)) return bed;
            AudioClip clip = Resources.Load<AudioClip>("Sounds/" + name);
            bed = new Bed(); beds[name] = bed;
            if (clip == null) return bed;
            var go = new GameObject("Bed " + name); go.transform.SetParent(transform, false);
            bed.Source = go.AddComponent<AudioSource>(); bed.Source.clip = clip; bed.Source.loop = true; bed.Source.spatialBlend = 0;
            bed.Source.playOnAwake = false; bed.Source.volume = 0; bed.Source.priority = 32;
            if (filtered) { bed.Filter = go.AddComponent<AudioLowPassFilter>(); bed.Filter.cutoffFrequency = 22000; }
            return bed;
        }
        void Mix(string name, float level, float cutoff, float dt, bool filtered = false)
        {
            Bed bed = BedFor(name, filtered); if (bed.Source == null) return;
            bed.Level = Mathf.MoveTowards(bed.Level, Mathf.Min(1, level * BedGain), dt * .9f);
            bed.Source.volume = bed.Level;
            if (bed.Filter != null) bed.Filter.cutoffFrequency = Mathf.Lerp(bed.Filter.cutoffFrequency, cutoff, Mathf.Clamp01(dt * 3));
            if (bed.Level > .0005f && !bed.Source.isPlaying) { bed.Source.time = Range(0, bed.Source.clip.length * .9f); bed.Source.Play(); }
            else if (bed.Level <= .0005f && bed.Source.isPlaying) bed.Source.Stop();
        }

        static float Bump(float hour, float start, float peak, float end)
        {
            if (hour <= start || hour >= end) return 0;
            return hour < peak ? Mathf.SmoothStep(0, 1, (hour - start) / (peak - start)) : Mathf.SmoothStep(1, 0, (hour - peak) / (end - peak));
        }
        bool Inside(World w, Point p) { foreach (List<Point> room in w.Rooms) if (Geometry.Contains(room, p)) return true; return false; }

        void Update()
        {
            if (game == null || ear == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            World w = game.World;
            Vector3 earAt = ear.transform.position; var ground = new Point(earAt.x, earAt.z);
            bool home = game.HomeShown || w == null;
            if (w != seen) { seen = w; ResetWorld(); }

            // ---- where the listener is ----
            bool character = !home && game.CharacterViewActive;
            float near = Mathf.Clamp01((48 - earAt.y) / 36);
            bool inside = !home && Inside(w, ground);
            float indoor = home ? 0 : character ? (inside ? 1 : 0) : (inside ? .55f : 0) * near;
            float hour = w == null ? 21.5f : w.Data.Minute % 1440 / 60f;
            int present = w == null ? 0 : w.Data.Residents.Count(r => w.IsPresent(r));
            float awake = hour >= 22 || hour < 6 ? .07f : hour < 6.5f ? .4f : 1;
            bool alarm = w != null && w.Data.Operations.Alarm, lockdown = w != null && w.Data.Operations.Lockdown;
            bool riot = w != null && w.Population.Riot.Phase != "None";
            float population = Mathf.Sqrt(Mathf.Min(present, 80) / 80f);
            float unrest = riot ? 1.6f : alarm ? 1.25f : lockdown ? .55f : 1;
            int outside = 0;
            if (w != null) foreach (Resident r in w.Data.Residents) if (w.IsPresent(r) && Geometry.Distance(r.Position, ground) < 30 && !Inside(w, r.Position)) outside++;

            // ---- beds ----
            float outdoor = home ? .55f : 1 - indoor * .75f, muffle = Mathf.Lerp(22000, 1400, indoor);
            float day = Bump(hour, 6.5f, 12, 19.5f), dawn = Bump(hour, 4.8f, 6.3f, 8.5f), dusk = Bump(hour, 17.8f, 19.4f, 21.2f);
            float night = Mathf.Clamp01(1 - day - dawn * .5f - dusk * .5f) * (hour >= 20 || hour < 6 ? 1 : .2f);
            Mix("amb_day", .30f * day * outdoor, muffle, dt, true);
            Mix("amb_dawn", .20f * dawn * outdoor, muffle, dt, true);
            Mix("amb_dusk", .18f * dusk * outdoor, muffle, dt, true);
            Mix("amb_night", .30f * night * outdoor, muffle, dt, true);
            Mix("amb_crickets", .13f * night * outdoor * (hour >= 21 || hour < 4 ? 1 : .3f), muffle, dt, true);
            Mix("amb_road", .10f * outdoor * (night > .5f ? .55f : 1), muffle, dt, true);
            Mix("amb_hvac", home ? 0 : .20f * Mathf.Max(indoor, .15f * near), Mathf.Lerp(900, 22000, indoor), dt, true);
            Mix("amb_hum", character && inside && w.Data.Operations.Lights ? .05f : 0, 22000, dt);
            float crowd = home ? 0 : .30f * population * awake * unrest * Mathf.Lerp(.35f, 1, indoor);
            Mix("amb_crowd", crowd, Mathf.Lerp(1100, 22000, indoor), dt, true);
            Mix("amb_yard", home ? 0 : .26f * Mathf.Sqrt(Mathf.Min(outside, 30) / 30f) * awake * (1 - indoor * .8f), Mathf.Lerp(22000, 1500, indoor), dt, true);
            Mix("loop_alarm_bell", alarm && !home ? .42f : 0, Mathf.Lerp(5000, 22000, indoor), dt * 4, true);

            if (home || w == null) { StopLoops(); return; }
            bool running = w.Data.Speed > 0 && w.Data.Speed <= 4;
            bool close = earAt.y < 16;
            Events(w, earAt, ground, hour, close, running);
            if (running && close) People(w, ground, earAt.y, dt);
            else walkers.Clear();
            Loops(w, ground, close && running, dt);
        }

        void ResetWorld()
        {
            walkers.Clear(); doorsRevision = -1; lastMinute = -1; beatsSeen = int.MaxValue; vanPhase = ""; fightPhase = "None"; riotPhase = "None";
            if (seen != null) { alarmWas = seen.Data.Operations.Alarm; lockdownWas = seen.Data.Operations.Lockdown; gateWas = seen.Data.Operations.GateOpen; }
            StopLoops();
        }
        void StopLoops() { foreach (AudioSource s in new[] { showerLoop, gateLoop, drillLoop }) if (s != null && s.isPlaying) s.Stop(); }

        static Vector3 At(Point p, float y = 1.2f) { return new Vector3(p.X, y, p.Y); }

        // ---- facility events --------------------------------------------------------------------------------------
        void Events(World w, Vector3 earAt, Point ground, float hour, bool close, bool running)
        {
            OperationsState ops = w.Data.Operations;
            if (ops.Lockdown && !lockdownWas) { Play2D("switch_big", .45f); Cascade(w, ground, 6); }
            if (ops.GateOpen != gateWas) { gateUntil = Time.unscaledTime + 6.5f; PlayAt("gate_clank", At(JusticeRules.GateInside), .8f, 1, .03f, 6); }
            alarmWas = ops.Alarm; lockdownWas = ops.Lockdown; gateWas = ops.GateOpen;

            // Schedule: count/wake, meals and lights out. Only for minutes the clock really crossed (not a load).
            int minute = w.Data.Minute;
            if (lastMinute >= 0 && minute > lastMinute && minute - lastMinute < 90)
                for (int m = lastMinute + 1; m <= minute; m++)
                {
                    int t = m % 1440;
                    if (t == 6 * 60 || t == 7 * 60 || t == 12 * 60 || t == 18 * 60) Play2D("chime", .32f, 2);
                    if (t == 22 * 60) { Play2D("chime", .28f, 2); Play2D("switch_big", .5f, 2); Cascade(w, ground, 8); }
                }
            lastMinute = minute;

            // The court van coming or going.
            string phase = w.CourtVan.Phase;
            if (phase != vanPhase && vanPhase != "" && (phase == "Inbound" || phase == "Departing" || phase == "Outbound")) PlayAt("car_pass", At(JusticeRules.GateInside + new Point(0, -8), .8f), .7f, 4, .04f, 8);
            vanPhase = phase;

            // Staff actions (the Hard Time pad): every new beat gets its sound at the actor.
            IReadOnlyList<ActionBeat> beats = w.ActionBeats;
            if (beatsSeen > beats.Count) beatsSeen = beats.Count;
            for (int i = beatsSeen; i < beats.Count; i++) Beat(w, beats[i]);
            beatsSeen = beats.Count;

            // Fights and riots: scuffle sounds while the scene is physical.
            FightScene fight = w.Fight;
            if (fight.Phase == "Scuffle" && running && Time.unscaledTime >= nextFight)
            {
                nextFight = Time.unscaledTime + Range(.35f, 1.1f) / Mathf.Max(1, w.Data.Speed * .5f);
                PlayAt(Chance(.15f) ? "bodyfall" : "punch", At(fight.Center, 1.1f), .85f, 0, .08f, 3);
            }
            if (fight.Phase == "Scuffle" && fightPhase != "Scuffle") Play2D("radio", .35f, 1);
            fightPhase = fight.Phase;
            string riotNow = w.Population.Riot.Phase;
            if (riotNow != "None" && riotPhase == "None") { Play2D("radio", .4f, 1); Play2D("switch_big", .4f, 1); }
            riotPhase = riotNow;

            if (!running) return;
            // Crews at work: hammering, the odd saw or drill run.
            if (Time.unscaledTime >= nextBuild)
            {
                nextBuild = Time.unscaledTime + Range(.45f, 1.3f);
                Crew crew = w.Data.Crews.Where(c => c.Arrived && c.Working && Geometry.Distance(c.Position, ground) < 45).OrderBy(c => random.Next()).FirstOrDefault();
                if (crew != null)
                {
                    if (Chance(.06f)) PlayAt("saw", At(crew.Position, 1), .45f, 6, .04f, 4);
                    else PlayAt("hammer", At(crew.Position, 1), .7f, 0, .07f, 3);
                    if (Chance(.03f)) { DrillAt(At(crew.Position, 1)); }
                }
            }
            if (!close) return;
            // Exercise benches: plates set down. Meal service: trays.
            if (Time.unscaledTime >= nextWeights)
            {
                nextWeights = Time.unscaledTime + Range(2.5f, 7);
                Fixture bench = w.Data.Fixtures.Where(f => f.Kind == "ExerciseBench" && Geometry.Distance(f.Position, ground) < 20 && w.Data.Residents.Any(r => Geometry.Distance(r.Position, f.Position) < 1.4f)).FirstOrDefault();
                if (bench != null) PlayAt("weights", At(bench.Position, .5f), .45f, 0, .06f);
            }
            bool meal = (hour >= 7 && hour < 9) || (hour >= 12 && hour < 14) || (hour >= 18 && hour < 20);
            if (meal && Time.unscaledTime >= nextTray)
            {
                nextTray = Time.unscaledTime + Range(1.2f, 3.5f);
                Fixture counter = w.Data.Fixtures.Where(f => (f.Kind == "ServingCounter" || f.Kind == "Table") && Geometry.Distance(f.Position, ground) < 18 && w.Data.Residents.Any(r => Geometry.Distance(r.Position, f.Position) < 1.6f)).OrderBy(f => random.Next()).FirstOrDefault();
                if (counter != null) PlayAt("tray", At(counter.Position, .9f), .4f, 0, .07f);
            }
            if (Time.unscaledTime >= nextFlush)
            {
                nextFlush = Time.unscaledTime + Range(25, 70);
                Fixture toilet = w.Data.Fixtures.Where(f => f.Kind == "Toilet" && Geometry.Distance(f.Position, ground) < 14).OrderBy(f => random.Next()).FirstOrDefault();
                if (toilet != null && Chance(.5f)) PlayAt("flush", At(toilet.Position, .6f), .3f);
            }
        }
        void Beat(World w, ActionBeat beat)
        {
            Point at;
            if (!PositionOf(w, beat.ActorId, out at)) return;
            Vector3 p = At(at, 1.1f);
            switch (beat.Kind)
            {
                case "Strike": case "Counter": if (beat.Landed) PlayAt("punch", p, beat.Power ? 1 : .8f, 0, .07f, 3); if (beat.Down) PlayAt("bodyfall", p, .8f); break;
                case "Takedown": PlayAt("bodyfall", p, .9f); break;
                case "Shove": case "Grab": PlayAt("punch", p, .4f, 0, .12f); break;
                case "Cuff": PlayAt("cuffs", p, .75f, 0, .04f); break;
                case "Lock": PlayAt("door_strike", p, .6f); PlayAt("gate_clank", p, .55f, 0, .05f); break;
                case "Search": PlayAt(Chance(.5f) ? "keys" : "drawer", p, .5f); break;
                case "PickUp": PlayAt("keys", p, .35f); break;
            }
        }
        bool PositionOf(World w, string id, out Point at)
        {
            Resident r = w.Data.Residents.FirstOrDefault(x => x.Id == id); if (r != null) { at = r.Position; return true; }
            StaffMember s = w.Data.Staff.FirstOrDefault(x => x.Id == id); if (s != null) { at = s.Position; return true; }
            at = default(Point); return false;
        }
        // Cell doors racked shut along the housing nearest the listener, a few metres apart in time and space.
        void Cascade(World w, Point ground, int count)
        {
            var homes = w.Data.Residents.Where(r => w.IsPresent(r)).Select(r => r.Home).OrderBy(h => Geometry.Distance(h, ground)).Take(count).ToList();
            for (int i = 0; i < homes.Count; i++)
            {
                Vector3 at = At(homes[i], 1.2f); float delay = .35f + i * Range(.28f, .55f);
                StartCoroutine(Later(delay, () => { PlayAt("door_strike", at, .45f); PlayAt("gate_clank", at, .7f, 0, .05f, 3); }));
            }
        }
        System.Collections.IEnumerator Later(float seconds, System.Action action) { yield return new WaitForSecondsRealtime(seconds); action(); }

        // ---- people: footsteps, doorways, keys --------------------------------------------------------------------
        void People(World w, Point ground, float earHeight, float dt)
        {
            if (doorsRevision != w.GeometryRevision)
            {
                doorsRevision = w.GeometryRevision; doors.Clear();
                foreach (Wall wall in w.Data.Walls) if (wall.Door >= 0) doors.Add(Geometry.OnLength(wall, wall.Door));
            }
            string controlled = w.CharacterId;
            var live = new HashSet<string>();
            int steps = 0;
            foreach (Resident r in w.Data.Residents) if (w.IsPresent(r)) Walk(w, r.Id, r.Position, false, ground, controlled, live, dt, ref steps);
            foreach (StaffMember s in w.Data.Staff) if (s.Arrived) Walk(w, s.Id, s.Position, true, ground, controlled, live, dt, ref steps);
            if (walkers.Count > live.Count) foreach (string gone in walkers.Keys.Where(k => !live.Contains(k)).ToList()) walkers.Remove(gone);
        }
        void Walk(World w, string id, Point at, bool staff, Point ground, string controlled, HashSet<string> live, float dt, ref int steps)
        {
            float far = Geometry.Distance(at, ground);
            if (far > 14) return;
            live.Add(id); Walker walker;
            if (!walkers.TryGetValue(id, out walker)) { walkers[id] = new Walker { Last = at, KeysAt = Time.unscaledTime + Range(2, 9) }; return; }
            float moved = Geometry.Distance(at, walker.Last); walker.Last = at;
            if (moved > 2.5f) { walker.Stride = 0; return; } // a teleport or a reload, not a walk
            // Real-time speed normalised by game speed: 1.6 m/s is an ordinary walk at 1x.
            float speed = moved / Mathf.Max(dt, .001f) / Mathf.Max(1, w.Data.Speed);
            bool runningFeet = speed > 2.3f;
            walker.Stride += moved;
            float stride = runningFeet ? 1.05f : .74f;
            bool me = id == controlled;
            Vector3 foot = At(at, .05f);
            if (walker.Stride >= stride)
            {
                walker.Stride -= stride;
                if (walker.Stride > stride) walker.Stride = 0;
                if (steps < 5 || me)
                {
                    steps++;
                    bool indoors = Inside(w, at);
                    string surface = runningFeet ? (indoors ? "step_run" : "step_gravel") : indoors ? (Chance(.12f) ? "step_squeak" : "step_concrete") : "step_gravel";
                    PlayAt(surface, foot, (me ? .55f : staff ? .38f : .3f) * (runningFeet ? 1.2f : 1), 0, .07f, me ? 1.5f : 2);
                }
            }
            // Officers carry keys: an occasional jingle while they walk.
            if (staff && moved > .002f && Time.unscaledTime >= walker.KeysAt)
            {
                walker.KeysAt = Time.unscaledTime + Range(5, 14);
                if (Chance(.45f)) PlayAt("keys", At(at, .95f), .22f, 0, .05f);
            }
            // Doorways: the electric strike and the steel leaf as someone passes through.
            int nearest = -1; float best = .7f;
            for (int i = 0; i < doors.Count; i++) { float d = Geometry.Distance(doors[i], at); if (d < best) { best = d; nearest = i; } }
            if (nearest >= 0 && walker.NearDoor != nearest)
            {
                float until;
                if (!doorQuiet.TryGetValue(nearest, out until) || Time.unscaledTime >= until)
                {
                    doorQuiet[nearest] = Time.unscaledTime + 7;
                    Vector3 door = At(doors[nearest], 1.4f);
                    PlayAt("door_strike", door, .32f, 0, .03f);
                    if (Chance(.55f)) { Vector3 later = door; StartCoroutine(Later(Range(.9f, 1.6f), () => PlayAt("gate_clank", later, .38f, 0, .06f, 2))); }
                }
            }
            walker.NearDoor = nearest;
        }

        // ---- positional loops: showers, the gate motor, a drill run -----------------------------------------------
        void Loops(World w, Point ground, bool active, float dt)
        {
            Fixture shower = active ? w.Data.Fixtures.Where(f => f.Kind == "Shower" && Geometry.Distance(f.Position, ground) < 18 && w.Data.Residents.Any(r => w.IsPresent(r) && Geometry.Distance(r.Position, f.Position) < 1.1f)).OrderBy(f => Geometry.Distance(f.Position, ground)).FirstOrDefault() : null;
            showerLoop = Hold(showerLoop, "loop_shower", shower != null, shower == null ? Vector3.zero : At(shower.Position, 1.8f), .42f, dt);
            gateLoop = Hold(gateLoop, "loop_gate", Time.unscaledTime < gateUntil && w.Data.Speed > 0, At(JusticeRules.GateInside, 1), .55f, dt, 7);
            if (drillLoop != null && drillLoop.isPlaying && Time.unscaledTime > drillUntil) drillLoop.Stop();
        }
        float drillUntil;
        void DrillAt(Vector3 at)
        {
            drillLoop = Hold(drillLoop, "loop_drill", true, at, .35f, 1, 4);
            drillUntil = Time.unscaledTime + Range(1.2f, 3);
        }
        AudioSource Hold(AudioSource source, string name, bool on, Vector3 at, float volume, float dt, float minDistance = 3)
        {
            if (source == null)
            {
                if (!on) return null;
                AudioClip clip = Resources.Load<AudioClip>("Sounds/" + name); if (clip == null) return null;
                var go = new GameObject("Loop " + name); go.transform.SetParent(transform, false);
                source = go.AddComponent<AudioSource>(); source.clip = clip; source.loop = true; source.spatialBlend = 1; source.dopplerLevel = 0;
                source.rolloffMode = AudioRolloffMode.Logarithmic; source.minDistance = minDistance; source.maxDistance = 80; source.volume = 0;
            }
            if (on) { source.transform.position = at; if (!source.isPlaying) { source.volume = 0; source.Play(); } }
            source.volume = Mathf.MoveTowards(source.volume, on ? Mathf.Min(1, volume * SpotGain) : 0, dt * 1.5f);
            if (!on && source.volume <= 0 && source.isPlaying) source.Stop();
            return source;
        }

        // ---- interface ----------------------------------------------------------------------------------------------
        // A soft mechanical tick for every button and toggle; approvals and signatures get a pen on paper.
        public static void BindInterface(VisualElement root)
        {
            root.RegisterCallback<ClickEvent>(e =>
            {
                if (instance == null) return;
                for (var el = e.target as VisualElement; el != null && el != root; el = el.parent)
                {
                    var button = el as Button;
                    if (button != null)
                    {
                        string text = button.text ?? "";
                        if (text.StartsWith("Approve") || text.StartsWith("Sign") || text.Contains("Accept")) instance.Play2D("sign", .32f, .3f);
                        else if (text.StartsWith("Hire")) instance.Play2D("stapler", .3f, .2f);
                        else instance.Play2D("ui_tick", .22f, .04f);
                        return;
                    }
                    if (el is Toggle || el is Slider) { instance.Play2D("ui_click", .2f, .04f); return; }
                }
            }, TrickleDown.TrickleDown);
        }

#if UNITY_EDITOR
        // Review only: the live mix (bed levels) and the one-shots sounding right now.
        public static string PreviewMix
        {
            get
            {
                if (instance == null) return "none";
                var parts = instance.beds.Where(b => b.Value.Source != null && b.Value.Level > .001f).Select(b => b.Key + "=" + b.Value.Level.ToString("0.00"));
                var playing = instance.voices.Where(v => v.isPlaying && v.clip != null).Select(v => v.clip.name);
                return string.Join(" ", parts) + " | " + string.Join(",", playing);
            }
        }
#endif
        void OnDestroy() { if (instance == this) instance = null; }
    }
}
