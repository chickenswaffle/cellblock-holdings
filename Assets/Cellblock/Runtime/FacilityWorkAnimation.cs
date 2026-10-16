using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
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
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 14);
            if (world.Data.Speed > 0) actor.PoseClock += Mathf.Min(Time.unscaledDeltaTime, 0.05f) * Mathf.Sqrt(world.Data.Speed);
            float t = actor.PoseClock + actor.Gait * 0.11f;
            if (carrying)
            {
                ArmPose(actor, 0, new Vector3(-0.27f, 1.08f, 0.30f), blend);
                ArmPose(actor, 1, new Vector3(0.27f, 1.08f, 0.30f), blend);
                actor.Cargo.localPosition = new Vector3(0, 1.05f + Mathf.Sin(actor.Gait * 2) * 0.008f, 0.31f);
                return;
            }
            bool at = Geometry.Distance(new Point(actor.Root.position.x, actor.Root.position.z), position) < 0.16f;
            bool loading = !world.Data.Operations.Alarm && crew.WorkPhase == "Collecting" && crew.SupplyLoadMinutes > 0;
            if ((!crew.Working && !loading) || !at) return;
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
            int toolIndex = task == "Saw" ? partner == 0 ? 1 : 3 : task == "Hammer" ? partner == 0 ? 0 : 2 : task == "Drill" || task == "Install" ? 2 : task == "Demolish" ? 4 : 3;
            actor.Workbench.gameObject.SetActive(toolIndex == 1);
            Transform tool = actor.Tools[toolIndex]; tool.gameObject.SetActive(true);
            Vector3 left = new Vector3(-0.19f, 1.14f, 0.36f), right = new Vector3(0.18f, 1.17f, 0.33f);
            Vector3 toolAngle = Vector3.zero;
            if (toolIndex == 0)
            {
                float swing = Mathf.Pow((Mathf.Sin(t * 6.2f) + 1) * 0.5f, 2);
                right = new Vector3(0.17f, 1.16f + swing * 0.36f, 0.34f - swing * 0.20f);
                toolAngle.x = -70 * swing; actor.Body.localRotation = Quaternion.Euler(4 + (1 - swing) * 5, 0, 0);
            }
            else if (toolIndex == 1)
            {
                float stroke = Mathf.Sin(t * 5.2f) * 0.085f;
                right = new Vector3(0.18f, 1.09f, 0.30f + stroke); left = new Vector3(-0.17f, 1.10f, 0.35f);
                toolAngle = new Vector3(6, -12, -8); actor.Body.localRotation = Quaternion.Euler(9, 0, Mathf.Sin(t * 5.2f) * 1.5f);
            }
            else if (toolIndex == 2)
            {
                float pressure = Mathf.Sin(t * 8) * 0.004f;
                right = new Vector3(0.13f, 1.20f, 0.33f + pressure); left = new Vector3(-0.06f, 1.24f, 0.38f + pressure);
                actor.Body.localRotation = Quaternion.Euler(4, 0, 0);
            }
            else if (toolIndex == 3)
            {
                right = new Vector3(0.17f, 1.12f, 0.29f); left = new Vector3(-0.12f, 1.17f + Mathf.Sin(t * 1.7f) * 0.025f, 0.33f);
                toolAngle.x = -12; actor.Body.localRotation = Quaternion.Euler(3, Mathf.Sin(t * 0.9f) * 4, 0);
            }
            else
            {
                float pull = Mathf.Sin(t * 3) * 0.07f;
                right = new Vector3(0.15f, 1.12f, 0.31f + pull); left = new Vector3(-0.08f, 1.16f, 0.35f + pull);
                actor.Body.localRotation = Quaternion.Euler(9 + pull * 25, 0, 0);
            }
            ArmPose(actor, 0, left, blend); ArmPose(actor, 1, right, blend);
            tool.rotation = actor.Root.rotation * Quaternion.Euler(toolAngle);
        }
    }
}
