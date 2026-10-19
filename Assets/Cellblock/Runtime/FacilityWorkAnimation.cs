using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
#if UNITY_EDITOR
        public float StudioBuilderClock { get; set; } = -1;
#endif
        // Reused jointed props: no meshes, materials or game objects are allocated while animating.
        void CreateBuilderKit(Actor actor)
        {
            actor.Tools = new Transform[5];
            for (int i = 0; i < actor.Tools.Length; i++)
            {
                Transform tool = new GameObject(new[] { "Framing hammer", "Panel hand saw", "Cordless drill", "Inspection clipboard", "Pry bar" }[i]).transform;
                tool.SetParent(actor.Hands[1], false); actor.Tools[i] = tool;
                if (i == 0)
                {
                    Box(tool, "Ash handle", new Vector3(0, 0, 0.12f), new Vector3(0.035f, 0.035f, 0.30f), "wood", new Color(0.57f, 0.40f, 0.24f));
                    Box(tool, "Steel head", new Vector3(0, 0, 0.27f), new Vector3(0.21f, 0.065f, 0.075f), "steel", Steel);
                }
                else if (i == 1)
                {
                    Box(tool, "Saw grip", Vector3.zero, new Vector3(0.09f, 0.11f, 0.08f), "tool-yellow", Ochre);
                    Box(tool, "Saw blade", new Vector3(0, 0, 0.25f), new Vector3(0.018f, 0.12f, 0.43f), "light-steel", new Color(0.64f, 0.67f, 0.67f));
                    for (int tooth = 0; tooth < 10; tooth++) Box(tool, "Cutting tooth", new Vector3(0, -0.068f, 0.06f + tooth * 0.039f), new Vector3(0.02f, 0.024f, 0.018f), "light-steel", Color.gray);
                }
                else if (i == 2)
                {
                    Box(tool, "Drill motor", new Vector3(0, 0.08f, 0.06f), new Vector3(0.12f, 0.13f, 0.25f), "tool-yellow", Ochre);
                    Box(tool, "Drill grip", new Vector3(0, -0.035f, 0.01f), new Vector3(0.075f, 0.17f, 0.07f), "rubber", Steel);
                    Box(tool, "Battery", new Vector3(0, -0.13f, 0.01f), new Vector3(0.13f, 0.06f, 0.11f), "rubber", Steel);
                    Box(tool, "Chuck and bit", new Vector3(0, 0.08f, 0.25f), new Vector3(0.035f, 0.035f, 0.19f), "steel", Steel);
                }
                else if (i == 3)
                {
                    Box(tool, "Inspection board", new Vector3(-0.08f, 0, 0.09f), new Vector3(0.23f, 0.025f, 0.31f), "wood", new Color(0.56f, 0.42f, 0.28f));
                    Box(tool, "Drawing", new Vector3(-0.08f, 0.016f, 0.09f), new Vector3(0.20f, 0.005f, 0.26f), "paper", new Color(0.87f, 0.87f, 0.77f));
                    for (int line = 0; line < 4; line++) Box(tool, "Drawing detail", new Vector3(-0.08f, 0.020f, 0.02f + line * 0.05f), new Vector3(0.14f, 0.004f, 0.008f), "ink", Steel);
                }
                else
                {
                    Box(tool, "Pry bar", new Vector3(0, 0, 0.22f), new Vector3(0.03f, 0.03f, 0.50f), "steel", Steel);
                    Box(tool, "Bent tip", new Vector3(0, -0.04f, 0.45f), new Vector3(0.045f, 0.10f, 0.035f), "steel", Steel);
                }
                Combine(tool); tool.gameObject.SetActive(false);
            }
            actor.WorkChips = new Transform[7];
            for (int chip = 0; chip < actor.WorkChips.Length; chip++)
            {
                actor.WorkChips[chip] = Box(actor.Root, "Work contact chip", Vector3.zero, Vector3.one * .035f, "work-fragment", new Color(.55f, .51f, .43f));
                actor.WorkChips[chip].gameObject.SetActive(false);
            }
            actor.Workbench = new GameObject("Folding saw station").transform; actor.Workbench.SetParent(actor.Root, false);
            for (int end = -1; end <= 1; end += 2)
            {
                Box(actor.Workbench, "Sawhorse top", new Vector3(end * 0.43f, 1.06f, 0.60f), new Vector3(0.08f, 0.08f, 0.54f), "wood", new Color(0.50f, 0.39f, 0.26f));
                for (int side = -1; side <= 1; side += 2)
                {
                    Transform leg = Box(actor.Workbench, "Sawhorse leg", new Vector3(end * 0.43f, 0.53f, 0.60f + side * 0.19f), new Vector3(0.065f, 1.06f, 0.065f), "wood", new Color(0.50f, 0.39f, 0.26f));
                    leg.localRotation = Quaternion.Euler(side * 12, 0, 0);
                }
            }
            Box(actor.Workbench, "Timber being cut", new Vector3(0, 1.13f, 0.60f), new Vector3(1.15f, 0.09f, 0.25f), "wood", new Color(0.64f, 0.50f, 0.32f));
            Combine(actor.Workbench); actor.Workbench.gameObject.SetActive(false);
            actor.Cargo = new GameObject("Carried building supplies").transform; actor.Cargo.SetParent(actor.Root, false);
            for (int i = 0; i < 3; i++) Box(actor.Cargo, "Timber / fitout package", new Vector3(0, i * 0.065f, 0), new Vector3(0.80f, 0.055f, 0.26f), "wood", new Color(0.62f, 0.48f, 0.30f));
            Box(actor.Cargo, "Shipping strap", new Vector3(0, 0.09f, 0), new Vector3(0.035f, 0.21f, 0.27f), "rubber", Steel);
            Combine(actor.Cargo); actor.Cargo.localPosition = new Vector3(0, 1.05f, 0.31f); actor.Cargo.gameObject.SetActive(false);
        }
        void ArmPose(Actor actor, int arm, Vector3 hand, float blend)
        {
            // Analytic two-bone arms keep the hands with the tool while elbows bend naturally.
            Vector3 shoulder = actor.Model.InverseTransformPoint(actor.Limbs[arm].position);
            Vector3 delta = hand - shoulder;
            float distance = Mathf.Clamp(delta.magnitude, 0.10f, 0.53f);
            Vector3 direction = delta.normalized;
            const float upper = 0.28f, lower = 0.26f;
            float along = (upper * upper - lower * lower + distance * distance) / (2 * distance);
            Vector3 bend = Vector3.ProjectOnPlane(new Vector3(arm == 0 ? -1 : 1, -0.25f, -0.2f), direction).normalized;
            Vector3 elbow = shoulder + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
            Vector3 grip = shoulder + direction * distance;
            Quaternion upperRotation = actor.Model.rotation * Quaternion.FromToRotation(Vector3.down, (elbow - shoulder).normalized);
            Quaternion lowerRotation = actor.Model.rotation * Quaternion.FromToRotation(Vector3.down, (grip - elbow).normalized);
            actor.Limbs[arm].rotation = Quaternion.Slerp(actor.Limbs[arm].rotation, upperRotation, blend);
            actor.Forearms[arm].rotation = Quaternion.Slerp(actor.Forearms[arm].rotation, lowerRotation, blend);
        }
        void AnimateBuilder(Actor actor, Crew crew, int partner, World world, bool snap)
        {
            Point position = partner == 0 ? crew.Position : crew.HelperPosition;
            actor.PoseArms = !world.Data.Operations.Alarm && (crew.Working || crew.WorkPhase == "Carrying" || (crew.WorkPhase == "Collecting" && crew.SupplyLoadMinutes > 0));
            AnimateWorker(actor, position, world, snap, false, "Builder");
            actor.Workbench.gameObject.SetActive(false);
            foreach (Transform unused in actor.Tools) unused.gameObject.SetActive(false);
            bool carrying = crew.WorkPhase == "Carrying" && !world.Data.Operations.Alarm;
            actor.Cargo.gameObject.SetActive(carrying);
            foreach (Transform chip in actor.WorkChips) chip.gameObject.SetActive(false);
            float dt = world.Data.Speed > 0 ? Mathf.Min(SceneFrameDelta, .05f) * Mathf.Sqrt(world.Data.Speed) : 0;
            float blend = snap ? 1 : 1 - Mathf.Exp(-dt * 18);
            if (world.Data.Speed > 0) actor.PoseClock += dt;
            float t = actor.PoseClock + actor.Gait * 0.11f;
#if UNITY_EDITOR
            if (StudioBuilderClock >= 0) { t = StudioBuilderClock; blend = 1; }
#endif
            if (carrying)
            {
                ArmPose(actor, 0, new Vector3(-0.27f, 1.08f, 0.30f), blend);
                ArmPose(actor, 1, new Vector3(0.27f, 1.08f, 0.30f), blend);
                actor.Cargo.localPosition = new Vector3(0, 1.05f + Mathf.Sin(actor.Gait * 2) * 0.008f, 0.31f);
                return;
            }
            bool at = Geometry.Distance(new Point(actor.Root.position.x, actor.Root.position.z), position) < 0.16f;
            bool loading = !world.Data.Operations.Alarm && crew.WorkPhase == "Collecting" && crew.SupplyLoadMinutes > 0;
            if (world.Data.Operations.Alarm || (!crew.Working && !loading) || !at || actor.Speed > .16f) return;
            Point focus = loading ? crew.SupplyPoint + new Point(-0.7f, 0) : crew.WorkFocus;
            Vector3 look = V(focus) - V(position);
            if (look.sqrMagnitude > 0.01f) actor.Root.rotation = Quaternion.Slerp(actor.Root.rotation, Quaternion.LookRotation(look), blend);
            if (loading)
            {
                float reach = (Mathf.Sin(t * 3) + 1) * 0.5f;
                ArmPose(actor, 0, new Vector3(-0.20f, Mathf.Lerp(1.13f, 0.98f, reach), 0.34f), blend);
                ArmPose(actor, 1, new Vector3(0.20f, Mathf.Lerp(1.13f, 0.98f, reach), 0.34f), blend);
                actor.Body.localRotation = Quaternion.Euler(8 + reach * 7, 0, 0); return;
            }
            string task = crew.WorkKind;
            // Partners alternate cutting/fixing and measuring/supporting at the same work front.
            int toolIndex = task == "Saw" ? partner == 0 ? 1 : 3 : task == "Hammer" ? partner == 0 ? 0 : 3 : task == "Drill" || task == "Install" ? partner == 0 ? 2 : 3 : task == "Demolish" ? partner == 0 ? 4 : 3 : 3;
            actor.Workbench.gameObject.SetActive(toolIndex == 1);
            Transform tool = actor.Tools[toolIndex]; tool.gameObject.SetActive(true);
            Vector3 left = new Vector3(-0.19f, 1.14f, 0.36f), right = new Vector3(0.18f, 1.17f, 0.33f);
            Vector3 toolAngle = Vector3.zero;
            // Reach toward the actual surface, keeping the tip beyond the hand. The helper measures/checks
            // from its own reachable position instead of drilling the same point through its colleague.
            float surface = Mathf.Clamp(look.magnitude / Mathf.Max(.01f, actor.Root.lossyScale.z), .55f, .86f);
            float cycle = Mathf.Repeat(t * (toolIndex == 0 ? 1.15f : toolIndex == 4 ? .48f : .85f), 1);
            float contact = 0;
            float pitch = 3, twist = 0;

            if (toolIndex == 0)
            {
                // Deliberate lift, fast downstroke, recoil and a short contact hold.
                float lift = cycle < .52f ? Mathf.SmoothStep(0, 1, cycle / .52f) : cycle < .65f ? 1 - Mathf.SmoothStep(0, 1, (cycle - .52f) / .13f) : .10f * Mathf.Sin((cycle - .65f) / .35f * Mathf.PI);
                right = new Vector3(.14f, 1.18f + lift * .35f, surface - .27f - lift * .22f);
                left = new Vector3(-.16f, 1.12f, Mathf.Min(.42f, surface - .20f));
                toolAngle.x = -68 * lift; pitch = 3 + (1 - lift) * 6; twist = -lift * 5;
                contact = cycle >= .65f && cycle < .78f ? (cycle - .65f) / .13f : -1;
            }
            else if (toolIndex == 1)
            {
                // The blade works along the station timber, with a bent supporting arm.
                float stroke = Mathf.Sin(cycle * Mathf.PI * 2) * .105f;
                right = new Vector3(.18f, 1.10f, .32f + stroke); left = new Vector3(-.23f, 1.15f, .40f);
                toolAngle = new Vector3(5, -9, -8); pitch = 9; twist = stroke * 14;
                contact = cycle;
            }
            else if (toolIndex == 2)
            {
                // Set the bit, press into the surface, withdraw and inspect before the next fastening.
                float press = Mathf.SmoothStep(0, 1, cycle / .16f) * (1 - Mathf.SmoothStep(0, 1, (cycle - .72f) / .20f));
                right = new Vector3(.13f, 1.16f, surface - .35f - (1 - press) * .12f);
                left = new Vector3(.02f, 1.26f, surface - .28f - (1 - press) * .12f);
                pitch = 3 + press * 4; contact = press > .95f ? cycle : -1;
            }
            else if (toolIndex == 3)
            {
                right = new Vector3(.17f, 1.12f, .29f); left = new Vector3(-.12f, 1.17f + Mathf.Sin(t * 1.7f) * .025f, .33f);
                toolAngle.x = -12; pitch = 3; twist = Mathf.Sin(t * .9f) * 4;
                contact = -1;
            }
            else
            {
                // Plant the pry tip, load the lever, pull with the torso, release, then reset.
                float pull = Mathf.SmoothStep(0, 1, (cycle - .15f) / .48f) * (1 - Mathf.SmoothStep(0, 1, (cycle - .75f) / .25f));
                right = new Vector3(.13f, 1.14f - pull * .08f, surface - .45f - pull * .08f);
                left = new Vector3(-.03f, 1.18f - pull * .05f, surface - .36f - pull * .08f);
                toolAngle.x = -pull * 22; pitch = 8 - pull * 7; twist = pull * -6;
                contact = cycle >= .63f && cycle < .84f ? (cycle - .63f) / .21f : -1;
            }
            actor.Body.localRotation = Quaternion.Slerp(actor.Body.localRotation, Quaternion.Euler(pitch, twist, 0), blend);
            ArmPose(actor, 0, left, blend); ArmPose(actor, 1, right, blend);
            tool.rotation = actor.Root.rotation * Quaternion.Euler(toolAngle);
            Vector3 tip = tool.TransformPoint(toolIndex == 4 ? new Vector3(0, -.04f, .45f) : new Vector3(0, 0, .27f));
            bool touching = toolIndex == 1 || Vector3.Distance(tip, V(focus, 1.12f)) < .25f;
            int impact = Mathf.FloorToInt(t * (toolIndex == 0 ? 1.15f : toolIndex == 4 ? .48f : .85f) - (toolIndex == 0 ? .65f : toolIndex == 4 ? .63f : .18f));
            if (partner == 0 && actor.BuilderSoundTask == crew.WorkId && actor.BuilderSoundTool == toolIndex && impact > actor.LastBuilderImpact && world.Data.Speed > 0 && touching && toolIndex != 3)
                FacilitySoundscape.BuilderContact(task, tip);
            actor.BuilderSoundTask = crew.WorkId; actor.BuilderSoundTool = toolIndex; actor.LastBuilderImpact = impact;
            if (!ReducedMotion && touching && partner == 0 && contact >= 0 && (toolIndex == 0 || toolIndex == 1 || toolIndex == 4))
            {
                Vector3 origin = toolIndex == 1 ? actor.Root.TransformPoint(new Vector3(.18f, 1.13f, .60f)) : V(focus, 1.12f);
                for (int i = 0; i < actor.WorkChips.Length; i++)
                {
                    Transform chip = actor.WorkChips[i]; chip.gameObject.SetActive(true);
                    float age = Mathf.Repeat(contact + i * .117f, 1), spread = .08f + i * .017f;
                    Vector3 side = actor.Root.right * ((i % 2 == 0 ? -1 : 1) * age * spread);
                    chip.position = origin + side - actor.Root.forward * (age * .18f) + Vector3.up * (.12f * age - .40f * age * age);
                    chip.localScale = Vector3.one * (toolIndex == 1 ? .018f : .035f) * (.6f + i % 3 * .2f) * (1 - age);
                }
            }
        }
    }
}
