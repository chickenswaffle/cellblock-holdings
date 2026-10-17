using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cellblock.Unity
{
    // Presentation for population pressure: the transfer coach, contraband drones and drops, floor mattresses,
    // intake holding markings, the riot zone and the inspector. Objects are pooled by id and reused; nothing
    // is allocated per frame. The simulation owns every position; this view only interpolates toward it.
    public sealed class FacilityPressureView : MonoBehaviour
    {
        sealed class Piece { public Transform Root; public bool Seen; public Transform[] Spin; public Renderer Light; }
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        readonly Dictionary<string, Piece> drones = new Dictionary<string, Piece>(), drops = new Dictionary<string, Piece>(), mattresses = new Dictionary<string, Piece>();
        readonly List<string> stale = new List<string>();
        Transform root, bus, riotZone, inspector, holding;
        Renderer riotRenderer;
        World presented;
        int holdingRevision = -1, holdingLanes = -1;
        float clock;

        public void Initialize()
        {
            root = new GameObject("Pressure").transform; root.SetParent(transform, false);
        }

        Material Material(string name, Color color, bool unlit = false)
        {
            Material value;
            if (materials.TryGetValue(name, out value)) return value;
            Material template = Resources.Load<Material>("BuildingMaterial");
            if (template == null || template.shader == null)
                throw new System.InvalidOperationException("BuildingMaterial is missing. Run Cellblock > Prepare project before Play or building a player.");
            value = new Material(template) { name = "pressure-" + name, color = color, enableInstancing = true };
            if (value.HasProperty("_Smoothness")) value.SetFloat("_Smoothness", name.Contains("glass") ? 0.7f : name.Contains("metal") ? 0.4f : 0.18f);
            if (unlit && value.HasProperty("_EmissionColor")) { value.EnableKeyword("_EMISSION"); value.SetColor("_EmissionColor", color * 1.6f); }
            materials.Add(name, value); return value;
        }
        Transform Shape(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, string material, Color color, bool shadow = true)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.transform.SetParent(parent, false); item.transform.localPosition = position; item.transform.localScale = scale;
            Renderer renderer = item.GetComponent<Renderer>(); renderer.sharedMaterial = Material(material, color, material.StartsWith("lamp"));
            renderer.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            Destroy(item.GetComponent<Collider>()); return item.transform;
        }
        Transform Box(Transform p, Vector3 at, Vector3 size, string m, Color c, bool shadow = true) { return Shape(PrimitiveType.Cube, p, at, size, m, c, shadow); }
        Transform Cylinder(Transform p, Vector3 at, Vector3 size, string m, Color c, bool shadow = true) { return Shape(PrimitiveType.Cylinder, p, at, size, m, c, shadow); }
        Transform Sphere(Transform p, Vector3 at, Vector3 size, string m, Color c, bool shadow = true) { return Shape(PrimitiveType.Sphere, p, at, size, m, c, shadow); }

        public void Refresh(World world, float dt)
        {
            if (world == null || root == null) return;
            if (!ReferenceEquals(world, presented))
            {
                for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
                drones.Clear(); drops.Clear(); mattresses.Clear(); bus = riotZone = inspector = holding = null; holdingRevision = -1;
                presented = world;
            }
            dt = Mathf.Min(dt, 0.05f); clock += dt;
            RefreshBus(world, dt);
            RefreshDrones(world, dt);
            RefreshDrops(world);
            RefreshMattresses(world);
            RefreshHolding(world);
            RefreshRiot(world);
            RefreshInspector(world, dt);
        }

        // ---- transfer coach ----------------------------------------------------------------------
        void RefreshBus(World world, float dt)
        {
            TransferBus data = world.Population.Bus;
            bool visible = data.Phase != "Idle" && !(data.Phase == "Inbound" && data.RoadMinutes > 0);
            if (bus == null) bus = Coach();
            bus.gameObject.SetActive(visible);
            if (!visible) return;
            Vector3 target = FacilityView.V(data.Position);
            if ((bus.position - target).sqrMagnitude > 400) bus.position = target;
            Vector3 travel = target - bus.position;
            bus.position = Vector3.MoveTowards(bus.position, target, Mathf.Max(1, world.Data.Speed) * 16 * dt + travel.magnitude * dt * 2);
            travel.y = 0;
            if (travel.sqrMagnitude > 0.01f) bus.rotation = Quaternion.Slerp(bus.rotation, Quaternion.LookRotation(travel), 1 - Mathf.Exp(-dt * 5));
        }
        Transform Coach()
        {
            Transform coach = new GameObject("Transfer coach").transform; coach.SetParent(root, false);
            Color white = new Color(0.86f, 0.87f, 0.84f), stripe = new Color(0.16f, 0.27f, 0.44f), glass = new Color(0.12f, 0.17f, 0.2f), tyre = new Color(0.08f, 0.08f, 0.08f);
            Box(coach, new Vector3(0, 1.55f, 0), new Vector3(2.5f, 2.4f, 9.6f), "coach-body", white);
            Box(coach, new Vector3(0, 1.15f, 0), new Vector3(2.52f, 0.28f, 9.62f), "coach-stripe", stripe);
            for (int side = -1; side <= 1; side += 2)
                Box(coach, new Vector3(side * 1.26f, 2.05f, -0.4f), new Vector3(0.02f, 0.7f, 7.6f), "coach-glass", glass);
            Box(coach, new Vector3(0, 2.05f, 4.81f), new Vector3(2.2f, 0.9f, 0.03f), "coach-glass", glass);
            // Security mesh over the side windows: the reason this coach reads as a transfer bus.
            for (int side = -1; side <= 1; side += 2)
                for (int bar = 0; bar < 12; bar++) Box(coach, new Vector3(side * 1.275f, 2.05f, -4.0f + bar * 0.62f), new Vector3(0.02f, 0.72f, 0.035f), "coach-metal", new Color(0.3f, 0.32f, 0.33f));
            for (int axle = -1; axle <= 1; axle += 2)
                for (int side = -1; side <= 1; side += 2)
                {
                    Transform wheel = Cylinder(coach, new Vector3(side * 1.12f, 0.48f, axle * 3.3f), new Vector3(0.95f, 0.16f, 0.95f), "coach-tyre", tyre);
                    wheel.localRotation = Quaternion.Euler(0, 0, 90);
                }
            Box(coach, new Vector3(-0.8f, 0.85f, 4.83f), new Vector3(0.3f, 0.16f, 0.03f), "lamp-head", new Color(0.95f, 0.9f, 0.7f), false);
            Box(coach, new Vector3(0.8f, 0.85f, 4.83f), new Vector3(0.3f, 0.16f, 0.03f), "lamp-head", new Color(0.95f, 0.9f, 0.7f), false);
            return coach;
        }

        // ---- drones ------------------------------------------------------------------------------
        void RefreshDrones(World world, float dt)
        {
            foreach (Piece p in drones.Values) p.Seen = false;
            foreach (Drone d in world.Contraband.Drones)
            {
                Piece piece;
                if (!drones.TryGetValue(d.Id, out piece)) { piece = DroneModel(d.Id); drones.Add(d.Id, piece); piece.Root.position = FacilityView.V(d.Position, 7); }
                piece.Seen = true;
                float altitude = d.Phase == "Hover" ? 2.6f : d.Phase == "Outbound" || d.Phase == "Repelled" ? 8 : Mathf.Lerp(2.8f, 7.5f, Mathf.Clamp01(Geometry.Distance(d.Position, d.Target) / 18));
                Vector3 target = FacilityView.V(d.Position, altitude) + new Vector3(0, Mathf.Sin(clock * 3.1f) * 0.06f, 0);
                piece.Root.position = Vector3.Lerp(piece.Root.position, target, 1 - Mathf.Exp(-dt * 6));
                for (int i = 0; i < 4; i++) piece.Spin[i].localRotation = Quaternion.Euler(0, clock * 1800 % 360, 0);
                // Package hangs until the drop; a detected drone blinks red.
                piece.Spin[4].gameObject.SetActive(d.Phase == "Inbound" || d.Phase == "Hover");
                piece.Light.enabled = d.Detected ? (clock * 3 % 1) < 0.5f : (clock * 1.2f % 1) < 0.15f;
            }
            Retire(drones);
        }
        Piece DroneModel(string id)
        {
            var piece = new Piece { Root = new GameObject("Drone · " + id).transform }; piece.Root.SetParent(root, false);
            Color dark = new Color(0.12f, 0.13f, 0.14f), grey = new Color(0.45f, 0.47f, 0.48f);
            Box(piece.Root, Vector3.zero, new Vector3(0.32f, 0.09f, 0.32f), "drone-body", dark);
            var spin = new Transform[5];
            for (int i = 0; i < 4; i++)
            {
                float a = (45 + i * 90) * Mathf.Deg2Rad; Vector3 arm = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Transform strut = Box(piece.Root, arm * 0.22f, new Vector3(0.05f, 0.03f, 0.3f), "drone-body", dark); strut.localRotation = Quaternion.LookRotation(arm);
                Transform hub = new GameObject("Rotor").transform; hub.SetParent(piece.Root, false); hub.localPosition = arm * 0.38f + Vector3.up * 0.06f;
                Box(hub, Vector3.zero, new Vector3(0.36f, 0.008f, 0.04f), "drone-blade", grey, false);
                spin[i] = hub;
            }
            Transform package = new GameObject("Package").transform; package.SetParent(piece.Root, false); package.localPosition = new Vector3(0, -0.32f, 0);
            Box(package, Vector3.zero, new Vector3(0.18f, 0.12f, 0.2f), "package", new Color(0.62f, 0.52f, 0.36f));
            Box(package, new Vector3(0, 0.2f, 0), new Vector3(0.01f, 0.4f, 0.01f), "drone-blade", grey, false);
            spin[4] = package; piece.Spin = spin;
            piece.Light = Sphere(piece.Root, new Vector3(0, -0.06f, 0.15f), Vector3.one * 0.05f, "lamp-red", new Color(1, 0.15f, 0.1f), false).GetComponent<Renderer>();
            return piece;
        }

        // ---- dropped packages and mattresses -------------------------------------------------------
        void RefreshDrops(World world)
        {
            foreach (Piece p in drops.Values) p.Seen = false;
            foreach (ContrabandItem item in world.Contraband.Items)
            {
                if (item.Place != "Dropped") continue;
                Piece piece;
                if (!drops.TryGetValue(item.Id, out piece))
                {
                    piece = new Piece { Root = new GameObject("Dropped package · " + item.Id).transform }; piece.Root.SetParent(root, false);
                    Box(piece.Root, new Vector3(0, 0.1f, 0), new Vector3(0.28f, 0.16f, 0.22f), "package", new Color(0.62f, 0.52f, 0.36f));
                    Box(piece.Root, new Vector3(0, 0.1f, 0), new Vector3(0.29f, 0.04f, 0.23f), "tape", new Color(0.82f, 0.8f, 0.72f));
                    piece.Light = Cylinder(piece.Root, new Vector3(0, 0.02f, 0), new Vector3(0.9f, 0.005f, 0.9f), "lamp-alert", new Color(0.95f, 0.75f, 0.2f), false).GetComponent<Renderer>();
                    piece.Root.rotation = Quaternion.Euler(0, (item.Id.GetHashCode() & 0xff) * 1.4f, 0);
                    drops.Add(item.Id, piece);
                }
                piece.Seen = true; piece.Root.position = FacilityView.V(item.Position, 0.14f);
                // Only drops staff know about are marked; an undetected package just sits there.
                piece.Light.enabled = item.Detected && (clock * 1.5f % 1) < 0.6f;
            }
            Retire(drops);
        }
        void RefreshMattresses(World world)
        {
            foreach (Piece p in mattresses.Values) p.Seen = false;
            foreach (Resident r in world.Data.Residents)
            {
                if (!r.FloorBed || world.InIntake(r) || !world.IsPresent(r)) continue;
                Piece piece;
                if (!mattresses.TryGetValue(r.Id, out piece))
                {
                    piece = new Piece { Root = new GameObject("Floor mattress · " + r.Name).transform }; piece.Root.SetParent(root, false);
                    Box(piece.Root, new Vector3(0, 0.07f, 0), new Vector3(0.78f, 0.1f, 1.85f), "mattress", new Color(0.36f, 0.44f, 0.5f));
                    Box(piece.Root, new Vector3(0, 0.13f, -0.72f), new Vector3(0.55f, 0.06f, 0.3f), "pillow", new Color(0.8f, 0.8f, 0.74f));
                    Box(piece.Root, new Vector3(0, 0.125f, 0.35f), new Vector3(0.8f, 0.03f, 0.9f), "floor-blanket", new Color(0.5f, 0.38f, 0.3f));
                    mattresses.Add(r.Id, piece);
                }
                piece.Seen = true;
                // The mattress lies beside the sleeping spot, along the room's long axis.
                piece.Root.position = FacilityView.V(r.Home, 0.14f) + new Vector3(0.45f, 0, 0);
            }
            Retire(mattresses);
        }
        void Retire(Dictionary<string, Piece> pool)
        {
            stale.Clear();
            foreach (KeyValuePair<string, Piece> pair in pool) if (!pair.Value.Seen) stale.Add(pair.Key);
            foreach (string id in stale) { Destroy(pool[id].Root.gameObject); pool.Remove(id); }
        }

        // ---- intake holding, riot zone, inspector --------------------------------------------------
        void RefreshHolding(World world)
        {
            // Stations only change with construction, so they are found again only when the geometry does.
            if (holding != null && holdingRevision == world.GeometryRevision && holdingLanes == world.Data.Fixtures.Count) return;
            List<Point> stations = world.IntakeStations();
            if (holding != null) Destroy(holding.gameObject);
            holding = new GameObject("Intake holding markings").transform; holding.SetParent(root, false);
            holdingRevision = world.GeometryRevision; holdingLanes = world.Data.Fixtures.Count;
            Color paint = new Color(0.93f, 0.78f, 0.22f);
            // Painted holding bays where arrivals wait, and a box at each processing station.
            Point origin = JusticeRules.SallyPort;
            for (int i = 0; i < 8; i++)
            {
                Vector3 at = FacilityView.V(origin + new Point(-3.6f - (i % 4) * 0.8f, 1.4f - (i / 4) * 0.8f), 0.15f);
                Box(holding, at, new Vector3(0.62f, 0.006f, 0.04f), "lamp-paint", paint, false);
                Box(holding, at + new Vector3(0, 0, 0.3f), new Vector3(0.62f, 0.006f, 0.04f), "lamp-paint", paint, false);
            }
            foreach (Point station in stations)
            {
                Vector3 at = FacilityView.V(station, 0.15f);
                Box(holding, at + new Vector3(0, 0, 0.45f), new Vector3(0.9f, 0.006f, 0.05f), "lamp-paint", paint, false);
                Box(holding, at + new Vector3(0, 0, -0.45f), new Vector3(0.9f, 0.006f, 0.05f), "lamp-paint", paint, false);
                Box(holding, at + new Vector3(0.45f, 0, 0), new Vector3(0.05f, 0.006f, 0.9f), "lamp-paint", paint, false);
                Box(holding, at + new Vector3(-0.45f, 0, 0), new Vector3(0.05f, 0.006f, 0.9f), "lamp-paint", paint, false);
            }
        }
        void RefreshRiot(World world)
        {
            Disturbance riot = world.Population.Riot;
            if (riotZone == null)
            {
                riotZone = new GameObject("Disturbance zone").transform; riotZone.SetParent(root, false);
                riotRenderer = Cylinder(riotZone, Vector3.zero, new Vector3(12, 0.004f, 12), "lamp-riot", new Color(0.86f, 0.18f, 0.14f), false).GetComponent<Renderer>();
                for (int i = 0; i < 7; i++)
                {
                    float a = i * 0.9f, r = 1.2f + (i % 3) * 1.1f;
                    Transform debris = Box(riotZone, new Vector3(Mathf.Cos(a) * r, 0.06f, Mathf.Sin(a) * r), new Vector3(0.3f + i % 2 * 0.2f, 0.08f, 0.18f), "debris", new Color(0.4f, 0.38f, 0.34f));
                    debris.localRotation = Quaternion.Euler(0, i * 47, i % 2 * 12);
                }
            }
            bool active = riot.Phase != "None";
            riotZone.gameObject.SetActive(active);
            if (!active) return;
            riotZone.position = FacilityView.V(riot.Center, 0.15f);
            float pulse = riot.Phase == "Riot" ? 0.6f + Mathf.Sin(clock * 4) * 0.4f : 0.35f + Mathf.Sin(clock * 2) * 0.15f;
            riotRenderer.transform.localScale = new Vector3(12 * pulse + 2, 0.004f, 12 * pulse + 2);
            for (int i = 1; i < riotZone.childCount; i++) riotZone.GetChild(i).gameObject.SetActive(riot.Phase == "Riot");
        }
        void RefreshInspector(World world, float dt)
        {
            AuditState audit = world.Population.Audit;
            if (inspector == null)
            {
                inspector = new GameObject("Inspector").transform; inspector.SetParent(root, false);
                Color suit = new Color(0.17f, 0.19f, 0.24f), skin = new Color(0.62f, 0.45f, 0.34f);
                Shape(PrimitiveType.Capsule, inspector, new Vector3(0, 0.92f, 0), new Vector3(0.38f, 0.48f, 0.26f), "suit", suit);
                Sphere(inspector, new Vector3(0, 1.6f, 0), new Vector3(0.22f, 0.27f, 0.23f), "inspector-skin", skin);
                Box(inspector, new Vector3(0.19f, 1.08f, 0.16f), new Vector3(0.2f, 0.27f, 0.02f), "clipboard", new Color(0.55f, 0.42f, 0.28f));
                Box(inspector, new Vector3(0.19f, 1.1f, 0.172f), new Vector3(0.16f, 0.2f, 0.005f), "paper", new Color(0.92f, 0.92f, 0.88f));
                Box(inspector, new Vector3(0, 1.32f, 0.12f), new Vector3(0.06f, 0.08f, 0.01f), "lamp-badge", new Color(0.85f, 0.75f, 0.35f), false);
            }
            bool active = audit.Phase == "In progress";
            inspector.gameObject.SetActive(active);
            if (!active) return;
            Vector3 target = FacilityView.V(audit.Inspector, 0.14f), travel = target - inspector.position;
            if (travel.sqrMagnitude > 200) inspector.position = target;
            inspector.position = Vector3.MoveTowards(inspector.position, target, Mathf.Max(1, world.Data.Speed) * 1.6f * dt + travel.magnitude * dt);
            travel.y = 0;
            if (travel.sqrMagnitude > 0.0004f) inspector.rotation = Quaternion.Slerp(inspector.rotation, Quaternion.LookRotation(travel), 1 - Mathf.Exp(-dt * 6));
        }
    }
}
