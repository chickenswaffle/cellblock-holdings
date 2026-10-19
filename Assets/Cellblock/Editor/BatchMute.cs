using System;
using UnityEditor;
using UnityEngine;

namespace Cellblock.Editor
{
    // Claude's batch reviews run on the owner's computer: they must never play game audio through its speakers.
    // Every batch-mode editor is muted (the editor master mute, which silences output but still lets reviews read the mix), unless -cellblockAudible is passed.
    [InitializeOnLoad]
    static class BatchMute
    {
        static BatchMute()
        {
            if (!Application.isBatchMode || Array.IndexOf(Environment.GetCommandLineArgs(), "-cellblockAudible") >= 0) return;
            // The toggle may persist as an editor preference: put back what it was when this batch editor quits, so the
            // owner's and Codex's interactive editors are never left muted.
            const string Saved = "Cellblock.BatchMute.Previous";
            if (!SessionState.GetBool(Saved + ".Set", false)) { SessionState.SetBool(Saved + ".Set", true); SessionState.SetBool(Saved, EditorUtility.audioMasterMute); }
            EditorApplication.quitting += () => EditorUtility.audioMasterMute = SessionState.GetBool(Saved, false);
            EditorUtility.audioMasterMute = true;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) EditorUtility.audioMasterMute = true; };
        }
    }
}
