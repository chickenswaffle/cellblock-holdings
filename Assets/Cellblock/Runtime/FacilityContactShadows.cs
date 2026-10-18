using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cellblock.Unity
{
    // Soft contact shadows: a soft dark ellipse under every visible person. They ground people at night and indoors where the
    // sun casts nothing, stretch along a body lying on the ground, and fade while someone is off their feet (a diving tackle
    // reads as airborne). Each person has one pooled quad, repositioned every frame; four pre-built quads carry the alpha
    // levels, so nothing is rebuilt or allocated per frame. The shader is SRP-batcher compatible, which keeps the many small
    // draws cheap. Claude's (escape overhaul; research brief section 9, the biggest readability win per millisecond).
    // A single combined dynamic mesh never drew in this pipeline, even with Unity's own Unlit material (verified with
    // EscapeCapture diagnostics on 2026-10-18), while finished per-person quads draw reliably.
    public sealed partial class FacilityView
    {
        sealed class ShadowSlot { public Transform Root; public MeshFilter Filter; public int Level = -1; }
        static readonly float[] ShadowLevels = { 0.5f, 0.34f, 0.2f, 0.08f };
        readonly Dictionary<Actor, ShadowSlot> shadowSlots = new Dictionary<Actor, ShadowSlot>();
        readonly List<Actor> shadowRetired = new List<Actor>();
        readonly Mesh[] shadowQuads = new Mesh[4];
        Material shadowMaterial;
        Transform shadowRoot;
        bool shadowsUnavailable;
        int shadowsDrawn;
        public bool ContactShadowsEnabled { get; set; } = true;

        void RefreshContactShadows()
        {
            if (shadowRoot != null) shadowRoot.gameObject.SetActive(ContactShadowsEnabled);
            if (!ContactShadowsEnabled) { shadowsDrawn = 0; return; }
            if (shadowsUnavailable) return;
            if (shadowMaterial == null)
            {
                Shader shader = Resources.Load<Shader>("ContactShadow");
                if (shader == null || !shader.isSupported) { shadowsUnavailable = true; return; }
                shadowMaterial = new Material(shader) { name = "Contact shadows" };
                for (int i = 0; i < shadowQuads.Length; i++) shadowQuads[i] = ShadowQuad(ShadowLevels[i]);
                shadowRoot = new GameObject("Contact shadows / presentation only").transform; shadowRoot.SetParent(transform, false);
            }
            shadowsDrawn = 0;
            foreach (Actor actor in actors.Values)
            {
                bool live = actor.Root.gameObject.activeInHierarchy;
                ShadowSlot slot;
                if (!shadowSlots.TryGetValue(actor, out slot))
                {
                    if (!live) continue;
                    var holder = new GameObject("Contact shadow");
                    holder.transform.SetParent(shadowRoot, false);
                    slot = new ShadowSlot { Root = holder.transform, Filter = holder.AddComponent<MeshFilter>() };
                    slot.Filter.sharedMesh = shadowQuads[0]; slot.Level = 0;
                    MeshRenderer renderer = holder.AddComponent<MeshRenderer>(); renderer.sharedMaterial = shadowMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                    shadowSlots.Add(actor, slot);
                }
                if (slot.Root.gameObject.activeSelf != live) slot.Root.gameObject.SetActive(live);
                if (!live) continue;
                Stride stride = StrideFor(actor);
                // The surface under the person: room floors at 0.13; outdoors the root sits 1.85 cm below the surface.
                float surface = stride.StandY >= RoomStand - 0.001f ? 0.13f : stride.StandY + SoleRest - 0.004f;
                Vector3 waist = actor.Body.position;
                float lying = 1 - Mathf.Clamp01(actor.Model.up.y * 1.15f);
                Vector3 forward = actor.Root.forward; forward.y = 0;
                forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
                // A lying body's length runs along its own up axis projected on the ground.
                Vector3 along = lying > 0.5f ? Flat(actor.Model.up, forward) : forward;
                // Wider than the feet so the soft rim shows around them from any angle.
                float halfLong = Mathf.Lerp(0.36f, 0.95f, lying), halfWide = Mathf.Lerp(0.29f, 0.36f, lying);
                float lift = Mathf.Max(0, waist.y - surface - Mathf.Lerp(0.86f, 0.18f, lying));
                float alpha = 0.5f * Mathf.Clamp01(1 - lift * 1.6f);
                int level = alpha >= 0.42f ? 0 : alpha >= 0.27f ? 1 : alpha >= 0.14f ? 2 : 3;
                if (level != slot.Level) { slot.Filter.sharedMesh = shadowQuads[level]; slot.Level = level; }
                // Just above the escape alert disc (1 cm), so the disc never buries it.
                slot.Root.SetPositionAndRotation(new Vector3(waist.x, surface + 0.014f, waist.z), Quaternion.LookRotation(along, Vector3.up));
                slot.Root.localScale = new Vector3(halfWide * 2, 1, halfLong * 2);
                shadowsDrawn++;
            }
            // People who left the roster (or a world that was replaced) leave their shadow behind once; retire it.
            if (Time.frameCount % 90 == 0)
            {
                shadowRetired.Clear();
                foreach (KeyValuePair<Actor, ShadowSlot> pair in shadowSlots) if (pair.Key.Root == null) shadowRetired.Add(pair.Key);
                foreach (Actor gone in shadowRetired) { if (shadowSlots[gone].Root != null) Destroy(shadowSlots[gone].Root.gameObject); shadowSlots.Remove(gone); }
            }
        }
        static Vector3 Flat(Vector3 v, Vector3 fallback) { v.y = 0; return v.sqrMagnitude > 0.0001f ? v.normalized : fallback; }
        // A finished unit quad on the ground plane; the alpha lives in its vertex colours.
        static Mesh ShadowQuad(float alpha)
        {
            var mesh = new Mesh { name = "Contact shadow " + alpha.ToString("0.00") };
            byte a = (byte)(255 * alpha);
            mesh.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.colors32 = new[] { new Color32(0, 0, 0, a), new Color32(0, 0, 0, a), new Color32(0, 0, 0, a), new Color32(0, 0, 0, a) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            return mesh;
        }
#if UNITY_EDITOR
        public int PreviewContactShadows { get { return shadowsUnavailable ? -1 : shadowsDrawn; } }
        public bool PreviewShadowsShown { set { if (shadowRoot != null) shadowRoot.gameObject.SetActive(value); } }
        public string PreviewContactShadowInfo { get { return shadowsDrawn + " shadows, " + shadowSlots.Count + " pooled, material " + (shadowMaterial == null ? "none" : shadowMaterial.shader.name); } }
#endif
    }
}
