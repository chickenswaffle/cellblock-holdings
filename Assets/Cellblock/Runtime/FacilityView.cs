using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cellblock.Unity
{
    // Presentation is derived from the simulation. None of these decorative meshes participate
    // in routing, selection, space approval, or the identity of an editable component.
    public sealed partial class FacilityView : MonoBehaviour
    {
        partial void ResidentVisibility(World world, Resident resident, ref bool visible);
        partial void RefreshJustice(World world, bool force);
        partial void PrepareResidentPose(Actor actor, Resident resident, World world);
        partial void AnimateResidentLife(Actor actor, Resident resident, World world);
        Transform structures, planned, ghost, people;
        sealed class Actor
        {
            public Transform Root, Model, Body, Head, Blink, Marker, SecurityProp, WatchProp;
            public TextMesh NameTag;
            public Transform[] Limbs;
            public float Speed, Gait, TurnRate;
            public List<Point> RenderRoute;
            public Point RouteTarget;
            public int RouteIndex, RouteRevision = -1;
            public float NextRouteRefresh;
            public bool Staff, Seen, PoseArms;
            public Resident Motion;
            public Transform WorkTool, Cargo, Workbench;
            public Transform[] Forearms, Hands, Tools, Knees, Feet, WorkChips;
            public float PoseClock;
            public string BuilderSoundTask = "";
            public int LastBuilderImpact, BuilderSoundTool;
        }
        readonly Dictionary<string, Actor> actors = new Dictionary<string, Actor>();
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        readonly HashSet<Texture2D> generatedTextures = new HashSet<Texture2D>();
        int revision = -1, previewHash = int.MinValue;
        string selectedResidentId = "";
        World presentedWorld;
        public bool FullHeight;
        public float WallHeight { get { return (FullHeight ? 3.05f : 1.15f) + 0.14f; } }
        static readonly Color Teal = new Color(0.14f, 0.78f, 0.74f);
        static readonly Color Ochre = new Color(0.91f, 0.64f, 0.22f);
        static readonly Color Steel = new Color(0.24f, 0.30f, 0.31f);
        static readonly Color Concrete = new Color(0.70f, 0.72f, 0.68f);
        public static Vector3 V(Point point, float height = 0) { return new Vector3(point.X, height, point.Y); }

        Material Material(string name, Color color)
        {
            Material value;
            if (materials.TryGetValue(name, out value)) return value;
            string surface = Surface(name);
            bool emission=name=="ghost"||name=="planned"||name=="handle"||name=="lamp"||name=="ceiling-on"||name=="alarm"||name=="door-outline";
            bool detailed=surface!=null&&surface!="grass";
            Material template = (emission ? null : SurfaceTemplate(name)) ?? Resources.Load<Material>(emission?"LuminaireMaterial":detailed?"DetailedBuildingMaterial":"BuildingMaterial");
            if (template == null || template.shader == null)
                throw new System.InvalidOperationException("BuildingMaterial is missing. Run Cellblock > Prepare project before Play or building a player.");
            value = new Material(template) { name = name, color = color, enableInstancing = true };
            bool metal = name.Contains("steel") || name.Contains("metal") || name == "cap";
            bool glass = name == "glass" || name == "screen";
            if (value.HasProperty("_Smoothness")) value.SetFloat("_Smoothness", glass ? 0.74f : metal ? 0.48f : name.StartsWith("skin") ? .28f : surface=="fabric" ? .12f : .22f);
            if (value.HasProperty("_SpecColor")) value.SetColor("_SpecColor", metal ? new Color(0.3f, 0.33f, 0.34f) : new Color(0.08f, 0.08f, 0.08f));
            if(value.HasProperty("_Metallic"))value.SetFloat("_Metallic",metal?.50f:0);
            if (surface != null) value.mainTexture = Texture(surface);
            if(detailed){value.SetTexture("_BumpMap",SurfaceNormal(surface));value.SetFloat("_BumpScale",.65f);}
            if (name == "grass" || name == "site-grass")
            {
                Texture2D meadow = Resources.Load<Texture2D>("Textures/GroundMeadow");
                if (meadow != null)
                {
                    value.mainTexture = meadow; value.mainTextureScale = new Vector2(0.25f, 0.25f);
                    value.color = name == "site-grass" ? new Color(0.88f, 0.94f, 0.85f) : new Color(0.74f, 0.83f, 0.72f);
                }
            }
            if (emission)
            {
                value.EnableKeyword("_EMISSION");
                value.SetColor("_EmissionColor", color * (name == "lamp" ? 0.8f : 0.18f));
            }
            if (!emission) DressSurface(value, name);
            materials[name] = value;
            return value;
        }

        static string Surface(string name)
        {
            if (name == "grass" || name == "site-grass" || name.StartsWith("leaf")) return "grass";
            if (name == "asphalt") return "asphalt";
            if (name == "brick") return "brick";
            if (name.StartsWith("floor")) return "tile";
            if (name == "wood" || name == "bark") return "wood";
            if (name == "gravel") return "gravel";
            if (name == "plaster" || name == "paving" || name == "foundation" || name == "cap") return "concrete";
            if (name == "blanket" || name == "linen" || name == "trousers" || name.StartsWith("uniform")) return "fabric";
            return null;
        }

        Texture2D Texture(string surface)
        {
            Texture2D texture;
            if (textures.TryGetValue(surface, out texture)) return texture;
            const int size = 128;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Cellblock " + surface, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                // Fixed integer noise keeps the site stable when companies or scenes are reloaded.
                uint seed = unchecked((uint)(x * 73856093 ^ y * 19349663 ^ 83492791));
                seed ^= seed >> 13; seed *= 1274126177u;
                float noise = (seed & 255) / 255f;
                float shade = 0.92f + noise * 0.13f;
                if (surface == "brick")
                {
                    int row = y / 16, offset = (row % 2) * 32;
                    bool mortar = y % 16 < 2 || (x + offset) % 64 < 2;
                    shade = mortar ? 0.64f : 0.85f + noise * 0.22f + (row % 3) * 0.025f;
                }
                else if (surface == "tile")
                    shade = x % 64 < 1 || y % 64 < 1 ? 0.76f : 0.96f + noise * 0.05f;
                else if (surface == "grass")
                    shade = 0.84f + noise * 0.25f + Mathf.Sin(x * 0.19f + Mathf.Sin(y * 0.15f) * 2) * 0.035f;
                else if (surface == "asphalt") shade = 0.82f + noise * 0.26f;
                else if (surface == "gravel") shade = 0.69f + noise * 0.38f;
                else if (surface == "wood") shade = 0.85f + Mathf.Sin(x * 0.35f + Mathf.Sin(y * 0.09f) * 0.8f) * 0.08f + noise * 0.09f;
                else if (surface == "fabric") shade = (x % 2 == 0 || y % 2 == 0 ? 0.91f : 1f) + noise * 0.03f;
                byte channel = (byte)Mathf.RoundToInt(Mathf.Clamp01(shade) * 255);
                pixels[y * size + x] = new Color32(channel, channel, channel, 255);
            }
            texture.SetPixels32(pixels); texture.Apply(true, true); textures[surface] = texture; generatedTextures.Add(texture);
            return texture;
        }

        public void Initialize()
        {
            structures = Root("Operational building"); planned = Root("Approved construction · not operational");
            ghost = Root("Unapproved plan preview"); people = Root("Residents");
            var sun = new GameObject("Late afternoon sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false); sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(43, -32, 0); sun.intensity = 1.5f;
            sun.color = new Color(1f, 0.93f, 0.83f); sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.86f; sun.shadowBias = 0.015f; sun.shadowNormalBias = 0.10f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.69f, 0.76f, 0.80f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.51f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.32f, 0.29f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(0.68f, 0.74f, 0.76f);
            RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 115; RenderSettings.fogEndDistance = 210;
            Transform site = Root("Site and landscape · presentation");
            Landscape(site);
            PublicFrontage(site);
            Combine(site);
        }

        Transform propertyBoundary;
        void Landscape(Transform site)
        {
            float siteX = Geometry.SiteX, siteY = Geometry.SiteY;
            float roadZ = -siteY - 4.8f;
            Box(site, "Meadow", new Vector3(0, -0.30f, 0), new Vector3(200, 0.4f, 180), "grass", new Color(0.36f, 0.43f, 0.31f));
            // Keep local lighting local: a single property-wide renderer selects only four
            // additional lights for the whole plot, hiding distant operational floodlights.
            for(int row=0;row<4;row++)for(int column=0;column<5;column++)
            {
                Transform sector=Root("Ground lighting sector "+column+"/"+row);
                Box(sector,"Buildable ground",new Vector3(-siteX+6+column*12,-.08f,-siteY+6+row*12),new Vector3(12,.12f,12),"site-grass",new Color(.40f,.47f,.34f));Combine(sector);
            }
            Box(site, "Public access road · outside property", new Vector3(0, -0.055f, roadZ), new Vector3(200, 0.13f, 7.4f), "asphalt", new Color(0.26f, 0.29f, 0.30f));
            Box(site, "Driveway", new Vector3(-2, 0, (roadZ - 8.7f) / 2), new Vector3(5.8f, 0.12f, -8.7f - roadZ), "asphalt", new Color(0.29f, 0.31f, 0.32f));
            Box(site, "Entrance walk", new Vector3(-2, 0.035f, -10), new Vector3(6.8f, 0.12f, 3.5f), "paving", new Color(0.65f, 0.66f, 0.61f));
            Box(site, "Building apron", new Vector3(-6, 0.015f, 0), new Vector3(18.6f, 0.13f, 18.6f), "paving", new Color(0.66f, 0.67f, 0.62f));
            Box(site, "Visitor parking", new Vector3(-24.5f, 0.005f, -9.6f), new Vector3(9, 0.12f, 13), "asphalt", new Color(0.30f, 0.32f, 0.32f));
            Box(site, "Parking access aisle", new Vector3(-18.2f, 0.005f, -10.6f), new Vector3(3.1f, 0.12f, 16.9f), "asphalt", new Color(0.30f, 0.32f, 0.32f));
            Box(site, "Parking connection", new Vector3(-10.1f, 0.005f, -18.1f), new Vector3(19.4f, 0.12f, 3.1f), "asphalt", new Color(0.30f, 0.32f, 0.32f));
            Box(site, "Staff and contractor parking apron", new Vector3(-16.6f, 0.006f, -19.2f), new Vector3(23, 0.12f, 8.8f), "asphalt", new Color(0.28f, 0.30f, 0.31f));
            Box(site, "Returning shuttle aisle", new Vector3(-14.1f, 0.007f, -14), new Vector3(27.8f, 0.12f, 2.5f), "asphalt", new Color(0.28f, 0.30f, 0.31f));
            for (int i = 0; i < 7; i++)
            {
                Point bay = World.ParkingBay(i);
                for (int side = -1; side <= 1; side += 2)
                    Box(site, "Contractor bay stripe", V(bay + new Point(side * 1.6f, 0), 0.077f), new Vector3(0.08f, 0.012f, 5), "paint", new Color(0.84f, 0.84f, 0.75f));
                Box(site, "Contractor wheel stop", V(bay + new Point(0, -2.1f), 0.15f), new Vector3(1.5f, 0.18f, 0.18f), "paving", Concrete);
                Box(site, "Bay end line", V(bay + new Point(0, 2.5f), 0.078f), new Vector3(3.1f, 0.012f, 0.07f), "paint", new Color(0.84f, 0.84f, 0.75f));
            }
            for (int i = -15; i <= 15; i++)
                Box(site, "Center line", new Vector3(i * 6, 0.021f, roadZ), new Vector3(2.8f, 0.015f, 0.10f), "paint", new Color(0.84f, 0.81f, 0.62f));
            for (int side = -1; side <= 1; side += 2)
            {
                Box(site, "Road edge", new Vector3(0, 0.021f, roadZ + side * 3.35f), new Vector3(200, 0.015f, 0.10f), "paint", new Color(0.78f, 0.79f, 0.73f));
                Box(site, "Gravel verge", new Vector3(0, -0.04f, roadZ + side * 4.2f), new Vector3(200, 0.10f, 0.7f), "gravel", new Color(0.54f, 0.55f, 0.48f));
            }
            for (int i = 0; i < 5; i++)
            {
                Box(site, "Parking stripe", new Vector3(-24.3f, 0.077f, -15 + i * 2.65f), new Vector3(9.2f, 0.012f, 0.08f), "paint", new Color(0.79f, 0.80f, 0.73f));
                Box(site, "Wheel stop", new Vector3(-28, 0.14f, -13.8f + i * 2.65f), new Vector3(0.16f, 0.22f, 1.6f), "paving", Concrete);
            }
            for (int i = 0; i < 6; i++)
                Box(site, "Entry paving joint", new Vector3(-5.35f + i * 1.34f, 0.10f, -10), new Vector3(0.014f, 0.01f, 3.5f), "joint", new Color(0.47f, 0.50f, 0.46f));
            // The visible property boundary is the same rectangle used by quote validation.
            // Leave its six metre entrance open; this fence is presentation, not a route barrier.
            if(propertyBoundary==null)propertyBoundary=Root("Property boundary");
            Fence(propertyBoundary, new Point(-siteX, siteY), new Point(siteX, siteY));
            Fence(propertyBoundary, new Point(-siteX, -siteY), new Point(-5, -siteY));
            Fence(propertyBoundary, new Point(1, -siteY), new Point(siteX, -siteY));
            Fence(propertyBoundary, new Point(-siteX, -siteY), new Point(-siteX, siteY));
            Fence(propertyBoundary, new Point(siteX, -siteY), new Point(siteX, siteY));
            Combine(propertyBoundary);
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -5.05f : 1.05f, z = i % 2 == 0 ? -siteY + 0.2f : -siteY + 1.8f;
                Cylinder(site, "Entry bollard", new Vector3(x, 0.53f, z), new Vector3(0.16f, 0.54f, 0.16f), "steel", Steel);
                Cylinder(site, "Reflective band", new Vector3(x, 0.82f, z), new Vector3(0.165f, 0.035f, 0.165f), "paint", new Color(0.85f, 0.77f, 0.44f));
            }
            for (int i = 0; i < 30; i++)
            {
                float x = i < 12 ? -35 - i % 3 * 4.8f : i < 24 ? 38 + i % 3 * 5.1f : -25 + (i - 24) * 10;
                float z = i < 24 ? -12 + i % 12 * 4.2f : 33 + i % 2 * 4;
                Tree(site, new Vector3(x, 0, z), 0.85f + i % 4 * 0.12f, i);
            }
            for (int i = 0; i < 14; i++)
            {
                float x = i < 7 ? -siteX - 2.2f : siteX + 2.2f;
                Sphere(site, "Native planting", new Vector3(x + Mathf.Sin(i * 3) * 0.8f, 0.32f, -13 + i % 7 * 5), new Vector3(1.5f, 0.8f, 1.2f), "leaf-light", new Color(0.34f, 0.42f, 0.28f));
            }
            SiteLamp(site, new Vector3(-siteX + 1.4f, 0, -siteY + 2)); SiteLamp(site, new Vector3(siteX - 1.4f, 0, -siteY + 2));
            SiteLamp(site, new Vector3(-siteX + 1.4f, 0, siteY - 2)); SiteLamp(site, new Vector3(siteX - 1.4f, 0, siteY - 2));
            Vehicle(site, new Vector3(-25.7f, 0.10f, -11), 90, new Color(0.36f, 0.43f, 0.45f), "vehicle-slate");
            Vehicle(site, new Vector3(-25.7f, 0.10f, -5.7f), 90, new Color(0.71f, 0.72f, 0.66f), "vehicle-cream");
            Transform sign = new GameObject("Property entrance marker").transform; sign.SetParent(site, false);
            sign.localPosition = new Vector3(5.7f, 0, -siteY + 0.55f);
            Box(sign, "Stone plinth", new Vector3(0, 0.52f, 0), new Vector3(3.6f, 1.05f, 0.5f), "plaster", new Color(0.67f, 0.69f, 0.64f));
            Box(sign, "Dark inset", new Vector3(0, 0.67f, -0.26f), new Vector3(2.8f, 0.46f, 0.025f), "steel", Steel);
            for (int i = 0; i < 3; i++) Box(sign, "Inset architectural line", new Vector3(-0.68f + i * 0.36f, 0.67f, -0.28f), new Vector3(0.12f, 0.28f, 0.025f), "paint", new Color(0.81f, 0.77f, 0.60f));
        }

        void Tree(Transform parent, Vector3 position, float scale, int variation)
        {
            Transform tree = new GameObject("Mature tree").transform; tree.SetParent(parent, false); tree.localPosition = position; tree.localScale = Vector3.one * scale;
            Cylinder(tree, "Trunk", new Vector3(0, 1.85f, 0), new Vector3(0.30f, 1.85f, 0.30f), "bark", new Color(0.33f, 0.28f, 0.21f));
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 1.9f + variation;
                Sphere(tree, "Canopy", new Vector3(Mathf.Cos(angle) * 0.85f, 3.8f + i % 2 * 0.75f, Mathf.Sin(angle) * 0.85f),
                    new Vector3(2.6f, 2.6f + i % 2 * 0.8f, 2.8f), variation % 2 == 0 ? "leaf" : "leaf-light",
                    variation % 2 == 0 ? new Color(0.26f, 0.37f, 0.24f) : new Color(0.34f, 0.42f, 0.28f));
            }
        }

        void SiteLamp(Transform parent, Vector3 position)
        {
            Transform lamp = new GameObject("Perimeter lighting").transform; lamp.SetParent(parent, false); lamp.localPosition = position;
            Cylinder(lamp, "Pole", new Vector3(0, 2.35f, 0), new Vector3(0.09f, 2.35f, 0.09f), "steel", Steel);
            Box(lamp, "Luminaire", new Vector3(0.22f, 4.7f, 0), new Vector3(0.8f, 0.12f, 0.38f), "steel", Steel);
            Box(lamp, "Lens", new Vector3(0.26f, 4.63f, 0), new Vector3(0.62f, 0.025f, 0.27f), "lamp", new Color(0.94f, 0.89f, 0.69f));
        }

        void Vehicle(Transform parent, Vector3 position, float yaw, Color paint, string paintName)
        {
            Transform car = new GameObject("Parked service vehicle").transform; car.SetParent(parent, false); car.localPosition = position; car.localRotation = Quaternion.Euler(0, yaw, 0);
            Box(car, "Lower body", new Vector3(0, 0.63f, 0), new Vector3(1.8f, 0.65f, 4.2f), paintName, paint);
            Box(car, "Cabin", new Vector3(0, 1.14f, 0.12f), new Vector3(1.62f, 0.66f, 2.2f), paintName, paint);
            Transform front = Box(car, "Windscreen", new Vector3(0, 1.15f, -0.98f), new Vector3(1.46f, 0.53f, 0.04f), "glass", new Color(0.20f, 0.31f, 0.34f));
            front.localRotation = Quaternion.Euler(-12, 0, 0);
            Box(car, "Rear glass", new Vector3(0, 1.16f, 1.24f), new Vector3(1.42f, 0.48f, 0.035f), "glass", new Color(0.20f, 0.31f, 0.34f));
            for (int side = -1; side <= 1; side += 2)
            {
                Box(car, "Side window", new Vector3(side * 0.82f, 1.17f, 0.11f), new Vector3(0.025f, 0.43f, 1.86f), "glass", new Color(0.20f, 0.31f, 0.34f));
                Box(car, "Door pillar", new Vector3(side * 0.84f, 1.17f, 0.12f), new Vector3(0.05f, 0.55f, 0.08f), paintName, paint);
                Box(car, "Mirror", new Vector3(side * 0.98f, 1.02f, -0.75f), new Vector3(0.20f, 0.15f, 0.27f), "steel", Steel);
                for (int axle = -1; axle <= 1; axle += 2)
                {
                    Transform wheel = Cylinder(car, "Tyre", new Vector3(side * 0.87f, 0.39f, axle * 1.33f), new Vector3(0.68f, 0.12f, 0.68f), "rubber", new Color(0.10f, 0.12f, 0.12f));
                    wheel.localRotation = Quaternion.Euler(0, 0, 90);
                    Transform hub = Cylinder(car, "Wheel hub", new Vector3(side * 1.0f, 0.39f, axle * 1.33f), new Vector3(0.36f, 0.018f, 0.36f), "light-steel", new Color(0.53f, 0.57f, 0.57f));
                    hub.localRotation = Quaternion.Euler(0, 0, 90);
                }
                Box(car, "Headlamp", new Vector3(side * 0.60f, 0.73f, -2.12f), new Vector3(0.44f, 0.18f, 0.04f), "lamp", new Color(0.94f, 0.89f, 0.69f));
                Box(car, "Tail lamp", new Vector3(side * 0.64f, 0.72f, 2.12f), new Vector3(0.31f, 0.17f, 0.035f), "tail-light", new Color(0.51f, 0.18f, 0.12f));
            }
            Box(car, "Front bumper", new Vector3(0, 0.40f, -2.12f), new Vector3(1.76f, 0.16f, 0.12f), "steel", Steel);
        }

        Transform Root(string name)
        {
            var value = new GameObject(name).transform; value.SetParent(transform, false); return value;
        }
        void Clear(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject; child.SetActive(false);
                foreach (MeshFilter filter in child.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null && filter.sharedMesh.name.StartsWith("Generated ")) Destroy(filter.sharedMesh);
                Destroy(child);
            }
        }
        Transform Shape(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, string material, Color color)
        {
            GameObject item = GameObject.CreatePrimitive(type); item.name = name;
            item.transform.SetParent(parent, false); item.transform.localPosition = position; item.transform.localScale = scale;
            Renderer renderer = item.GetComponent<Renderer>(); renderer.sharedMaterial = Material(material, color);
            renderer.shadowCastingMode = material == "ghost" || material == "handle" ? ShadowCastingMode.Off : ShadowCastingMode.On;
            Destroy(item.GetComponent<Collider>()); return item.transform;
        }
        Transform Box(Transform parent, string name, Vector3 position, Vector3 scale, string material, Color color)
        { return SoftEdge(name,material,scale)?SoftBox(parent,name,position,scale,material,color):Shape(PrimitiveType.Cube, parent, name, position, scale, material, color); }
        Transform Sphere(Transform parent, string name, Vector3 position, Vector3 scale, string material, Color color)
        { return Shape(PrimitiveType.Sphere, parent, name, position, scale, material, color); }
        Transform Cylinder(Transform parent, string name, Vector3 position, Vector3 scale, string material, Color color)
        { return Shape(PrimitiveType.Cylinder, parent, name, position, scale, material, color); }
        Transform Capsule(Transform parent, string name, Vector3 position, Vector3 scale, string material, Color color)
        { return Shape(PrimitiveType.Capsule, parent, name, position, scale, material, color); }

        // Detailed scenery is baked by material when it changes, rather than submitting every
        // fence wire, bed leg, and wall trim as a separate renderer on a phone.
        void Combine(Transform root)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (MeshFilter filter in filters)
            {
                Renderer renderer = filter.GetComponent<Renderer>();
                if (renderer == null || filter.sharedMesh == null) continue;
                List<CombineInstance> group;
                if (!groups.TryGetValue(renderer.sharedMaterial, out group)) groups.Add(renderer.sharedMaterial, group = new List<CombineInstance>());
                group.Add(new CombineInstance { mesh = filter.sharedMesh, transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix });
            }
            var combined = new List<Mesh>();
            foreach (KeyValuePair<Material, List<CombineInstance>> group in groups)
            {
                var mesh = new Mesh { name = "Generated combined " + group.Key.name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(group.Value.ToArray(), true, true);
                // World sized UVs avoid stretched brickwork and concrete on long/curved walls.
                if (group.Key.mainTexture != null)
                {
                    Vector3[] vertices = mesh.vertices, normals = mesh.normals; var uv = new Vector2[vertices.Length];
                    for (int i = 0; i < uv.Length; i++)
                    {
                        Vector3 p = vertices[i], n = normals[i];
                        uv[i] = Mathf.Abs(n.y) > 0.65f ? new Vector2(p.x, p.z) : Mathf.Abs(n.z) > Mathf.Abs(n.x) ? new Vector2(p.x, p.y) : new Vector2(p.z, p.y);
                    }
                    mesh.uv = uv; mesh.RecalculateTangents();
                }
                combined.Add(mesh);
            }
            // Source meshes are no longer referenced after CombineMeshes copies their data.
            Clear(root);
            int index = 0;
            foreach (KeyValuePair<Material, List<CombineInstance>> group in groups)
            {
                var item = new GameObject(group.Key.name + " surfaces"); item.transform.SetParent(root, false);
                item.AddComponent<MeshFilter>().sharedMesh = combined[index++];
                MeshRenderer renderer = item.AddComponent<MeshRenderer>(); renderer.sharedMaterial = group.Key;
                renderer.shadowCastingMode = group.Key.name == "ghost" || group.Key.name == "handle" ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }
        }

        void Fence(Transform parent, Point a, Point b)
        {
            float length = Geometry.Distance(a, b); int posts = Mathf.CeilToInt(length / 3);
            for (int i = 0; i <= posts; i++)
            {
                Point p = a + (b - a) * (i / (float)posts);
                Cylinder(parent, "Galvanized perimeter post", V(p, 1.34f), new Vector3(0.08f, 1.34f, 0.08f), "fence-steel", new Color(0.43f, 0.48f, 0.46f));
                Sphere(parent, "Post cap", V(p, 2.70f), new Vector3(0.11f, 0.07f, 0.11f), "fence-steel", new Color(0.43f, 0.48f, 0.46f));
            }
            for (int i = 0; i <= 4; i++) Beam(parent, a, b, 0.16f + i * 0.60f, i == 4 ? 0.055f : 0.018f, 0.025f, "fence-steel", new Color(0.43f, 0.48f, 0.46f));
            int wires = Mathf.CeilToInt(length / 0.28f);
            for (int i = 0; i < wires; i++)
            {
                Point p = a + (b - a) * (i / (float)wires);
                Box(parent, "Wire mesh", V(p, 1.35f), new Vector3(0.012f, 2.4f, 0.012f), "fence-steel", new Color(0.43f, 0.48f, 0.46f));
            }
        }
        Transform Beam(Transform parent, Point a, Point b, float y, float height, float thickness, string material, Color color)
        {
            Point d = b - a;
            Transform beam = Box(parent, "Architectural element", V((a + b) * 0.5f, y), new Vector3(d.Length + 0.012f, height, thickness), material, color);
            beam.localRotation = Quaternion.Euler(0, -Mathf.Atan2(d.Y, d.X) * Mathf.Rad2Deg, 0); return beam;
        }

        void WallView(Transform parent, Wall wall, string palette = null)
        {
            bool preview = palette != null;
            float height = preview ? 0.30f : FullHeight ? 3.05f : 1.15f;
            Color color = palette == "ghost" ? Teal : palette == "planned" ? Ochre : wall.Finish == 1 ? new Color(0.56f, 0.37f, 0.28f) : Concrete;
            string material = palette ?? (wall.Finish == 1 ? "brick" : "plaster");
            foreach (Segment segment in Geometry.Segments(wall, true))
            {
                Beam(parent, segment.A, segment.B, height / 2 + 0.14f, height, preview ? 0.18f : 0.24f, material, color);
                if (preview)
                {
                    Beam(parent, segment.A, segment.B, 0.17f, 0.035f, 0.40f, material, color);
                    if (palette == "planned") Beam(parent, segment.A, segment.B, 0.66f, 0.065f, 0.045f, "planned", Ochre);
                }
                else
                {
                    Beam(parent, segment.A, segment.B, 0.19f, 0.12f, 0.28f, "foundation", new Color(0.43f, 0.47f, 0.45f));
                    Beam(parent, segment.A, segment.B, height + 0.16f, 0.075f, 0.29f, "cap", new Color(0.47f, 0.52f, 0.51f));
                    if (!wall.Curved && wall.Finish == 0)
                    {
                        Point span = segment.B - segment.A;
                        int panels = Mathf.FloorToInt(span.Length / 2.7f);
                        for (int i = 1; i <= panels; i++)
                        {
                            Transform joint = Box(parent, "Precast panel reveal", V(segment.A + span * (i / (panels + 1f)), height / 2 + 0.14f),
                                new Vector3(0.012f, height - 0.10f, 0.244f), "joint", new Color(0.47f, 0.50f, 0.46f));
                            joint.localRotation = Quaternion.Euler(0, -Mathf.Atan2(span.Y, span.X) * Mathf.Rad2Deg, 0);
                        }
                    }
                    if (FullHeight)
                    {
                        Beam(parent, segment.A, segment.B, 2.98f, 0.15f, 0.29f, "cap", new Color(0.47f, 0.52f, 0.51f));
                        if (!wall.Curved && (segment.B - segment.A).Length > 3.8f) Window(parent, segment);
                    }
                }
            }
            if (preview)
            {
                foreach (Point p in new[] { wall.A, wall.B })
                {
                    Cylinder(parent, "Setting out pin", V(p, 0.45f), new Vector3(0.085f, 0.34f, 0.085f), material, color);
                    Cylinder(parent, "Survey marker", V(p, 0.16f), new Vector3(0.48f, 0.012f, 0.48f), material, color);
                }
            }
            if (wall.Door < 0) return;
            float length = Mathf.Max(0.001f, Geometry.Length(wall));
            Point center = Geometry.OnLength(wall, wall.Door);
            Point a = Geometry.OnLength(wall, Mathf.Max(0, wall.Door - wall.DoorWidth / (2 * length)));
            Point b = Geometry.OnLength(wall, Mathf.Min(1, wall.Door + wall.DoorWidth / (2 * length)));
            float frameHeight = preview ? 0.45f : FullHeight ? 2.35f : height;
            Color doorColor = preview ? color : new Color(0.26f, 0.35f, 0.35f);
            string frameMaterial = palette ?? "door-steel";
            foreach (Point p in new[] { a, b })
                Cylinder(parent, "Door jamb", V(p, frameHeight / 2 + 0.14f), new Vector3(0.10f, frameHeight / 2, 0.10f), frameMaterial, doorColor);
            Beam(parent, a, b, 0.155f, 0.035f, 0.31f, preview ? palette : "threshold", preview ? color : new Color(0.47f, 0.51f, 0.50f));
            if (!preview && FullHeight)
            {
                Beam(parent, a, b, 2.49f, 0.24f, 0.25f, frameMaterial, doorColor);
                Beam(parent, a, b, 2.89f, 0.48f, 0.24f, material, color);
            }
            // Opening remains visibly open: residents follow the simulation's door clearance.
            if (!preview)
            {
                Point direction = (b - a) * (1 / Mathf.Max(0.001f, (b - a).Length));
                Point normal = new Point(-direction.Y, direction.X);
                Beam(parent, a + normal * 0.11f, a + normal * 0.11f + direction * 0.07f, 0.86f, 0.20f, 0.08f, "light-steel", new Color(0.53f, 0.57f, 0.57f));
                Beam(parent, center - direction * 0.18f, center + direction * 0.18f, 0.177f, 0.012f, 0.03f, "paint", new Color(0.79f, 0.80f, 0.73f));
            }
        }

        void Window(Transform parent, Segment segment)
        {
            Point d = segment.B - segment.A;
            Point tangent = d * (1 / d.Length), normal = new Point(-tangent.Y, tangent.X);
            Point center = (segment.A + segment.B) * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                Point offset = normal * (side * 0.128f), a = center - tangent * 0.68f + offset, b = center + tangent * 0.68f + offset;
                Beam(parent, a, b, 2.17f, 0.58f, 0.025f, "glass", new Color(0.20f, 0.31f, 0.34f));
                Beam(parent, a, b, 1.85f, 0.08f, 0.11f, "cap", new Color(0.47f, 0.52f, 0.51f));
                Beam(parent, a, b, 2.49f, 0.06f, 0.07f, "door-steel", new Color(0.26f, 0.35f, 0.35f));
                for (int i = 0; i <= 5; i++)
                    Cylinder(parent, "Window grille", V(a + (b - a) * (i / 5f), 2.18f), new Vector3(0.025f, 0.31f, 0.025f), "light-steel", new Color(0.53f, 0.57f, 0.57f));
            }
        }

        void Floor(List<List<Point>> rooms, int index, Transform parent = null)
        {
            List<Point> vertices = Geometry.FloorTriangles(rooms, index);
            int[] triangles = Enumerable.Range(0, vertices.Count).ToArray();
            for (int i = 0; i < triangles.Length; i += 3) { int swap = triangles[i + 1]; triangles[i + 1] = triangles[i + 2]; triangles[i + 2] = swap; }
            var mesh = new Mesh { name = "Generated room floor" };
            mesh.vertices = vertices.Select(p => V(p, 0.13f)).ToArray(); mesh.triangles = triangles;
            mesh.uv = vertices.Select(p => new Vector2(p.X, p.Y)).ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var floor = new GameObject("Room " + (index + 1)); floor.transform.SetParent(parent != null ? parent : structures, false);
            floor.AddComponent<MeshFilter>().sharedMesh = mesh;
            floor.AddComponent<MeshRenderer>().sharedMaterial = Material("floor-" + index % 3,
                index % 3 == 0 ? new Color(0.63f, 0.69f, 0.67f) : index % 3 == 1 ? new Color(0.73f, 0.71f, 0.64f) : new Color(0.68f, 0.72f, 0.71f));
        }

        void FixtureView(Transform parent, Fixture fixture, string palette = null)
        {
            FixtureDefinition definition = FixtureCatalog.Get(fixture.Kind);
            if (definition == null) return;
            var root = new GameObject(fixture.Kind + " · " + fixture.Id).transform; root.SetParent(parent, false);
            root.localPosition = V(fixture.Position, 0.14f); root.localRotation = Quaternion.Euler(0, fixture.Rotation, 0);
            bool preview = palette != null;
            Color steel = Steel;
            string metal = "steel";
            if (preview)
            {
                Color tint = palette == "ghost" ? Teal : Ochre;
                Box(root, "Exact proposed footprint", new Vector3(0, 0.025f, 0), new Vector3(definition.Width, 0.025f, definition.Depth), palette, tint);
            }
            SecurityFixture(root, fixture);
            if (fixture.Kind == "CCTV")
            {
                Cylinder(root, "Camera mast", new Vector3(0, 1.25f, 0), new Vector3(0.085f, 1.25f, 0.085f), metal, steel);
                Box(root, "Mounting plate", new Vector3(0, 0.04f, 0), new Vector3(0.32f, 0.08f, 0.32f), metal, steel);
                Box(root, "Weatherproof camera", new Vector3(0, 2.48f, 0.15f), new Vector3(0.24f, 0.18f, 0.43f), "camera-case", new Color(0.79f, 0.81f, 0.76f));
                Sphere(root, "Lens", new Vector3(0, 2.48f, 0.38f), new Vector3(0.13f, 0.13f, 0.04f), "screen", new Color(0.045f, 0.08f, 0.09f));
                Sphere(root, "Status indicator", new Vector3(0.08f, 2.44f, 0.37f), Vector3.one * 0.025f, "handle", Teal);
            }
            if (fixture.Kind == "Bed")
            {
                Box(root, "Welded frame", new Vector3(0, 0.32f, 0), new Vector3(0.94f, 0.13f, 1.98f), metal, steel);
                Box(root, "Mattress", new Vector3(0, 0.47f, 0), new Vector3(0.86f, 0.20f, 1.95f), "linen", new Color(0.81f, 0.82f, 0.74f));
                Box(root, "Folded blanket", new Vector3(0, 0.585f, 0.30f), new Vector3(0.87f, 0.045f, 1.20f), "blanket", new Color(0.34f, 0.44f, 0.46f));
                Box(root, "Blanket fold", new Vector3(0, 0.615f, -0.22f), new Vector3(0.88f, 0.035f, 0.14f), "blanket-fold", new Color(0.42f, 0.52f, 0.53f));
                Sphere(root, "Pillow", new Vector3(0, 0.62f, -0.69f), new Vector3(0.62f, 0.15f, 0.37f), "linen", new Color(0.81f, 0.82f, 0.74f));
                for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                {
                    Cylinder(root, "Tubular bed post", new Vector3(x * 0.42f, 0.35f, z * 0.93f), new Vector3(0.052f, 0.35f, 0.052f), metal, steel);
                    Cylinder(root, "Foot pad", new Vector3(x * 0.42f, 0.027f, z * 0.93f), new Vector3(0.10f, 0.026f, 0.10f), "rubber", new Color(0.10f, 0.12f, 0.12f));
                }
                for (int z = -1; z <= 1; z += 2)
                {
                    Box(root, "End rail", new Vector3(0, z < 0 ? 0.75f : 0.60f, z * 0.93f), new Vector3(0.89f, 0.055f, 0.052f), metal, steel);
                    Box(root, "End panel", new Vector3(0, 0.50f, z * 0.95f), new Vector3(0.77f, 0.18f, 0.035f), metal, steel);
                }
                Box(root, "Underbed storage", new Vector3(0, 0.16f, 0.12f), new Vector3(0.65f, 0.21f, 0.80f), "storage", new Color(0.45f, 0.49f, 0.47f));
                Box(root, "Storage handle", new Vector3(0, 0.18f, 0.53f), new Vector3(0.19f, 0.035f, 0.025f), "light-steel", new Color(0.53f, 0.57f, 0.57f));
            }
            else if (fixture.Kind == "Table" || fixture.Kind == "Desk")
            {
                bool table = fixture.Kind == "Table";
                Box(root, "Worktop edge", new Vector3(0, 0.76f, 0), new Vector3(1.83f, 0.11f, 1.03f), "steel", Steel);
                Box(root, "Laminate surface", new Vector3(0, 0.825f, 0), new Vector3(1.78f, 0.025f, 0.98f), "wood", new Color(0.64f, 0.58f, 0.45f));
                for (int x = -1; x <= 1; x += 2)
                {
                    Box(root, "Pedestal", new Vector3(x * 0.66f, 0.39f, 0), new Vector3(0.09f, 0.73f, 0.62f), metal, steel);
                    Box(root, "Floor anchor", new Vector3(x * 0.66f, 0.055f, 0), new Vector3(0.38f, 0.07f, 0.80f), metal, steel);
                }
                if (table)
                {
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        Box(root, "Fixed bench", new Vector3(0, 0.43f, sign * 0.9f), new Vector3(1.8f, 0.11f, 0.34f), "wood", new Color(0.64f, 0.58f, 0.45f));
                        for (int x = -1; x <= 1; x += 2)
                        {
                            Box(root, "Bench pedestal", new Vector3(x * 0.65f, 0.22f, sign * 0.9f), new Vector3(0.10f, 0.40f, 0.22f), metal, steel);
                            Box(root, "Bench brace", new Vector3(x * 0.65f, 0.20f, sign * 0.45f), new Vector3(0.10f, 0.08f, 0.9f), metal, steel);
                        }
                        Box(root, "Meal tray", new Vector3(sign * 0.47f, 0.852f, sign * 0.19f), new Vector3(0.42f, 0.025f, 0.30f), "light-steel", new Color(0.53f, 0.57f, 0.57f));
                        Cylinder(root, "Cup", new Vector3(sign * 0.69f, 0.91f, -sign * 0.23f), new Vector3(0.085f, 0.065f, 0.085f), "linen", new Color(0.81f, 0.82f, 0.74f));
                    }
                }
                else
                {
                    Box(root, "Drawer cabinet", new Vector3(0.56f, 0.38f, 0), new Vector3(0.52f, 0.66f, 0.82f), "storage", new Color(0.45f, 0.49f, 0.47f));
                    for (int i = 0; i < 3; i++) Box(root, "Drawer handle", new Vector3(0.56f, 0.22f + i * 0.20f, -0.42f), new Vector3(0.20f, 0.024f, 0.025f), "light-steel", new Color(0.53f, 0.57f, 0.57f));
                    Box(root, "Monitor base", new Vector3(-0.20f, 0.86f, 0.24f), new Vector3(0.30f, 0.04f, 0.21f), metal, steel);
                    Box(root, "Monitor stand", new Vector3(-0.20f, 1.00f, 0.29f), new Vector3(0.055f, 0.28f, 0.045f), metal, steel);
                    Box(root, "Monitor", new Vector3(-0.20f, 1.15f, 0.29f), new Vector3(0.56f, 0.36f, 0.045f), "rubber", new Color(0.10f, 0.12f, 0.12f));
                    Box(root, "Display", new Vector3(-0.20f, 1.15f, 0.263f), new Vector3(0.50f, 0.29f, 0.015f), "screen", new Color(0.19f, 0.32f, 0.35f));
                    Box(root, "Keyboard", new Vector3(-0.20f, 0.86f, -0.16f), new Vector3(0.45f, 0.035f, 0.15f), "rubber", new Color(0.10f, 0.12f, 0.12f));
                    Box(root, "Paperwork", new Vector3(0.54f, 0.857f, -0.13f), new Vector3(0.26f, 0.025f, 0.34f), "linen", new Color(0.81f, 0.82f, 0.74f));
                    Cylinder(root, "Tucked stool", new Vector3(-0.29f, 0.43f, -0.36f), new Vector3(0.37f, 0.055f, 0.37f), "steel", Steel);
                    Cylinder(root, "Stool pedestal", new Vector3(-0.29f, 0.23f, -0.36f), new Vector3(0.065f, 0.20f, 0.065f), "light-steel", new Color(0.53f, 0.57f, 0.57f));
                }
            }
            else DetailedFixture(root, fixture.Kind);
            if (!preview && parent == structures) AddPresentationDetail(root, fixture.Kind);
            if (preview)
            {
                Material tint = Material(palette, palette == "ghost" ? Teal : Ochre);
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = tint;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        void DetailedFixture(Transform root, string kind)
        {
            if (kind == "Payphone") { BuildPayphone(root); return; }
            Color ceramic = new Color(0.84f, 0.86f, 0.80f), paleMetal = new Color(0.55f, 0.61f, 0.59f);
            Color timber = new Color(0.64f, 0.58f, 0.45f), dark = new Color(0.10f, 0.12f, 0.12f);
            if (kind == "Chair" || kind == "Bench")
            {
                bool bench = kind == "Bench"; float width = bench ? 1.64f : 0.53f;
                for (int i = 0; i < 3; i++)
                    Box(root, "Seat slat", new Vector3(0, 0.45f, -0.15f + i * 0.15f), new Vector3(width, 0.065f, 0.125f), "wood", timber);
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * (bench ? 0.70f : 0.22f);
                    Box(root, "Back upright", new Vector3(x, 0.45f, 0.24f), new Vector3(0.045f, 0.84f, 0.045f), "steel", Steel);
                    Box(root, "Front leg", new Vector3(x, 0.23f, -0.22f), new Vector3(0.045f, 0.43f, 0.045f), "steel", Steel);
                    Box(root, "Seat brace", new Vector3(x, 0.39f, 0), new Vector3(0.045f, 0.05f, 0.51f), "steel", Steel);
                    Box(root, "Foot pads", new Vector3(x, 0.025f, 0), new Vector3(0.08f, 0.045f, 0.55f), "rubber", dark);
                }
                for (int i = 0; i < 2; i++)
                    Box(root, "Back slat", new Vector3(0, 0.66f + i * 0.14f, 0.25f), new Vector3(width, 0.11f, 0.055f), "wood", timber);
            }
            else if (kind == "Locker" || kind == "FilingCabinet")
            {
                bool locker = kind == "Locker"; float height = locker ? 1.76f : 1.28f;
                Box(root, "Cabinet carcass", new Vector3(0, height / 2 + 0.035f, 0.01f), new Vector3(0.55f, height, locker ? 0.55f : 0.60f), "cabinet-steel", paleMetal);
                int panels = locker ? 1 : 4;
                float depth = locker ? -0.277f : -0.302f;
                for (int i = 0; i < panels; i++)
                {
                    float y = locker ? 0.91f : 0.22f + i * 0.29f;
                    Box(root, locker ? "Locker door" : "Drawer front", new Vector3(0, y, depth), new Vector3(0.50f, locker ? 1.65f : 0.25f, 0.023f), "storage", new Color(0.45f, 0.49f, 0.47f));
                    Box(root, "Recessed handle", new Vector3(locker ? 0.16f : 0, locker ? 0.97f : y + 0.03f, depth - 0.019f), new Vector3(locker ? 0.045f : 0.18f, locker ? 0.15f : 0.024f, 0.015f), "steel", Steel);
                    if (!locker)
                        Box(root, "Record label", new Vector3(0, y - 0.045f, depth - 0.014f), new Vector3(0.13f, 0.045f, 0.008f), "linen", ceramic);
                }
                if (locker)
                {
                    for (int i = 0; i < 5; i++)
                        Box(root, "Ventilation slot", new Vector3(0, 1.40f + i * 0.04f, -0.292f), new Vector3(0.30f, 0.012f, 0.009f), "steel", Steel);
                    Box(root, "Identity plate", new Vector3(-0.10f, 1.18f, -0.294f), new Vector3(0.16f, 0.07f, 0.012f), "linen", ceramic);
                }
            }
            else if (kind == "Toilet")
            {
                Box(root, "Concealed cistern", new Vector3(0, 0.54f, 0.32f), new Vector3(0.54f, 0.56f, 0.20f), "ceramic", ceramic);
                Capsule(root, "Toilet pedestal", new Vector3(0, 0.22f, -0.05f), new Vector3(0.28f, 0.21f, 0.35f), "ceramic", ceramic);
                Sphere(root, "Ceramic bowl", new Vector3(0, 0.39f, -0.08f), new Vector3(0.58f, 0.29f, 0.69f), "ceramic", ceramic);
                Sphere(root, "Seat rim", new Vector3(0, 0.49f, -0.09f), new Vector3(0.54f, 0.07f, 0.64f), "linen", ceramic);
                Sphere(root, "Recessed bowl", new Vector3(0, 0.52f, -0.11f), new Vector3(0.32f, 0.018f, 0.41f), "basin-shadow", new Color(0.32f, 0.40f, 0.39f));
                Box(root, "Flush control", new Vector3(0.16f, 0.80f, 0.20f), new Vector3(0.07f, 0.035f, 0.025f), "light-steel", paleMetal);
                for (int side = -1; side <= 1; side += 2)
                    Cylinder(root, "Floor bolt", new Vector3(side * 0.19f, 0.075f, 0.08f), new Vector3(0.045f, 0.015f, 0.045f), "light-steel", paleMetal);
            }
            else if (kind == "Sink")
            {
                Capsule(root, "Basin pedestal", new Vector3(0, 0.39f, 0.04f), new Vector3(0.19f, 0.37f, 0.22f), "ceramic", ceramic);
                Box(root, "Wash basin", new Vector3(0, 0.83f, -0.015f), new Vector3(0.67f, 0.15f, 0.53f), "ceramic", ceramic);
                Box(root, "Basin recess", new Vector3(0, 0.91f, -0.055f), new Vector3(0.47f, 0.014f, 0.33f), "basin-shadow", new Color(0.32f, 0.40f, 0.39f));
                Box(root, "Basin interior", new Vector3(0, 0.919f, -0.055f), new Vector3(0.36f, 0.01f, 0.23f), "ceramic", ceramic);
                Box(root, "Splash guard", new Vector3(0, 0.99f, 0.265f), new Vector3(0.70f, 0.19f, 0.055f), "ceramic", ceramic);
                Cylinder(root, "Tap riser", new Vector3(0, 0.99f, 0.14f), new Vector3(0.045f, 0.075f, 0.045f), "light-steel", paleMetal);
                Box(root, "Tap spout", new Vector3(0, 1.05f, 0.08f), new Vector3(0.045f, 0.035f, 0.16f), "light-steel", paleMetal);
                Cylinder(root, "Drain", new Vector3(0, 0.927f, -0.07f), new Vector3(0.055f, 0.008f, 0.055f), "steel", Steel);
            }
            else if (kind == "Shower")
            {
                Box(root, "Raised shower tray", new Vector3(0, 0.045f, 0), new Vector3(0.98f, 0.08f, 0.98f), "ceramic", ceramic);
                Box(root, "Tiled service wall", new Vector3(0, 1.09f, 0.465f), new Vector3(0.98f, 2.08f, 0.055f), "floor-shower", new Color(0.69f, 0.76f, 0.73f));
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(root, "Privacy screen", new Vector3(side * 0.46f, 1.09f, 0.04f), new Vector3(0.035f, 1.0f, 0.83f), "frosted-glass", new Color(0.65f, 0.74f, 0.72f));
                    Box(root, "Screen post", new Vector3(side * 0.46f, 1.05f, -0.39f), new Vector3(0.045f, 1.98f, 0.045f), "light-steel", paleMetal);
                }
                Cylinder(root, "Shower pipe", new Vector3(0, 1.36f, 0.408f), new Vector3(0.035f, 0.66f, 0.035f), "light-steel", paleMetal);
                Box(root, "Shower arm", new Vector3(0, 2.02f, 0.26f), new Vector3(0.035f, 0.035f, 0.32f), "light-steel", paleMetal);
                Cylinder(root, "Shower head", new Vector3(0, 2.0f, 0.10f), new Vector3(0.18f, 0.025f, 0.18f), "light-steel", paleMetal);
                Sphere(root, "Mixer valve", new Vector3(0, 0.97f, 0.40f), new Vector3(0.12f, 0.12f, 0.07f), "light-steel", paleMetal);
                Box(root, "Tray drain", new Vector3(0.23f, 0.091f, 0.20f), new Vector3(0.17f, 0.012f, 0.17f), "steel", Steel);
                for (int i = 0; i < 4; i++) Box(root, "Drain slot", new Vector3(0.17f + i * 0.04f, 0.099f, 0.20f), new Vector3(0.012f, 0.005f, 0.12f), "rubber", dark);
            }
            else if (kind == "ServingCounter")
            {
                Box(root, "Service cabinet", new Vector3(0, 0.46f, 0), new Vector3(1.94f, 0.86f, 0.78f), "cabinet-steel", paleMetal);
                Box(root, "Stainless worktop", new Vector3(0, 0.92f, 0), new Vector3(1.98f, 0.075f, 0.83f), "light-steel", paleMetal);
                for (int i = 0; i < 3; i++)
                {
                    float x = -0.61f + i * 0.61f;
                    Box(root, "Inset serving tray", new Vector3(x, 0.966f, -0.10f), new Vector3(0.49f, 0.018f, 0.43f), "steel", Steel);
                    Box(root, "Tray interior", new Vector3(x, 0.979f, -0.10f), new Vector3(0.41f, 0.012f, 0.35f), "meal-" + i,
                        i == 0 ? new Color(0.48f, 0.53f, 0.30f) : i == 1 ? new Color(0.69f, 0.58f, 0.38f) : new Color(0.56f, 0.38f, 0.26f));
                    Box(root, "Lower cabinet door", new Vector3(x, 0.47f, -0.397f), new Vector3(0.57f, 0.66f, 0.02f), "storage", new Color(0.45f, 0.49f, 0.47f));
                    Box(root, "Door pull", new Vector3(x + 0.18f, 0.62f, -0.414f), new Vector3(0.025f, 0.15f, 0.012f), "light-steel", paleMetal);
                }
                for (int side = -1; side <= 1; side += 2)
                    Box(root, "Sneeze screen upright", new Vector3(side * 0.90f, 1.23f, 0.29f), new Vector3(0.035f, 0.59f, 0.035f), "steel", Steel);
                Box(root, "Sneeze screen", new Vector3(0, 1.30f, 0.29f), new Vector3(1.82f, 0.36f, 0.025f), "frosted-glass", new Color(0.65f, 0.74f, 0.72f));
                Box(root, "Upper shelf", new Vector3(0, 1.53f, 0.22f), new Vector3(1.94f, 0.035f, 0.35f), "light-steel", paleMetal);
            }
            else if (kind == "Bookshelf")
            {
                Box(root, "Shelf back", new Vector3(0, 0.88f, 0.18f), new Vector3(0.93f, 1.70f, 0.035f), "wood", timber);
                for (int side = -1; side <= 1; side += 2)
                    Box(root, "Side panel", new Vector3(side * 0.44f, 0.88f, 0), new Vector3(0.055f, 1.70f, 0.40f), "wood", timber);
                for (int shelf = 0; shelf < 5; shelf++)
                {
                    float y = 0.10f + shelf * 0.40f;
                    Box(root, "Shelf board", new Vector3(0, y, 0), new Vector3(0.89f, 0.04f, 0.39f), "wood", timber);
                    if (shelf == 4) continue;
                    for (int book = 0; book < 8; book++)
                    {
                        int cover = (book + shelf) % 4; float height = 0.22f + (book * 3 + shelf) % 4 * 0.035f;
                        Color color = cover == 0 ? new Color(0.36f, 0.48f, 0.48f) : cover == 1 ? new Color(0.56f, 0.36f, 0.28f) : cover == 2 ? new Color(0.60f, 0.57f, 0.41f) : new Color(0.39f, 0.44f, 0.34f);
                        Box(root, "Book spine", new Vector3(-0.36f + book * 0.095f, y + 0.025f + height / 2, -0.04f), new Vector3(0.07f, height, 0.25f), "book-" + cover, color);
                    }
                }
            }
            else if (kind == "Television")
            {
                Box(root, "Media cabinet", new Vector3(0, 0.30f, 0), new Vector3(1.10f, 0.53f, 0.48f), "wood", timber);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(root, "Cabinet front", new Vector3(side * 0.27f, 0.30f, -0.245f), new Vector3(0.50f, 0.43f, 0.018f), "storage", new Color(0.45f, 0.49f, 0.47f));
                    Box(root, "Cabinet pull", new Vector3(side * 0.08f, 0.36f, -0.258f), new Vector3(0.025f, 0.12f, 0.01f), "light-steel", paleMetal);
                }
                Box(root, "Television stand", new Vector3(0, 0.66f, 0.08f), new Vector3(0.09f, 0.19f, 0.08f), "steel", Steel);
                Box(root, "Television foot", new Vector3(0, 0.58f, 0.03f), new Vector3(0.52f, 0.035f, 0.30f), "steel", Steel);
                Box(root, "Display surround", new Vector3(0, 1.06f, 0.09f), new Vector3(1.10f, 0.67f, 0.065f), "rubber", dark);
                Box(root, "Television screen", new Vector3(0, 1.06f, 0.052f), new Vector3(1.02f, 0.58f, 0.018f), "screen", new Color(0.19f, 0.32f, 0.35f));
                Box(root, "Screen horizon", new Vector3(0, 1.13f, 0.039f), new Vector3(0.98f, 0.22f, 0.008f), "screen-sky", new Color(0.34f, 0.47f, 0.49f));
                Box(root, "Remote control", new Vector3(0.40f, 0.584f, -0.12f), new Vector3(0.07f, 0.025f, 0.17f), "rubber", dark);
            }
            else if (kind == "ExerciseBench")
            {
                Box(root, "Padded bench", new Vector3(0, 0.52f, -0.13f), new Vector3(0.46f, 0.12f, 1.60f), "rubber", dark);
                for (int end = -1; end <= 1; end += 2)
                {
                    float z = end < 0 ? -0.72f : 0.55f;
                    Box(root, "Bench pedestal", new Vector3(0, 0.26f, z), new Vector3(0.09f, 0.47f, 0.09f), "steel", Steel);
                    Box(root, "Stabilizer foot", new Vector3(0, 0.055f, z), new Vector3(0.78f, 0.08f, 0.22f), "steel", Steel);
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(root, "Bar support", new Vector3(side * 0.39f, 0.65f, 0.56f), new Vector3(0.055f, 1.25f, 0.055f), "steel", Steel);
                    Cylinder(root, "Bar cradle", new Vector3(side * 0.39f, 1.20f, 0.53f), new Vector3(0.10f, 0.035f, 0.10f), "light-steel", paleMetal);
                    Transform plate = Cylinder(root, "Weight plate", new Vector3(side * 0.46f, 1.20f, 0.53f), new Vector3(0.28f, 0.025f, 0.28f), "rubber", dark);
                    plate.localRotation = Quaternion.Euler(0, 0, 90);
                }
                Transform bar = Cylinder(root, "Supported barbell", new Vector3(0, 1.20f, 0.53f), new Vector3(0.04f, 0.53f, 0.04f), "light-steel", paleMetal);
                bar.localRotation = Quaternion.Euler(0, 0, 90);
            }
        }

        static int IdentitySeed(string id)
        {
            uint seed = 2166136261u;
            unchecked { foreach (char letter in id ?? "") { seed ^= letter; seed *= 16777619u; } }
            return (int)(seed & 0x7fffffffu);
        }
        Actor Person(string id, bool staff = false, string role = "Officer")
        {
            int index = IdentitySeed(id);
            var root = new GameObject((staff ? role : "Resident") + " · " + id).transform; root.SetParent(people, false);
            var model = new GameObject("Articulated model").transform; model.SetParent(root, false);
            var body = new GameObject("Body").transform; body.SetParent(model, false);
            Color skin = index % 4 == 0 ? new Color(0.43f, 0.29f, 0.21f) : index % 4 == 1 ? new Color(0.65f, 0.45f, 0.32f) : index % 4 == 2 ? new Color(0.77f, 0.60f, 0.45f) : new Color(0.55f, 0.37f, 0.28f);
            string skinName = "skin-" + index % 4;
            Color uniform = staff ? role == "Builder" ? new Color(0.91f, 0.60f, 0.12f) : role == "Officer" ? new Color(0.19f, 0.27f, 0.33f) : role == "Technician" ? new Color(0.30f, 0.39f, 0.44f) : new Color(0.39f, 0.47f, 0.43f) :
                index % 2 == 0 ? new Color(0.38f, 0.48f, 0.51f) : new Color(0.44f, 0.52f, 0.53f);
            if(role=="Visitor")uniform=new[]{new Color(.37f,.43f,.51f),new Color(.46f,.31f,.27f),new Color(.34f,.43f,.35f),new Color(.57f,.50f,.35f)}[index%4];
            if(role=="Inspector")uniform=new Color(.30f,.34f,.39f);
            string uniformName = staff ? "uniform-staff-" + role + (role=="Visitor"?"-"+index%4:"") : "uniform-" + index % 2;
            Capsule(body, "Neck", new Vector3(0, 1.48f, 0), new Vector3(0.115f, 0.085f, 0.115f), skinName, skin);
            Capsule(body, "Shirt", new Vector3(0, 1.20f, 0), new Vector3(0.38f, 0.29f, 0.25f), uniformName, uniform);
            Box(body, "Shirt seam", new Vector3(0.065f, 1.25f, 0.129f), new Vector3(0.007f, 0.30f, 0.012f), "seam", new Color(0.38f, 0.31f, 0.24f));
            Box(body, "Chest pocket", new Vector3(-0.09f, 1.31f, 0.126f), new Vector3(0.10f, 0.10f, 0.018f), uniformName, uniform);
            if (staff)
            {
                Box(body, "Identity badge", new Vector3(0.10f, 1.32f, 0.14f), new Vector3(0.055f, 0.075f, 0.012f), "badge", new Color(0.81f, 0.77f, 0.60f));
                Box(body, "Duty belt", new Vector3(0, 0.96f, 0), new Vector3(0.34f, 0.045f, 0.25f), "rubber", new Color(0.10f, 0.12f, 0.12f));
            }
            Capsule(body, "Hips", new Vector3(0, 0.92f, 0), new Vector3(0.32f, 0.13f, 0.24f), "trousers", new Color(0.30f, 0.34f, 0.33f));
            DressCharacter(body,index,staff,role,uniformName,uniform);
            Combine(body);
            var torso = new GameObject("Waist / upper-body pivot").transform; torso.SetParent(model, false); torso.localPosition = Vector3.up * 0.9f;
            body.SetParent(torso, false); body.localPosition = Vector3.down * 0.9f;
            Transform head=DetailedHead(torso,index,skinName,skin,staff);
            Transform blink=CreateFaceMotion(head,index,skinName,skin);
            var parts = new Transform[4];
            var forearms = new Transform[2]; var hands = new Transform[2]; var knees = new Transform[2]; var feet = new Transform[2];
            for (int i = 0; i < 4; i++)
            {
                bool arm = i < 2; float side = i % 2 == 0 ? -1 : 1;
                var pivot = new GameObject(arm ? "Shoulder" : "Hip").transform; pivot.SetParent(arm ? torso : model, false);
                pivot.localPosition = new Vector3(side * (arm ? 0.23f : 0.09f), arm ? 0.50f : 0.90f, 0);
                if (arm)
                {
                    Capsule(pivot, "Sleeve", new Vector3(0, -0.15f, 0), new Vector3(0.13f, 0.16f, 0.14f), uniformName, uniform);
                    Combine(pivot);
                    var elbow = new GameObject("Elbow").transform; elbow.SetParent(pivot, false); elbow.localPosition = new Vector3(0, -0.28f, 0);
                    Capsule(elbow, "Forearm", new Vector3(0, -0.11f, 0.025f), new Vector3(0.095f, 0.135f, 0.095f), skinName, skin);
                    Sphere(elbow, "Hand", new Vector3(0, -0.25f, 0.03f), new Vector3(0.078f, 0.11f, 0.060f), skinName, skin);
                    Sphere(elbow, "Thumb", new Vector3(-side * .034f, -.231f, .048f), new Vector3(.029f, .059f, .032f), skinName, skin);
                    for (int finger = 0; finger < 3; finger++) Sphere(elbow, "Knuckle", new Vector3(-.021f + finger * .021f, -.274f, .061f), new Vector3(.018f, .028f, .013f), skinName, skin);
                    Combine(elbow); forearms[i] = elbow;
                    hands[i] = new GameObject("Grip").transform; hands[i].SetParent(elbow, false); hands[i].localPosition = new Vector3(0, -0.25f, 0.05f);
                }
                else
                {
                    Capsule(pivot, "Upper trouser leg", new Vector3(0, -0.20f, 0), new Vector3(0.15f, 0.20f, 0.17f), "trousers", new Color(0.30f, 0.34f, 0.33f));
                    Combine(pivot);
                    Transform knee = new GameObject("Knee").transform; knee.SetParent(pivot, false); knee.localPosition = new Vector3(0, -0.40f, 0);
                    Capsule(knee, "Lower trouser leg", new Vector3(0, -0.19f, 0), new Vector3(0.135f, 0.20f, 0.15f), "trousers", new Color(0.30f, 0.34f, 0.33f));
                    Sphere(knee, "Continuous trouser knee", new Vector3(0, -.008f, .003f), new Vector3(.145f, .16f, .168f), "trousers", new Color(.30f, .34f, .33f));
                    Box(knee,"Trouser side seam",new Vector3(side*.067f,-.19f,0),new Vector3(.008f,.32f,.014f),"trouser-seam",new Color(.24f,.28f,.28f));
                    Combine(knee);
                    Transform ankle = new GameObject("Ankle / planted shoe").transform; ankle.SetParent(knee, false); ankle.localPosition = new Vector3(0, -.43f, 0);
                    Sphere(ankle, "Work shoe", new Vector3(0, .02f, .052f), new Vector3(.16f, .14f, .29f), "rubber", new Color(.10f, .12f, .12f));
                    Box(ankle, "Shoe sole", new Vector3(0, -.03f, .052f), new Vector3(.162f, .035f, .28f), "rubber", new Color(.08f, .09f, .09f));
                    for (int lace=0;lace<3;lace++) Box(ankle,"Boot lace",new Vector3(0,.075f,.04f+lace*.035f),new Vector3(.075f,.009f,.008f),"shoe-lace",new Color(.23f,.24f,.22f));
                    Combine(ankle); feet[i - 2] = ankle;
                    knees[i - 2] = knee;
                }
                parts[i] = pivot;
            }
            root.localScale = new Vector3(.94f+index%5*.025f,.96f+index%4*.025f,.94f+index%5*.025f);
            Transform marker = Cylinder(root, "Selected person", new Vector3(0, 0.015f, 0), new Vector3(0.72f, 0.008f, 0.72f), "handle", Teal);
            marker.gameObject.SetActive(false);
            var actor = new Actor { Root = root, Model = model, Body = torso, Head = head, Blink = blink, Limbs = parts, Marker = marker, Staff = staff, Motion = new Resident(), Forearms = forearms, Hands = hands, Knees = knees, Feet = feet, Gait = index % 97 * 0.13f, TurnRate = 7 + index % 5 };
            if (role == "Builder")
            {
                Sphere(head, "Hard hat", new Vector3(0, .14f, 0), new Vector3(0.28f, 0.16f, 0.29f), "hi-vis", new Color(0.96f, 0.77f, 0.14f));
                Cylinder(head, "Hat brim", new Vector3(0, .11f, 0), new Vector3(0.34f, 0.018f, 0.34f), "hi-vis", new Color(0.96f, 0.77f, 0.14f));
                Box(body, "Reflective vest band", new Vector3(0, 1.20f, 0.14f), new Vector3(0.34f, 0.06f, 0.018f), "reflective", new Color(0.94f, 0.94f, 0.80f));
                CreateBuilderKit(actor);
            }
            CreateCharacterNameTag(actor);
            actors.Add(id, actor); return actor;
        }

        public void SelectResident(string id)
        {
            selectedResidentId = id ?? "";
            foreach (KeyValuePair<string, Actor> pair in actors) pair.Value.Marker.gameObject.SetActive(pair.Key == selectedResidentId);
        }
        public bool TryGetPersonPosition(string id, out Vector3 position)
        {
            Actor actor;
            if (id != null && actors.TryGetValue(id, out actor) && actor.Root.gameObject.activeInHierarchy)
            {
                position = actor.Model.position; return true;
            }
            position = Vector3.zero; return false;
        }
        public string PickResident(Camera camera, Vector2 screen)
        {
            string best = null; float bestDistance = float.MaxValue;
            foreach (KeyValuePair<string, Actor> pair in actors)
            {
                Actor actor = pair.Value;
                if (actor.Staff || !actor.Root.gameObject.activeInHierarchy) continue;
                Vector3 head = camera.WorldToScreenPoint(actor.Root.position + Vector3.up * 1.35f);
                Vector3 foot = camera.WorldToScreenPoint(actor.Root.position + Vector3.up * 0.25f);
                if (head.z <= 0) continue;
                Vector2 center = new Vector2((head.x + foot.x) / 2, (head.y + foot.y) / 2);
                float radius = Mathf.Clamp(Vector2.Distance(head, foot) * 0.50f, 18, 42);
                float distance = Vector2.Distance(screen, center);
                if (distance < radius && distance < bestDistance) { bestDistance = distance; best = pair.Key; }
            }
            return best;
        }

        void MoveActor(Actor actor, Resident resident, World world, bool snap)
        {
#if UNITY_EDITOR
            if (NaturalMotionProbeOwns(actor)) return;
#endif
            MoveActor(actor, resident, world, snap, Mathf.Min(SceneFrameDelta, .05f));
        }
        void MoveActor(Actor actor, Resident resident, World world, bool snap, float dt)
        {
            // New handed-over floors can change support under an idle person. Rebind
            // transient contacts to the authoritative surfaces after a geometry revision.
            Stride contactStride = StrideFor(actor);
            if (contactStride.ContactRevision != world.GeometryRevision)
            { ResetFootContacts(actor); contactStride.ContactRevision = world.GeometryRevision; }
            // Soles on whatever the person stands on: room floors, paving or grass (FacilityMotion.StandHeight).
            Vector3 target = V(resident.Position, StandHeight(actor, resident.Position, world)), previous = actor.Root.position;
            if (snap || (target - previous).sqrMagnitude > 200)
            {
                actor.Root.position = target; actor.Speed = 0; previous = target; ResetFootContacts(actor);
                actor.RenderRoute = null; actor.RouteIndex = 0; actor.RouteRevision = -1; actor.NextRouteRefresh = 0;
            }
            Vector3 travel = target - previous;
            float gap = travel.magnitude;
            if (gap > 0.012f)
            {
                Point from = new Point(previous.x, previous.z), to = resident.Position;
                Vector3 waypoint = target;
                if (!world.Routes.Clear(from, to))
                {
                    if (actor.RouteRevision != world.GeometryRevision)
                    {
                        actor.RenderRoute = null; actor.RouteIndex = 0; actor.NextRouteRefresh = 0;
                        actor.RouteRevision = world.GeometryRevision;
                    }
                    bool exhausted = actor.RenderRoute == null || actor.RouteIndex >= actor.RenderRoute.Count;
                    bool changedTarget = Geometry.Distance(actor.RouteTarget, to) > 0.20f;
                    bool blockedCachedRoute = !exhausted && !world.Routes.Clear(from, actor.RenderRoute[actor.RouteIndex]);
                    // The core owns routing. A cached, occasional renderer path only prevents
                    // interpolation from cutting a corner; never allocate a lattice search per frame.
                    if (Time.unscaledTime >= actor.NextRouteRefresh && (exhausted || changedTarget || blockedCachedRoute))
                    {
                        actor.RenderRoute = world.Routes.Find(from, to); actor.RouteIndex = 0; actor.RouteTarget = to;
                        actor.NextRouteRefresh = Time.unscaledTime + 0.25f;
                    }
                    waypoint = previous;
                    if (actor.RenderRoute != null)
                    {
                        while (actor.RouteIndex < actor.RenderRoute.Count && Geometry.Distance(from, actor.RenderRoute[actor.RouteIndex]) < 0.06f)
                            actor.RouteIndex++;
                        while (actor.RouteIndex + 1 < actor.RenderRoute.Count && world.Routes.Clear(from, actor.RenderRoute[actor.RouteIndex + 1]))
                            actor.RouteIndex++;
                        if (actor.RouteIndex < actor.RenderRoute.Count && world.Routes.Clear(from, actor.RenderRoute[actor.RouteIndex]))
                            waypoint = V(actor.RenderRoute[actor.RouteIndex], target.y);
                    }
                }
                else { actor.RenderRoute = null; actor.RouteIndex = 0; }
                float baseSpeed = 1.6f * Mathf.Max(1, world.Data.Speed) * resident.WalkSpeed;
                // A sprinting escapee or a running responder moves faster than a walk; follow the simulation's own pace (FacilityMotion.cs).
                float desiredSpeed = Mathf.Min(Mathf.Max(baseSpeed * 1.20f, TrackedPace(actor, target)), gap / (0.16f / Mathf.Sqrt(Mathf.Max(1,world.Data.Speed))));
                Stride stride = StrideFor(actor);
                actor.Speed = Mathf.SmoothDamp(actor.Speed, desiredSpeed, ref stride.SpeedVelocity, desiredSpeed > actor.Speed ? .12f : .18f, Mathf.Infinity, dt);
                Vector3 candidate = Vector3.MoveTowards(previous, waypoint, actor.Speed * dt);
                if (world.Routes.Clear(from, new Point(candidate.x, candidate.z))) actor.Root.position = candidate;
            }
            else actor.Speed = Mathf.MoveTowards(actor.Speed, 0, dt * 15);
            // Gait, posture and limbs for what the root just did (FacilityMotion.cs).
            Locomote(actor, resident, world, previous, dt);
        }

        public void Refresh(World world, bool force = false)
        {
            if (!object.ReferenceEquals(presentedWorld, world))
            {
                ResetMotionPresentation();
                foreach (Actor oldActor in actors.Values) { Clear(oldActor.Root); Destroy(oldActor.Root.gameObject); }
                actors.Clear(); looks.Clear();residentActorIds.Clear();staffActorIds.Clear();crewActorIds.Clear();actorRetireMinute=-1;
                foreach (ProjectView oldProject in constructionViews.Values) { Clear(oldProject.Root); Destroy(oldProject.Root.gameObject); }
                constructionViews.Clear();
                if (transportRoot != null) Clear(transportRoot); trucks.Clear();
                watchedCamera = null;
                presentedWorld = world; force = true; SelectResident(null);
            }
            if (force || revision != world.GeometryRevision)
            {
                Clear(structures); revision = world.GeometryRevision;
                for (int i = 0; i < world.Rooms.Count; i++) Floor(world.Rooms, i);
                foreach (Wall wall in world.Data.Walls) WallView(structures, wall);
                foreach (Fixture fixture in world.Data.Fixtures) FixtureView(structures, fixture);
                Combine(structures);
            }
            if(propertyBoundary!=null)propertyBoundary.gameObject.SetActive(world.FacilityMode.PerimeterRisk<=0);
            RefreshConstruction(world, force);
            foreach (Actor actor in actors.Values) actor.Seen = false;
            for (int i = 0; i < world.Data.Residents.Count; i++)
            {
                Resident resident = world.Data.Residents[i];
                bool visible = true; ResidentVisibility(world, resident, ref visible); if (!visible) continue;
                string id = string.IsNullOrEmpty(resident.Id) ? "legacy-" + i : resident.Id;
                Actor actor; bool created = !actors.TryGetValue(id, out actor);
                if (created) actor = Person(id);
                actor.Seen = true; actor.Root.gameObject.SetActive(true);
                actor.Marker.gameObject.SetActive(id == selectedResidentId);
                UpdateCharacterNameTag(actor,resident.Name,id==selectedResidentId);
                PrepareResidentPose(actor, resident, world);
                // A fight owns the arms; a running escapee keeps the shared gait's arm pump (FacilityMotion.cs).
                if (world.IncidentOwnsResident(resident)) actor.PoseArms = true;
                MoveActor(actor, resident, world, force || created);
                AnimateResidentLife(actor, resident, world);
            }
            RefreshWorkers(world, force);
            RefreshOperations(world, force);
            RefreshJustice(world, force);
            RefreshIncidentVisuals(world);
            foreach (Actor actor in actors.Values) if (!actor.Seen) actor.Root.gameObject.SetActive(false);
            RetireDepartedActors(world);
        }

        readonly HashSet<string> residentActorIds=new HashSet<string>();
        readonly HashSet<string> staffActorIds=new HashSet<string>();
        readonly HashSet<string> crewActorIds=new HashSet<string>();
        int actorRetireMinute=-1,retireResidentCount=-1,retireStaffCount=-1,retireCrewCount=-1;
        // Being away at court is temporary. A roster removal is permanent; retire its meshes
        // and appearance binding rather than retaining every released person for the whole game.
        void RetireDepartedActors(World world)
        {
            if(actorRetireMinute==world.Data.Minute&&retireResidentCount==world.Data.Residents.Count&&retireStaffCount==world.Data.Staff.Count&&retireCrewCount==world.Data.Crews.Count&&
                world.Data.Residents.All(r=>residentActorIds.Contains(r.Id))&&world.Data.Staff.All(s=>staffActorIds.Contains(s.Id))&&world.Data.Crews.All(c=>crewActorIds.Contains(c.Id+"/builder-0")&&crewActorIds.Contains(c.Id+"/builder-1")))return;
            actorRetireMinute=world.Data.Minute;retireResidentCount=world.Data.Residents.Count;retireStaffCount=world.Data.Staff.Count;retireCrewCount=world.Data.Crews.Count;
            var residents=new HashSet<string>(world.Data.Residents.Select(r=>r.Id));
            var staff=new HashSet<string>(world.Data.Staff.Select(s=>s.Id));
            var crews=new HashSet<string>(world.Data.Crews.SelectMany(c=>new[]{c.Id+"/builder-0",c.Id+"/builder-1"}));
            foreach(string id in residentActorIds.Where(id=>!residents.Contains(id)).Concat(staffActorIds.Where(id=>!staff.Contains(id))).Concat(crewActorIds.Where(id=>!crews.Contains(id))).ToArray())
            {
                Actor actor;if(!actors.TryGetValue(id,out actor))continue;
                ForgetActorMotion(actor);looks.Remove(actor);Clear(actor.Root);Destroy(actor.Root.gameObject);actors.Remove(id);
                if(selectedResidentId==id)selectedResidentId=null;
            }
            residentActorIds.Clear();residentActorIds.UnionWith(residents);staffActorIds.Clear();staffActorIds.UnionWith(staff);crewActorIds.Clear();crewActorIds.UnionWith(crews);
        }
