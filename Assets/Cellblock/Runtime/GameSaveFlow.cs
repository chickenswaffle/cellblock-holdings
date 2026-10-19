using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Cellblock.Unity
{
    public sealed partial class CompanySave
    {
        public string Name, Location, Institution, Problem = "";
        public int Day, Residents;
        public long CashCents;
        public DateTime SavedUtc;
        public bool Available, RecoveryNeeded, Latest;
    }
    public sealed partial class GameController
    {
        public GamePreferences Preferences { get; private set; } = new GamePreferences();
        public string SaveProblem { get; private set; } = "";
        public string PreferencesProblem { get; private set; } = "";
        public DateTime LastSavedUtc { get; private set; }
        public bool RecoveredSave { get; private set; }
        string savedJson = "";
#if UNITY_EDITOR
        string reviewSaveRoot;
#endif
        string CompanyDirectory { get {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(reviewSaveRoot)) return Path.Combine(reviewSaveRoot, "companies");
#endif
            return Path.Combine(Application.persistentDataPath, "companies");
        } }
        string PreferencesPath { get { return Path.Combine(Path.GetDirectoryName(CompanyDirectory), "preferences.json"); } }
        static bool SaveName(string name) { return name != null && Regex.IsMatch(name, "^company-[a-f0-9]{32}\\.json$"); }
        void InitializePreferences() { Preferences = GamePreferences.Read(PreferencesPath); ApplyPreferences(); }
        public void ApplyPreferences()
        {
            Preferences.Normalize(); AudioListener.volume = Preferences.MasterVolume;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = Preferences.FrameRate;
            if (cameraRig != null) cameraRig.GetUniversalAdditionalCameraData().renderShadows = Preferences.Shadows;
            if (view != null) { view.ReducedMotion = Preferences.ReducedMotion; view.ContactShadowsEnabled = Preferences.Shadows; }
            if (hud != null) hud.ApplyInterfacePreferences();
            PresentationQuality.Apply(Preferences.GraphicsQuality); PerformanceOverlay.Show(Preferences.PerformanceOverlay);
            FacilitySoundscape.Levels(Preferences.AmbienceVolume, Preferences.EffectsVolume, Preferences.InterfaceVolume);
        }
        public bool SavePreferences()
        {
            ApplyPreferences();
            try { Preferences.Write(PreferencesPath); PreferencesProblem = ""; return true; }
            catch (Exception e) { PreferencesProblem = "Changes apply now, but settings could not be stored. Check available storage and folder access."; Debug.LogWarning("Settings save: " + e.Message); return false; }
        }
        public string SaveStatus
        {
            get
            {
                if (presentationPreview) return "REVIEW / SAVES ISOLATED";
                if (SaveProblem != "") return "SAVE NEEDS ATTENTION";
                if (LastSavedUtc == default(DateTime)) return "AUTOSAVE ON · " + Preferences.AutosaveSeconds + "s";
                double seconds = Math.Max(0, (DateTime.UtcNow - LastSavedUtc).TotalSeconds);
                return seconds < 10 ? "SAVED JUST NOW" : seconds < 60 ? "SAVED " + (int)seconds + "s AGO" : "SAVED " + (int)(seconds / 60) + "m AGO";
            }
        }
        void TickAutosave()
        {
            if (presentationPreview || appPaused || unfocused) return;
            autosave += Time.unscaledDeltaTime;
            if (autosave >= Preferences.AutosaveSeconds) { autosave = 0; Save(false); }
        }
        bool TrySaveState(string path, out State state)
        {
            state = null;
            try { state = JsonUtility.FromJson<State>(File.ReadAllText(path)); new World(state); return true; }
            catch { state = null; return false; }
        }
        string ReadPointer()
        {
            try { string name = File.ReadAllText(PointerPath).Trim(); return SaveName(name) ? name : null; } catch { return null; }
        }
        void WritePointer(string name)
        {
            if (!SaveName(name)) throw new InvalidDataException("Invalid holding identifier.");
            Directory.CreateDirectory(SaveDirectory); string temp = PointerPath + ".tmp"; File.WriteAllText(temp, name);
            if (File.Exists(PointerPath)) File.Replace(temp, PointerPath, null); else File.Move(temp, PointerPath);
        }
        List<CompanySave> FindCompanySaves()
        {
            var result = new List<CompanySave>();
            if (!Directory.Exists(SaveDirectory)) return result;
            try
            {
                var names = Directory.GetFiles(SaveDirectory, "company-*.json*").Select(Path.GetFileName)
                    .Select(n => n.EndsWith(".bak", StringComparison.Ordinal) ? n.Substring(0, n.Length - 4) : n).Where(SaveName).Distinct();
                foreach (string name in names)
                {
                    string path = Path.Combine(SaveDirectory, name); State state;
                    bool primary = TrySaveState(path, out state), backup = false;
                    if (!primary) backup = TrySaveState(path + ".bak", out state);
                    var save = new CompanySave { FileName = name, Name = "Holding " + name.Substring(8, 6), Available = primary || backup, RecoveryNeeded = backup };
                    save.SavedUtc = File.GetLastWriteTimeUtc(primary ? path : backup ? path + ".bak" : path);
                    if (state != null)
                    {
                        ContractOffer offer = Cellblock.Core.World.Offers.FirstOrDefault(o => o.Id == state.ContractId);
                        if (state.Mode != null && state.Mode.ExplicitTerms && GameModes.TypeOf(state) != null) offer = GameModes.Offer(GameModes.TypeOf(state), GameModes.LocationOf(state));
                        save.Name = offer == null ? save.Name : offer.Name; save.Location = offer == null ? "Saved holding" : offer.City;
                        var type = GameModes.TypeOf(state); save.Institution = type == null ? "Established holding" : type.Name;
                        save.Day = state.Minute / 1440 + 1; save.CashCents = state.CashCents; save.Residents = state.Residents.Count;
                        save.Title = save.Name + " · " + save.Location + " · Day " + save.Day + " · " + GameHud.Money(save.CashCents);
                    }
                    else { save.Problem = "The holding and its backup could not be read. Both files are preserved."; save.Title = save.Name + " · needs recovery"; }
                    result.Add(save);
                }
            }
            catch (Exception e) { SaveProblem = "The save library could not be read. Check storage and folder access, then try again."; Debug.LogWarning("Save library: " + e.Message); if (hud != null) hud.ShowSaveProblem(SaveProblem); }
            result = result.OrderByDescending(s => s.Available).ThenByDescending(s => s.SavedUtc).ThenBy(s => s.FileName, StringComparer.Ordinal).ToList();
            string pointer = ReadPointer(); CompanySave latest = result.FirstOrDefault(s => s.Available && s.FileName == pointer) ?? result.FirstOrDefault(s => s.Available);
            if (latest != null) latest.Latest = true;
            return result;
        }
        public CompanySave ContinueSave() { return FindCompanySaves().FirstOrDefault(s => s.Latest); }
        bool SaveCompany(bool feedback)
        {
            if (World == null || presentationPreview) return true;
            string previousProblem = SaveProblem;
            try
            {
                if (!Regex.IsMatch(saveId, "^[a-f0-9]{32}$")) throw new InvalidDataException("Invalid holding identifier.");
                Directory.CreateDirectory(SaveDirectory);
                string name = "company-" + saveId + ".json", path = Path.Combine(SaveDirectory, name), json = JsonUtility.ToJson(World.Data, true);
                if (json != savedJson || !File.Exists(path) || SaveProblem != "")
                {
                    string temp = path + ".tmp"; File.WriteAllText(temp, json);
                    if (RecoveredSave && File.Exists(path))
                    {
                        // Never replace the known-good backup with the damaged primary during recovery.
                        File.Copy(path, path + ".damaged-" + DateTime.UtcNow.Ticks, false);
                        File.Replace(temp, path, null);
                    }
                    else if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
                    savedJson = json; LastSavedUtc = DateTime.UtcNow; RecoveredSave = false;
                }
                WritePointer(name); SaveProblem = ""; if (hud != null) hud.ClearSaveProblem(previousProblem); autosave = 0;
                if (feedback) Message("Holding saved. Autosave is on every " + Preferences.AutosaveSeconds + " seconds.");
                return true;
            }
            catch (Exception e)
            {
                SaveProblem = "Saving could not finish. Keep this session open, check storage and folder access, then choose Retry save."; Debug.LogWarning("Save detail: " + e.Message);
                Message(SaveProblem); if (hud != null) hud.ShowSaveProblem(SaveProblem); Debug.LogWarning(SaveProblem); return false;
            }
        }
#if UNITY_EDITOR
        public void PreviewSaveSandbox(string directory)
        {
            if (!presentationPreview) throw new InvalidOperationException("A disposable preview is required before redirecting saves.");
            reviewSaveRoot = Path.GetFullPath(directory); Directory.CreateDirectory(reviewSaveRoot);
            presentationPreview = false; savedJson = ""; LastSavedUtc = default(DateTime); SaveProblem = ""; RecoveredSave = false;
            Preferences = new GamePreferences(); ApplyPreferences();
        }
        public string PreviewSaveDirectory { get { return SaveDirectory; } }
        public void PreviewAutosaveDue() { bool oldFocus = unfocused; unfocused = false; autosave = Preferences.AutosaveSeconds; TickAutosave(); unfocused = oldFocus; }
        public bool PreviewHasSaveSandbox { get { return reviewSaveRoot != null; } }
        public void PreviewUnloadHolding() { if (reviewSaveRoot == null) throw new InvalidOperationException("No save sandbox."); World = null; savedJson = ""; saveId = ""; }
        public bool PreviewPreferencesReload() { string before = JsonUtility.ToJson(Preferences); Preferences = GamePreferences.Read(PreferencesPath); ApplyPreferences(); return before == JsonUtility.ToJson(Preferences); }
        public void PreviewEndSaveSandbox()
        {
            presentationPreview = true; reviewSaveRoot = null; savedJson = ""; SaveProblem = ""; LastSavedUtc = default(DateTime); RecoveredSave = false;
            InitializePreferences();
        }
#endif
    }
}
