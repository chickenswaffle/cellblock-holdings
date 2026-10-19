using System.Linq;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        // Progress dressing only. Original geometry remains authoritative until project handover.
        void DemolitionDetail(Transform root, World world, WorkItem work, float progress)
        {
            if (progress <= 0 || work.Kind != "Removal") return;
            Wall wall = world.Data.Walls.FirstOrDefault(w => w.Id == work.RemovedWallId);
            Fixture fixture = world.Data.Fixtures.FirstOrDefault(f => f.Id == work.RemovedFixtureId);
            Point site = wall != null ? Geometry.OnLength(wall, .5f) : fixture != null ? fixture.Position : work.Site;
            if (wall != null)
            {
                // Narrow diagonal scored joints read as dismantling, without showing a walkable breach early.
                for (int i = 0; i < Mathf.CeilToInt(progress * 7); i++)
                {
                    Point p = Geometry.OnLength(wall, .15f + i * .1f);
                    Point tangent = wall.At(Mathf.Min(1, .18f + i * .1f)) - wall.At(Mathf.Max(0, .12f + i * .1f));
                    float yaw = Mathf.Atan2(tangent.X, tangent.Y) * Mathf.Rad2Deg;
                    foreach (float side in new[] { -.145f, .145f })
                    {
                        var mark = Box(root, "Scored demolition joint", V(p, .62f + i % 2 * .25f), new Vector3(.009f, .20f + i % 3 * .035f, .007f), "work-scoring", new Color(.31f, .30f, .27f));
                        mark.rotation = Quaternion.Euler(0, yaw, 0);
                        mark.position += mark.right * side; mark.Rotate(0, 0, i % 2 == 0 ? 24 : -24);
                    }
                }
            }
            // Debris lies beside the active work front, never pretending the whole facility has disappeared.
            Point offset = new Point(.65f, -.65f);
            Crew crew = world.Data.Crews.FirstOrDefault(c => c.Working && c.WorkId.EndsWith("/" + work.TaskId));
            if (crew != null) { site = crew.WorkTarget; offset = new Point(-.40f, -.30f); }
            for (int i = 0; i < Mathf.CeilToInt(progress * 12); i++)
            {
                float angle = i * 2.4f, radius = .10f + i % 4 * .065f;
                Point p = site + offset + new Point(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Transform fragment = Box(root, fixture != null ? "Dismantled fixings" : "Masonry debris", V(p, .16f + i % 3 * .025f),
                    new Vector3(.12f + i % 2 * .06f, .055f, .09f), "work-fragment", new Color(.55f, .51f, .43f));
                fragment.localRotation = Quaternion.Euler(i * 13 % 27, i * 37, i * 7 % 19);
            }
            if (progress > .35f)
                Box(root, "Demolition salvage tray", V(site + offset + new Point(.45f, .10f), .23f), new Vector3(.38f, .18f, .30f), "rubber", Steel);
        }
    }
}
