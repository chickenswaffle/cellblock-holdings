using System;
using System.IO;
using UnityEngine;

namespace Cellblock.Unity
{
    [Serializable]
    public sealed class GamePreferences
    {
        public int Version = 1, AutosaveSeconds = 30, FrameRate = 60;
        public float MasterVolume = .65f, InterfaceScale = 1, CameraSensitivity = 1;
        // Direct control (Claude, 2026-10-18): look speed for mouse, controller and touch; vertical invert; and whether the
        // third-person camera swings behind you as you walk (0 follows, 1 stays where you put it).
        public float CharacterLookSpeed = 1;
        public bool InvertLook;
        public int CharacterCamera;
        public bool CharacterCameraFollows { get { return CharacterCamera == 0; } }
        public bool Shadows = true, SmoothCamera = true, ReducedMotion;
        public void Normalize()
        {
            Version = 1;
            if (AutosaveSeconds != 30 && AutosaveSeconds != 60 && AutosaveSeconds != 120) AutosaveSeconds = 30;
            if (FrameRate != 30 && FrameRate != 60) FrameRate = 60;
            MasterVolume = Finite(MasterVolume, .65f, 0, 1); InterfaceScale = Finite(InterfaceScale, 1, 1, 1.3f);
            CameraSensitivity = Finite(CameraSensitivity, 1, .5f, 1.75f);
            CharacterLookSpeed = CharacterLookSpeed <= 0 ? 1 : Finite(CharacterLookSpeed, 1, .5f, 2f);
            if (CharacterCamera != 0 && CharacterCamera != 1) CharacterCamera = 0;
        }
        static float Finite(float value, float fallback, float min, float max) { return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max); }
        internal static GamePreferences Read(string path)
        {
            try { var value = JsonUtility.FromJson<GamePreferences>(File.ReadAllText(path)); if (value == null) return new GamePreferences(); value.Normalize(); return value; }
            catch { return new GamePreferences(); }
        }
        internal void Write(string path)
        {
            Normalize(); Directory.CreateDirectory(Path.GetDirectoryName(path)); string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(this, true));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
        }
    }
}
