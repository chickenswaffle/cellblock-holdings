using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    public sealed partial class GameHud
    {
        VisualElement controlPane, cctvRoster, prefabPackages, crewRoster;
        Button controlTab, lightsControl, alarmControl, gateControl, lockdownControl, cctvControl;
        Label controlStatus, feedStatus, arrivalStatus, reportStatus, staffCost, prefabSummary;
        Image feed;
        string cameraSignature = "", crewSignature = "";
        World crewWorld;
        readonly Dictionary<string, Button> crewButtons = new Dictionary<string, Button>();
        World cameraWorld;
        readonly Dictionary<string, Button> cameraButtons = new Dictionary<string, Button>(), prefabButtons = new Dictionary<string, Button>();
        readonly List<Button> packageButtons = new List<Button>();
        public bool ControlIsVisible { get { return workspace == Workspace.Control && inspectorOpen && !BlocksWorldInput; } }
        void BuildPrefabSelector()
        {
            VisualElement pane = categoryItems["Prefabs"];
            pane.Add(Text("ONE PROJECT / CHOOSE YOUR FITOUT", "section-label"));
            prefabPackages = Element("package-selector"); pane.Add(prefabPackages);
            for (int i = 0; i < 3; i++)
            {
                int package = i;
                Button button = Action(new[] { "Shell", "Essential", "Enhanced" }[i], () => { game.SetPrefabPackage(package); RefreshPrefabSelector(); }, "package-button");
                prefabPackages.Add(button); packageButtons.Add(button);
            }
            prefabSummary = Text("", "muted"); pane.Add(prefabSummary);
            foreach (PrefabDefinition definition in PrefabCatalog.All)
            {
                string id = definition.Id;
                Button button = CatalogCard(definition.Name, definition.Description, "One complete quote", "EDITABLE UNIT", () => {
                    game.SelectPrefab(id, game.PrefabPackage); if (compact) inspectorOpen = false; Refresh(true);
                });
                pane.Add(button); prefabButtons.Add(id, button);
            }
        }
        void RefreshPrefabSelector()
        {
            if (prefabSummary == null) return;
            for (int i = 0; i < packageButtons.Count; i++) packageButtons[i].EnableInClassList("selected", i == game.PrefabPackage);
            prefabSummary.text = game.PrefabPackage == 0 ? "Walls and openings only. Furnish later. Rotate the whole unit with Rotate 90°." :
                "Walls + " + (game.PrefabPackage == 1 ? "essential" : "enhanced") + " furnishings in one project. Materials, labor and access are reviewed together; everything opens at handover.";
            foreach (var item in prefabButtons) item.Value.EnableInClassList("selected", game.Tool == BuildTool.Wing && game.PrefabId == item.Key);
        }
        void BuildControl()
        {
            controlPane = Element("control-pane"); inspector.Add(controlPane);
            var heading = Element("control-compact-heading"); heading.Add(Text("PRISON CONTROL", "eyebrow"));
            heading.Add(Action("Close", () => { inspectorOpen = false; Layout(); }, "compact-panel-close")); controlPane.Add(heading);
            controlStatus = Text("", "body"); controlPane.Add(controlStatus);
            var switches = Element("operations-switches"); controlPane.Add(switches);
            lightsControl = Action("Lights", () => { game.World.SetLights(!game.World.Data.Operations.Lights); Refresh(true); });
            alarmControl = Action("Alarm", () => { game.World.SetAlarm(!game.World.Data.Operations.Alarm); Refresh(true); });
            gateControl = Action("Vehicle gate", () => { game.World.SetGate(!game.World.Data.Operations.GateOpen); Refresh(true); });
            lockdownControl = Action("Lockdown", () => { game.World.SetLockdown(!game.World.Data.Operations.Lockdown); Refresh(true); });
            switches.Add(lightsControl); switches.Add(alarmControl); switches.Add(gateControl); switches.Add(lockdownControl);
            controlPane.Add(Text("Alarm dispatches officers, holds construction, closes the vehicle gate and starts lockdown. Clear each restriction deliberately after the response.", "muted"));
            controlPane.Add(Text("CCTV / LIVE SURVEILLANCE", "section-label"));
            var cameras = Element("wrap-row");
            cctvControl = Action("CCTV enabled", () => { game.World.SetCameras(!game.World.Data.Operations.CamerasEnabled); Refresh(true); }); cameras.Add(cctvControl);
            cameras.Add(Action("Place camera", () => { ShowBuildCatalog("Security"); ChooseFixture("CCTV"); }, "primary")); controlPane.Add(cameras);
            feed = new Image { scaleMode = ScaleMode.ScaleToFit }; feed.AddToClassList("cctv-feed"); controlPane.Add(feed);
            feedStatus = Text("", "body"); controlPane.Add(feedStatus);
            cctvRoster = Element("cctv-roster"); controlPane.Add(cctvRoster);
            controlPane.Add(Text("Aim with rotation before placing. A camera covers a 120° cone; walls block its view. Upgrades improve range and night vision. Power and local lighting affect availability. A technician maintains camera condition.", "muted"));
            controlPane.Add(Text("HIRING / SHIFTS", "section-label"));
            foreach (string role in new[] { "Officer", "Caseworker", "Technician" })
            {
                string selected = role;
                controlPane.Add(Action("Hire " + role.ToLowerInvariant() + " · $500", () => { game.HireStaff(selected); Refresh(true); }));
            }
            staffCost = Text("", "muted"); controlPane.Add(staffCost);
            controlPane.Add(Text("Officers patrol and perform welfare checks. Caseworkers relieve stress in nearby consultations. Technicians service CCTV. Duties and staffing effects require arrival, a reachable position and an active shift.", "body"));
            controlPane.Add(Text("ARRIVALS / CREW TRANSPORT", "section-label"));
            controlPane.Add(Action("Hire construction crew · $500", () => { game.HireConstructionCrew(); Refresh(true); }, "primary"));
            arrivalStatus = Text("", "body"); controlPane.Add(arrivalStatus);
            crewRoster = Element("crew-roster"); controlPane.Add(crewRoster);
            controlPane.Add(Text("Two builders per crew. Two crews can share a waiting pickup; staff share shuttles. Late hires get a new trip. Arrival, unloading and walking take game time. Seven marked bays are reserved per trip; staff shuttles leave after dropoff. A closed gate holds arrivals. View a crew to watch its current task.", "muted"));
            controlPane.Add(Text("SHIFT REPORTS / EVERY 8 HOURS", "section-label"));
            reportStatus = Text("", "body"); reportStatus.AddToClassList("shift-report-output"); controlPane.Add(reportStatus);
        }
        public void ShowFacility()
        { workspace = Workspace.Facility; inspectorOpen = true; cameraOpen = false; ((ScrollView)inspector).scrollOffset = Vector2.zero; Refresh(true); }
        public void ShowControl()
        {
            workspace = Workspace.Control; inspectorOpen = true; cameraOpen = false;
            ((ScrollView)inspector).scrollOffset = Vector2.zero;
            if (game.Tool != BuildTool.Inspect && game.Draft == null) game.SetTool(BuildTool.Inspect);
            Refresh(true);
        }
        void RefreshControl(World world)
        {
            controlPane.style.display = workspace == Workspace.Control ? DisplayStyle.Flex : DisplayStyle.None;
            OperationsState state = world.Data.Operations;
            controlStatus.text = world.Data.Staff.Count(world.IsOnShift) + " on duty / " + world.Data.Staff.Count(s => !s.Arrived) + " arriving\n" +
                world.Data.Crews.Count(c => c.Arrived) + " crews on site / " + world.Data.Crews.Count(c => !c.Arrived) + " inbound";
            lightsControl.text = "Lights · " + (state.Lights ? "on" : "off"); lightsControl.EnableInClassList("selected", state.Lights);
            alarmControl.text = state.Alarm ? "Silence alarm" : "Raise alarm"; alarmControl.EnableInClassList("danger", state.Alarm);
            gateControl.text = "Gate · " + (state.GateOpen ? "open" : "closed"); gateControl.EnableInClassList("selected", state.GateOpen);
            lockdownControl.text = state.Lockdown ? "Lift lockdown" : "Start lockdown"; lockdownControl.EnableInClassList("danger", state.Lockdown);
            cctvControl.text = "CCTV · " + (state.CamerasEnabled ? "enabled" : "disabled");
            string signature = string.Join("|", world.Data.Fixtures.Where(f => f.Kind == "CCTV").Select(f => f.Id));
            if (cameraWorld != world || signature != cameraSignature)
            {
                cameraWorld = world; cameraSignature = signature; cctvRoster.Clear(); cameraButtons.Clear();
                foreach (Fixture camera in world.Data.Fixtures.Where(f => f.Kind == "CCTV"))
                {
                    string id = camera.Id; Button button = Action("", () => game.WatchCamera(id), "person-roster-row");
                    cctvRoster.Add(button); cameraButtons.Add(id, button);
                }
            }
            int number = 0;
            foreach (Fixture camera in world.Data.Fixtures.Where(f => f.Kind == "CCTV"))
            {
                Button button = cameraButtons[camera.Id]; number++;
                button.text = "Camera " + number + " · " + (world.CameraOnline(camera) ? "online" : "offline") + " · " + Mathf.RoundToInt(camera.Condition) + "% condition";
                button.EnableInClassList("selected", game.WatchedCamera == camera);
            }
            feed.image = game.CameraFeed;
            feed.style.display = game.WatchedCamera != null ? DisplayStyle.Flex : DisplayStyle.None;
            feedStatus.text = world.Data.Fixtures.Any(f => f.Kind == "CCTV") ? world.CameraCoverage() + " / " + world.Data.Residents.Count(world.IsPresent) + " residents on site currently observed.\n" +
                (game.WatchedCamera == null ? "Select a camera to open its live feed." : world.CameraOnline(game.WatchedCamera) ? "LIVE · selected feed / 5 updates per second" : "OFFLINE · check network and condition") :
                "No operational cameras. Place one indoors or outdoors, approve its installation, and let a crew complete the work.";
            staffCost.text = "Additional staff wages: " + Money(world.Data.Staff.Sum(s => s.DailyCents + s.SecurityDailyCents)) + " / day. Day shift 08:00–20:00; night shift 20:00–08:00. Initial contract staff are included in operations.";
            var trips = world.Data.Trips.Where(t => t.Phase != "Gone").Reverse().Take(5).ToArray();
            arrivalStatus.text = trips.Length == 0 ? "No hired arrivals scheduled." : string.Join("\n", trips.Select(t => (t.Kind == "Crew" ? "PICKUP" : "SHUTTLE") + " · " + t.Phase + " · " + t.Passengers.Count + (t.Kind == "Crew" ? " crews" : " staff")));
            string crews = string.Join("|", world.Data.Crews.Select(c => c.Id));
            if (crewWorld != world || crews != crewSignature)
            {
                crewWorld = world; crewSignature = crews; crewRoster.Clear(); crewButtons.Clear();
                foreach (Crew crew in world.Data.Crews)
                {
                    string id = crew.Id;
                    Button watch = Action("", () => { Crew selected = game.World.Data.Crews.FirstOrDefault(c => c.Id == id); if (selected != null) {
                        TransportTrip trip = game.World.Data.Trips.FirstOrDefault(t => t.Id == selected.TripId);
                        if (selected.Arrived) game.FocusWork(selected.Position); else game.FocusPoint(trip != null ? trip.Position : selected.Position);
                    } }, "person-roster-row");
                    crewRoster.Add(watch); crewButtons.Add(id, watch);
                }
            }
            foreach (Crew crew in world.Data.Crews) crewButtons[crew.Id].text = "View " + crew.Name + " · " + crew.Activity;
            ShiftReport live = world.CurrentShift();
            reportStatus.text = "LIVE SHIFT · " + live.Summary + "\n" + live.Incidents + " incidents · " + live.CoveredResidents + " residents observed · " + live.ActiveProjects + " active projects\n\n" +
                (world.Data.ShiftReports.Count == 0 ? "First recorded report will be filed at the next 8-hour boundary." : string.Join("\n\n", world.Data.ShiftReports.AsEnumerable().Reverse().Take(3).Select(r =>
                    "DAY " + (r.Minute / 1440 + 1) + " / " + (r.Minute / 60 % 24).ToString("00") + ":00 · " + r.Summary + "\n" + r.StaffOnDuty + " on duty · " + r.Incidents + " incidents · " + r.CompletedProjects + " projects completed · cash " + Money(r.CashCents))));
        }
    }
}
