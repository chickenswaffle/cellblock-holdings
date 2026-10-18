using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class GameController
    {
        float SimulationFrameDelta
        {
            get
            {
#if UNITY_EDITOR
                // Encoding review frames can take longer than the captured frame. Only an isolated
                // preview may use this fixed step; player companies always use the ordinary clock.
                if (presentationPreview && Time.captureDeltaTime > 0) return Time.captureDeltaTime;
#endif
                return Time.unscaledDeltaTime;
            }
        }
    }
}
