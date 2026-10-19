using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cellblock.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Editor
{
    // Development-only Play check. Requests live in ignored Temp; preview companies never save.
    [InitializeOnLoad]
    public static partial class PresentationPreview
    {
        const string Request = "Temp/CellblockPresentation.request";
        static string Output { get { string mode = SessionState.GetString(ModeKey, ""); return mode == "motion" ? "Temp/CellblockNaturalMotion" : mode == "escape" ? "Temp/CellblockEscape" : mode == "characters" ? "Temp/CellblockCharacters" : mode == "routines" ? "Temp/CellblockRoutines" : mode == "incidents" ? "Temp/CellblockIncidents" : mode == "frontend" ? "Temp/CellblockFrontend" : mode == "metrics" ? "Temp/CellblockMetrics" : mode == "polish" ? "Temp/CellblockPolish" : mode == "stories" ? "Temp/CellblockStories" : mode == "operations" ? "Temp/CellblockOperations" : mode == "security" ? "Temp/CellblockSecurity" : mode == "justice" ? "Temp/CellblockJustice" : "Temp/CellblockPresentation"; } }
        const string ModeKey = "Cellblock.Presentation.Mode", StageKey = "Cellblock.Presentation.Stage";
        const string WaitKey = "Cellblock.Presentation.Wait", StartKey = "Cellblock.Presentation.Start", ErrorKey = "Cellblock.Presentation.Errors";
        const string PendingKey = "Cellblock.Presentation.Pending", WidthKey = "Cellblock.Presentation.Width", HeightKey = "Cellblock.Presentation.Height";
        static PresentationPreview()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += Log;
            EditorApplication.quitting += ReleaseRefresh;
            EditorApplication.quitting += AbortEscapeOnQuit;
        }
        [MenuItem("Cellblock/Open game and Play")]
        public static void Play() { RequestRun("play"); }
        [MenuItem("Cellblock/Capture presentation review")]
        public static void Capture() { RequestRun("capture"); }
        internal static void RequestEscapeReview() { RequestRun("escape"); }
        internal static void RequestFeatureReview(string mode) { RequestRun(mode); }
        static bool FeatureMode(string mode) { return mode == "yard" || mode == "actions" || mode == "sanitation" || mode == "look" || mode == "workshop"; }
        static void BeginFeature(string mode)
        {
            if (mode == "yard") YardCapture.BeginExisting();
            else if (mode == "actions") ActionCapture.BeginExisting();
            else if (mode == "sanitation") SanitationCapture.BeginExisting();
            else if (mode == "workshop") ConstructionStudio.BeginExisting();
            else LookCapture.BeginExisting();
        }
        static bool FeatureRunning(string mode)
        {
            return mode == "yard" ? YardCapture.IsRunning : mode == "actions" ? ActionCapture.IsRunning :
                mode == "sanitation" ? SanitationCapture.IsRunning : mode == "workshop" ? ConstructionStudio.IsRunning : LookCapture.IsRunning;
        }
        static void AbortFeature(string mode, string reason)
        {
            if (mode == "yard") YardCapture.Abort(reason);
            else if (mode == "actions") ActionCapture.Abort(reason);
            else if (mode == "sanitation") SanitationCapture.Abort(reason);
            else if (mode == "look") LookCapture.Abort(reason);
            else if (mode == "workshop") ConstructionStudio.Abort(reason);
        }
        [Serializable] sealed class FeatureReport { public bool passed; public string editor; public string[] errors, notes, captures; }
        internal static void WriteFeatureEvidence(string output, bool passed, string[] errors, string[] notes)
        {
            string[] allErrors = errors.Concat(ReviewErrors()).Distinct().ToArray();
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(new FeatureReport {
                passed = passed && allErrors.Length == 0, editor = Application.unityVersion, errors = allErrors,
                notes = notes, captures = Directory.GetFiles(output, "*.png").Select(Path.GetFullPath).ToArray()
            }, true));
        }
        static void AbortEscapeOnQuit()
        {
            AbortFeature(SessionState.GetString(ModeKey, ""), "Editor closed during feature review.");
            if (SessionState.GetString(ModeKey, "") == "escape") EscapeCapture.Abort("Editor closed during escape review.", ReviewErrors());
            if (SessionState.GetString(ModeKey, "") == "motion") NaturalMotionReview.Abort("Editor closed during motion review.", ReviewErrors());
        }
        internal static string[] ReviewErrors() { return SessionState.GetString(ErrorKey, "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries); }
        static void RequestRun(string mode) { Directory.CreateDirectory("Temp"); File.WriteAllText(Request, mode); }
        static void Log(string condition, string stack, LogType type)
        {
            if ((SessionState.GetString(ModeKey, "") != "capture" && SessionState.GetString(ModeKey, "") != "operations" && SessionState.GetString(ModeKey, "") != "justice" && SessionState.GetString(ModeKey, "") != "security" && SessionState.GetString(ModeKey, "") != "stories" && SessionState.GetString(ModeKey, "") != "polish" && SessionState.GetString(ModeKey, "") != "metrics" && SessionState.GetString(ModeKey, "") != "frontend" && SessionState.GetString(ModeKey, "") != "incidents" && SessionState.GetString(ModeKey, "") != "routines" && SessionState.GetString(ModeKey, "") != "characters" && SessionState.GetString(ModeKey, "") != "escape" && SessionState.GetString(ModeKey, "") != "motion" && !FeatureMode(SessionState.GetString(ModeKey, ""))) || !EditorApplication.isPlaying) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            string errors = SessionState.GetString(ErrorKey, "");
            string message = condition.Replace('\n', ' ');
            if (!errors.Contains(message)) SessionState.SetString(ErrorKey, errors + message + "\n");
        }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string mode = SessionState.GetString(ModeKey, "");
            if (mode != "" && File.Exists(Request) && File.ReadAllText(Request).Trim() == "edit")
            {
                File.Delete(Request);
                try
                {
                    AbortFeature(mode, "Feature review cancelled by an edit request.");
                    if (mode == "escape") EscapeCapture.Abort("Escape review cancelled by an edit request.", ReviewErrors());
                    if (mode == "motion") NaturalMotionReview.Abort("Motion review cancelled by an edit request.", ReviewErrors());
                }
                finally { ReleaseRefresh(); }
                GameController active = UnityEngine.Object.FindFirstObjectByType<GameController>();
                if (active != null && !active.Save(false)) { Fail("Could not save before ending review.", false); return; }
                SessionState.SetString(ModeKey, "stopping"); EditorApplication.isPlaying = false; return;
            }
            if (mode == "")
            {
                if (!File.Exists(Request)) return;
                mode = File.ReadAllText(Request).Trim(); File.Delete(Request);
                if (mode != "play" && mode != "capture" && mode != "edit" && mode != "operations" && mode != "justice" && mode != "security" && mode != "stories" && mode != "polish" && mode != "metrics" && mode != "frontend" && mode != "incidents" && mode != "routines" && mode != "characters" && mode != "escape" && mode != "motion" && !FeatureMode(mode)) return;
                SessionState.SetString(ModeKey, mode); SessionState.SetInt(StageKey, -1);
                SessionState.SetString(ErrorKey, ""); SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
                SessionState.SetString(PendingKey, "");
                if (mode == "escape") EscapeCapture.PrepareRequest();
                Directory.CreateDirectory(Output);
                if (mode == "capture" || mode == "operations" || mode == "justice" || mode == "security" || mode == "stories" || mode == "polish" || mode == "metrics" || mode == "frontend" || (mode == "incidents" || mode == "routines" || mode == "characters"))
                {
                    foreach (string file in Directory.GetFiles(Output, "*.png")) File.Delete(file);
                    if (File.Exists(Path.Combine(Output, "report.json"))) File.Delete(Path.Combine(Output, "report.json"));
                }
                if (EditorApplication.isPlaying)
                {
                    GameController current = UnityEngine.Object.FindFirstObjectByType<GameController>();
                    if (current != null && !current.Save(false)) { Fail("The active company could not be saved; preview was cancelled.", false); return; }
                    EditorApplication.isPlaying = false;
                }
                return;
            }
            if (mode == "stopping")
            { if (!EditorApplication.isPlayingOrWillChangePlaymode) SessionState.SetString(ModeKey, ""); return; }
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartKey, 0) > (mode == "operations" || mode == "security" || mode == "stories" || mode == "polish" || mode == "metrics" || mode == "frontend" || (mode == "incidents" || mode == "routines" || mode == "characters" || mode == "escape" || mode == "motion" || FeatureMode(mode)) ? 600 : 150))
            { Fail("Unity presentation check timed out."); return; }
            int stage = SessionState.GetInt(StageKey, -1);
            if (stage == -1)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (mode == "edit") { ReleaseRefresh(); SessionState.SetString(ModeKey, ""); Debug.Log("CELLBLOCK_EDIT_MODE_READY"); return; }
                try
                {
                    for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                        if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) { Fail("Save the open editor scene before running presentation review. No scene was replaced."); return; }
                    ProjectSetup.Validate();
                    EditorSceneManager.OpenScene("Assets/Cellblock/Scenes/Main.unity");
                    SetGameSize(1600, 900);
                    SessionState.SetInt(StageKey, 0); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 3);
                    EditorApplication.isPaused = false; EditorApplication.isPlaying = true;
                }
                catch (Exception error) { Fail(error.ToString()); }
                return;
            }
            if (EditorApplication.timeSinceStartup < SessionState.GetFloat(WaitKey, 0)) return;
            if (!EditorApplication.isPlaying)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) Fail("Unity could not enter Play mode. Resolve the editor compilation errors before retrying.");
                return;
            }
            if ((mode == "capture" || mode == "operations" || mode == "justice" || mode == "security" || mode == "stories" || mode == "polish" || mode == "metrics" || mode == "frontend" || (mode == "incidents" || mode == "routines" || mode == "characters" || mode == "escape" || mode == "motion" || FeatureMode(mode))) && !SessionState.GetBool("Cellblock.Review.RefreshHeld", false))
            { AssetDatabase.DisallowAutoRefresh(); SessionState.SetBool("Cellblock.Review.RefreshHeld", true); }
            EditorApplication.isPaused = false;
            if (mode == "capture" || mode == "operations" || mode == "justice" || mode == "security" || mode == "stories" || mode == "polish" || mode == "metrics" || mode == "frontend" || (mode == "incidents" || mode == "routines" || mode == "characters" || mode == "escape" || mode == "motion" || FeatureMode(mode))) Application.runInBackground = true;
            string pending = SessionState.GetString(PendingKey, "");
            if (pending != "")
            {
                if (!File.Exists(pending)) return;
                byte[] bytes = File.ReadAllBytes(pending);
                int width = bytes[16] << 24 | bytes[17] << 16 | bytes[18] << 8 | bytes[19];
                int height = bytes[20] << 24 | bytes[21] << 16 | bytes[22] << 8 | bytes[23];
                if (width != SessionState.GetInt(WidthKey, 0) || height != SessionState.GetInt(HeightKey, 0))
                { Fail("The Game view capture has an unexpected size: " + width + " × " + height); return; }
                SessionState.SetString(PendingKey, "");
                if (mode == "characters" && ApplyCharacterAfterCapture()) return;
                if (mode == "incidents" && ApplyIncidentAfterCapture()) return;
            }
            GameController game = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (game == null) { Fail("The game controller did not initialize in Play mode."); return; }
            if (mode == "play") { SessionState.SetString(ModeKey, ""); Debug.Log("CELLBLOCK_PLAY_MODE_READY"); return; }
            if (FeatureMode(mode))
            {
                try
                {
                    if (stage == 0) { BeginFeature(mode); SessionState.SetInt(StageKey, 1); }
                    else if (!FeatureRunning(mode))
                    { ReleaseRefresh(); SessionState.SetString(ModeKey, "stopping"); EditorApplication.isPlaying = false; }
                }
                catch (Exception error) { Fail(error.ToString()); }
                return;
            }
            if (mode == "motion")
            {
                try
                {
                    if (stage == 0) { NaturalMotionReview.Begin(game); SessionState.SetInt(StageKey, 1); }
                    else if (NaturalMotionReview.TickReview(game, ReviewErrors()))
                    { ReleaseRefresh(); SessionState.SetString(ModeKey, "stopping"); EditorApplication.isPlaying = false; }
                }
                catch (Exception error) { Fail(error.ToString()); }
                return;
            }
            if (mode == "escape")
            {
                try
                {
                    if (stage == 0) { EscapeCapture.Begin(game); SessionState.SetInt(StageKey, 1); }
                    else if (EscapeCapture.TickReview(game, ReviewErrors()))
                    { ReleaseRefresh(); SessionState.SetString(ModeKey, "stopping"); EditorApplication.isPlaying = false; }
                }
                catch (Exception error) { Fail(error.ToString()); }
                return;
            }
            if(mode=="characters")
            {
                try { if(!RunCharacterStage(game,stage))return;SessionState.SetInt(StageKey,stage+1);SessionState.SetFloat(WaitKey,(float)EditorApplication.timeSinceStartup+1.5f); }
                catch(Exception error){Fail(error.ToString());}return;
            }
            if(mode=="routines")
            {
                try { if(!RunRoutineStage(game,stage))return;SessionState.SetInt(StageKey,stage+1);SessionState.SetFloat(WaitKey,(float)EditorApplication.timeSinceStartup+1.5f); }
                catch(Exception error){Fail(error.ToString());}return;
            }
            if(mode=="incidents")
            {
                try{if(!RunIncidentStage(game,stage))return;SessionState.SetInt(StageKey,stage+1);SessionState.SetFloat(WaitKey,(float)EditorApplication.timeSinceStartup+1.2f);}
                catch(Exception error){Fail(error.ToString());}return;
            }
            if(mode=="frontend")
            {
                try{if(!RunFrontendStage(game,stage))return;SessionState.SetInt(StageKey,stage+1);SessionState.SetFloat(WaitKey,(float)EditorApplication.timeSinceStartup+1.8f);}
                catch(Exception error){Fail(error.ToString());}return;
            }
            if(mode=="metrics")
            {
                try{if(!RunMetricsStage(game,stage))return;SessionState.SetInt(StageKey,stage+1);SessionState.SetFloat(WaitKey,(float)EditorApplication.timeSinceStartup+1.8f);}
                catch(Exception error){Fail(error.ToString());}return;
            }
            if(mode=="polish")
            {
                try{if(!RunPolishStage(game,stage))return;SessionState.SetInt(StageKey,stage+1);SessionState.SetFloat(WaitKey,(float)EditorApplication.timeSinceStartup+2.3f);}
                catch(Exception error){Fail(error.ToString());}return;
            }
            if (mode == "stories")
            {
                try { if(!RunStoriesStage(game,stage))return;SessionState.SetInt(StageKey,stage+1);SessionState.SetFloat(WaitKey,(float)EditorApplication.timeSinceStartup+1.8f); }
                catch(Exception error){Fail(error.ToString());}return;
            }
            if (mode == "security")
            {
                try
                {
                    if(!RunSecurityStage(game,stage))return;
                    SessionState.SetInt(StageKey,stage+1);SessionState.SetFloat(WaitKey,(float)EditorApplication.timeSinceStartup+1.8f);
                }
                catch(Exception error){Fail(error.ToString());}
                return;
            }
            if (mode == "justice")
            {
                try
                {
                    if (!RunJusticeStage(game, stage)) return;
                    SessionState.SetInt(StageKey, stage + 1); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 1.8f);
                }
                catch (Exception error) { Fail(error.ToString()); }
                return;
            }
            if (mode == "operations")
            {
                try
                {
                    if (!RunOperationsStage(game, stage)) return;
                    SessionState.SetInt(StageKey, stage + 1); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 1.8f);
                }
                catch (Exception error) { Fail(error.ToString()); }
                return;
            }
            try
            {
                switch (stage)
                {
                    case 0: Shot(game, "01-contracts-desktop.png"); break;
                    case 1: game.StartPresentationPreview("county"); break;
                    case 2:
                        Require(game.World != null && game.World.Rooms.Count > 0 && game.World.Reconciles(), "The starter facility or ledger failed to initialize.");
                        Shot(game, "02-facility-desktop.png"); break;
                    case 3:
                        game.PreviewConstruction(); Require(game.CurrentQuote != null && game.CurrentQuote.Valid, "The suggested connected wing did not receive a valid construction quote."); break;
                    case 4: Shot(game, "03-construction-review.png"); break;
                    case 5: game.Cancel(); game.Orbit(-18, -12); game.Zoom(2.4f); break;
                    case 6: Shot(game, "04-architecture-detail.png"); break;
                    case 7: game.ToggleHeight(); break;
                    case 8: Shot(game, "05-full-height.png"); break;
                    case 9: game.ToggleHeight(); game.Recenter(); SetGameSize(900, 420); break;
                    case 10: Shot(game, "06-facility-phone.png"); break;
                    case 11: game.PreviewConstruction(); break;
                    case 12: Shot(game, "07-construction-phone.png"); break;
                    case 13: game.Cancel(); game.GetComponentInChildren<GameHud>().ShowMenu(); break;
                    case 14: Shot(game, "08-contracts-phone.png"); break;
                    case 15: SetGameSize(1600, 900); break;
                    case 16:
                        game.GetComponentInChildren<GameHud>().CloseOverlays();
                        game.GetComponentInChildren<GameHud>().ShowBuildCatalog("Hygiene");
                        var roundTrip = new Cellblock.Core.World(JsonUtility.FromJson<Cellblock.Core.State>(JsonUtility.ToJson(game.World.Data)));
                        Require(roundTrip.Data.Residents[0].Id == game.World.Data.Residents[0].Id && roundTrip.Data.Residents[0].Gang == game.World.Data.Residents[0].Gang && roundTrip.Data.Residents[0].Relationships.Count == game.World.Data.Residents[0].Relationships.Count && roundTrip.Data.Staff.Count == game.World.Data.Staff.Count,
                            "Unity JSON did not preserve identities, social ties, or staff."); break;
                    case 17: Shot(game, "09-build-catalog.png"); break;
                    case 18: game.InspectResident(game.World.Data.Residents[0].Id); game.GetComponentInChildren<GameHud>().ShowPeople(); break;
                    case 19: Shot(game, "10-resident-profile.png"); break;
                    case 20: game.InspectStaff(game.World.Data.Staff.OrderBy(s => s.Integrity).First().Id); break;
                    case 21: Shot(game, "11-staff-profile.png"); break;
                    case 22: game.Recenter(); game.World.Data.Speed = 1; break;
                    case 23: Shot(game, "12-individual-routines.png"); game.World.Data.Speed = 0; break;
                    case 24: SetGameSize(900, 420); game.InspectResident(game.World.Data.Residents[0].Id); break;
                    case 25: Shot(game, "13-people-phone.png"); break;
                    case 26: game.GetComponentInChildren<GameHud>().ShowBuildCatalog("Recreation"); break;
                    case 27: Shot(game, "14-catalog-phone.png"); break;
                    case 28: SetGameSize(1600, 900); game.InspectResident(game.World.Data.Residents[0].Id); break;
                    case 29:
                        var document = game.GetComponentInChildren<UIDocument>();
                        document.rootVisualElement.Q<ScrollView>("inspector").ScrollTo(document.rootVisualElement.Q<VisualElement>(className: "relationships-pane")); break;
                    case 30: Shot(game, "15-relationships.png"); break;
                    case 31: game.PreviewFurnishingStudy(); break;
                    case 32: Shot(game, "16-furnishing-models.png"); break;
                    case 33: Finish(); return;
                }
                SessionState.SetInt(StageKey, stage + 1); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + (stage == 22 ? 8 : 1.8f));
            }
            catch (Exception error) { Fail(error.ToString()); }
        }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void Shot(GameController game, string name)
        {
            game.GetComponentInChildren<GameHud>().Refresh(true);
            string path = Path.GetFullPath(Path.Combine(Output, name)); SessionState.SetString(PendingKey, path);
            game.CapturePresentationFrame(path);
        }
        [Serializable] sealed class Report { public bool passed; public string editor, message; public string[] errors, captures; }
        static void Finish()
        {
            string[] errors = SessionState.GetString(ErrorKey, "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (Directory.GetFiles(Output, "*.png").Length != 16) { Fail("The review did not produce all sixteen required captures."); return; }
            var report = new Report { passed = errors.Length == 0, editor = Application.unityVersion,
                message = "Actual Unity Play mode: boot, desktop/phone UI, categorized catalog, catalog furnishing models, people/staff profiles, live individual routines, Unity JSON social-state round trip, connected wing quote, camera changes, full-height view. Manual pointer/touch feel and device performance remain separate checks.",
                errors = errors, captures = Directory.GetFiles(Output, "*.png").Select(Path.GetFullPath).ToArray() };
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
            Debug.Log(report.passed ? "CELLBLOCK_PRESENTATION_CHECK_PASSED" : "CELLBLOCK_PRESENTATION_CHECK_FAILED");
            ReleaseRefresh(); SessionState.SetString(ModeKey, "stopping"); EditorApplication.isPlaying = false;
        }
        static void Fail(string error, bool stopPlay = true)
        {
            AbortFeature(SessionState.GetString(ModeKey, ""), error);
            var active = UnityEngine.Object.FindFirstObjectByType<GameController>(); if (active != null && active.PreviewHasSaveSandbox) active.PreviewEndSaveSandbox();
            if (SessionState.GetString(ModeKey, "") == "escape" || SessionState.GetString(ModeKey, "") == "motion")
            {
                bool motion = SessionState.GetString(ModeKey, "") == "motion";
                try { if (motion) NaturalMotionReview.Abort(error, ReviewErrors()); else EscapeCapture.Abort(error, ReviewErrors()); }
                finally
                {
                    ReleaseRefresh(); SessionState.SetString(ModeKey, stopPlay && EditorApplication.isPlaying ? "stopping" : "");
                    if (stopPlay && EditorApplication.isPlaying) EditorApplication.isPlaying = false;
                }
                Debug.LogError((motion ? "Cellblock motion review: " : "Cellblock escape review: ") + error); return;
            }
            Directory.CreateDirectory(Output);
            var report = new Report { passed = false, editor = Application.unityVersion, message = error, errors = new[] { error }, captures = Directory.GetFiles(Output, "*.png").Select(Path.GetFullPath).ToArray() };
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
            ReleaseRefresh();
            SessionState.SetString(ModeKey, stopPlay && EditorApplication.isPlaying ? "stopping" : "");
            if (stopPlay && EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            Debug.LogError("Cellblock presentation review: " + error);
        }
        static void ReleaseRefresh()
        {
            if (!SessionState.GetBool("Cellblock.Review.RefreshHeld", false)) return;
            SessionState.SetBool("Cellblock.Review.RefreshHeld", false); AssetDatabase.AllowAutoRefresh();
        }
        static void SetGameSize(int width, int height)
        {
            Assembly assembly = typeof(EditorWindow).Assembly;
            Type gameViewType = assembly.GetType("UnityEditor.GameView"), sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            Type groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType"), sizeType = assembly.GetType("UnityEditor.GameViewSize"), kindType = assembly.GetType("UnityEditor.GameViewSizeType");
            if (gameViewType == null || sizesType == null || groupType == null || sizeType == null || kindType == null) throw new InvalidOperationException("Unity Game view sizing API is unavailable.");
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            object group = sizesType.GetMethod("GetGroup", flags).Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            Type concreteGroup = group.GetType(); int count = (int)concreteGroup.GetMethod("GetTotalCount", flags).Invoke(group, null), index = -1;
            for (int i = 0; i < count; i++)
            {
                object item = concreteGroup.GetMethod("GetGameViewSize", flags).Invoke(group, new object[] { i });
                int w = (int)sizeType.GetProperty("width", flags).GetValue(item), h = (int)sizeType.GetProperty("height", flags).GetValue(item);
                if (w == width && h == height) { index = i; break; }
            }
            if (index < 0)
            {
                object item = Activator.CreateInstance(sizeType, flags, null, new object[] { Enum.Parse(kindType, "FixedResolution"), width, height, "Cellblock " + width + " × " + height }, null);
                concreteGroup.GetMethod("AddCustomSize", flags).Invoke(group, new[] { item }); index = count;
            }
            EditorWindow window = Resources.FindObjectsOfTypeAll(gameViewType).OfType<EditorWindow>().FirstOrDefault(v => v.name == "Cellblock review");
            if (window == null)
            {
                window = (EditorWindow)ScriptableObject.CreateInstance(gameViewType); window.name = "Cellblock review";
                window.titleContent = new GUIContent("Cellblock · Play"); window.minSize = new Vector2(640, 360); window.Show();
                window.position = new Rect(60, 80, 1640, 960);
            }
            window.Show(); window.Focus();
            foreach (EditorWindow gameView in Resources.FindObjectsOfTypeAll(gameViewType).OfType<EditorWindow>())
            {
                gameViewType.GetProperty("selectedSizeIndex", flags).SetValue(gameView, index); gameView.Repaint();
            }
            SessionState.SetInt(WidthKey, width); SessionState.SetInt(HeightKey, height);
        }
    }
}
