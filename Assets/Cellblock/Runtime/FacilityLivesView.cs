using System.Collections.Generic;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    // Resident appearance and everyday poses, through the two resident hooks Codex added to FacilityView.
    // Appearance is built once per actor (faces, hair styles, facial hair, glasses, tags, tattoos) and merged by
    // material. Jumpsuit colour follows intake and classification. Poses own the upper body only while active
    // (PoseArms) and every local offset is eased back to neutral when the pose ends. No per-frame allocations.
    public sealed partial class FacilityView
    {
        enum LifePose { None, Talk, Eat, Sleep, SleepFloor, Cuffed, Riot, Sway }
        sealed class Look
        {
            public Renderer[] Shirt, Trousers;
            public Material[] ShirtOriginal, TrousersOriginal;
            public string Palette = "";
            public LifePose Pose;
            public float Weight, NextScan, Phase;
            public Vector3 SeatOffset; public float SeatYaw; public bool HasSeat;
        }
        readonly Dictionary<Actor, Look> looks = new Dictionary<Actor, Look>();
        readonly List<Actor> lostLooks = new List<Actor>();

        partial void PrepareResidentPose(Actor actor, Resident resident, World world)
        {
            Look look;
            if (!looks.TryGetValue(actor, out look)) { look = Dress(actor, resident); looks.Add(actor, look); PruneLooks(); }
            string palette = Palette(world, resident);
            if (palette != look.Palette) Recolor(look, palette);
            if (Time.unscaledTime >= look.NextScan) { look.NextScan = Time.unscaledTime + 0.4f + (actor.Gait % 0.2f); ChoosePose(actor, look, resident, world); }
            actor.PoseArms = look.Pose == LifePose.Talk || look.Pose == LifePose.Eat || look.Pose == LifePose.Cuffed || look.Pose == LifePose.Riot ||
                look.Pose == LifePose.Sleep || look.Pose == LifePose.SleepFloor;
        }

        partial void AnimateResidentLife(Actor actor, Resident resident, World world)
        {
            Look look; if (!looks.TryGetValue(actor, out look)) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if(world.Data.Speed>0)actor.PoseClock+=dt*Mathf.Sqrt(world.Data.Speed);
            float t=actor.PoseClock+actor.Gait*.37f;
            look.Weight = Mathf.MoveTowards(look.Weight, look.Pose == LifePose.None ? 0 : 1, dt * 2.5f);
            // MoveActor has already posed every joint for walking this frame; blend from it by the pose weight.
            float w = look.Weight, ease = dt * 10;
            Vector3 modelPosition = Vector3.zero; Quaternion modelRotation = Quaternion.identity;
            switch (look.Pose)
            {
                case LifePose.Talk:
                {
                    float wave = Mathf.Sin(t * 2.4f + look.Phase);
                    actor.Limbs[0].localRotation = Quaternion.Slerp(actor.Limbs[0].localRotation, Quaternion.Euler(-32 - wave * 14, 0, 6), w);
                    actor.Forearms[0].localRotation = Quaternion.Slerp(actor.Forearms[0].localRotation, Quaternion.Euler(-48 + wave * 10, 0, 0), w);
                    actor.Limbs[1].localRotation = Quaternion.Slerp(actor.Limbs[1].localRotation, Quaternion.Euler(-6, 0, -4), w);
                    actor.Forearms[1].localRotation = Quaternion.Slerp(actor.Forearms[1].localRotation, Quaternion.Euler(-14, 0, 0), w);
                    actor.Body.localRotation = Quaternion.Slerp(actor.Body.localRotation, Quaternion.Euler(2, Mathf.Sin(t * 0.7f) * 6, 0), w);
                    break;
                }
                case LifePose.Eat:
                {
                    float bite = Mathf.Max(0, Mathf.Sin(t * 1.3f + look.Phase));
                    if (look.HasSeat) { modelPosition = look.SeatOffset * w + Vector3.down * 0.42f * w; actor.Root.rotation = Quaternion.Slerp(actor.Root.rotation, Quaternion.Euler(0, look.SeatYaw, 0), ease * 0.5f); }
                    for (int leg = 0; leg < 2; leg++)
                    {
                        actor.Limbs[2 + leg].localRotation = Quaternion.Slerp(actor.Limbs[2 + leg].localRotation, Quaternion.Euler(-86 * w, 0, 0), w);
                        actor.Knees[leg].localRotation = Quaternion.Slerp(actor.Knees[leg].localRotation, Quaternion.Euler(86 * w, 0, 0), w);
                    }
                    actor.Limbs[0].localRotation = Quaternion.Slerp(actor.Limbs[0].localRotation, Quaternion.Euler(-38 - bite * 22, 0, 4), w);
                    actor.Forearms[0].localRotation = Quaternion.Slerp(actor.Forearms[0].localRotation, Quaternion.Euler(-70 - bite * 35, 0, 0), w);
                    actor.Limbs[1].localRotation = Quaternion.Slerp(actor.Limbs[1].localRotation, Quaternion.Euler(-40, 0, -4), w);
                    actor.Forearms[1].localRotation = Quaternion.Slerp(actor.Forearms[1].localRotation, Quaternion.Euler(-55, 0, 0), w);
                    actor.Body.localRotation = Quaternion.Slerp(actor.Body.localRotation, Quaternion.Euler(10, 0, 0), w);
                    break;
                }
                case LifePose.Sleep:
                case LifePose.SleepFloor:
                {
                    // Lie along the bed (or the mattress beside a floor spot), head toward the pillow end.
                    if (look.HasSeat) actor.Root.rotation = Quaternion.Slerp(actor.Root.rotation, Quaternion.Euler(0, look.SeatYaw, 0), ease * 0.5f);
                    // Back on the mattress (its top plus half the torso depth); feet at the foot, head toward the pillow (-z).
                    float breathe = Mathf.Sin(t * 0.9f) * 0.006f;
                    modelPosition = (look.HasSeat ? look.SeatOffset : Vector3.zero) * w + Vector3.up * ((look.Pose == LifePose.Sleep ? 0.70f : 0.25f) + breathe) * w;
                    modelRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(-90, 0, 0), w);
                    for (int arm = 0; arm < 2; arm++)
                    {
                        actor.Limbs[arm].localRotation = Quaternion.Slerp(actor.Limbs[arm].localRotation, Quaternion.Euler(-8, 0, arm == 0 ? 10 : -10), w);
                        actor.Forearms[arm].localRotation = Quaternion.Slerp(actor.Forearms[arm].localRotation, Quaternion.Euler(-30, 0, 0), w);
                    }
                    for (int leg = 0; leg < 2; leg++) actor.Knees[leg].localRotation = Quaternion.Slerp(actor.Knees[leg].localRotation, Quaternion.Euler(leg == 0 ? 14 : 4, 0, 0), w);
                    break;
                }
                case LifePose.Cuffed:
                    // Hands held behind the back while walking through intake.
                    for (int arm = 0; arm < 2; arm++)
                    {
                        actor.Limbs[arm].localRotation = Quaternion.Slerp(actor.Limbs[arm].localRotation, Quaternion.Euler(24, 0, arm == 0 ? -8 : 8), w);
                        actor.Forearms[arm].localRotation = Quaternion.Slerp(actor.Forearms[arm].localRotation, Quaternion.Euler(-62, arm == 0 ? 18 : -18, 0), w);
                    }
                    actor.Body.localRotation = Quaternion.Slerp(actor.Body.localRotation, Quaternion.Euler(6, 0, 0), w);
                    break;
                case LifePose.Riot:
                {
                    float shake = Mathf.Sin(t * 7 + look.Phase);
                    for (int arm = 0; arm < 2; arm++)
                    {
                        actor.Limbs[arm].localRotation = Quaternion.Slerp(actor.Limbs[arm].localRotation, Quaternion.Euler(-150 + shake * (arm == 0 ? 18 : -18), 0, arm == 0 ? 12 : -12), w);
                        actor.Forearms[arm].localRotation = Quaternion.Slerp(actor.Forearms[arm].localRotation, Quaternion.Euler(-20, 0, 0), w);
                    }
                    modelPosition = Vector3.up * Mathf.Abs(Mathf.Sin(t * 5 + look.Phase)) * 0.07f * w;
                    break;
                }
                case LifePose.Sway:
                    actor.Body.localRotation = Quaternion.Slerp(actor.Body.localRotation, Quaternion.Euler(4, 0, Mathf.Sin(t * 1.1f) * 7), w);
                    break;
                default:
                {
                    // Idle: a slow weight shift so people standing still are not statues.
                    if (actor.Speed < 0.05f) actor.Body.localRotation = Quaternion.Slerp(actor.Body.localRotation, Quaternion.Euler(Mathf.Sin(t * 0.8f) * 1.2f, 0, Mathf.Sin(t * 0.43f) * 1.6f), 0.3f);
                    break;
                }
            }
            actor.Model.localPosition = Vector3.Lerp(actor.Model.localPosition, modelPosition, ease);
            actor.Model.localRotation = Quaternion.Slerp(actor.Model.localRotation, modelRotation, ease);
        }

        void ChoosePose(Actor actor, Look look, Resident resident, World world)
        {
            LifePose pose = LifePose.None;
            bool still = actor.Speed < 0.06f;
            Disturbance riot = world.Population.Riot;
            int minute = world.Data.Minute % 1440;
            if (riot.Phase != "None" && riot.Rioters.Contains(resident.Id)) pose = LifePose.Riot;
            else if (world.InIntake(resident)) pose = LifePose.Cuffed;
            else if (still && (minute >= 22 * 60 || minute < 6 * 60) && Geometry.Distance(resident.Position, resident.Home) < 0.3f) pose = resident.FloorBed ? LifePose.SleepFloor : LifePose.Sleep;
            else if (still && (resident.HighUntil > world.Data.Minute || resident.DrunkUntil > world.Data.Minute)) pose = LifePose.Sway;
            else if (still)
            {
                int index = world.Data.Residents.IndexOf(resident);
                if (index >= 0 && world.MealDue(index, resident) && Geometry.Distance(resident.Position, world.ServiceDestination(index, true)) < 0.5f) pose = LifePose.Eat;
                else if (minute >= 7 * 60 && minute < 21 * 60)
                    for (int i = 0; i < world.Data.Residents.Count; i++)
                    {
                        Resident other = world.Data.Residents[i];
                        if (other != resident && Geometry.Distance(other.Position, resident.Position) < 1.5f && world.IsPresent(other)) { pose = LifePose.Talk; break; }
                    }
            }
            if (pose != look.Pose) { look.Phase = (actor.Gait * 1.7f) % 6.28f; look.HasSeat = false; Seat(look, pose, resident, world); }
            look.Pose = pose;
        }
        // Where the body goes for eating and sleeping, relative to the person's standing point.
        void Seat(Look look, LifePose pose, Resident resident, World world)
        {
            if (pose == LifePose.Eat)
            {
                Fixture table = null; float best = 3;
                foreach (Fixture f in world.Data.Fixtures)
                    if (f.Kind == "Table") { float d = Geometry.Distance(f.Position, resident.Position); if (d < best) { best = d; table = f; } }
                if (table == null) return;
                Quaternion tableRotation=Quaternion.Euler(0,table.Rotation,0);
                Vector3 local=Quaternion.Inverse(tableRotation)*(V(resident.Position)-V(table.Position));
                // Only the six physical bench places support sitting. Align to the bench,
                // rather than diagonally toward the table's center and into a neighbor.
                if(Mathf.Abs(Mathf.Abs(local.z)-1.45f)>.25f||Mathf.Abs(local.x)>.9f)return;
                look.SeatYaw=table.Rotation+(local.z>0?180:0);look.SeatOffset=Vector3.forward*.55f;look.HasSeat=true;
            }
            else if (pose == LifePose.Sleep)
            {
                foreach (Fixture f in world.Data.Fixtures)
                    if (f.Kind == "Bed" && Geometry.Distance(Geometry.AccessPoint(f), resident.Home) < 0.1f)
                    {
                        // The access point is 0.8 m to the bed's side; lie on the bed itself, feet at its foot.
                        look.SeatYaw = f.Rotation; look.SeatOffset = new Vector3(-0.8f, 0, 0.88f); look.HasSeat = true; return;
                    }
            }
            else if (pose == LifePose.SleepFloor) { look.SeatYaw = 0; look.SeatOffset = new Vector3(0.45f, 0, 0.88f); look.HasSeat = true; }
        }