#if UNITY_EDITOR
        public int PreviewResidentActorCount {get{return actors.Count(p=>residentActorIds.Contains(p.Key));}}
        public int PreviewLookCount {get{return looks.Count;}}
#endif

        static int Hash(int hash, Wall wall)
        {
            unchecked
            {
                hash = hash * 31 + wall.A.X.GetHashCode(); hash = hash * 31 + wall.A.Y.GetHashCode();
                hash = hash * 31 + wall.B.X.GetHashCode(); hash = hash * 31 + wall.B.Y.GetHashCode();
                hash = hash * 31 + wall.Control.X.GetHashCode(); hash = hash * 31 + wall.Control.Y.GetHashCode();
                hash = hash * 31 + wall.Curved.GetHashCode(); hash = hash * 31 + wall.Door.GetHashCode();
                return hash * 31 + wall.DoorWidth.GetHashCode();
            }
        }
        public void Preview(Plan plan, Wall selected = null, Fixture selectedFixture = null)
        {
            int hash = selected == null ? 17 : Hash(23, selected);
            unchecked
            {
                hash = hash * 31 + FullHeight.GetHashCode();
                if (selectedFixture != null)
                {
                    hash = hash * 31 + selectedFixture.Position.X.GetHashCode(); hash = hash * 31 + selectedFixture.Position.Y.GetHashCode();
                    hash = hash * 31 + selectedFixture.Rotation.GetHashCode(); hash = hash * 31 + selectedFixture.Kind.GetHashCode();
                }
                if (plan != null)
                {
                    hash = hash * 31 + 1;
                    foreach (Wall wall in plan.Walls) hash = Hash(hash, wall);
                    foreach (Fixture fixture in plan.Fixtures)
                    {
                        hash = hash * 31 + fixture.Position.X.GetHashCode(); hash = hash * 31 + fixture.Position.Y.GetHashCode();
                        hash = hash * 31 + fixture.Rotation.GetHashCode(); hash = hash * 31 + fixture.Kind.GetHashCode();
                    }
                }
            }
            if (hash == previewHash) return;
            previewHash = hash; Clear(ghost);
            if (plan != null)
            {
                foreach (Wall wall in plan.Walls){WallView(ghost, wall, "ghost");if(wall.Door>=0)DoorPlanFrame(ghost,wall);}
                foreach (Fixture fixture in plan.Fixtures) FixtureView(ghost, fixture, "ghost");
            }
            if (selected != null)
            {
                foreach (Segment segment in Geometry.Segments(selected, true))
                    Beam(ghost, segment.A, segment.B, FullHeight ? 3.25f : 1.35f, 0.045f, 0.33f, "handle", Teal);
                foreach (Point p in new[] { selected.A, selected.B, selected.Curved ? selected.Control : (selected.A + selected.B) * 0.5f })
                {
                    Cylinder(ghost, "Edit handle base", V(p, 0.17f), new Vector3(0.60f, 0.018f, 0.60f), "handle", Teal);
                    Sphere(ghost, "Edit handle", V(p, 1.47f), Vector3.one * 0.23f, "handle", Teal);
                    Cylinder(ghost, "Handle guide", V(p, 0.82f), new Vector3(0.025f, 0.65f, 0.025f), "handle", Teal);
                }
            }
            if (selectedFixture != null)
            {
                Transform outline = new GameObject("Selected furnishing").transform; outline.SetParent(ghost, false);
                outline.localPosition = V(selectedFixture.Position, 0.18f); outline.localRotation = Quaternion.Euler(0, selectedFixture.Rotation, 0);
                FixtureDefinition definition = FixtureCatalog.Get(selectedFixture.Kind);
                float halfX = (definition == null ? 1 : definition.Width) / 2 + 0.04f;
                float halfZ = (definition == null ? 1 : definition.Depth) / 2 + 0.04f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(outline, "Selection edge", new Vector3(side * halfX, 0, 0), new Vector3(0.035f, 0.025f, halfZ * 2), "handle", Teal);
                    Box(outline, "Selection edge", new Vector3(0, 0, side * halfZ), new Vector3(halfX * 2, 0.025f, 0.035f), "handle", Teal);
                }
            }
            Combine(ghost);
        }
