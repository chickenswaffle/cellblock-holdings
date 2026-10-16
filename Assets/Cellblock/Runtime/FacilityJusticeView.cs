using System.Collections.Generic;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    // Court transport and custody presentation. Everything here is visual and never enters routing.
    public sealed partial class FacilityView
    {
        sealed class CourtVanView { public Transform Root, LightBar; public Transform[] Wheels; }
        // Kept apart from the crew/staff truck dictionary, which is pruned against State.Trips.
        readonly Dictionary<string, CourtVanView> courtVans = new Dictionary<string, CourtVanView>();
        Transform justiceRoot, sallyPort, escapeAlert, escapeBeacon;
        World justiceWorld;
        static readonly Color CourtWhite = new Color(0.86f, 0.87f, 0.84f), CourtBlue = new Color(0.16f, 0.27f, 0.44f), AlertRed = new Color(0.86f, 0.18f, 0.14f);

        partial void ResidentVisibility(World world, Resident resident, ref bool visible)
        {
            // Residents aboard, at court, released, or at large are hidden rather than moved off the validated site.
            if (!world.IsPresent(resident)) visible = false;
        }

        partial void RefreshJustice(World world, bool force)
        {
            if (justiceRoot == null) justiceRoot = Root("Court transport and custody");
            if (!ReferenceEquals(justiceWorld, world))
            {
                foreach (CourtVanView old in courtVans.Values) { Clear(old.Root); Destroy(old.Root.gameObject); }
                courtVans.Clear(); justiceWorld = world; force = true;
            }
            if (sallyPort == null) BuildSallyPort();
            RefreshCourtVan(world, force);
            RefreshEscapeAlert(world);
            RefreshPersonnelView(world);
        }

        void BuildSallyPort()
        {
            sallyPort = new GameObject("Court transport bay · presentation").transform; sallyPort.SetParent(justiceRoot, false);
            Color paint = new Color(0.86f, 0.74f, 0.32f);
            Vector3 bay = V(JusticeRules.VanParking, 0.075f);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(sallyPort, "Bay line", bay + new Vector3(side * 1.45f, 0, 0), new Vector3(0.10f, 0.012f, 6.0f), "court-bay", paint);
                Box(sallyPort, "Bay line", bay + new Vector3(0, 0, side * 3.0f), new Vector3(2.9f, 0.012f, 0.10f), "court-bay", paint);
            }
            for (int i = 0; i < 5; i++)
                Box(sallyPort, "Bay hatch", bay + new Vector3(0, 0, -2.2f + i * 1.1f), new Vector3(2.6f, 0.012f, 0.07f), "court-bay", paint);
            Cylinder(sallyPort, "Boarding point", V(JusticeRules.SallyPort, 0.08f), new Vector3(0.9f, 0.006f, 0.9f), "court-bay", paint);
            Combine(sallyPort);
        }

        CourtVanView BuildCourtVan()
        {
            Transform root = new GameObject("Court transport van").transform; root.SetParent(justiceRoot, false);
            Color glass = new Color(0.17f, 0.27f, 0.31f);
            Box(root, "Chassis", new Vector3(0, 0.52f, 0), new Vector3(2.0f, 0.28f, 5.2f), "court-van-body", CourtWhite);
            Box(root, "Cab", new Vector3(0, 1.20f, 1.62f), new Vector3(1.94f, 1.10f, 1.80f), "court-van-body", CourtWhite);
            Box(root, "Secure compartment", new Vector3(0, 1.42f, -0.82f), new Vector3(2.0f, 1.56f, 3.12f), "court-van-body", CourtWhite);
            Box(root, "Windshield", new Vector3(0, 1.38f, 2.53f), new Vector3(1.66f, 0.56f, 0.025f), "glass", glass);
            Box(root, "Rear doors", new Vector3(0, 1.42f, -2.39f), new Vector3(1.90f, 1.50f, 0.04f), "court-van-body", CourtWhite);
            Box(root, "Rear door seam", new Vector3(0, 1.42f, -2.42f), new Vector3(0.02f, 1.45f, 0.02f), "rubber", Steel);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(root, "Cab window", new Vector3(side * 0.98f, 1.40f, 1.65f), new Vector3(0.025f, 0.50f, 1.0f), "glass", glass);
                Box(root, "Livery stripe", new Vector3(side * 1.005f, 0.98f, -0.10f), new Vector3(0.02f, 0.16f, 5.0f), "court-van-stripe", CourtBlue);
                Box(root, "Mirror", new Vector3(side * 1.08f, 1.30f, 2.15f), new Vector3(0.18f, 0.17f, 0.12f), "rubber", Color.gray);
                Box(root, "Headlight", new Vector3(side * 0.65f, 0.76f, 2.61f), new Vector3(0.36f, 0.20f, 0.04f), "lamp", new Color(0.94f, 0.87f, 0.64f));
                for (int i = 0; i < 3; i++)
                {
                    float z = -1.95f + i * 0.92f;
                    Box(root, "Secure window", new Vector3(side * 1.005f, 1.78f, z), new Vector3(0.02f, 0.30f, 0.52f), "glass", glass);
                    for (int bar = -1; bar <= 1; bar++)
                        Box(root, "Window bar", new Vector3(side * 1.02f, 1.78f, z + bar * 0.17f), new Vector3(0.025f, 0.32f, 0.025f), "steel", Steel);
                }
            }
            Combine(root);
            // Added after baking so they can animate.
            Transform lightBar = Box(root, "Roof light bar", new Vector3(0, 1.82f, 1.55f), new Vector3(1.3f, 0.12f, 0.26f), "court-light", new Color(0.95f, 0.62f, 0.16f));
            var wheels = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                wheels[i] = Cylinder(root, "Road wheel", new Vector3(i % 2 == 0 ? -0.98f : 0.98f, 0.38f, i < 2 ? 1.62f : -1.55f), new Vector3(0.64f, 0.11f, 0.64f), "rubber", new Color(0.10f, 0.12f, 0.12f));
                wheels[i].localRotation = Quaternion.Euler(0, 0, 90);
            }
            return new CourtVanView { Root = root, LightBar = lightBar, Wheels = wheels };
        }

        void RefreshCourtVan(World world, bool force)
        {
            CourtRun run = world.CourtVan;
            CourtVanView van;
            bool created = !courtVans.TryGetValue("court-van", out van);
            if (created) { van = BuildCourtVan(); courtVans.Add("court-van", van); }
            bool visible = world.CourtVanOnRoad;
            van.Root.gameObject.SetActive(visible);
            if (!visible) return;
            Vector3 target = V(run.Position, 0.10f), before = van.Root.position;
            if (force || created || (target - before).sqrMagnitude > 900)
            {
                van.Root.position = target; before = target;
                Vector3 heading = V(JusticeRules.NextWaypoint(run)) - V(run.Position);
                if (run.Phase == "Boarding" || run.Phase == "Unloading") heading = Vector3.forward;
                if (heading.sqrMagnitude > 0.01f) van.Root.rotation = Quaternion.LookRotation(heading);
            }
            else van.Root.position = Vector3.Lerp(before, target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 16));
            Vector3 moved = van.Root.position - before;
            if (moved.sqrMagnitude > 0.0001f) van.Root.rotation = Quaternion.Slerp(van.Root.rotation, Quaternion.LookRotation(moved), 1 - Mathf.Exp(-Time.unscaledDeltaTime * 12));
            foreach (Transform wheel in van.Wheels) wheel.Rotate(0, moved.magnitude * 180, 0, Space.Self);
            // Hazard flash while residents board or step down at the sally port.
            bool stopped = run.Phase == "Boarding" || run.Phase == "Unloading";
            van.LightBar.gameObject.SetActive(!stopped || world.Data.Speed == 0 || Mathf.Repeat(Time.unscaledTime, 1) < 0.5f);
        }

        void RefreshEscapeAlert(World world)
        {
            if (escapeAlert == null)
            {
                escapeAlert = new GameObject("Escape alert marker").transform; escapeAlert.SetParent(justiceRoot, false);
                Cylinder(escapeAlert, "Alert ring", new Vector3(0, 0.03f, 0), new Vector3(1.5f, 0.008f, 1.5f), "justice-alert", AlertRed);
                escapeBeacon = Sphere(escapeAlert, "Alert beacon", new Vector3(0, 2.25f, 0), new Vector3(0.24f, 0.24f, 0.24f), "justice-alert", AlertRed);
            }
            Resident escapee = world.Escapee;
            // Only an attempt the facility has detected is shown; an unseen attempt stays unseen.
            bool show = escapee != null && world.Escape.Detected;
            escapeAlert.gameObject.SetActive(show);
            if (!show) return;
            Actor actor;
            Vector3 position = actors.TryGetValue(escapee.Id, out actor) && actor.Root.gameObject.activeInHierarchy ? actor.Root.position : V(escapee.Position, 0.14f);
            escapeAlert.position = new Vector3(position.x, 0.12f, position.z);
            float pulse = 1 + Mathf.Sin(Time.unscaledTime * 6) * 0.18f;
            escapeAlert.localScale = new Vector3(pulse, 1, pulse);
            escapeBeacon.localPosition = new Vector3(0, 2.25f + Mathf.Sin(Time.unscaledTime * 3) * 0.08f, 0);
        }
    }
}
