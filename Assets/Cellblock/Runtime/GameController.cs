using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public enum BuildTool { Inspect, Wall, Curve, Wing, Door, Bed, Table, Meals, MoveStart, MoveEnd, Bend, MoveFixture, Furnishing }
    public sealed partial class CompanySave { public string FileName, Title; }

    public sealed partial class GameController : MonoBehaviour
    {
        public World World { get; private set; }
        public Plan Draft { get; private set; }
        public Quote CurrentQuote { get; private set; }
        public Wall SelectedWall { get; private set; }
        public Fixture SelectedFixture { get; private set; }
        public Resident SelectedResident { get; private set; }
        public StaffMember SelectedStaff { get; private set; }
        public string PlacementFixtureKind { get; private set; } = "Bed";
        public float PlacementRotation { get; private set; }
        public bool Snapping = true;
        public BuildTool Tool { get; private set; }
        public string Instruction { get; private set; }
        FacilityView view;
        FacilitySanitationView sanitationView;
        MacTrackpad trackpad;
        Camera cctvCamera;
        RenderTexture cctvTexture;
        float nextFeed;
        string watchedCameraId = "";
        int pinchFingerA = -1, pinchFingerB = -1;
        bool pinchOwnedByUI;
        public string PrefabId { get; private set; } = "wing";
        public int PrefabPackage { get; private set; }
        public bool NativePinchAvailable { get { return trackpad != null && trackpad.Available; } }
        public Texture CameraFeed { get { return World != null && World.CameraOnline(WatchedCamera) ? cctvTexture : Texture2D.blackTexture; } }
        public Fixture WatchedCamera { get { return World == null ? null : World.Data.Fixtures.FirstOrDefault(f => f.Id == watchedCameraId && f.Kind == "CCTV"); } }
        GameHud hud;
        Camera cameraRig;
        bool nativeInputWasBlocked = true;
        ConstructionGuides guides;
        Vector3 focus = new Vector3(-5, 0, 0), displayedFocus;
        Vector2 displayedViewCenter = new Vector2(0.5f, 0.5f);
        float distance = 33, yaw = 36, pitch = 52, displayedDistance, displayedYaw, displayedPitch;
        float accumulator, autosave, previewTime;
        bool appPaused, unfocused, dragging, gesture, hasStart, hasEnd, pointerOwnedByUI, pointerOrbit, pointerActive;
        // Drag-to-move: pressing on the selected furnishing and dragging moves it instead of panning the camera.
        bool fixtureGrab, fixtureDragging, placingDrag;
        bool planView, presentationPreview;
        Point start, end;
        Vector2 pointerDown, lastPointer;
        public string PlacementSummary { get; private set; } = "";
        public string CameraModeLabel { get { return (planView ? "Plan" : "3D") + " · " + Mathf.RoundToInt(pitch) + "°"; } }
        public string HeightLabel { get { return view != null && view.FullHeight ? "Full walls" : "Cutaway"; } }
        string saveId = "";
        string SaveDirectory { get { return CompanyDirectory; } }
        string PointerPath { get { return Path.Combine(SaveDirectory, "last-company.txt"); } }
        public bool HasSave { get { return ContinueSave() != null; } }
        public IEnumerable<CompanySave> SavedCompanies()
        {
            return FindCompanySaves();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<GameController>() == null) new GameObject("Cellblock Holdings").AddComponent<GameController>();
        }
        void Awake()
        {
            InitializePreferences();
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            cameraRig = new GameObject("Management camera").AddComponent<Camera>();
            cameraRig.transform.SetParent(transform); cameraRig.tag = "MainCamera"; cameraRig.orthographic = false;
            cameraRig.fieldOfView = 42; cameraRig.nearClipPlane = 0.15f; cameraRig.farClipPlane = 350;
            cameraRig.cullingMask = ~(1 << 30);
            if (FindFirstObjectByType<AudioListener>() == null) cameraRig.gameObject.AddComponent<AudioListener>();
            trackpad = gameObject.AddComponent<MacTrackpad>();
            cameraRig.allowHDR = true; cameraRig.allowMSAA = true;
            cameraRig.clearFlags = CameraClearFlags.SolidColor; cameraRig.backgroundColor = new Color(0.66f, 0.73f, 0.77f);
            PresentationLighting.Configure(cameraRig, transform);
            view = new GameObject("Facility presentation").AddComponent<FacilityView>(); view.transform.SetParent(transform); view.Initialize();
            pressureView = new GameObject("Population pressure presentation").AddComponent<FacilityPressureView>(); pressureView.transform.SetParent(transform); pressureView.Initialize();
            sanitationView = new GameObject("Sanitation presentation").AddComponent<FacilitySanitationView>(); sanitationView.transform.SetParent(transform); sanitationView.Initialize();
            guides = new GameObject("Construction measurements").AddComponent<ConstructionGuides>(); guides.transform.SetParent(transform); guides.Initialize(cameraRig);
            hud = new GameObject("Management interface").AddComponent<GameHud>(); hud.transform.SetParent(transform); hud.Initialize(this); ApplyPreferences();
            view.Refresh(Cellblock.Core.World.Start(Cellblock.Core.World.Offers[0].Id), true);
            SetTool(BuildTool.Inspect); CameraPosition(true); hud.ShowMenu();
        }
        public bool StartCompany(string contract) { return CreateCompany(contract, false); }
        bool CreateCompany(string contract, bool preview, string modeType=null, string modeLocation=null)
        {
            if (World != null && !Save()) return false;
            presentationPreview = preview;
            EndCharacterView(false); if (hud != null) hud.CloseCharacterPhone(); selectedCrewCharacter = ""; EndRideAlongSilently(); ResetDoorEditing(); PlacingConnection=false;
            World = modeType==null?Cellblock.Core.World.Start(contract):Cellblock.Core.World.StartMode(modeType,modeLocation); saveId = Guid.NewGuid().ToString("N");

            World.Data.Speed = 0; watchedCameraId = ""; nextFeed = 0; nativeInputWasBlocked = true;
            Draft = null; CurrentQuote=null; SelectedWall = null; SelectedFixture = null; SelectedResident = null; SelectedStaff = null; hasStart = hasEnd = false;
            ResetIncidentCamera(); accumulator = 0; savedJson = ""; RecoveredSave = false; SaveProblem = ""; LastSavedUtc = default(DateTime); autosave = 0; view.Refresh(World, true); view.Preview(null); SetTool(BuildTool.Inspect); Recenter(); Save();
            return true;
        }
        public void SetTool(BuildTool tool)
        {
            if (Draft != null) { Message("Approve or discard the current plan before starting another."); return; }
            if ((tool == BuildTool.MoveStart || tool == BuildTool.MoveEnd || tool == BuildTool.Bend) && SelectedWall == null)
            { Message("Select a wall first."); return; }
            if (tool == BuildTool.MoveFixture && SelectedFixture == null) { Message("Select a furnishing first."); return; }
            Tool = tool; hasStart = hasEnd = false;
            if (tool != BuildTool.Inspect) { SelectedResident = null; SelectedStaff = null; selectedCrewCharacter = ""; }
            if (tool == BuildTool.Wall || tool == BuildTool.Curve || tool == BuildTool.Wing || tool == BuildTool.Door || tool == BuildTool.Bed || tool == BuildTool.Table || tool == BuildTool.Furnishing || tool == BuildTool.Meals)
            { SelectedWall = null; SelectedFixture = null; }
            if (tool == BuildTool.Bed) PlacementFixtureKind = "Bed";
            if (tool == BuildTool.Table) PlacementFixtureKind = "Table";
            switch (tool)
            {
                case BuildTool.Wall: Instruction = "WALL · Tap a start point, then an end point. Review before construction."; break;
                case BuildTool.Curve: Instruction = "CURVE · Tap the start, end, then bend position."; break;
                case BuildTool.Wing: Instruction = "PREFAB WING · Tap its lower-left connection point. Every wall remains editable after construction."; break;
                case BuildTool.Door: Instruction = "DOOR · Tap an existing wall where the opening should go."; break;
                case BuildTool.Bed: case BuildTool.Table: Instruction = tool.ToString().ToUpperInvariant() + " · Tap a location, then review the fit-out project."; break;
                case BuildTool.Furnishing: Instruction = FixtureCatalog.Get(PlacementFixtureKind).Name.ToUpperInvariant() + " · Tap a position, or drag and let go. Review the fit, cost, and access before approving."; break;
                case BuildTool.Meals: Instruction = "MEAL SERVICE · Tap an installed table to move meal service and compare travel times."; break;
                case BuildTool.MoveStart: case BuildTool.MoveEnd: Instruction = "WALL EDIT · Tap the new endpoint position."; break;
                case BuildTool.Bend: Instruction = "WALL EDIT · Tap the bend position. Straight walls can become curves."; break;
                case BuildTool.MoveFixture: Instruction = "MOVE FURNISHING · Tap its new position."; break;
                default: Instruction = "Drag to pan · Drag a selected item to move it · Right-drag to orbit · Scroll to zoom · Tap a component to edit"; break;
            }
            PlacementSummary = "";
            if (guides != null) { if (SelectedFixture != null) guides.ShowFixture(SelectedFixture); else guides.Show(SelectedWall); }
            if (view != null) view.Preview(null, SelectedWall, SelectedFixture);
            if (view != null) view.SelectResident(SelectedCharacterId == "" ? null : SelectedCharacterId);
            if (hud != null) hud.Refresh(true);
        }
        public void SetFixtureTool(string kind)
        {
            if (Draft != null) { Message("Approve or discard the current plan first."); return; }
            if (!FixtureCatalog.Contains(kind)) { Message("This furnishing is not in the catalog."); return; }
            PlacementFixtureKind = kind; SetTool(BuildTool.Furnishing);
        }
        public void RotatePlacement()
        {
            if (Draft != null) return;
            PlacementRotation = (PlacementRotation + 90) % 360; previewTime = 0;
            Message("Placement rotated to " + PlacementRotation.ToString("0") + "°. Check the access side before approving."); hud.Refresh(true);
        }
        public void InspectResident(string id)
        {
            if (World == null || Draft != null) return;
            selectedCrewCharacter = ""; SelectedResident = World.Data.Residents.FirstOrDefault(r => r.Id == id); SelectedStaff = null;
            SelectedWall = null; SelectedFixture = null; Tool = BuildTool.Inspect; guides.Show(null);
            if (SelectedResident != null) { focus = FacilityView.V(SelectedResident.Position); distance = Mathf.Min(distance, 24); }
            view.Preview(null); view.SelectResident(SelectedResident == null ? null : SelectedResident.Id); hud.RevealSelection(); hud.Refresh(true);
        }
        public void InspectStaff(string id)
        {
            if (World == null || Draft != null) return;
            selectedCrewCharacter = ""; SelectedStaff = World.Data.Staff.FirstOrDefault(s => s.Id == id); SelectedResident = null;
            SelectedWall = null; SelectedFixture = null; Tool = BuildTool.Inspect; guides.Show(null);
            if (SelectedStaff != null) { focus = FacilityView.V(SelectedStaff.Position); distance = Mathf.Min(distance, 24); }
            view.Preview(null); view.SelectResident(SelectedStaff == null ? null : SelectedStaff.Id); hud.RevealSelection(); hud.Refresh(true);
        }
        public void Message(string message) { if (World != null) World.Notice = message; }
        void Update()
        {
            // Always drain AppKit events, including contracts/help and focus transitions.
            float magnification = trackpad != null ? trackpad.Consume() : 0;
            bool blocked = World == null || hud == null || hud.BlocksWorldInput || appPaused || unfocused;
            if (blocked || nativeInputWasBlocked) magnification = 0;
            nativeInputWasBlocked = blocked;
            UpdateControlDevice();
            if (World == null) return;
            UpdateCharacterSession();
            if (!appPaused && (!unfocused || presentationPreview) && !hud.BlocksSimulation && World.Data.Speed > 0)
            {
                accumulator += Mathf.Min(SimulationFrameDelta, 0.1f) * World.Data.Speed;
                int steps = 0;
                while (accumulator >= 0.2f && steps++ < 8) { World.AdvanceMinutes(1); accumulator -= 0.2f; }
            }
            else accumulator = 0;
            if (SelectedFixture != null) SelectedFixture = World.Data.Fixtures.FirstOrDefault(f => f.Id == SelectedFixture.Id);
#if UNITY_EDITOR
            view.PreviewFixedFrameDelta = presentationPreview && Time.captureDeltaTime > 0 ? Time.captureDeltaTime : 0;
#endif
            if (!CharacterInputUpdate(magnification)) InputUpdate(magnification); view.ReducedMotion = Preferences.ReducedMotion; view.Refresh(World); view.RefreshCharacterPhones(World, hud.CharacterPhoneOpen ? CharacterPhoneSubjectId : ""); view.SetWatchedCamera(WatchedCamera); pressureView.Refresh(World, Time.unscaledDeltaTime); sanitationView.Refresh(World, Time.unscaledDeltaTime); UpdateRideAlong(); if (!CharacterViewActive) UpdateIncidentCamera(); hud.Refresh();
            TickAutosave();
        }
        void LateUpdate() { if (cameraRig != null) { if (CharacterViewActive) CharacterCamera(); else if (RideAlongActive) RideAlongCamera(); else CameraPosition(); } RefreshCameraFeed(); RefreshBodyCam(); }
        void CameraPosition(bool immediate = false)
        {
            // Following an incident, the camera eases more gently so a chase does not jerk it around.
            float blend = immediate || !Preferences.SmoothCamera || Preferences.ReducedMotion ? 1 : 1 - Mathf.Exp(-Time.unscaledDeltaTime * (followIncident ? 3.5f : 14));
            displayedFocus = Vector3.Lerp(displayedFocus, focus, blend);
            displayedDistance = Mathf.Lerp(displayedDistance, distance, blend);
            displayedYaw = Mathf.LerpAngle(displayedYaw, yaw, blend); displayedPitch = Mathf.Lerp(displayedPitch, pitch, blend);
            cameraRig.orthographic = planView; cameraRig.orthographicSize = displayedDistance * 0.48f;
            Quaternion rotation = Quaternion.Euler(displayedPitch, displayedYaw, 0);
            Vector2 viewCenter = hud == null || hud.BlocksWorldInput ? new Vector2(0.5f, 0.5f) : hud.WorldViewCenter;
            displayedViewCenter = Vector2.Lerp(displayedViewCenter, viewCenter, blend);
            float visibleHeight = planView ? cameraRig.orthographicSize * 2 : displayedDistance * 3.1f * Mathf.Tan(cameraRig.fieldOfView * Mathf.Deg2Rad * 0.5f);
            Vector3 composition = rotation * new Vector3((0.5f - displayedViewCenter.x) * visibleHeight * cameraRig.aspect,
                (0.5f - displayedViewCenter.y) * visibleHeight, 0);
            cameraRig.transform.position = displayedFocus + rotation * new Vector3(0, 0, -displayedDistance * 1.55f) + composition;
            cameraRig.transform.rotation = rotation;
            // A short rotational kick when a blow lands in view (FacilityEscapeView): trauma squared on smooth noise. Rotation
            // only, so the camera never shakes into a wall; never with reduced motion.
            float kick = view == null || Preferences.ReducedMotion ? 0 : view.CameraImpulse;
            if (kick > 0.001f && Vector3.Distance(view.CameraImpulsePoint, displayedFocus) < displayedDistance * 0.9f)
            {
                float trauma = kick * kick, time = Time.unscaledTime * 14;
                cameraRig.transform.rotation = rotation * Quaternion.Euler(1.0f * trauma * (Mathf.PerlinNoise(31.7f, time) * 2 - 1),
                    1.2f * trauma * (Mathf.PerlinNoise(7.3f, time) * 2 - 1), 1.6f * trauma * (Mathf.PerlinNoise(91.1f, time) * 2 - 1));
            }
        }
        // A chase seen side-on: the camera looks across the line of the run (or of the hold) and drifts toward that slowly,
        // instead of swinging with the bearing between the pursuer and the fugitive.
        float ChaseYaw(Point from, Point toward, float current, bool snap)
        {
            Point d = toward - from;
            if (d.Length < 0.05f) return current;
            float heading = Mathf.Atan2(d.X, d.Y) * Mathf.Rad2Deg;
            float a = heading + 90, b = heading - 90;
            float desired = Mathf.Abs(Mathf.DeltaAngle(current, a)) <= Mathf.Abs(Mathf.DeltaAngle(current, b)) ? a : b;
            return snap ? desired : Mathf.MoveTowardsAngle(current, desired, Time.unscaledDeltaTime * 18);
        }
        public void Recenter()
        {
            yaw = 36; pitch = 52; planView = false;
            focus = new Vector3(-5, 0, 0); distance = 33;
            if (World != null && World.Data.Walls.Count > 0)
            {
                var bounds = new Bounds(FacilityView.V(World.Data.Walls[0].A), Vector3.zero);
                foreach (Wall wall in World.Data.Walls) foreach (Point point in Geometry.Sample(wall)) bounds.Encapsulate(FacilityView.V(point));
                focus = bounds.center; focus.y = 0;
                distance = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.9f + 17, 26, 65);
            }
        }
        public void ApplyPinchScale(float scale, Vector2 center)
        { if (scale > 0 && !float.IsNaN(scale) && !float.IsInfinity(scale)) ZoomAt(Mathf.Log(Mathf.Clamp(scale, 0.2f, 5)) / 0.12f, center); }
        public void SelectPrefab(string id, int package)
        { if (Draft != null) { Message("Approve or discard the current plan first."); return; } PrefabId = id; PrefabPackage = package; SetTool(BuildTool.Wing); }
        public void SetPrefabPackage(int package)
        { if (Draft != null) return; PrefabPackage = Mathf.Clamp(package, 0, 2); previewTime = 0; hud.Refresh(true); }
        public void HireConstructionCrew()
        { if (World != null && World.HireCrew()) FrameArrivals(); }
        public void HireStaff(string role)
        { if (World != null && World.HireStaff(role)) FrameArrivals(); }
        void FrameArrivals() { focus = new Vector3(-8, 0, -15); distance = 35; pitch = 57; yaw = 20; }
        public void FocusPoint(Point point, float zoomDistance = 24) { focus = FacilityView.V(point); distance = Mathf.Clamp(zoomDistance, 10, 110); }
        public void FocusWork(Point point) { FocusPoint(point, 11); pitch = 38; yaw = 145; planView = false; }
        public void WatchCamera(string id)
        { watchedCameraId = id; if (WatchedCamera != null) { focus = FacilityView.V(WatchedCamera.Position); distance = 19; } hud.Refresh(true); }
        void RefreshCameraFeed()
        {
            Fixture fixture = WatchedCamera;
            if (World == null || fixture == null || !World.CameraOnline(fixture) || !hud.ControlIsVisible || Time.unscaledTime < nextFeed) return;
            nextFeed = Time.unscaledTime + 0.2f;
            if (cctvCamera == null)
            {
                cctvCamera = new GameObject("Selected CCTV feed · one camera").AddComponent<Camera>(); cctvCamera.transform.SetParent(transform);
                cctvCamera.cullingMask = ~(1 << 29); cctvCamera.enabled = false; cctvCamera.fieldOfView = 88.5f; cctvCamera.nearClipPlane = 0.12f; cctvCamera.farClipPlane = 60;
                cctvCamera.clearFlags = CameraClearFlags.SolidColor; cctvCamera.backgroundColor = Color.black;
                cctvTexture = new RenderTexture(384, 216, 16) { name = "Selected CCTV monitor" }; cctvTexture.Create();
            }
            cctvCamera.transform.position = FacilityView.V(fixture.Position, 2.62f) + Quaternion.Euler(0, fixture.Rotation, 0) * new Vector3(0, 0, 0.26f);
            cctvCamera.transform.rotation = Quaternion.Euler(12, fixture.Rotation, 0);
            cctvCamera.farClipPlane = World.CameraRangeFor(WatchedCamera) + 1;
            var request = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = cctvTexture };
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cctvCamera, request);
        }
        void OnDestroy() { EndCharacterView(false); DisposeDoorPreview();if (cctvTexture != null) { cctvTexture.Release(); Destroy(cctvTexture); } ReleaseBodyCam(); }
        public void Orbit(float horizontalDegrees, float verticalDegrees)
        { yaw += horizontalDegrees; pitch = Mathf.Clamp(pitch + verticalDegrees, 28, 78); }
        public void Zoom(float amount) { distance = Mathf.Clamp(distance * Mathf.Exp(-amount * 0.12f), 11, 85); }
        void ZoomAt(float amount, Vector2 screen)
        {
            float previous = distance; distance = Mathf.Clamp(distance * Mathf.Exp(-amount * 0.12f), 11, 85);
            Point anchor;
            if (Ground(screen, out anchor)) focus += (FacilityView.V(anchor) - displayedFocus) * (1 - distance / previous);
            ClampFocus();
        }
        public void PanCamera(Vector2 axis)
        {
            Vector3 right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            Vector3 forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            focus += (right * axis.x + forward * axis.y); ClampFocus();
        }
        public void SetViewPreset(bool overhead)
        { planView = overhead; pitch = overhead ? 78 : 52; if (overhead) yaw = 0; }
        public void ToggleHeight()
        {
            view.FullHeight = !view.FullHeight; if (World != null) view.Refresh(World, true);
            guides.Height = view.WallHeight + 0.35f;
            if (SelectedWall != null) guides.Show(SelectedWall);
            view.Preview(Draft, SelectedWall, SelectedFixture);
        }
        bool Ground(Vector2 screen, out Point point)
        {
            float enter; Ray ray = cameraRig.ScreenPointToRay(screen);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out enter))
            {
                Vector3 hit = ray.GetPoint(enter); point = new Point(hit.x, hit.z); return true;
            }
            point = default(Point); return false;
        }
        Point Snap(Point point) { return FurnishingTool ? FitFurniture(point) : Geometry.Snap(point, World.Data.Walls, Snapping); }
        bool FurnishingTool { get { return Tool == BuildTool.Bed || Tool == BuildTool.Table || Tool == BuildTool.Furnishing || (Tool == BuildTool.MoveFixture && SelectedFixture != null); } }
        // Furniture snaps to the half-metre grid, then settles at the nearest spot that fits: flush against a wall, never in it.
        Point FitFurniture(Point raw)
        {
            Point p = Snapping ? new Point(Mathf.Round(raw.X * 2) / 2, Mathf.Round(raw.Y * 2) / 2) : raw;
            bool moving = Tool == BuildTool.MoveFixture && SelectedFixture != null;
            string kind = moving ? SelectedFixture.Kind : PlacementFixtureKind;
            float rotation = moving ? SelectedFixture.Rotation : PlacementRotation;
            return World.FitFurnishing(kind, rotation, p, moving ? SelectedFixture.Id : null);
        }
        void Pan(Vector2 previous, Vector2 current)
        {
            Point a, b;
            if (Ground(previous, out a) && Ground(current, out b)) focus += FacilityView.V(a - b);
            ClampFocus();
        }
        void ClampFocus() { if (RideAlongActive) return; focus.x = Mathf.Clamp(focus.x, -35, 35); focus.z = Mathf.Clamp(focus.z, -30, 30); focus.y = 0; }
        void InputUpdate(float magnification)
        {
            if (hud.BlocksWorldInput)
            {
                pointerActive = false; pointerOwnedByUI = true; dragging = false; gesture = Input.touchCount > 0; placingDrag = false; hud.ClearCatalogPress();
                if (Input.GetKeyDown(KeyCode.Escape)) hud.NavigateBack();
                return;
            }
            if (Input.touchCount > 0)
            {
                if (Input.touchCount >= 2)
                {
                    placingDrag = false;
                    Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
                    if (pinchFingerA != a.fingerId || pinchFingerB != b.fingerId)
                    {
                        pinchFingerA = a.fingerId; pinchFingerB = b.fingerId;
                        pinchOwnedByUI = (pointerActive && pointerOwnedByUI) || hud.IsOverUI(a.position) || hud.IsOverUI(b.position);
                        gesture = true; pointerActive = false; return;
                    }
                    if ((a.phase == TouchPhase.Began && hud.IsOverUI(a.position)) || (b.phase == TouchPhase.Began && hud.IsOverUI(b.position))) pointerOwnedByUI = true;
                    gesture = true;
                    if (pinchOwnedByUI) return;
                    Vector2 center = (a.position + b.position) / 2;
                    Vector2 previous = (a.position - a.deltaPosition + b.position - b.deltaPosition) / 2;
                    Pan(previous, center);
                    float before = Vector2.Distance(a.position - a.deltaPosition, b.position - b.deltaPosition);
                    float now = Vector2.Distance(a.position, b.position);
                    if (before > 5 && now > 5) ApplyPinchScale(now / before, center);
                    Vector2 beforeDirection = (b.position - b.deltaPosition) - (a.position - a.deltaPosition);
                    Vector2 direction = b.position - a.position;
                    if (beforeDirection.sqrMagnitude > 25 && direction.sqrMagnitude > 25)
                        Orbit(-Vector2.SignedAngle(beforeDirection, direction), 0);
                    return;
                }
                pinchFingerA = pinchFingerB = -1;
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    pointerDown = lastPointer = touch.position; dragging = false; pointerActive = true;
                    pointerOwnedByUI = hud.IsOverUI(touch.position);
                    if (pointerOwnedByUI) gesture = true;
                    fixtureGrab = !pointerOwnedByUI && GrabbingSelectedFixture(touch.position); fixtureDragging = false; placingDrag = false;
                }
                float slop = Mathf.Max(10, Screen.dpi > 0 ? Screen.dpi * 0.04f : 15);
                // A catalog card pressed and dragged off the panel becomes that furnishing, following the finger.
                if (touch.phase == TouchPhase.Moved && pointerActive && pointerOwnedByUI && Vector2.Distance(pointerDown, touch.position) > slop && !hud.IsOverUI(touch.position) && BeginCardDrag())
                { gesture = false; dragging = true; }
                if (touch.phase == TouchPhase.Moved && !gesture)
                {
                    if (Vector2.Distance(pointerDown, touch.position) > slop) dragging = true;
                    if (dragging && fixtureGrab && !fixtureDragging) BeginFixtureDrag();
                    if (dragging && !fixtureDragging && !pointerOwnedByUI && PlacementTool && Draft == null) placingDrag = true;
                    if (dragging && (Tool == BuildTool.Inspect || Draft!=null)) Pan(lastPointer, touch.position);
                    lastPointer = touch.position;
                    if (!hud.IsOverUI(touch.position)) PreviewAt(placingDrag ? Lifted(touch.position) : touch.position);
                }
                if (touch.phase == TouchPhase.Ended)
                {
                    if (fixtureDragging) EndFixtureDrag(touch.position);
                    else if (placingDrag && !gesture) EndPlacementDrag(Lifted(touch.position), touch.position);
                    else if (pointerActive && !pointerOwnedByUI && !gesture && !dragging && !hud.IsOverUI(touch.position)) Tap(touch.position);
                    fixtureGrab = false; placingDrag = false; hud.ClearCatalogPress();
                    gesture = false; pointerActive = false; pointerOwnedByUI = false;
                }
                if (touch.phase == TouchPhase.Canceled) { gesture = false; pointerActive = false; pointerOwnedByUI = false; dragging = false; if (fixtureDragging) { fixtureDragging = false; SetTool(BuildTool.Inspect); } fixtureGrab = false; placingDrag = false; hud.ClearCatalogPress(); }
                return;
            }
            if (gesture) { gesture = false; pointerActive = false; pointerOwnedByUI = false; }
            pinchFingerA = pinchFingerB = -1;
            Vector2 cursor = Input.mousePosition;
            if (Mathf.Abs(magnification) > 0.00001f && !hud.IsOverUI(cursor))
            { ApplyPinchScale(1 + magnification, cursor); pointerActive = false; }
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                pointerDown = lastPointer = cursor; dragging = false; pointerActive = true; pointerOwnedByUI = hud.IsOverUI(cursor);
                pointerOrbit = Input.GetMouseButton(1) || (Input.GetMouseButton(0) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)));
                fixtureGrab = Input.GetMouseButtonDown(0) && !pointerOrbit && !pointerOwnedByUI && GrabbingSelectedFixture(cursor); fixtureDragging = false; placingDrag = false;
            }
            bool holding = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
            if (pointerActive && holding && Vector2.Distance(pointerDown, cursor) > 6) dragging = true;
            if (dragging && fixtureGrab && !fixtureDragging && Input.GetMouseButton(0)) BeginFixtureDrag();
            if (dragging && pointerOwnedByUI && Input.GetMouseButton(0) && !hud.IsOverUI(cursor)) BeginCardDrag();
            if (dragging && !fixtureDragging && !pointerOwnedByUI && !pointerOrbit && Input.GetMouseButton(0) && PlacementTool && Draft == null) placingDrag = true;
            if ((fixtureDragging || placingDrag) && holding) PreviewAt(cursor);
            if (pointerActive && holding && dragging && !pointerOwnedByUI)
            {
                Vector2 delta = cursor - lastPointer;
                if (pointerOrbit) Orbit(delta.x * 0.22f * Preferences.CameraSensitivity, -delta.y * 0.16f * Preferences.CameraSensitivity);
                else if (Input.GetMouseButton(2) || (Input.GetMouseButton(0) && (Tool == BuildTool.Inspect || Draft!=null))) Pan(lastPointer, cursor);
            }
            if (Input.GetMouseButtonUp(0) && fixtureDragging) EndFixtureDrag(cursor);
            else if (Input.GetMouseButtonUp(0) && placingDrag) EndPlacementDrag(cursor, cursor);
            else if (Input.GetMouseButtonUp(0) && pointerActive && !pointerOwnedByUI && !pointerOrbit && !dragging && !hud.IsOverUI(cursor)) Tap(cursor);
            if (Input.GetMouseButtonUp(0)) { fixtureGrab = false; placingDrag = false; hud.ClearCatalogPress(); }
            if (!holding) pointerActive = false;
            if (!hud.IsOverUI(cursor))
            {
                if (Mathf.Abs(magnification) < 0.00001f && Mathf.Abs(Input.mouseScrollDelta.y) > 0.001f) ZoomAt(Input.mouseScrollDelta.y, cursor);
                if (!holding) PreviewAt(cursor);
            }
            lastPointer = cursor;
            if (Input.GetKeyDown(KeyCode.Escape)) { if (RideAlongActive) StopRideAlong(); else Cancel(); }
            if (Input.GetKeyDown(KeyCode.Space)) World.Data.Speed = World.Data.Speed == 0 ? 1 : 0;
            if (RideAlongActive && Input.GetKeyDown(KeyCode.Tab)) NextRideAlong(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1);
            if (Input.GetKeyDown(KeyCode.Home)) Recenter();
            if(Input.GetKeyDown(KeyCode.Z)&&(Input.GetKey(KeyCode.LeftCommand)||Input.GetKey(KeyCode.LeftControl))&&Draft!=null)UndoDoor();
            if (Input.GetKeyDown(KeyCode.Z) && Draft == null && (Tool == BuildTool.Furnishing || Tool == BuildTool.Wing)) RotatePlacement();
            Vector2 movement = new Vector2((Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) ? 1 : 0),
                (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) ? 1 : 0));
            float speed = Preferences.CameraSensitivity * distance * 0.55f * Time.unscaledDeltaTime * (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 2 : 1);
            if (movement.sqrMagnitude > 0) PanCamera(movement.normalized * speed);
            float orbit = (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0);
            float tilt = (Input.GetKey(KeyCode.R) ? 1 : 0) - (Input.GetKey(KeyCode.F) ? 1 : 0);
            Orbit(orbit * Time.unscaledDeltaTime * 55 * Preferences.CameraSensitivity, tilt * Time.unscaledDeltaTime * 35 * Preferences.CameraSensitivity);
        }
        // Pressing on the furnishing that is already selected, in Inspect with no open plan.
        bool GrabbingSelectedFixture(Vector2 screen)
        {
            if (Tool != BuildTool.Inspect || Draft != null || SelectedFixture == null || World == null || RideAlongActive) return false;
            FixtureDefinition definition = FixtureCatalog.Get(SelectedFixture.Kind); if (definition == null) return false;
            Vector3 size = new Vector3(definition.Width, definition.Height, definition.Depth); float hit;
            return HitBox(cameraRig.ScreenPointToRay(screen), FacilityView.V(SelectedFixture.Position, size.y * 0.5f + 0.13f), Quaternion.Euler(0, SelectedFixture.Rotation, 0), size, out hit);
        }
        void BeginFixtureDrag()
        {
            fixtureDragging = true; SetTool(BuildTool.MoveFixture);
            if (Tool != BuildTool.MoveFixture) { fixtureDragging = false; return; }
            Instruction = "MOVE FURNISHING · Drag to the new position and let go to review the move.";
            previewTime = 0; hud.Refresh(true);
        }
        // Letting go over the floor proposes the move with its quote; letting go over the interface cancels it.
        // New placements: a drag shows the item where it would go and letting go asks for the quote. On touch the item
        // sits a little above the finger, so the finger never hides what it places; a plain tap still places exactly.
        bool PlacementTool { get { return Tool == BuildTool.Bed || Tool == BuildTool.Table || Tool == BuildTool.Furnishing || Tool == BuildTool.Wing; } }
        static Vector2 Lifted(Vector2 screen) { return screen + new Vector2(0, Screen.dpi > 0 ? Screen.dpi * 0.42f : 64); }
        bool BeginCardDrag()
        {
            string kind = hud.PressedCatalogKind;
            if (kind == null || Draft != null || RideAlongActive) return false;
            hud.ClearCatalogPress(); hud.PlaceFromCatalog(kind);
            if (Tool != BuildTool.Furnishing || PlacementFixtureKind != kind) return false;
            pointerOwnedByUI = false; placingDrag = true; previewTime = 0;
            Instruction = FixtureCatalog.Get(kind).Name.ToUpperInvariant() + " · Drag it into place and let go to review the quote. Let go over the panel to cancel.";
            hud.Refresh(true); return true;
        }
        void EndPlacementDrag(Vector2 placed, Vector2 pointer)
        {
            placingDrag = false;
            if (hud.IsOverUI(pointer)) { view.Preview(null, SelectedWall, SelectedFixture); guides.Show(null); PlacementSummary = "Placement cancelled"; hud.Refresh(true); return; }
            Tap(placed);
        }
        void EndFixtureDrag(Vector2 screen)
        {
            fixtureDragging = false; fixtureGrab = false;
            if (hud.IsOverUI(screen)) { SetTool(BuildTool.Inspect); return; }
            Tap(screen);
            if (Draft == null && Tool == BuildTool.MoveFixture) SetTool(BuildTool.Inspect);
        }
        void PreviewAt(Vector2 screen)
        {
            if(Time.unscaledTime<previewTime)return;
            if(DoorClickAvailable){previewTime=Time.unscaledTime+.15f;PreviewDoor(screen);return;}
            HideDoorPreview();
            if(Draft!=null)return;
            previewTime = Time.unscaledTime + 0.08f;
            Point p; if (!Ground(screen, out p)) return; p = Snap(p);
            Plan preview = null;
            if (Tool == BuildTool.Wing) preview = PrefabCatalog.Create(PrefabId, PrefabPackage, p, PlacementRotation);
            if ((Tool == BuildTool.Wall || Tool == BuildTool.Curve) && hasStart)
            {
                var wall = new Wall { A = start, B = hasEnd ? end : p, Curved = Tool == BuildTool.Curve && hasEnd, Control = p };
                if (Geometry.Distance(wall.A, wall.B) > 0.1f) { preview = new Plan(); preview.Walls.Add(wall); }
            }
            if (Tool == BuildTool.Bed || Tool == BuildTool.Table || Tool == BuildTool.Furnishing)
            {
                preview = new Plan(); preview.Fixtures.Add(new Fixture { Kind = PlacementFixtureKind, Position = p, Rotation = PlacementRotation });
            }
            if (SelectedWall != null && (Tool == BuildTool.MoveStart || Tool == BuildTool.MoveEnd || Tool == BuildTool.Bend))
            {
                Wall changed = SelectedWall.Copy();
                if (Tool == BuildTool.MoveStart) changed.A = p;
                if (Tool == BuildTool.MoveEnd) changed.B = p;
                if (Tool == BuildTool.Bend) { changed.Curved = true; changed.Control = p; }
                preview = new Plan(); preview.Walls.Add(changed);
            }
            if (Tool == BuildTool.MoveFixture && SelectedFixture != null)
            {
                Fixture changed = SelectedFixture.Copy(); changed.Position = p; preview = new Plan(); preview.Fixtures.Add(changed);
            }
            Wall measured = preview != null && preview.Walls.Count > 0 ? preview.Walls[0] : null;
            if (measured != null)
            {
                float angle = Mathf.Atan2(measured.B.Y - measured.A.Y, measured.B.X - measured.A.X) * Mathf.Rad2Deg;
                PlacementSummary = Geometry.Length(measured).ToString("0.0") + " m" + (measured.Curved ? " · curved" : " · " + Mathf.RoundToInt(Mathf.Repeat(angle, 180)) + "°") + (Snapping ? " · snap on" : " · free placement");
                guides.Show(measured, Geometry.Validate(measured) != "");
            }
            else if (preview != null && preview.Fixtures.Count > 0)
            {
                PlacementSummary = FixtureCatalog.Get(preview.Fixtures[0].Kind).Name + " · " + (Snapping ? "0.5 m snap" : "free placement") + (placingDrag || fixtureDragging ? " · let go to review" : " · tap or drag to place");
                guides.ShowFixture(preview.Fixtures[0]);
            }
            else if (Tool != BuildTool.Inspect) { PlacementSummary = hasStart ? "Choose the next point" : "Tap to begin · drag with two fingers to move"; guides.Show(null); }
            if (Tool != BuildTool.Inspect) view.Preview(preview, SelectedWall, SelectedFixture);
        }
        public bool PlacingConnection { get; private set; }
        public void BeginDraftConnection()
        {
            if (Draft == null) return;
            PlacingConnection = true; Instruction = "Click any wall where you want a door. Planned and existing walls both work; the quote stays open.";
            hud.Refresh(true);
        }
        public void UpgradeSelected()
        {
            if (SelectedFixture != null && Draft == null) SetDraft(World.PlanSecurityUpgrade(SelectedFixture.Id));
        }
        void Tap(Vector2 screen)
        {
            if(DoorClickAvailable){PlaceDoor(screen);return;}
            if (Draft != null) { Message("Approve or discard the current plan first."); return; }
            Point p; if (!Ground(screen, out p)) return;
            Point raw = p; p = Snap(p);
            if (Tool == BuildTool.Inspect)
            {
                SelectComponent(screen, raw);
                if (SelectedWall != null) { guides.Show(SelectedWall); PlacementSummary = Geometry.Length(SelectedWall).ToString("0.0") + " m · " + (SelectedWall.Curved ? "curved wall" : "straight wall"); }
                else if (SelectedFixture != null) { guides.ShowFixture(SelectedFixture); PlacementSummary = "Selected " + SelectedFixture.Kind.ToLowerInvariant(); }
                else if (SelectedResident != null) { guides.Show(null); PlacementSummary = SelectedResident.Name + " · " + SelectedResident.Temperament; }
                else if (SelectedStaff != null) { guides.Show(null); PlacementSummary = SelectedStaff.Name + " · " + SelectedStaff.Role; }
                else { guides.Show(null); PlacementSummary = ""; }
                view.Preview(null, SelectedWall, SelectedFixture);
                view.SelectResident(SelectedCharacterId == "" ? null : SelectedCharacterId);
                hud.RevealSelection(); hud.Refresh(true); return;
            }
            if (Tool == BuildTool.Wall || Tool == BuildTool.Curve)
            {
                if (!hasStart) { start = p; hasStart = true; Instruction = "Tap the wall endpoint."; return; }
                if (Tool == BuildTool.Curve && !hasEnd) { end = p; hasEnd = true; Instruction = "Tap the bend position."; return; }
                var plan = new Plan { Name = Tool == BuildTool.Curve ? "Curved wall" : "Wall" };
                plan.Walls.Add(new Wall { A = start, B = hasEnd ? end : p, Curved = Tool == BuildTool.Curve, Control = p });
                SetDraft(plan); return;
            }
            if (Tool == BuildTool.Wing) { SetDraft(PrefabCatalog.Create(PrefabId, PrefabPackage, p, PlacementRotation)); return; }
            if (Tool == BuildTool.Meals) { World.DesignateDining(raw); hud.Refresh(true); return; }
            if (Tool == BuildTool.Bed || Tool == BuildTool.Table || Tool == BuildTool.Furnishing)
            {
                var plan = new Plan { Name = "Install " + FixtureCatalog.Get(PlacementFixtureKind).Name.ToLowerInvariant() };
                plan.Fixtures.Add(new Fixture { Kind = PlacementFixtureKind, Position = p, Rotation = PlacementRotation }); SetDraft(plan); return;
            }
            if (SelectedWall != null)
            {
                Wall changed = SelectedWall.Copy();
                if (Tool == BuildTool.MoveStart) changed.A = p;
                if (Tool == BuildTool.MoveEnd) changed.B = p;
                if (Tool == BuildTool.Bend) { changed.Curved = true; changed.Control = p; }
                Replace(SelectedWall, changed, "Wall renovation"); return;
            }
            if (SelectedFixture != null && Tool == BuildTool.MoveFixture)
            {
                Fixture changed = SelectedFixture.Copy(); changed.Position = p;
                ReplaceFixture(changed, "Move " + changed.Kind.ToLowerInvariant());
            }
        }
        Wall NearestWall(Point p)
        {
            return World.Data.Walls.OrderBy(w => Geometry.Distance(p, Geometry.OnLength(w, Geometry.NearestFraction(w, p))))
                .FirstOrDefault(w => Geometry.Distance(p, Geometry.OnLength(w, Geometry.NearestFraction(w, p))) < 0.75f);
        }
        static bool HitBox(Ray ray, Vector3 center, Quaternion rotation, Vector3 size, out float distance)
        {
            Quaternion inverse = Quaternion.Inverse(rotation);
            Ray local = new Ray(inverse * (ray.origin - center), inverse * ray.direction);
            return new Bounds(Vector3.zero, size).IntersectRay(local, out distance);
        }
        void FindWall(Vector2 screen, out Wall selected, out float distance, out Point point)
        {
            selected = null; distance = float.MaxValue; point = default(Point); Ray ray = cameraRig.ScreenPointToRay(screen);
            float height = view.WallHeight;
            foreach (Wall wall in World.Data.Walls)
                foreach (Segment segment in Geometry.Segments(wall, true))
                {
                    Point delta = segment.B - segment.A; float hit;
                    Quaternion rotation = Quaternion.Euler(0, -Mathf.Atan2(delta.Y, delta.X) * Mathf.Rad2Deg, 0);
                    if (HitBox(ray, FacilityView.V((segment.A + segment.B) * 0.5f, height * 0.5f + 0.12f), rotation, new Vector3(delta.Length, height, 0.28f), out hit) && hit < distance)
                    { selected = wall; distance = hit; Vector3 position = ray.GetPoint(hit); point = new Point(position.x, position.z); }
                }
        }
        void SelectComponent(Vector2 screen, Point ground)
        {
            Wall wall; float nearest; Point wallPoint; FindWall(screen, out wall, out nearest, out wallPoint);
            selectedCrewCharacter = ""; SelectedWall = wall; SelectedFixture = null; SelectedResident = null; SelectedStaff = null; Ray ray = cameraRig.ScreenPointToRay(screen);
            foreach (Fixture fixture in World.Data.Fixtures)
            {
                float hit;
                FixtureDefinition definition = FixtureCatalog.Get(fixture.Kind);
                Vector3 size = new Vector3(definition.Width, definition.Height, definition.Depth);
                if (HitBox(ray, FacilityView.V(fixture.Position, size.y * 0.5f + 0.13f), Quaternion.Euler(0, fixture.Rotation, 0), size, out hit) && hit < nearest)
                { SelectedFixture = fixture; SelectedWall = null; nearest = hit; }
            }
            foreach (Resident resident in World.Data.Residents)
            {
                float hit; Vector3 position;
                if (!view.TryGetPersonPosition(resident.Id, out position)) position = FacilityView.V(resident.Position, 0.14f);
                if (HitBox(ray, position + Vector3.up * 0.88f, Quaternion.identity, new Vector3(0.58f, 1.85f, 0.58f), out hit) && hit < nearest)
                { SelectedResident = resident; SelectedFixture = null; SelectedWall = null; nearest = hit; }
            }
            foreach (StaffMember staff in World.Data.Staff)
            {
                if (!staff.Arrived) continue;
                float hit; Vector3 position;
                if (!view.TryGetPersonPosition(staff.Id, out position)) position = FacilityView.V(staff.Position, 0.14f);
                if (HitBox(ray, position + Vector3.up * 0.88f, Quaternion.identity, new Vector3(0.58f, 1.85f, 0.58f), out hit) && hit < nearest)
                { SelectedStaff = staff; SelectedResident = null; SelectedFixture = null; SelectedWall = null; nearest = hit; }
            }
            foreach (Crew crew in World.Data.Crews.Where(c => c.Arrived))
                for (int builder = 0; builder < 2; builder++)
                {
                    string id = crew.Id + "/builder-" + builder; float hit; Vector3 position;
                    if (!view.TryGetPersonPosition(id, out position)) continue;
                    if (HitBox(ray, position + Vector3.up * .88f, Quaternion.identity, new Vector3(.58f, 1.85f, .58f), out hit) && hit < nearest)
                    { selectedCrewCharacter = id; SelectedStaff = null; SelectedResident = null; SelectedFixture = null; SelectedWall = null; nearest = hit; }
                }
            if (selectedCrewCharacter == "" && SelectedResident == null && SelectedStaff == null && SelectedFixture == null && SelectedWall == null)
            {
                string person = view.PickResident(cameraRig, screen);
                if (person != null) SelectedResident = World.Data.Residents.FirstOrDefault(r => r.Id == person);
                else SelectedWall = NearestWall(ground);
            }
        }
        void Replace(Wall old, Wall changed, string name)
        {
            var plan = new Plan { Name = name }; plan.RemoveWalls.Add(old.Id); plan.Walls.Add(changed); SetDraft(plan);
        }
        void ReplaceFixture(Fixture changed, string name)
        {
            var plan = new Plan { Name = name }; plan.RemoveFixtures.Add(SelectedFixture.Id); plan.Fixtures.Add(changed); SetDraft(plan);
        }
        public void ChangeFinish()
        {
            if (SelectedWall == null || Draft != null) return;
            Wall changed = SelectedWall.Copy(); changed.Finish = (changed.Finish + 1) % 2; Replace(SelectedWall, changed, "Wall finish renovation");
        }
        public void RotateFixture()
        {
            if (SelectedFixture == null || Draft != null) return;
            Fixture changed = SelectedFixture.Copy(); changed.Rotation = (changed.Rotation + 90) % 360; ReplaceFixture(changed, "Rotate furnishing");
        }
        public void RemoveSelection()
        {
            if (Draft != null) return;
            var plan = new Plan { Name = "Demolition and removal" };
            if (SelectedWall != null) plan.RemoveWalls.Add(SelectedWall.Id);
            if (SelectedFixture != null) plan.RemoveFixtures.Add(SelectedFixture.Id);
            if (plan.RemoveWalls.Count + plan.RemoveFixtures.Count > 0) SetDraft(plan);
        }
        void SetDraft(Plan plan)
        {
            Draft = plan; World.Data.DraftPlan = plan; CurrentQuote = World.Quote(plan); hasStart = hasEnd = false;
            if (plan.Walls.Count > 0) guides.Show(plan.Walls[0], CurrentQuote != null && !CurrentQuote.Valid);
            else if (plan.Fixtures.Count > 0) guides.ShowFixture(plan.Fixtures[0], CurrentQuote != null && !CurrentQuote.Valid);
            view.Preview(CurrentQuote!=null&&CurrentQuote.Valid?CurrentQuote.Plan:plan); hud.Refresh(true);
        }
        public bool CanRepeatPlacement { get { return Draft!=null&&Draft.Fixtures.Count==1&&Draft.Walls.Count==0&&Draft.RemoveFixtures.Count==0&&Draft.RemoveWalls.Count==0; } }
        public void ConfirmAndRepeat()
        {
            if(!CanRepeatPlacement)return;
            string kind=Draft.Fixtures[0].Kind;float rotation=Draft.Fixtures[0].Rotation;
            Confirm();if(Draft!=null)return;
            PlacementRotation=rotation;SetFixtureTool(kind);
            Instruction="Tap to place another "+FixtureCatalog.Get(kind).Name.ToLowerInvariant()+". Z rotates; Escape returns to Inspect.";
        }
        public void Confirm()
        {
            if (Draft == null) return;
            if (!World.Approve(Draft)) { CurrentQuote = World.Quote(Draft); hud.Refresh(true); return; }
            ResetDoorEditing();PlacingConnection = false; Draft = null; World.Data.DraftPlan = null; CurrentQuote = null; SelectedWall = null; SelectedFixture = null; SelectedResident = null; SelectedStaff = null;
            guides.Show(null); PlacementSummary = "";
            view.Preview(null); SetTool(BuildTool.Inspect); Save(false); hud.Refresh(true);
        }
        public void Cancel()
        {
            ResetDoorEditing();PlacingConnection = false; Draft = null; if (World != null) World.Data.DraftPlan = null; CurrentQuote = null; hasStart = hasEnd = false; view.Preview(null, SelectedWall, SelectedFixture); SetTool(BuildTool.Inspect);
        }
        public bool Save(bool feedback = true)
        {
            return SaveCompany(feedback);
        }