#if UNITY_EDITOR
        public void ShowFurnishingStudy()
        {
            Transform study = Root("Editor furnishing review");
            for (int i = 0; i < FixtureCatalog.All.Count; i++)
            {
                FixtureDefinition definition = FixtureCatalog.All[i];
                Point position = new Point(6 + i % 4 * 3.6f, 10 + i / 4 * 3.6f);
                Box(study, "Review plinth", V(position, 0.05f), new Vector3(3.4f, 0.10f, 3.4f), "paving", Concrete);
                FixtureView(study, new Fixture { Id = "review-" + definition.Id, Kind = definition.Id, Position = position });
                var labelObject = new GameObject(definition.Name); labelObject.transform.SetParent(study, false);
                labelObject.transform.localPosition = V(position + new Point(0, -1.35f), 0.23f);
                labelObject.transform.rotation = Quaternion.Euler(60, 25, 0);
                var label = labelObject.AddComponent<TextMesh>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 64; label.characterSize = 0.045f;
                label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
                label.text = definition.Name; label.color = new Color(0.08f, 0.12f, 0.13f);
                labelObject.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            }
        }
#endif
        void OnDestroy()
        {
            DisposeMotionResources();
            if (cornerHousing != null) Destroy(cornerHousing);
            if (cornerLens != null) Destroy(cornerLens);
            DisposeCharacterPresentation();
            if (sirenClip != null) Destroy(sirenClip);
            // Include baked scenery and resident parts so leaving Play does not leak generated meshes.
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && filter.sharedMesh.name.StartsWith("Generated ")) Destroy(filter.sharedMesh);
            if (radioClick != null) Destroy(radioClick);
            foreach (Material material in materials.Values) Destroy(material);
            foreach (Texture2D texture in generatedTextures) if (texture != null) Destroy(texture);
            generatedTextures.Clear(); textures.Clear();
            foreach(Mesh mesh in bevels.Values)Destroy(mesh);
        }
    }
}
