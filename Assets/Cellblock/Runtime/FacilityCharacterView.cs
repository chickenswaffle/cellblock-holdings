using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        struct CharacterRendererState { public Renderer Renderer; public ShadowCastingMode Shadow; public bool ForceOff; }
        readonly List<CharacterRendererState> characterHeadRenderers = new List<CharacterRendererState>();
        readonly List<CharacterRendererState> characterOverlayRenderers = new List<CharacterRendererState>();
        Actor characterHiddenActor;
        Camera characterMainCamera;
        bool characterFirstPerson, characterRenderHooks, characterRenderHidden;
        Transform characterCeilings;
        World characterCeilingWorld;
        int characterCeilingRevision = -1;
        sealed class CharacterCameraObstacle { public List<Point> Footprint; public float Height; }
        readonly List<CharacterCameraObstacle> characterCameraObstacles = new List<CharacterCameraObstacle>();
        World characterObstacleWorld;
        int characterObstacleRevision = -1, characterObstacleCount = -1;

        public bool TryGetCharacterPose(string id, out Vector3 position, out Vector3 eye, out Vector3 forward)
        {
            Actor actor;
            if (!string.IsNullOrEmpty(id) && actors.TryGetValue(id, out actor) && actor.Root != null && actor.Root.gameObject.activeInHierarchy)
            {
                position = actor.Root.position;
                eye = actor.Head != null ? actor.Head.TransformPoint(new Vector3(0, .02f, .10f)) : actor.Model.position + Vector3.up * 1.65f;
                forward = actor.Root.forward; return true;
            }
            position = eye = Vector3.zero; forward = Vector3.forward; return false;
        }

        public void FaceCharacter(string id, float heading)
        {
            Actor actor;
            if (actors.TryGetValue(id, out actor) && actor.Root != null) actor.Root.rotation = Quaternion.Euler(0, heading, 0);
        }

        public void SetCharacterPresentation(World world, string id, bool firstPerson, Camera mainCamera)
        {
            Actor actor = null;
            if (!string.IsNullOrEmpty(id)) actors.TryGetValue(id, out actor);
            if (!firstPerson || actor == null)
            {
                RestoreCharacterRenderers(); characterFirstPerson = false; characterHiddenActor = null;
                characterHeadRenderers.Clear(); characterOverlayRenderers.Clear();
                if (characterCeilings != null) characterCeilings.gameObject.SetActive(false);
                RemoveCharacterRenderHooks(); return;
            }
            if (characterHiddenActor != actor)
            {
                RestoreCharacterRenderers(); characterHeadRenderers.Clear(); characterOverlayRenderers.Clear(); characterHiddenActor = actor;
                if (actor.Head != null)
                    foreach (Renderer renderer in actor.Head.GetComponentsInChildren<Renderer>(true)) AddCharacterRenderer(characterHeadRenderers, renderer);
                // The builder's hard hat is a separate uncombined body child; it must not occlude their own eye camera.
                foreach (Transform part in actor.Model.GetComponentsInChildren<Transform>(true))
                    if (part.name == "Hard hat" || part.name == "Hat brim" || part.name == "Medical cap")
                        foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>(true)) AddCharacterRenderer(characterHeadRenderers, renderer);
                if (actor.Marker != null) foreach (Renderer renderer in actor.Marker.GetComponentsInChildren<Renderer>(true)) AddCharacterRenderer(characterOverlayRenderers, renderer);
                if (actor.NameTag != null) AddCharacterRenderer(characterOverlayRenderers, actor.NameTag.GetComponent<Renderer>());
            }
            characterFirstPerson = true; characterMainCamera = mainCamera;
            if (!characterRenderHooks)
            {
                RenderPipelineManager.beginCameraRendering += BeginCharacterCameraRender;
                RenderPipelineManager.endCameraRendering += EndCharacterCameraRender;
                characterRenderHooks = true;
            }
            RefreshCharacterCeilings(world);
        }

        static void AddCharacterRenderer(List<CharacterRendererState> list, Renderer renderer)
        {
            if (renderer == null) return;
            list.Add(new CharacterRendererState { Renderer = renderer, Shadow = renderer.shadowCastingMode, ForceOff = renderer.forceRenderingOff });
        }
        void BeginCharacterCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (!characterFirstPerson || camera != characterMainCamera) return;
            foreach (CharacterRendererState state in characterHeadRenderers)
                if (state.Renderer != null) state.Renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            foreach (CharacterRendererState state in characterOverlayRenderers)
                if (state.Renderer != null) state.Renderer.forceRenderingOff = true;
            characterRenderHidden = true;
        }
        void EndCharacterCameraRender(ScriptableRenderContext context, Camera camera)
        { if (camera == characterMainCamera) RestoreCharacterRenderers(); }
        void RestoreCharacterRenderers()
        {
            if (!characterRenderHidden) return;
            foreach (CharacterRendererState state in characterHeadRenderers)
                if (state.Renderer != null) { state.Renderer.shadowCastingMode = state.Shadow; state.Renderer.forceRenderingOff = state.ForceOff; }
            foreach (CharacterRendererState state in characterOverlayRenderers)
                if (state.Renderer != null) state.Renderer.forceRenderingOff = state.ForceOff;
            characterRenderHidden = false;
        }
        void RemoveCharacterRenderHooks()
        {
            if (!characterRenderHooks) return;
            RenderPipelineManager.beginCameraRendering -= BeginCharacterCameraRender;
            RenderPipelineManager.endCameraRendering -= EndCharacterCameraRender;
            characterRenderHooks = false;
        }
        public void DisposeCharacterPresentation()
        {
            RestoreCharacterRenderers(); RemoveCharacterRenderHooks(); characterFirstPerson = false; characterHiddenActor = null;
            characterHeadRenderers.Clear(); characterOverlayRenderers.Clear();
            if (characterCeilings != null) characterCeilings.gameObject.SetActive(false);
        }

        void RefreshCharacterCeilings(World world)
        {
            if (world == null) return;
            if (characterCeilings == null) characterCeilings = Root("Completed room ceilings / first person");
            characterCeilings.gameObject.SetActive(true);
            if (characterCeilingWorld == world && characterCeilingRevision == world.GeometryRevision) return;
            Clear(characterCeilings); characterCeilingWorld = world; characterCeilingRevision = world.GeometryRevision;
            List<List<Point>> rooms = world.Rooms;
            for (int room = 0; room < rooms.Count; room++)
            {
                List<Point> vertices = Geometry.FloorTriangles(rooms, room);
                if (vertices.Count < 3) continue;
                var mesh = new Mesh { name = "Generated first-person ceiling", indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.vertices = vertices.Select(p => V(p, 3.18f)).ToArray();
                // Floor reverses these triangles to face upward; the original winding faces down into the room.
                mesh.triangles = Enumerable.Range(0, vertices.Count).ToArray(); mesh.uv = vertices.Select(p => new Vector2(p.X, p.Y)).ToArray();
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var item = new GameObject("Ceiling " + (room + 1)); item.transform.SetParent(characterCeilings, false);
                item.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = item.AddComponent<MeshRenderer>(); renderer.sharedMaterial = Material("character-ceiling", new Color(.70f, .72f, .69f));
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
            }
        }

        // Cameras have a small clearance volume. Presentation primitives intentionally have no physics colliders,
        // so this samples completed authoritative walls/fixtures instead of relying on an empty Physics scene.
        public Vector3 ClipCharacterCamera(World world, Vector3 target, Vector3 desired)
        {
            Vector3 delta = desired - target; float length = delta.magnitude;
            if (world == null || length < .01f) return desired;
            if (characterObstacleWorld != world || characterObstacleRevision != world.GeometryRevision || characterObstacleCount != world.Data.Fixtures.Count)
            {
                characterCameraObstacles.Clear(); characterObstacleWorld = world; characterObstacleRevision = world.GeometryRevision; characterObstacleCount = world.Data.Fixtures.Count;
                foreach (Fixture fixture in world.Data.Fixtures)
                {
                    FixtureDefinition definition = FixtureCatalog.Get(fixture.Kind);
                    if (definition != null) characterCameraObstacles.Add(new CharacterCameraObstacle { Footprint = Geometry.Footprint(fixture), Height = definition.Height + .24f });
                }
            }
            int count = Mathf.Max(1, Mathf.CeilToInt(length / .10f)); Vector3 previous = target;
            for (int i = 1; i <= count; i++)
            {
                Vector3 point = target + delta * (i / (float)count);
                if (point.y < .30f || Mathf.Abs(point.x) > Geometry.SiteX - .20f || Mathf.Abs(point.z) > Geometry.SiteY - .20f) return previous;
                Point a = new Point(previous.x, previous.z), b = new Point(point.x, point.z);
                if (Mathf.Min(point.y, previous.y) < WallHeight + .13f && Mathf.Max(point.y, previous.y) > .10f && !world.SightLines.Clear(a, b)) return previous;
                foreach (CharacterCameraObstacle obstacle in characterCameraObstacles)
                {
                    if (Mathf.Min(point.y, previous.y) > obstacle.Height) continue;
                    List<Point> footprint = obstacle.Footprint;
                    if (Geometry.Contains(footprint, b)) return previous;
                    for (int side = 0; side < footprint.Count; side++)
                        if (Geometry.SegmentDistance(new Segment(a, b), new Segment(footprint[side], footprint[(side + 1) % footprint.Count])) < .15f) return previous;
                }
                previous = point;
            }
            return desired;
        }
#if UNITY_EDITOR
        public bool PreviewMedicalCapAligned(string id) { Transform badge; Actor actor; if (!postBadges.TryGetValue(id, out badge) || badge == null || !actors.TryGetValue(id, out actor)) return false; Transform cap = badge.Find("Medical cap"); return cap != null && Mathf.Abs(cap.position.y - actor.Head.position.y) < .3f; }
        public int PreviewCharacterCeilingCount { get { return characterCeilings == null || !characterCeilings.gameObject.activeSelf ? 0 : characterCeilings.childCount; } }
        public bool PreviewCharacterFirstPerson { get { return characterFirstPerson && characterHiddenActor != null; } }
#endif
    }
}