#if UNITY_EDITOR
        public float PreviewCameraDistance { get { return distance; } }
        public void StartPresentationPreview(string contract)
        {
            if (!CreateCompany(contract, true)) throw new InvalidOperationException("Could not preserve the active company before preview.");
            hud.CloseOverlays(); Recenter(); CameraPosition(true);
        }
        public void ReloadPresentationPreview()
        {
            if (!presentationPreview) throw new InvalidOperationException("Only disposable previews can use this review helper.");
            string residentId = SelectedResident == null ? "" : SelectedResident.Id;
            EndCharacterView(false); if (hud != null) hud.CloseCharacterPhone(); selectedCrewCharacter = ""; EndRideAlongSilently(); ResetDoorEditing(); PlacingConnection=false;
            World = new World(JsonUtility.FromJson<State>(JsonUtility.ToJson(World.Data)));
            SelectedResident = World.Data.Residents.FirstOrDefault(r => r.Id == residentId); SelectedStaff = null;
            SelectedWall = null; SelectedFixture = null; accumulator = 0; nextFeed = 0;
            Draft=World.Data.DraftPlan; CurrentQuote=Draft==null?null:World.Quote(Draft);
            view.Refresh(World, true); view.Preview(CurrentQuote!=null&&CurrentQuote.Valid?CurrentQuote.Plan:Draft); hud.Refresh(true);
        }
        public void PreviewDraft(Plan plan) { SetDraft(plan); }
        // Key art and the app icon come from the game's own renderer: a disposable company of a real facility type at a
        // chosen time of day, framed by this rig. Saves stay isolated like every other preview.
        public void StartArtPreview(string typeId, string locationId, int minuteOfDay)
        {
            if (!CreateCompany(null, true, typeId, locationId)) throw new InvalidOperationException("Could not start the art preview.");
            hud.CloseOverlays();
            int target = (World.Data.Minute / 1440) * 1440 + minuteOfDay; if (target <= World.Data.Minute) target += 1440;
            // The clock only runs unpaused; advance to the hour, then hold it there for the camera.
            World.Data.Speed = 1; World.AdvanceMinutes(target - World.Data.Minute); World.Data.Speed = 0; view.Refresh(World, true);
        }
        public Camera ArtCamera { get { return cameraRig; } }
        // Editor reviews from Claude's clone (batchmode, no Game view): the HUD target, the Pressure workspace, the clock.
        public void PreviewUiTarget(RenderTexture target) { hud.PreviewUiTarget(target); }
        public void PreviewRelayout() { hud.PreviewRelayout(); }
        public void PreviewHygieneCard() { hud.PreviewHygieneCard(); }
        public void PreviewPressureSection(string heading) { hud.PreviewPressure(); hud.Refresh(true); hud.PreviewScrollToSection(heading); }
        public void PreviewAdvance(int minutes) { int speed = World.Data.Speed; World.Data.Speed = 1; World.AdvanceMinutes(minutes); World.Data.Speed = speed; view.Refresh(World, true); }
        // Places the camera like the management rig does (focus, distance, yaw, pitch) without the interface offset.
        public void FrameArt(Vector3 artFocus, float artDistance, float artYaw, float artPitch)
        {
            planView = false; cameraRig.orthographic = false;
            Quaternion rotation = Quaternion.Euler(artPitch, artYaw, 0);
            cameraRig.transform.position = artFocus + rotation * new Vector3(0, 0, -artDistance * 1.55f); cameraRig.transform.rotation = rotation;
        }
        public Vector3 ArtCenter
        {
            get
            {
                if (World == null || World.Data.Walls.Count == 0) return Vector3.zero;
                var bounds = new Bounds(FacilityView.V(World.Data.Walls[0].A), Vector3.zero);
                foreach (Wall wall in World.Data.Walls) foreach (Point point in Geometry.Sample(wall)) bounds.Encapsulate(FacilityView.V(point));
                return new Vector3(bounds.center.x, 0, bounds.center.z);
            }
        }
        public void PreviewInspectFixture(string id)
        {
            SelectedWall=null;SelectedResident=null;SelectedStaff=null;SelectedFixture=World.Data.Fixtures.FirstOrDefault(f=>f.Id==id);
            hud.ShowFacility();hud.Refresh(true);
        }
        public void PreviewConstruction()
        {
            hud.ShowBuildCatalog("Structure"); PrefabId = "wing"; PrefabPackage = 0; PlacementRotation = 0;
            SetTool(BuildTool.Wing); SetDraft(Facility.Wing(new Point(2, -8))); hud.Refresh(true);
        }
        public void PreviewFurnishingStudy()
        {
            SelectedResident = null; SelectedStaff = null; SetTool(BuildTool.Inspect);
            hud.GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement.style.display = UnityEngine.UIElements.DisplayStyle.None;
            view.ShowFurnishingStudy(); focus = new Vector3(11.4f, 0, 19f); distance = 23; pitch = 62; yaw = 25;
        }
        public void CapturePresentationFrame(string path) { StartCoroutine(CaptureFrame(path)); }
        System.Collections.IEnumerator CaptureFrame(string path)
        {
            yield return new WaitForEndOfFrame();
            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
            if (frame == null) { Debug.LogError("Unity did not produce a presentation frame."); yield break; }
            File.WriteAllBytes(path, frame.EncodeToPNG());
            Debug.Log("Cellblock captured " + Path.GetFileName(path) + " · " + frame.width + " × " + frame.height);
            Destroy(frame);
        }
