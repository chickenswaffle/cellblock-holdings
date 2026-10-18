using System.Collections.Generic;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        readonly Dictionary<Actor, Transform> incidentRadios = new Dictionary<Actor, Transform>();
        World incidentWorld;
        LineRenderer incidentRing;
        AudioSource incidentAudio;
        AudioClip radioClick;
        string heardRadio = "";
        int incidentPoseCount;
        void RefreshIncidentVisuals(World world)
        {
            incidentPoseCount = 0;
            if (incidentWorld != world) { incidentWorld = world; incidentRadios.Clear(); heardRadio = ""; }
            var fight = world.Fight;
            if (fight.Active && fight.Phase != "Converging")
            {
                Actor first, second;
                if (actors.TryGetValue(fight.A, out first) && actors.TryGetValue(fight.B, out second))
                {
                    Resident a = world.Data.Residents.Find(r => r.Id == fight.A), b = world.Data.Residents.Find(r => r.Id == fight.B);
                    bool firstReady = a != null && SettleIncidentActor(first, a.Position, world);
                    bool secondReady = b != null && SettleIncidentActor(second, b.Position, world);
                    // Once both are on their marks the brawl view stages the exchanges (FacilityBrawlView.cs).
                    if (firstReady && secondReady) incidentPoseCount += 2;
                }
            }
            foreach (StaffMember staff in world.Data.Staff)
            {
                Actor actor; if (!actors.TryGetValue(staff.Id, out actor)) continue;
                bool radio = staff.Duty == "Radioing control" && world.IncidentReport.Guard == staff.Id && world.IncidentReport.Phase == "Radioing control";
                Transform prop;
                if (radio && !incidentRadios.TryGetValue(actor, out prop))
                {
                    prop = new GameObject("Handheld radio / real dispatch action").transform; prop.SetParent(actor.Hands[1], false);
                    Box(prop, "Radio body", new Vector3(0, .035f, .025f), new Vector3(.07f, .15f, .045f), "radio-body", new Color(.08f, .11f, .13f));
                    Cylinder(prop, "Radio antenna", new Vector3(.022f, .15f, .025f), new Vector3(.012f, .06f, .012f), "radio-body", new Color(.08f, .11f, .13f));
                    Box(prop, "Transmit indicator", new Vector3(0, .075f, .05f), new Vector3(.02f, .018f, .008f), "lamp", Color.green);
                    Combine(prop); incidentRadios.Add(actor, prop);
                }
                if (incidentRadios.TryGetValue(actor, out prop) && prop != null) prop.gameObject.SetActive(radio);
                if (radio)
                {
                    incidentPoseCount++; actor.PoseArms = true; Upright(actor);
                    actor.Limbs[1].localRotation = Quaternion.Euler(-48, -22, 18); actor.Forearms[1].localRotation = Quaternion.Euler(-138, 0, 0);
                    actor.Limbs[0].localRotation = Quaternion.Euler(-12, 0, -10); actor.Forearms[0].localRotation = Quaternion.Euler(-20, 0, 0);
                    actor.Body.localRotation = Quaternion.Euler(3, -6, 0);
                    ShowIncidentName(actor, staff.Name + "\nRADIOING CONTROL", new Color(.88f, .76f, .48f));
                }
                // Responders run with the shared locomotion (FacilityMotion.cs); the escape scene poses the struggle (FacilityEscapeView.cs).
                else if (staff.Duty == "Responding to escape" || staff.Duty == "Responding to fight") incidentPoseCount++;
                else if (staff.Duty == "Separating residents" && fight.Active)
                {
                    if (!SettleIncidentActor(actor, staff.Position, world)) { JogPose(actor, world, true); continue; }
                    incidentPoseCount++; Upright(actor); FaceIncident(actor, V(fight.Center));
                    for (int arm = 0; arm < 2; arm++)
                    { actor.Limbs[arm].localRotation = Quaternion.Euler(-75, arm == 0 ? -18 : 18, 0); actor.Forearms[arm].localRotation = Quaternion.Euler(-25, 0, 0); }
                    ShowIncidentName(actor, staff.Name + "\nSEPARATING", new Color(.55f, .78f, .87f));
                }
            }
            RefreshEscapeScene(world);
            RefreshIncidentRing(world);
            string radioKey = world.IncidentReport.Guard + "/" + world.IncidentReport.Phase + "/" + world.IncidentReport.PhaseMinute;
            if (world.Data.Speed > 0 && radioKey != heardRadio && (world.IncidentReport.Phase == "Radioing control" || world.IncidentReport.Phase == "Broadcast"))
            {
                heardRadio = radioKey;
                if (incidentAudio == null)
                {
                    incidentAudio = gameObject.AddComponent<AudioSource>(); incidentAudio.playOnAwake = false; incidentAudio.volume = .14f; incidentAudio.spatialBlend = 0;
                    radioClick = AudioClip.Create("Radio acknowledgement", 6615, 1, 22050, false);
                    var samples = new float[6615];
                    for (int i = 0; i < samples.Length; i++) { float t = i / 22050f; samples[i] = Mathf.Sin(t * 2 * Mathf.PI * (t < .10f ? 1250 : 900)) * Mathf.Clamp01(1 - t / .3f) * (t < .1f || t > .16f ? .5f : 0); }
                    radioClick.SetData(samples, 0);
                }
                incidentAudio.PlayOneShot(radioClick);
            }
        }
        bool SettleIncidentActor(Actor actor, Point position, World world)
        {
            // A slow frame may leave presentation behind the core. Finish that final clear approach
            // before playing contact poses; never cut a wall or teleport to match a phase label.
            Vector3 target = V(position, StandHeight(actor, position, world)), before = actor.Root.position;
            if (world.Routes.Clear(new Point(before.x, before.z), position))
                actor.Root.position = Vector3.MoveTowards(before, target, Mathf.Min(Time.unscaledDeltaTime, .15f) * 3.2f);
            return Vector3.Distance(actor.Root.position, target) < .12f;
        }
        void Upright(Actor actor) { actor.Model.localPosition = Vector3.zero; actor.Model.localRotation = Quaternion.identity; }
        void FaceIncident(Actor actor, Vector3 target)
        {
            Vector3 direction = target - actor.Root.position; direction.y = 0;
            if (direction.sqrMagnitude > .001f) actor.Root.rotation = Quaternion.Slerp(actor.Root.rotation, Quaternion.LookRotation(direction), Time.unscaledDeltaTime * 12);
        }
        void ShowIncidentName(Actor actor, string text, Color color)
        {
            if (actor.NameTag == null) return; actor.NameTag.gameObject.SetActive(true); actor.NameTag.text = text; actor.NameTag.color = color;
        }
        void FightPose(Actor actor, Actor other, World world)
        {
            incidentPoseCount++; Upright(actor); FaceIncident(actor, other.Root.position);
            if (actor.NameTag != null) { actor.NameTag.gameObject.SetActive(true); actor.NameTag.color = new Color(.95f, .73f, .48f); }
            float phase = actor.PoseClock * 5 + (actors.TryGetValue(world.Fight.A, out Actor first) && actor == first ? 0 : Mathf.PI), jab = Mathf.Max(0, Mathf.Sin(phase));
            bool scuffle = world.Fight.Phase == "Scuffle";
            actor.Body.localRotation = Quaternion.Euler(scuffle ? 9 + Mathf.Sin(phase) * 4 : 3, Mathf.Sin(phase * .5f) * 7, 0);
            actor.Limbs[0].localRotation = Quaternion.Euler(-65, -8, -8); actor.Forearms[0].localRotation = Quaternion.Euler(-95, 0, 0);
            actor.Limbs[1].localRotation = Quaternion.Euler(scuffle ? -80 - jab * 22 : -48, 5, 7);
            actor.Forearms[1].localRotation = Quaternion.Euler(scuffle ? -100 + jab * 92 : -60, 0, 0);
            if (world.Fight.Phase == "Separating")
                for (int arm = 0; arm < 2; arm++) { actor.Limbs[arm].localRotation = Quaternion.Euler(-35, 0, arm == 0 ? -18 : 18); actor.Forearms[arm].localRotation = Quaternion.Euler(-45, 0, 0); }
        }
        void JogPose(Actor actor, World world, bool urgent)
        {
            incidentPoseCount++; Upright(actor);
            if (actor.Staff && world.Data.Speed > 0) actor.PoseClock += Mathf.Min(Time.unscaledDeltaTime, .05f) * Mathf.Sqrt(world.Data.Speed);
            float swing = Mathf.Sin(actor.Gait) * (urgent ? 32 : 17);
            actor.Body.localRotation = Quaternion.Euler(urgent ? 15 : 7, 0, 0);
            for (int arm = 0; arm < 2; arm++)
            { actor.Limbs[arm].localRotation = Quaternion.Euler(swing * (arm == 0 ? 1 : -1) - 12, 0, arm == 0 ? -8 : 8); actor.Forearms[arm].localRotation = Quaternion.Euler(-80, 0, 0); }
        }
        void RefreshIncidentRing(World world)
        {
            bool visible = world.Fight.Active || world.Escape.Phase != "None";
            if (incidentRing == null)
            {
                incidentRing = new GameObject("Incident location / presentation only").AddComponent<LineRenderer>(); incidentRing.transform.SetParent(transform, false);
                incidentRing.sharedMaterial = Material("incident-ring", new Color(.91f, .48f, .25f)); incidentRing.useWorldSpace = true; incidentRing.loop = true;
                incidentRing.positionCount = 40; incidentRing.startWidth = incidentRing.endWidth = .045f;
            }
            incidentRing.gameObject.SetActive(visible); if (!visible) return;
            Point center = world.Escapee != null ? world.Escapee.Position : world.Fight.Center;
            float ringHeight = StandHeight(world, center) + SoleRest + 0.03f;
            for (int i = 0; i < 40; i++) { float angle = i * Mathf.PI / 20; incidentRing.SetPosition(i, V(center, ringHeight) + new Vector3(Mathf.Cos(angle) * 1.5f, 0, Mathf.Sin(angle) * 1.5f)); }
        }
#if UNITY_EDITOR
        public int PreviewIncidentPoseCount { get { return incidentPoseCount; } }
        public bool PreviewFightActorsAligned
        {
            get
            {
                if (incidentWorld == null || !incidentWorld.Fight.Active) return false;
                Actor a, b;
                Resident first = incidentWorld.Data.Residents.Find(r => r.Id == incidentWorld.Fight.A), second = incidentWorld.Data.Residents.Find(r => r.Id == incidentWorld.Fight.B);
                return first != null && second != null && actors.TryGetValue(first.Id, out a) && actors.TryGetValue(second.Id, out b) &&
                    Vector3.Distance(a.Root.position, V(first.Position, a.Root.position.y)) < .15f && Vector3.Distance(b.Root.position, V(second.Position, b.Root.position.y)) < .15f && Vector3.Distance(a.Root.position, b.Root.position) > .55f;
            }
        }
        public bool PreviewRadioPropVisible { get { foreach (var pair in incidentRadios) if (pair.Value != null && pair.Value.gameObject.activeInHierarchy) return true; return false; } }
#endif
    }
}
