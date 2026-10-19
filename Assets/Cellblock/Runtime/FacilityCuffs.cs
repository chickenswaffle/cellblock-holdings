using System.Collections.Generic;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    // Handcuffs you can see (Claude, 2026-10-19; owner: "you don't even see the cuffs"). Two steel rings close round
    // the wrists, joined by a short chain. During cuffing they follow the published click count (first click: one ring
    // on the wrist, the other hanging from the chain; second click: both closed); afterwards they stay on for as long
    // as the person is held cuffed or escorted. Placed on the skinned body's wrist bones after every other pass each
    // frame, so they sit where the hands actually are in both views. Shared meshes and material; one object per person.
    public sealed partial class FacilityView
    {
        sealed class Cuffs { public Transform Root, Left, Right, Chain; }
        readonly Dictionary<Actor, Cuffs> cuffs = new Dictionary<Actor, Cuffs>();
        Mesh cuffRing, cuffLink; Material cuffSteel;
        const float RingRadius = .043f, RingTube = .011f, ChainLength = .07f;

        int CuffCount(HumanRig rig, World world)
        {
            ActionChoreoFrame frame;
            if (TryGetActionChoreoFrame(rig.Id, out frame) && frame.IsResident && frame.CuffsApplied > 0) return frame.CuffsApplied;
            if (world.HeldResidentId == rig.Id && world.HoldPhase == "Cuffed") return 2;
            if (world.Struggle.Escorted == rig.Id) return 2;
            return 0;
        }

        void PlaceCuffs()
        {
            World world = presentedWorld; if (world == null) return;
            foreach (HumanRig rig in humanRigs)
            {
                if (rig.Actor.Root == null) continue;
                int count = rig.Actor.Root.gameObject.activeInHierarchy ? CuffCount(rig, world) : 0;
                Cuffs c; cuffs.TryGetValue(rig.Actor, out c);
                if (c != null && c.Root == null) { cuffs.Remove(rig.Actor); c = null; } // the person's body was rebuilt
                if (count == 0) { if (c != null && c.Root.gameObject.activeSelf) c.Root.gameObject.SetActive(false); continue; }
                if (rig.HandBones[0] == null || rig.HandBones[1] == null || rig.ElbowBones[0] == null || rig.ElbowBones[1] == null) continue;
                if (c == null) { c = BuildCuffs(rig.Actor); cuffs[rig.Actor] = c; }
                if (!c.Root.gameObject.activeSelf) c.Root.gameObject.SetActive(true);
                Vector3 left = Wrist(rig, 0, out Vector3 leftAxis), right = Wrist(rig, 1, out Vector3 rightAxis);
                Ring(c.Left, left, leftAxis);
                if (count >= 2) Ring(c.Right, right, rightAxis);
                else
                {
                    // Second ring open, hanging from the chain below the first.
                    Vector3 hang = left + Vector3.down * (ChainLength + 2 * RingRadius);
                    Ring(c.Right, hang, Vector3.Cross(leftAxis, Vector3.up).sqrMagnitude > .01f ? Vector3.Cross(leftAxis, Vector3.up).normalized : Vector3.right);
                }
                // Chain between the rings' near edges.
                Vector3 a = c.Left.position, b = c.Right.position, span = b - a;
                float gap = Mathf.Max(.001f, span.magnitude - 2 * RingRadius);
                c.Chain.position = a + span * .5f;
                c.Chain.rotation = Quaternion.FromToRotation(Vector3.up, span.sqrMagnitude > 1e-6f ? span.normalized : Vector3.up);
                c.Chain.localScale = new Vector3(1, Mathf.Min(gap, .18f) / ChainLength, 1);
            }
        }
        // Just above the hand on the forearm, and the forearm's direction there.
        static Vector3 Wrist(HumanRig rig, int side, out Vector3 axis)
        {
            Vector3 hand = rig.HandBones[side].position, elbow = rig.ElbowBones[side].position;
            axis = (hand - elbow).sqrMagnitude > 1e-6f ? (hand - elbow).normalized : Vector3.down;
            return hand - axis * .03f;
        }
        // A flat band like a real cuff: the round tube is stretched along the forearm.
        static void Ring(Transform ring, Vector3 at, Vector3 axis) { ring.position = at; ring.rotation = Quaternion.FromToRotation(Vector3.up, axis); ring.localScale = new Vector3(1, 1.7f, 1); }

        Cuffs BuildCuffs(Actor actor)
        {
            if (cuffRing == null) { cuffRing = Torus(RingRadius, RingTube, 20, 8, "Handcuff ring"); cuffLink = Chain(ChainLength, "Handcuff chain"); }
            if (cuffSteel == null) cuffSteel = Material("handcuff-steel", new Color(.58f, .60f, .63f));
            var root = new GameObject("Handcuffs").transform; root.SetParent(actor.Root, false);
            Transform Part(string name, Mesh mesh)
            {
                var go = new GameObject(name); go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = cuffSteel;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return go.transform;
            }
            return new Cuffs { Root = root, Left = Part("Cuff left", cuffRing), Right = Part("Cuff right", cuffRing), Chain = Part("Cuff chain", cuffLink) };
        }
        // A ring round the Y axis.
        static Mesh Torus(float radius, float tube, int segments, int sides, string name)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments; Vector3 centre = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                for (int j = 0; j <= sides; j++)
                {
                    float b = j * Mathf.PI * 2 / sides; Vector3 normal = Mathf.Cos(b) * centre.normalized + Mathf.Sin(b) * Vector3.up;
                    v.Add(centre + normal * tube); n.Add(normal);
                }
            }
            for (int i = 0; i < segments; i++) for (int j = 0; j < sides; j++)
            {
                int a = i * (sides + 1) + j, b = a + sides + 1;
                t.Add(a); t.Add(a + 1); t.Add(b); t.Add(a + 1); t.Add(b + 1); t.Add(b); // outward-facing
            }
            var mesh = new Mesh { name = name }; mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0); mesh.RecalculateBounds(); return mesh;
        }
        // Three small links stacked along Y, centred on the origin, total length `length`.
        static Mesh Chain(float length, string name)
        {
            var combine = new List<CombineInstance>(); Mesh link = Torus(length / 6, .0035f, 10, 6, "link");
            for (int i = 0; i < 3; i++)
                combine.Add(new CombineInstance { mesh = link, transform = Matrix4x4.TRS(new Vector3(0, (i - 1) * length / 3, 0), Quaternion.Euler(90, i % 2 * 90, 0), new Vector3(1, 1, 1.6f)) });
            var mesh = new Mesh { name = name }; mesh.CombineMeshes(combine.ToArray(), true, true); mesh.RecalculateBounds();
            Release(link); return mesh;
        }
    }
}
