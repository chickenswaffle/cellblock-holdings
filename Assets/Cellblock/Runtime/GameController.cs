using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public enum BuildTool { Inspect, Wall, Curve, Wing, Door, Bed, Table, Meals, MoveStart, MoveEnd, Bend, MoveFixture }
    public sealed class CompanySave { public string FileName, Title; }

    public sealed class GameController : MonoBehaviour
    {
        public World World { get; private set; }
        public Plan Draft { get; private set; }
        public Quote CurrentQuote { get; private set; }
        public Wall SelectedWall { get; private set; }
        public Fixture SelectedFixture { get; private set; }
        public bool Snapping = true;
        public BuildTool Tool { get; private set; }
        public string Instruction { get; private set; }
        FacilityView view;
        GameHud hud;
        Camera cameraRig;
        Vector3 focus = new Vector3(-3, 0, 0);
        float distance = 43, yaw = 36, accumulator, autosave, previewTime;
        bool appPaused, unfocused, dragging, gesture, hasStart, hasEnd;
        Point start, end;
        Vector2 pointerDown, lastPointer;
        string saveId = "";
        string SaveDirectory { get { return Path.Combine(Application.persistentDataPath, "companies"); } }
        string PointerPath { get { return Path.Combine(SaveDirectory, "last-company.txt"); } }
        public bool HasSave { get { return File.Exists(PointerPath); } }
        public IEnumerable<CompanySave> SavedCompanies()
        {
            if (!Directory.Exists(SaveDirectory)) return new CompanySave[0];
            var result = new List<CompanySave>();
            foreach (string path in Directory.GetFiles(SaveDirectory, "company-*.json"))
            {
                string name = Path.GetFileName(path);
                if (!Regex.IsMatch(name, "^company-[a-f0-9]{32}\\.json$")) continue;
                string title = "Company " + name.Substring(8, 6);
                try
                {
                    State state = JsonUtility.FromJson<State>(File.ReadAllText(path));
                    ContractOffer offer = Cellblock.Core.World.Offers.FirstOrDefault(o => o.Id == state.ContractId);
                    if (offer != null) title = offer.Name + " · Day " + (state.Minute / 1440 + 1) + " · " + GameHud.Money(state.CashCents) + " · " + name.Substring(8, 6);
                }
                catch { title += " · try backup recovery"; }
                result.Add(new CompanySave { FileName = name, Title = title });
            }
            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindObjectOfType<GameController>() == null) new GameObject("Cellblock Holdings").AddComponent<GameController>();
        }
        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            cameraRig = new GameObject("Management camera").AddComponent<Camera>();
            cameraRig.transform.SetParent(transform); cameraRig.orthographic = true;
            cameraRig.nearClipPlane = 0.1f; cameraRig.farClipPlane = 250;
            cameraRig.clearFlags = CameraClearFlags.SolidColor; cameraRig.backgroundColor = new Color(0.65f, 0.71f, 0.72f);
            view = new GameObject("Facility presentation").AddComponent<FacilityView>(); view.transform.SetParent(transform); view.Initialize();
            hud = new GameObject("Management interface").AddComponent<GameHud>(); hud.transform.SetParent(transform); hud.Initialize(this);
            SetTool(BuildTool.Inspect); CameraPosition(); hud.ShowMenu();
        }
        public bool StartCompany(string contract)
        {
            if (World != null && !Save()) return false;
            World = Cellblock.Core.World.Start(contract); saveId = Guid.NewGuid().ToString("N");
            Draft = null; SelectedWall = null; SelectedFixture = null; hasStart = hasEnd = false;
            accumulator = 0; view.Refresh(World, true); view.Preview(null); SetTool(BuildTool.Inspect); Recenter(); Save();
            return true;
        }
        public void SetTool(BuildTool tool)
        {
            if (Draft != null) { Message("Approve or discard the current plan before starting another."); return; }
            if ((tool == BuildTool.MoveStart || tool == BuildTool.MoveEnd || tool == BuildTool.Bend) && SelectedWall == null)
            { Message("Select a wall first."); return; }
            if (tool == BuildTool.MoveFixture && SelectedFixture == null) { Message("Select a furnishing first."); return; }
            Tool = tool; hasStart = hasEnd = false;
            switch (tool)
            {
                case BuildTool.Wall: Instruction = "WALL · Tap a start point, then an end point. Review before construction."; break;
                case BuildTool.Curve: Instruction = "CURVE · Tap the start, end, then bend position."; break;
                case BuildTool.Wing: Instruction = "PREFAB WING · Tap its lower-left connection point. Every wall remains editable after construction."; break;
                case BuildTool.Door: Instruction = "DOOR · Tap an existing wall where the opening should go."; break;
                case BuildTool.Bed: case BuildTool.Table: Instruction = tool.ToString().ToUpperInvariant() + " · Tap a location, then review the fit-out project."; break;
                case BuildTool.Meals: Instruction = "MEAL SERVICE · Tap an installed table to move meal service and compare travel times."; break;
                case BuildTool.MoveStart: case BuildTool.MoveEnd: Instruction = "WALL EDIT · Tap the new endpoint position."; break;
                case BuildTool.Bend: Instruction = "WALL EDIT · Tap the bend position. Straight walls can become curves."; break;
                case BuildTool.MoveFixture: Instruction = "MOVE FURNISHING · Tap its new position."; break;
                default: Instruction = "INSPECT · Tap walls or furnishings to edit. Two fingers pan/pinch; mouse wheel zooms and right-drag pans."; break;
            }
            if (view != null) view.Preview(null, SelectedWall);
            if (hud != null) hud.Refresh(true);
        }
        public void Message(string message) { if (World != null) World.Notice = message; }
        void Update()
        {
            CameraPosition();
            if (World == null) return;
            if (!appPaused && !unfocused && World.Data.Speed > 0)
            {
                accumulator += Mathf.Min(Time.unscaledDeltaTime, 0.1f) * World.Data.Speed;
                int steps = 0;
                while (accumulator >= 0.2f && steps++ < 8) { World.AdvanceMinutes(1); accumulator -= 0.2f; }
            }
            else accumulator = 0;
            InputUpdate(); view.Refresh(World); hud.Refresh();
            autosave += Time.unscaledDeltaTime;
            if (autosave >= 30) { autosave = 0; Save(false); }
        }
        void CameraPosition()
        {
            cameraRig.orthographicSize = distance * 0.46f;
            Quaternion rotation = Quaternion.Euler(52, yaw, 0);
            cameraRig.transform.position = focus + rotation * new Vector3(0, 0, -distance * 1.7f);
            cameraRig.transform.rotation = rotation;
        }
        public void Recenter() { focus = new Vector3(-3, 0, 0); distance = 43; yaw = 36; CameraPosition(); }
        public void ToggleHeight() { view.FullHeight = !view.FullHeight; view.Refresh(World, true); }
        bool Ground(Vector2 screen, out Point point)
        {
            float enter; Ray ray = cameraRig.ScreenPointToRay(screen);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out enter))
            {
                Vector3 hit = ray.GetPoint(enter); point = new Point(hit.x, hit.z); return true;
            }
            point = default(Point); return false;
        }
        Point Snap(Point point) { return Geometry.Snap(point, World.Data.Walls, Snapping); }
        void Pan(Vector2 previous, Vector2 current)
        {
            Point a, b;
            if (Ground(previous, out a) && Ground(current, out b)) focus += FacilityView.V(a - b);
            focus.x = Mathf.Clamp(focus.x, -23, 23); focus.z = Mathf.Clamp(focus.z, -20, 20);
        }
        void InputUpdate()
        {
            if (Input.touchCount > 0)
            {
                if (Input.touchCount >= 2)
                {
                    Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
                    if (hud.IsOverUI(a.position) || hud.IsOverUI(b.position)) return;
                    gesture = true;
                    Vector2 center = (a.position + b.position) / 2;
                    Vector2 previous = (a.position - a.deltaPosition + b.position - b.deltaPosition) / 2;
                    Pan(previous, center);
                    float before = Vector2.Distance(a.position - a.deltaPosition, b.position - b.deltaPosition);
                    float now = Vector2.Distance(a.position, b.position);
                    if (before > 1 && now > 1) distance = Mathf.Clamp(distance * before / now, 14, 85);
                    return;
                }
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    pointerDown = lastPointer = touch.position; dragging = false;
                    if (hud.IsOverUI(touch.position)) gesture = true;
                }
                if (touch.phase == TouchPhase.Moved && !gesture)
                {
                    if (Vector2.Distance(pointerDown, touch.position) > 15) dragging = true;
                    if (dragging && Tool == BuildTool.Inspect) Pan(lastPointer, touch.position);
                    lastPointer = touch.position;
                    if (!hud.IsOverUI(touch.position)) PreviewAt(touch.position);
                }
                if (touch.phase == TouchPhase.Ended)
                {
                    if (!gesture && !dragging && !hud.IsOverUI(touch.position)) Tap(touch.position);
                    gesture = false;
                }
                if (touch.phase == TouchPhase.Canceled) gesture = false;
                return;
            }
            gesture = false;
            Vector2 cursor = Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) { pointerDown = cursor; dragging = false; }
            if (Input.GetMouseButton(0) && Vector2.Distance(pointerDown, cursor) > 8) dragging = true;
            if (Input.GetMouseButtonUp(0) && !dragging && !hud.IsOverUI(cursor)) Tap(cursor);
            if (!hud.IsOverUI(cursor))
            {
                distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * 2, 14, 85);
                if (Input.GetMouseButton(1) || Input.GetMouseButton(2)) Pan(lastPointer, cursor);
                PreviewAt(cursor);
            }
            lastPointer = cursor;
            if (Input.GetKeyDown(KeyCode.Escape)) Cancel();
            if (Input.GetKeyDown(KeyCode.Space)) World.Data.Speed = World.Data.Speed == 0 ? 1 : 0;
            if (Input.GetKeyDown(KeyCode.Home)) Recenter();
            if (Input.GetKey(KeyCode.Q)) yaw -= Time.unscaledDeltaTime * 45;
            if (Input.GetKey(KeyCode.E)) yaw += Time.unscaledDeltaTime * 45;
        }
        void PreviewAt(Vector2 screen)
        {
            if (Draft != null || Time.unscaledTime < previewTime) return;
            previewTime = Time.unscaledTime + 0.08f;
            Point p; if (!Ground(screen, out p)) return; p = Snap(p);
            Plan preview = null;
            if (Tool == BuildTool.Wing) preview = Facility.Wing(p);
            if ((Tool == BuildTool.Wall || Tool == BuildTool.Curve) && hasStart)
            {
                var wall = new Wall { A = start, B = hasEnd ? end : p, Curved = Tool == BuildTool.Curve && hasEnd, Control = p };
                if (Geometry.Distance(wall.A, wall.B) > 0.1f) { preview = new Plan(); preview.Walls.Add(wall); }
            }
            if (Tool != BuildTool.Inspect) view.Preview(preview, SelectedWall);
        }
        void Tap(Vector2 screen)
        {
            if (Draft != null) { Message("Approve or discard the current plan first."); return; }
            Point p; if (!Ground(screen, out p)) return;
            Point raw = p; p = Snap(p);
            if (Tool == BuildTool.Inspect)
            {
                SelectedWall = NearestWall(raw);
                SelectedFixture = World.Data.Fixtures.OrderBy(f => Geometry.Distance(f.Position, raw)).FirstOrDefault(f => Geometry.Distance(f.Position, raw) < 1.2f);
                if (SelectedFixture != null) SelectedWall = null;
                view.Preview(null, SelectedWall); hud.Refresh(true); return;
            }
            if (Tool == BuildTool.Wall || Tool == BuildTool.Curve)
            {
                if (!hasStart) { start = p; hasStart = true; Instruction = "Tap the wall endpoint."; return; }
                if (Tool == BuildTool.Curve && !hasEnd) { end = p; hasEnd = true; Instruction = "Tap the bend position."; return; }
                var plan = new Plan { Name = Tool == BuildTool.Curve ? "Curved wall" : "Wall" };
                plan.Walls.Add(new Wall { A = start, B = hasEnd ? end : p, Curved = Tool == BuildTool.Curve, Control = p });
                SetDraft(plan); return;
            }
            if (Tool == BuildTool.Wing) { SetDraft(Facility.Wing(p)); return; }
            if (Tool == BuildTool.Meals) { World.DesignateDining(raw); hud.Refresh(true); return; }
            if (Tool == BuildTool.Bed || Tool == BuildTool.Table)
            {
                var plan = new Plan { Name = Tool == BuildTool.Bed ? "Install bed" : "Install table" };
                plan.Fixtures.Add(new Fixture { Kind = Tool == BuildTool.Bed ? "Bed" : "Table", Position = p }); SetDraft(plan); return;
            }
            if (Tool == BuildTool.Door)
            {
                Wall wall = NearestWall(raw);
                if (wall == null) { Message("Tap closer to the wall where you want the doorway."); return; }
                Wall changed = wall.Copy(); changed.Door = Geometry.NearestFraction(wall, raw);
                Replace(wall, changed, "Doorway renovation"); return;
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
            view.Preview(plan); hud.Refresh(true);
        }
        public void Confirm()
        {
            if (Draft == null) return;
            if (!World.Approve(Draft)) { CurrentQuote = World.Quote(Draft); hud.Refresh(true); return; }
            Draft = null; World.Data.DraftPlan = null; CurrentQuote = null; SelectedWall = null; SelectedFixture = null;
            view.Preview(null); SetTool(BuildTool.Inspect); Save(false); hud.Refresh(true);
        }
        public void Cancel()
        {
            Draft = null; if (World != null) World.Data.DraftPlan = null; CurrentQuote = null; hasStart = hasEnd = false; view.Preview(null, SelectedWall); SetTool(BuildTool.Inspect);
        }
        public bool Save(bool feedback = true)
        {
            if (World == null) return true;
            try
            {
                Directory.CreateDirectory(SaveDirectory);
                if (!Regex.IsMatch(saveId, "^[a-f0-9]{32}$")) throw new InvalidDataException("Invalid company identifier.");
                string name = "company-" + saveId + ".json", path = Path.Combine(SaveDirectory, name), temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(World.Data, true));
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak"); else File.Move(temporary, path);
                string pointer = PointerPath + ".tmp"; File.WriteAllText(pointer, name);
                if (File.Exists(PointerPath)) File.Replace(pointer, PointerPath, null); else File.Move(pointer, PointerPath);
                if (feedback) Message("Company saved. Time pauses when the game is closed.");
                return true;
            }
            catch (Exception e) { Message("Save failed: " + e.Message + ". Keep this session open."); if (hud != null) hud.ShowMenuError("Save failed: " + e.Message); Debug.LogError(e); return false; }
        }
        public bool Load(string fileName = null)
        {
            try
            {
                if (World != null && !Save(false)) return false;
                string name = fileName ?? File.ReadAllText(PointerPath).Trim();
                if (!Regex.IsMatch(name, "^company-[a-f0-9]{32}\\.json$")) throw new InvalidDataException("Invalid company save name.");
                string path = Path.Combine(SaveDirectory, name);
                World restored;
                try { restored = new World(JsonUtility.FromJson<State>(File.ReadAllText(path))); }
                catch
                {
                    if (!File.Exists(path + ".bak")) throw;
                    restored = new World(JsonUtility.FromJson<State>(File.ReadAllText(path + ".bak")));
                    restored.Notice = "Recovered the previous successful save. Your original file is retained.";
                }
                World = restored; World.Data.Speed = 0; accumulator = 0; saveId = name.Substring(8, 32);
                Draft = null; CurrentQuote = null; SelectedWall = null; SelectedFixture = null;
                view.Refresh(World, true); SetTool(BuildTool.Inspect); Recenter();
                if (World.Data.DraftPlan != null)
                {
                    try { Draft = World.Data.DraftPlan; CurrentQuote = World.Quote(Draft); view.Preview(Draft); }
                    catch { Draft = null; CurrentQuote = null; World.Data.DraftPlan = null; Message("Company recovered; an invalid unapproved plan was discarded."); }
                }
                return true;
            }
            catch (Exception e) { Debug.LogError("Could not load company: " + e.Message); Message("Could not load company: " + e.Message); hud.ShowMenuError("Could not load company: " + e.Message); return false; }
        }
        void OnApplicationPause(bool paused) { appPaused = paused; accumulator = 0; if (paused) Save(false); }
        void OnApplicationFocus(bool focused) { unfocused = !focused; accumulator = 0; if (!focused) Save(false); }
        void OnApplicationQuit() { Save(false); }
    }
}
