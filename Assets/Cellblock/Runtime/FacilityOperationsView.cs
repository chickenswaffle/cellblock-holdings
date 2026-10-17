using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        sealed class ProjectView { public Transform Root; public int Stage; }
        sealed class TruckView { public Transform Root; public Transform[] Wheels; public Vector3 Velocity; }
        readonly Dictionary<string, ProjectView> constructionViews = new Dictionary<string, ProjectView>();
        readonly Dictionary<string, TruckView> trucks = new Dictionary<string, TruckView>();
        Transform transportRoot, gateRoot, lightRoot, alarmRoot, feedWalls;
        Light facilitySun, alarmLight;
        readonly List<Light> facilityLights = new List<Light>();
        readonly List<Renderer> luminaireMeshes = new List<Renderer>();
        AudioSource siren;
        AudioClip sirenClip;
        LineRenderer coverage;
        Fixture watchedCamera;
        int lightingRevision = -1, feedWallRevision = -1;
        bool feedFullHeight;
        float nextCoverage;
        public void SetWatchedCamera(Fixture camera) { watchedCamera = camera; }

        void RefreshConstruction(World world, bool force)
        {
            foreach (string id in constructionViews.Keys.ToArray())
                if (!world.Data.Projects.Any(p => p.Id == id && !p.Completed))
                { Clear(constructionViews[id].Root); Destroy(constructionViews[id].Root.gameObject); constructionViews.Remove(id); }
            foreach (Project project in world.Data.Projects.Where(p => !p.Completed))
            {
                int stage = 17;
                unchecked
                {
                    stage = stage * 31 + Mathf.FloorToInt((15 - project.Preparation) / 5);
                    foreach (WorkItem work in project.Work) stage = stage * 31 + Mathf.FloorToInt((1 - work.Remaining / work.Required) * 20);
                    stage = stage * 31 + (FullHeight ? 1 : 0);
                }
                ProjectView visual;
                if (!constructionViews.TryGetValue(project.Id, out visual))
                { visual = new ProjectView { Root = new GameObject(project.Name + " · live construction").transform, Stage = int.MinValue }; visual.Root.SetParent(planned, false); constructionViews.Add(project.Id, visual); }
                if (!force && visual.Stage == stage) continue;
                visual.Stage = stage; Clear(visual.Root);
                if (project.Preparation < 10)
                {
                    var allWalls = world.Data.Walls.Where(w => !project.RemoveWalls.Contains(w.Id)).Concat(project.Work.Where(w => w.Wall != null).Select(w => w.Wall)).ToList();
                    List<List<Point>> futureRooms = Geometry.Rooms(allWalls);
                    for (int room = 0; room < futureRooms.Count; room++)
                    {
                        Point center = futureRooms[room].Aggregate(new Point(), (sum, p) => sum + p) * (1f / futureRooms[room].Count);
                        if (!world.Rooms.Any(r => Geometry.Contains(r, center))) Floor(futureRooms, room, visual.Root);
                    }
                }
                foreach (WorkItem work in project.Work)
                {
                    float progress = Mathf.Clamp01(1 - work.Remaining / work.Required);
                    if (work.Wall != null) ConstructionWall(visual.Root, work.Wall, progress);
                    if (work.Fixture != null)
                    {
                        if (progress >= 0.95f) FixtureView(visual.Root, work.Fixture);
                        else
                        {
                            FixtureDefinition definition = FixtureCatalog.Get(work.Fixture.Kind);
                            Transform crate = Box(visual.Root, progress > 0 ? "Fitout in progress" : "Material delivery crate", V(work.Fixture.Position, 0.30f),
                                new Vector3(definition.Width, progress > 0 ? 0.60f : 0.18f, definition.Depth), "crate", new Color(0.57f, 0.47f, 0.31f));
                            crate.localRotation = Quaternion.Euler(0, work.Fixture.Rotation, 0);
                            if (progress > 0.35f)
                            {
                                Transform item = new GameObject("Unfinished furnishing").transform; item.SetParent(visual.Root, false);
                                FixtureView(item, work.Fixture); item.localScale = new Vector3(1, Mathf.Lerp(0.25f, 0.95f, progress), 1);
                            }
                        }
                    }
                }
                WorkItem first = project.Work.FirstOrDefault();
                if (first != null)
                {
                    Point site = first.Site;
                    for (int i = 0; i < 3; i++) Box(visual.Root, "Stacked materials", V(site + new Point(1.2f, -1.3f), 0.20f + i * 0.12f), new Vector3(0.8f, 0.10f, 0.8f), "wood", new Color(0.54f, 0.43f, 0.27f));
                }
                Combine(visual.Root);
            }
        }
        void ConstructionWall(Transform root, Wall wall, float progress)
        {
            float completedHeight = FullHeight ? 3.05f : 1.15f;
            foreach (Segment segment in Geometry.Segments(wall, true))
            {
                Beam(root, segment.A, segment.B, 0.17f, 0.08f, 0.42f, "foundation", new Color(0.49f, 0.49f, 0.43f));
                Beam(root, segment.A, segment.B, 0.23f, 0.025f, 0.07f, "planned", Ochre);
                if (progress <= 0.02f) continue;
                float height = Mathf.Lerp(0.14f, completedHeight, Mathf.Clamp01(progress / 0.8f));
                int posts = Mathf.Max(1, Mathf.CeilToInt((segment.B - segment.A).Length / 1.4f));
                for (int i = 0; i <= posts; i++)
                    Box(root, "Rising frame", V(segment.A + (segment.B - segment.A) * (i / (float)posts), 0.18f + height / 2), new Vector3(0.07f, height, 0.07f), "frame-timber", new Color(0.61f, 0.49f, 0.31f));
                Beam(root, segment.A, segment.B, 0.18f + height, 0.07f, 0.14f, "frame-timber", new Color(0.61f, 0.49f, 0.31f));
                if (progress > 0.25f)
                {
                    float clad = completedHeight * Mathf.Clamp01((progress - 0.25f) / 0.7f);
                    Beam(root, segment.A, segment.B, 0.18f + clad / 2, clad, 0.23f, wall.Finish == 1 ? "brick" : "plaster", wall.Finish == 1 ? new Color(0.56f, 0.37f, 0.28f) : Concrete);
                }
            }
            if (wall.Door >= 0)
            {
                Point door = Geometry.OnLength(wall, wall.Door);
                Cylinder(root, "Door setting-out", V(door, 0.30f), new Vector3(0.12f, 0.20f, 0.12f), "planned", Ochre);
            }
        }
        void RefreshWorkers(World world, bool force)
        {
            foreach (StaffMember member in world.Data.Staff)
            {
                if (!member.Arrived) continue;
                Actor actor; bool created = !actors.TryGetValue(member.Id, out actor);
                if (created) actor = Person(member.Id, true, member.Role);
                actor.PoseArms = member.Duty == "Climbing tower";
                AnimateWorker(actor, member.Position, world, force || created, !member.Duty.StartsWith("Walking") && member.Duty != "Patrolling" && member.Duty != "Responding to alarm" && member.Duty != "Responding to escape" && world.IsOnShift(member), member.Role);
                AnimateSecurityOfficer(actor, member, world, force || created);
                actor.Marker.gameObject.SetActive(member.Id == selectedResidentId);
                UpdateCharacterNameTag(actor,world.StaffTitle(member)+" "+member.Name,member.Id==selectedResidentId);
            }
            foreach (Crew crew in world.Data.Crews.Where(c => c.Arrived))
            {
                for (int i = 0; i < 2; i++)
                {
                    string id = crew.Id + "/builder-" + i; Actor actor; bool created = !actors.TryGetValue(id, out actor);
                    if (created) actor = Person(id, true, "Builder");
                    AnimateBuilder(actor, crew, i, world, force || created);
                }
            }
        }
        void AnimateWorker(Actor actor, Point position, World world, bool snap, bool working, string role)
        {
            actor.Seen = true; actor.Root.gameObject.SetActive(true);
            actor.Motion.Position = position; actor.Motion.WalkSpeed = 1.15f;
            MoveActor(actor, actor.Motion, world, snap);
            if (actor.WorkTool != null) actor.WorkTool.gameObject.SetActive(working);
            if (!working || world.Data.Speed == 0 || Geometry.Distance(new Point(actor.Root.position.x, actor.Root.position.z), position) > 0.2f) return;
            float phase = Time.unscaledTime * Mathf.Max(1, world.Data.Speed) * (role == "Builder" ? 5.8f : 1.6f) + actor.Gait;
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 12);
            actor.Limbs[0].localRotation = Quaternion.Slerp(actor.Limbs[0].localRotation, Quaternion.Euler(role == "Builder" ? -45 : -65, 0, -8), blend);
            actor.Limbs[1].localRotation = Quaternion.Slerp(actor.Limbs[1].localRotation, Quaternion.Euler(role == "Builder" ? -65 + Mathf.Sin(phase) * 40 : role == "Officer" ? -105 : -55 + Mathf.Sin(phase) * 5, 0, 8), blend);
            actor.Body.localRotation = Quaternion.Slerp(actor.Body.localRotation, Quaternion.Euler(role == "Builder" ? 5 : 0, Mathf.Sin(phase * 0.4f) * 3, 0), blend);
        }
        TruckView Truck(Transform parent, string kind)
        {
            Transform root = new GameObject(kind == "Crew" ? "Contractor pickup / shared crew" : "Staff shuttle").transform; root.SetParent(parent, false);
            Color color = kind == "Crew" ? new Color(0.78f, 0.52f, 0.18f) : new Color(0.34f, 0.43f, 0.47f);
            Box(root, "Chassis", new Vector3(0, 0.55f, 0), new Vector3(1.85f, 0.30f, 4.3f), "vehicle-" + kind, color);
            Box(root, "Cab", new Vector3(0, 1.12f, 0.65f), new Vector3(1.77f, 0.92f, 1.95f), "vehicle-" + kind, color);
            Box(root, "Windshield", new Vector3(0, 1.26f, 1.64f), new Vector3(1.50f, 0.50f, 0.025f), "glass", new Color(0.17f, 0.27f, 0.31f));
            for (int side = -1; side <= 1; side += 2)
            {
                Box(root, "Cab side window", new Vector3(side * 0.90f, 1.26f, 0.70f), new Vector3(0.025f, 0.50f, 1.25f), "glass", new Color(0.17f, 0.27f, 0.31f));
                Box(root, "Mirror", new Vector3(side * 1.02f, 1.15f, 1.15f), new Vector3(0.18f, 0.17f, 0.12f), "rubber", Color.gray);
                Box(root, "Bed side", new Vector3(side * 0.85f, 0.95f, -1.10f), new Vector3(0.12f, 0.60f, 1.65f), "vehicle-" + kind, color);
                Box(root, "Headlight", new Vector3(side * 0.62f, 0.70f, 2.17f), new Vector3(0.37f, 0.22f, 0.04f), "lamp", new Color(0.94f, 0.87f, 0.64f));
            }
            Box(root, "Tailgate", new Vector3(0, 0.95f, -1.96f), new Vector3(1.72f, 0.60f, 0.12f), "vehicle-" + kind, color);
            if (kind == "Crew")
            {
                Box(root, "Tool chest", new Vector3(0, 1.03f, -0.65f), new Vector3(1.50f, 0.40f, 0.55f), "steel", Steel);
                for (int x = -1; x <= 1; x += 2) Box(root, "Ladder rail", new Vector3(x * 0.55f, 1.70f, 0), new Vector3(0.055f, 0.08f, 3.6f), "light-steel", Color.gray);
                for (int i = 0; i < 8; i++) Box(root, "Ladder rung", new Vector3(0, 1.70f, -1.4f + i * 0.40f), new Vector3(1.1f, 0.05f, 0.05f), "light-steel", Color.gray);
            }
            else Box(root, "Shuttle rear cabin", new Vector3(0, 1.12f, -1.02f), new Vector3(1.75f, 0.95f, 1.72f), "vehicle-" + kind, color);
            Combine(root);
            var wheels = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                wheels[i] = Cylinder(root, "Road wheel", new Vector3(i % 2 == 0 ? -0.91f : 0.91f, 0.38f, i < 2 ? 1.30f : -1.25f), new Vector3(0.62f, 0.11f, 0.62f), "rubber", new Color(0.10f, 0.12f, 0.12f));
                wheels[i].localRotation = Quaternion.Euler(0, 0, 90);
            }
            return new TruckView { Root = root, Wheels = wheels };
        }
        void RefreshOperations(World world, bool force)
        {
            if (transportRoot == null) transportRoot = Root("Arrivals and shared transport");
            foreach (TransportTrip trip in world.Data.Trips)
            {
                TruckView truck;
                if (!trucks.TryGetValue(trip.Id, out truck)) { truck = Truck(transportRoot, trip.Kind); truck.Root.position = V(trip.Position, 0.10f); trucks.Add(trip.Id, truck); }
                truck.Root.gameObject.SetActive(trip.Phase != "Gone" && trip.Phase != "Boarding");
                Vector3 before = truck.Root.position, target = V(trip.Position, 0.10f);
                truck.Root.position = force ? target : Vector3.SmoothDamp(before, target, ref truck.Velocity, 0.16f / Mathf.Sqrt(Mathf.Max(1, world.Data.Speed)), Mathf.Infinity, Time.unscaledDeltaTime);
                Vector3 moved = truck.Root.position - before;
                if (moved.sqrMagnitude > 0.0001f) truck.Root.rotation = Quaternion.Slerp(truck.Root.rotation, Quaternion.LookRotation(trip.Phase == "Departing" && trip.Waypoint == 0 ? -moved : moved), 1 - Mathf.Exp(-Time.unscaledDeltaTime * 12));
                if (trip.Phase == "Unloading" || trip.Phase == "Parked") truck.Root.rotation = Quaternion.Slerp(truck.Root.rotation, Quaternion.Euler(0, 180, 0), 1 - Mathf.Exp(-Time.unscaledDeltaTime * 5));
                foreach (Transform wheel in truck.Wheels) wheel.Rotate(0, moved.magnitude * (trip.Phase == "Departing" && trip.Waypoint == 0 ? -180 : 180), 0, Space.Self);
            }
            foreach (string id in trucks.Keys.Where(id => !world.Data.Trips.Any(t => t.Id == id)).ToArray())
            { Clear(trucks[id].Root); Destroy(trucks[id].Root.gameObject); trucks.Remove(id); }
            if (gateRoot == null)
            {
                gateRoot = Root("Motorized vehicle gate");
                Fence(gateRoot, new Point(-3, 0), new Point(3, 0));
                Box(gateRoot, "Warning stripe", new Vector3(0, 1.30f, -0.05f), new Vector3(5.8f, 0.16f, 0.08f), "planned", Ochre); Combine(gateRoot);
            }
            gateRoot.localPosition = Vector3.Lerp(gateRoot.localPosition, new Vector3(world.Data.Operations.GateOpen ? -8.3f : -2, 0, -24), force ? 1 : 1 - Mathf.Exp(-Time.unscaledDeltaTime * 4));
            RefreshLighting(world, force); RefreshCoverage(world); RefreshSecurity(world, force);
            if (force || feedWallRevision != world.GeometryRevision || feedFullHeight != FullHeight)
            {
                if (feedWalls == null) feedWalls = Root("CCTV full-height wall extensions · layer 30");
                Clear(feedWalls); feedWallRevision = world.GeometryRevision; feedFullHeight = FullHeight;
                if (!FullHeight) foreach (Wall wall in world.Data.Walls) foreach (Segment segment in Geometry.Segments(wall, true))
                    Beam(feedWalls, segment.A, segment.B, 2.25f, 1.90f, 0.24f, "plaster", Concrete);
                Combine(feedWalls); foreach (Transform child in feedWalls.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
            }
        }
        void RefreshLighting(World world, bool force)
        {
            if (facilitySun == null) facilitySun = GetComponentsInChildren<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            float hour = world.Data.Minute % 1440 / 60f;
            float daylight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Sin((hour - 6) / 12 * Mathf.PI) * 2));
            if (facilitySun != null) { facilitySun.intensity = Mathf.Lerp(0.08f, 1.5f, daylight); facilitySun.transform.rotation = Quaternion.Euler(15 + daylight * 35, -32, 0); }
            RenderSettings.ambientIntensity = Mathf.Lerp(0.18f, 1, daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.055f, 0.065f, 0.10f), new Color(0.53f, 0.57f, 0.57f), daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.025f, 0.03f, 0.045f), new Color(0.30f, 0.32f, 0.28f), daylight);
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.08f, 0.11f, 0.20f), new Color(0.69f, 0.76f, 0.80f), daylight);
            if (force || lightingRevision != world.GeometryRevision)
            {
                if (lightRoot == null) lightRoot = Root("Facility luminaires"); Clear(lightRoot); facilityLights.Clear(); luminaireMeshes.Clear(); lightingRevision = world.GeometryRevision;
                foreach (List<Point> room in world.Rooms)
                {
                    Point center = room.Aggregate(new Point(), (sum, p) => sum + p) * (1f / room.Count);
                    Transform housing = Box(lightRoot, "Ceiling luminaire", V(center, 2.9f), new Vector3(1.2f, 0.08f, 0.18f), "ceiling-on", new Color(0.91f, 0.87f, 0.73f));
                    luminaireMeshes.Add(housing.GetComponent<Renderer>());
                    var lamp = new GameObject("Interior light").AddComponent<Light>(); lamp.transform.SetParent(lightRoot, false); lamp.transform.position = V(center, 2.6f);
                    lamp.type = LightType.Point; lamp.range = 10; lamp.intensity = 3; lamp.color = new Color(1, 0.89f, 0.70f); lamp.shadows = LightShadows.None; facilityLights.Add(lamp);
                }
            }
            for (int i = 0; i < facilityLights.Count; i++)
                facilityLights[i].enabled = world.Data.Operations.Lights && world.PowerAt(new Point(facilityLights[i].transform.position.x, facilityLights[i].transform.position.z));
            Material luminaireOn = Material("ceiling-on", new Color(0.91f, 0.87f, 0.73f));
            luminaireOn.EnableKeyword("_EMISSION"); luminaireOn.SetColor("_EmissionColor", new Color(0.73f, 0.69f, 0.55f));
            Material luminaireOff = Material("ceiling-off", new Color(0.25f, 0.28f, 0.29f));
            for (int i = 0; i < luminaireMeshes.Count; i++) luminaireMeshes[i].sharedMaterial = facilityLights[i].enabled ? luminaireOn : luminaireOff;
            if (alarmRoot == null)
            {
                alarmRoot = Root("Alarm beacon"); alarmRoot.position = new Vector3(-2, 3.2f, -8);
                Cylinder(alarmRoot, "Red alarm lens", Vector3.zero, new Vector3(0.22f, 0.17f, 0.22f), "alarm", Color.red);
                alarmLight = alarmRoot.gameObject.AddComponent<Light>(); alarmLight.type = LightType.Point; alarmLight.range = 8; alarmLight.color = Color.red;
                siren = alarmRoot.gameObject.AddComponent<AudioSource>(); siren.loop = true; siren.volume = 0.035f; siren.spatialBlend = 0;
                const int rate = 22050; var samples = new float[rate * 2];
                for (int i = 0; i < samples.Length; i++) { float t = i / (float)rate; samples[i] = Mathf.Sin(t * (580 + 110 * Mathf.Sin(t * Mathf.PI)) * Mathf.PI * 2) * 0.35f; }
                sirenClip = AudioClip.Create("Facility alert", samples.Length, 1, rate, false); sirenClip.SetData(samples, 0); siren.clip = sirenClip;
            }
            bool alarm = world.Data.Operations.Alarm; alarmLight.enabled = alarm;
            alarmLight.intensity = alarm ? 1.5f + Mathf.Sin(Time.unscaledTime * 7) * 1.2f : 0;
            if (alarm && world.Data.Speed > 0 && Application.isFocused) { if (!siren.isPlaying) siren.Play(); } else if (siren.isPlaying) siren.Stop();
        }
        void RefreshCoverage(World world)
        {
            if (coverage == null)
            {
                coverage = new GameObject("Selected CCTV field of view").AddComponent<LineRenderer>(); coverage.transform.SetParent(transform, false);
                coverage.gameObject.layer = 29;
                coverage.sharedMaterial = Material("handle", Teal); coverage.startWidth = coverage.endWidth = 0.025f; coverage.useWorldSpace = true;
            }
            coverage.gameObject.SetActive(watchedCamera != null && world.CameraOnline(watchedCamera));
            if (watchedCamera == null || !world.CameraOnline(watchedCamera)) return;
            if (Time.unscaledTime < nextCoverage) return; nextCoverage = Time.unscaledTime + 0.2f;
            var points = new List<Vector3>(); var walls = world.SightLines;
            for (int i = 0; i <= 20; i++)
            {
                float angle = (watchedCamera.Rotation - 60 + i * 6) * Mathf.Deg2Rad;
                Point direction = new Point(Mathf.Sin(angle), Mathf.Cos(angle)); Point end = watchedCamera.Position;
                for (float distance = 0.35f; distance <= world.CameraRangeFor(watchedCamera); distance += 0.35f)
                { Point candidate = watchedCamera.Position + direction * distance; if (!walls.Clear(watchedCamera.Position, candidate)) break; end = candidate; }
                points.Add(V(watchedCamera.Position, 0.18f)); points.Add(V(end, 0.18f)); points.Add(V(watchedCamera.Position, 0.18f));
            }
            coverage.positionCount = points.Count; coverage.SetPositions(points.ToArray());
        }
    }
}