#if UNITY_EDITOR
        public bool PreviewLookBindingsAlive()
        {
            foreach(var pair in looks)
            {
                if(pair.Key.Root==null)continue;
                foreach(var renderer in pair.Value.Shirt)if(renderer==null)return false;
                foreach(var renderer in pair.Value.Trousers)if(renderer==null)return false;
            }
            return true;
        }
        public int PreviewLifePoseCount(string pose)
        {
            int count=0;foreach(var pair in looks)if(pair.Key.Root!=null&&pair.Key.Root.gameObject.activeInHierarchy&&pair.Value.Pose.ToString()==pose&&pair.Value.Weight>.95f&&pair.Key.Speed<.06f&&pair.Value.HasSeat)count++;return count;
        }
#endif
        static string Palette(World world, Resident resident)
        {
            if (world.InIntake(resident)) return "intake";
            string level = resident.Intake == null ? "" : resident.Intake.SecurityLevel;
            return level == "Maximum" ? "maximum" : level == "Minimum" ? "minimum" : level == "Medium" ? "medium" : "default";
        }
        void Recolor(Look look, string palette)
        {
            look.Palette = palette;
            Color shirt, trousers;
            switch (palette)
            {
                case "intake": shirt = trousers = new Color(0.86f, 0.45f, 0.16f); break;
                case "maximum": shirt = trousers = new Color(0.60f, 0.20f, 0.16f); break;
                case "minimum": shirt = new Color(0.36f, 0.50f, 0.38f); trousers = new Color(0.30f, 0.42f, 0.32f); break;
                case "medium": shirt = new Color(0.64f, 0.56f, 0.39f); trousers = new Color(0.52f, 0.46f, 0.32f); break;
                default:
                    for (int i = 0; i < look.Shirt.Length; i++) if(look.Shirt[i]!=null)look.Shirt[i].sharedMaterial = look.ShirtOriginal[i];
                    for (int i = 0; i < look.Trousers.Length; i++) if(look.Trousers[i]!=null)look.Trousers[i].sharedMaterial = look.TrousersOriginal[i];
                    return;
            }
            Material top = Material("uniform-jumpsuit-" + palette, shirt), bottom = Material("uniform-jumpsuit-" + palette + "-legs", trousers);
            foreach (Renderer r in look.Shirt) if(r!=null)r.sharedMaterial = top;
            foreach (Renderer r in look.Trousers) if(r!=null)r.sharedMaterial = bottom;
        }
        void PruneLooks()
        {
            if (looks.Count <= actors.Count + 8) return;
            lostLooks.Clear();
            foreach (KeyValuePair<Actor, Look> pair in looks) if (pair.Key.Root == null) lostLooks.Add(pair.Key);
            foreach (Actor gone in lostLooks) looks.Remove(gone);
        }

        // One-time appearance for a resident actor, seeded by their id so a person always looks the same.
        Look Dress(Actor actor, Resident resident)
        {
            int seed = IdentitySeed(resident.Id);
            var look = new Look();
            var shirts = new List<Renderer>(); var legs = new List<Renderer>();
            foreach (Renderer r in actor.Model.GetComponentsInChildren<Renderer>(true))
            {
                // Combine deactivates source objects and destroys them at frame end.
                // Cache only the live combined renderers, never those retired sources.
                if(!r.gameObject.activeInHierarchy)continue;
                string name = r.sharedMaterial == null ? "" : r.sharedMaterial.name;
                if (name.StartsWith("uniform-")) shirts.Add(r); else if (name == "trousers") legs.Add(r);
            }
            look.Shirt = shirts.ToArray(); look.Trousers = legs.ToArray();
            look.ShirtOriginal = new Material[look.Shirt.Length]; look.TrousersOriginal = new Material[look.Trousers.Length];
            for (int i = 0; i < look.Shirt.Length; i++) look.ShirtOriginal[i] = look.Shirt[i].sharedMaterial;
            for (int i = 0; i < look.Trousers.Length; i++) look.TrousersOriginal[i] = look.Trousers[i].sharedMaterial;

            // The shared Person factory already owns the detailed head, hair, glasses and ID.
            // Add resident-specific markings without stacking a second face over that head.
            if (resident.Gang != "Unaffiliated")
            {
                var ink = new GameObject("Tattoo").transform; ink.SetParent(actor.Forearms[seed % 2], false);
                bool harbor = resident.Gang == "Harbor Union";
                Box(ink, "Ink", new Vector3(0, -0.1f, 0.05f), new Vector3(0.052f, 0.1f, 0.01f), harbor ? "ink-harbor" : "ink-cedar", harbor ? new Color(0.12f, 0.2f, 0.26f) : new Color(0.24f, 0.12f, 0.12f));
                Combine(ink);
            }
            return look;
        }
    }
}