#endif
        public bool Load(string fileName = null)
        {
            try
            {
                string name = fileName ?? ContinueSave()?.FileName;
                if (name == null) throw new InvalidDataException("No playable holding is available. Open the save library.");
                if (World != null && !Save(false)) return false;
                if (!Regex.IsMatch(name, "^company-[a-f0-9]{32}\\.json$")) throw new InvalidDataException("Invalid company save name.");
                string path = Path.Combine(SaveDirectory, name);
                World restored; bool recovered = false;
                try { restored = new World(JsonUtility.FromJson<State>(File.ReadAllText(path))); }
                catch
                {
                    if (!File.Exists(path + ".bak")) throw;
                    restored = new World(JsonUtility.FromJson<State>(File.ReadAllText(path + ".bak")));
                    recovered = true;
                    restored.Notice = "Recovered the previous successful save. Your original file is retained.";
                }
                WritePointer(name);
                EndCharacterView(false); if (hud != null) hud.CloseCharacterPhone(); selectedCrewCharacter = ""; EndRideAlongSilently(); ResetDoorEditing(); PlacingConnection=false;
                ResetIncidentCamera(); World = restored; RecoveredSave = recovered; savedJson = ""; SaveProblem = ""; LastSavedUtc = File.GetLastWriteTimeUtc(recovered ? path + ".bak" : path); autosave = 0; watchedCameraId = ""; nextFeed = 0; nativeInputWasBlocked = true; presentationPreview = false; World.Data.Speed = 0; accumulator = 0; saveId = name.Substring(8, 32);
                Draft = null; CurrentQuote = null; SelectedWall = null; SelectedFixture = null; SelectedResident = null; SelectedStaff = null;
                view.Refresh(World, true); SetTool(BuildTool.Inspect); Recenter();
                if (World.Data.DraftPlan != null)
                {
                    try { Draft = World.Data.DraftPlan; CurrentQuote = World.Quote(Draft); view.Preview(CurrentQuote!=null&&CurrentQuote.Valid?CurrentQuote.Plan:Draft); }
                    catch { Draft = null; CurrentQuote = null; World.Data.DraftPlan = null; Message("Company recovered; an invalid unapproved plan was discarded."); }
                }
                return true;
            }
            catch (Exception e) { Debug.LogWarning("Could not load holding: " + e.Message); Message("That holding could not be opened. The saved files are preserved."); hud.ShowMenuError("That holding could not be opened. The saved files are preserved. Try another holding in Save Library."); return false; }
        }
        void OnApplicationPause(bool paused) { appPaused = paused; accumulator = 0; if (paused) { ClearCharacterInput(); if (hud != null) hud.ResetCharacterSticks(); Save(false); } }
        void OnApplicationFocus(bool focused) { unfocused = !focused; accumulator = 0; nativeInputWasBlocked = true; if (trackpad != null) trackpad.Consume(); if (!focused) { ClearCharacterInput(); if (hud != null) hud.ResetCharacterSticks(); Save(false); } }
        void OnApplicationQuit() { Save(false); }
    }
}
