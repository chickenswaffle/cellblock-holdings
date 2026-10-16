using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed class MacTrackpad : MonoBehaviour
    {
        public bool Available { get; private set; }
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        [DllImport("CellblockGestures")] static extern int CBStartMagnify();
        [DllImport("CellblockGestures")] static extern double CBConsumeMagnify();
        [DllImport("CellblockGestures")] static extern void CBStopMagnify();
        void OnEnable()
        {
            try { Available = CBStartMagnify() != 0; }
            catch (Exception error) when (error is DllNotFoundException || error is EntryPointNotFoundException)
            { Available = false; Debug.LogWarning("Trackpad pinch plugin unavailable; rebuild with Tools/build-mac-gestures.sh. Scroll zoom remains available."); }
        }
        public float Consume()
        {
            if (!Available) return 0;
            float delta = (float)CBConsumeMagnify();
#if UNITY_EDITOR
            var focused = UnityEditor.EditorWindow.focusedWindow;
            if (focused == null || focused.GetType().Name != "GameView") return 0;
#endif
            return Application.isFocused ? Mathf.Clamp(delta, -0.8f, 2) : 0;
        }
        void OnDisable() { if (Available) CBStopMagnify(); Available = false; }
#else
        public float Consume() { return 0; }
#endif
    }
}
