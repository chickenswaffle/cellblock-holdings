using System.Collections.Generic;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cellblock.Unity
{
    // Presentation for sanitation: running water at a shower or basin while someone washes there, a wet tray, and a
    // yellow wet-floor sign at any shower, basin or toilet that is out of service. Pieces are pooled by fixture id and
    // reused; materials and pieces are destroyed with the world they showed. The simulation owns every state.
    public sealed class FacilitySanitationView : MonoBehaviour
    {
        sealed class Piece { public Transform Root, Water, Wet, Sign; public Transform[] Streaks; public float Seed; public bool Seen; }
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        readonly Dictionary<string, Piece> pieces = new Dictionary<string, Piece>();
        readonly HashSet<string> running = new HashSet<string>();
        readonly List<string> stale = new List<string>();
        Transform root;
        World presented;
        float clock;

        public void Initialize() { root = new GameObject("Sanitation").transform; root.SetParent(transform, false); }

        void OnDestroy()
        {
            foreach (Material m in materials.Values) if (m != null) Destroy(m);
            materials.Clear();
        }

        Material Material(string name, Color color, bool glow)
        {
            Material value;
            if (materials.TryGetValue(name, out value)) return value;
            Material template = Resources.Load<Material>(glow ? "LuminaireMaterial" : "BuildingMaterial");
            if (template == null || template.shader == null)
                throw new System.InvalidOperationException("BuildingMaterial is missing. Run Cellblock > Prepare project before Play or building a player.");
            value = new Material(template) { name = "sanitation-" + name, color = color, enableInstancing = true };
            if (value.HasProperty("_Smoothness")) value.SetFloat("_Smoothness", name == "water" || name == "wet" ? 0.92f : 0.25f);
            if (glow && value.HasProperty("_EmissionColor")) { value.EnableKeyword("_EMISSION"); value.SetColor("_EmissionColor", color * 0.55f); }
            materials.Add(name, value); return value;
        }
        Transform Shape(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Quaternion rotation, string material, Color color, bool glow = false)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.transform.SetParent(parent, false); item.transform.localPosition = position; item.transform.localRotation = rotation; item.transform.localScale = scale;
            Renderer renderer = item.GetComponent<Renderer>(); renderer.sharedMaterial = Material(material, color, glow);
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = !glow;
            Destroy(item.GetComponent<Collider>()); return item.transform;
        }

        public void Refresh(World world, float dt)
        {
            if (world == null || root == null) return;
            if (!ReferenceEquals(world, presented))
            {
                for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
                pieces.Clear(); presented = world;
            }
            if (world.Data.Sanitation == null) return;
            clock += Mathf.Min(dt, 0.05f) * Mathf.Max(0.35f, Mathf.Sqrt(Mathf.Max(1, world.Data.Speed)));
            running.Clear();
            foreach (Resident r in world.Data.Residents)
            {
                if (!world.Washing(r)) continue;
                WashAssignment a = world.WashAssignmentOf(r);
                if (a != null) running.Add(a.FixtureId);
            }
            foreach (Piece p in pieces.Values) p.Seen = false;
            foreach (Fixture f in world.Data.Fixtures)
            {
                if (!SanitationRules.HygieneFixture(f.Kind)) continue;
                bool broken = world.OutOfService(f), water = running.Contains(f.Id);
                Piece piece;
                if (!pieces.TryGetValue(f.Id, out piece))
                {
                    if (!broken && !water) continue;
                    piece = Build(f); pieces.Add(f.Id, piece);
                }
                piece.Seen = true;
                piece.Root.localPosition = FacilityView.V(f.Position); piece.Root.localRotation = Quaternion.Euler(0, f.Rotation, 0);
                if (piece.Sign != null) piece.Sign.gameObject.SetActive(broken);
                if (piece.Water != null) piece.Water.gameObject.SetActive(water && !broken);
                if (piece.Wet != null) piece.Wet.gameObject.SetActive((water || world.Data.Minute - LastUse(world, f) < 20) && !broken);
                if (water && piece.Streaks != null) Animate(piece, f.Kind == "Shower");
            }
            stale.Clear();
            foreach (var pair in pieces) if (!pair.Value.Seen) stale.Add(pair.Key);
            foreach (string id in stale) { Destroy(pieces[id].Root.gameObject); pieces.Remove(id); }
        }
        // The last wash at a fixture, from the residents who washed there today (the tray dries for 20 minutes).
        static int LastUse(World world, Fixture f)
        {
            int last = -100000;
            foreach (Resident r in world.Data.Residents)
            {
                WashAssignment a = world.WashAssignmentOf(r);
                if (a != null && a.FixtureId == f.Id && r.LastWashMinute > last) last = r.LastWashMinute;
            }
            return last;
        }

        // Fixture-local frame (matching FacilityView's models): +z is the service wall, the access side is -z.
        Piece Build(Fixture f)
        {
            var piece = new Piece { Root = new GameObject("Sanitation " + f.Id).transform, Seed = (f.Id.GetHashCode() & 1023) / 1023f };
            piece.Root.SetParent(root, false);
            Color water = new Color(0.72f, 0.86f, 0.95f), wet = new Color(0.36f, 0.45f, 0.50f), sign = new Color(0.95f, 0.78f, 0.12f);
            if (f.Kind == "Shower")
            {
                piece.Water = new GameObject("Running water").transform; piece.Water.SetParent(piece.Root, false);
                piece.Streaks = new Transform[10];
                for (int i = 0; i < piece.Streaks.Length; i++)
                    piece.Streaks[i] = Shape(PrimitiveType.Cylinder, piece.Water, Vector3.zero, new Vector3(0.022f, 0.22f, 0.022f), Quaternion.identity, "water", water, true);
                Shape(PrimitiveType.Cylinder, piece.Water, new Vector3(0, 0.094f, 0.10f), new Vector3(0.34f, 0.003f, 0.34f), Quaternion.identity, "splash", new Color(0.82f, 0.92f, 0.98f), true);
                piece.Wet = Shape(PrimitiveType.Cylinder, piece.Root, new Vector3(0, 0.092f, 0.06f), new Vector3(0.78f, 0.002f, 0.74f), Quaternion.identity, "wet", wet);
            }
            else if (f.Kind == "Sink")
            {
                piece.Water = new GameObject("Running tap").transform; piece.Water.SetParent(piece.Root, false);
                piece.Streaks = new Transform[2];
                for (int i = 0; i < piece.Streaks.Length; i++)
                    piece.Streaks[i] = Shape(PrimitiveType.Cylinder, piece.Water, Vector3.zero, new Vector3(0.014f, 0.035f, 0.014f), Quaternion.identity, "water", water, true);
            }
            // A folding wet-floor sign in front of the fixture: two leaning yellow boards.
            piece.Sign = new GameObject("Out of service").transform; piece.Sign.SetParent(piece.Root, false);
            piece.Sign.localPosition = new Vector3(0.18f, 0, f.Kind == "Shower" ? -0.72f : -0.62f);
            for (int side = -1; side <= 1; side += 2)
                Shape(PrimitiveType.Cube, piece.Sign, new Vector3(0, 0.30f, side * 0.07f), new Vector3(0.30f, 0.60f, 0.012f), Quaternion.Euler(side * 13, 0, 0), "sign", sign);
            Shape(PrimitiveType.Cube, piece.Sign, new Vector3(0, 0.33f, -0.135f), new Vector3(0.20f, 0.05f, 0.004f), Quaternion.Euler(-13, 0, 0), "sign-mark", new Color(0.12f, 0.12f, 0.12f));
            return piece;
        }
        // Falling streaks loop from the head (or spout) down to the tray (or bowl), each at its own phase.
        void Animate(Piece piece, bool shower)
        {
            float top = shower ? 1.97f : 1.03f, bottom = shower ? 0.11f : 0.93f, span = top - bottom;
            for (int i = 0; i < piece.Streaks.Length; i++)
            {
                float phase = Mathf.Repeat(clock * (shower ? 1.9f : 2.6f) + i / (float)piece.Streaks.Length + piece.Seed, 1);
                float angle = (i * 2.399f + piece.Seed * 6.28f), radius = shower ? 0.03f + 0.07f * ((i * 37) % 7) / 7f : 0.004f;
                Vector3 origin = shower ? new Vector3(0, 0, 0.10f) : new Vector3(0, 0, 0.02f);
                piece.Streaks[i].localPosition = origin + new Vector3(Mathf.Cos(angle) * radius, top - phase * span, Mathf.Sin(angle) * radius);
            }
        }
    }
}
