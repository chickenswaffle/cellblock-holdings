using System.Collections.Generic;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    // Personnel presentation: post badges on the warden and nurse, and a link between a watch officer and
    // the resident they shadow while either is selected. Visual only; nothing here enters routing.
    public sealed partial class FacilityView
    {
        readonly Dictionary<string, Transform> postBadges = new Dictionary<string, Transform>();
        readonly Dictionary<string, string> badgePosts = new Dictionary<string, string>();
        LineRenderer watchLink;
        World personnelWorld;

        void RefreshPersonnelView(World world)
        {
            if (!ReferenceEquals(personnelWorld, world)) { postBadges.Clear(); badgePosts.Clear(); personnelWorld = world; }
            foreach (StaffMember staff in world.Data.Staff)
            {
                PersonnelFile file = world.FileFor(staff);
                string post = file == null ? "" : file.Post;
                Actor actor;
                if (!actors.TryGetValue(staff.Id, out actor)) continue;
                string shown;
                Transform badge;
                bool has = postBadges.TryGetValue(staff.Id, out badge) && badge != null;
                if (has && badgePosts.TryGetValue(staff.Id, out shown) && shown == post) continue;
                if (has) { Destroy(badge.gameObject); postBadges.Remove(staff.Id); badgePosts.Remove(staff.Id); }
                if (post == "") continue;
                badge = new GameObject(post + " insignia").transform; badge.SetParent(actor.Body, false); badge.localPosition = Vector3.down * .9f;
                if (post == "Warden")
                {
                    for (int side = -1; side <= 1; side += 2)
                        Box(badge, "Shoulder board", new Vector3(side * 0.17f, 1.37f, 0), new Vector3(0.11f, 0.022f, 0.16f), "badge-gold", new Color(0.82f, 0.66f, 0.30f));
                    Box(badge, "Tie", new Vector3(0, 1.22f, 0.128f), new Vector3(0.045f, 0.22f, 0.012f), "badge-tie", new Color(0.45f, 0.12f, 0.14f));
                }
                else
                {
                    Cylinder(badge, "Medical cap", new Vector3(0, 1.755f, -0.01f), new Vector3(0.24f, 0.035f, 0.24f), "badge-white", new Color(0.94f, 0.95f, 0.94f));
                    Box(badge, "Armband", new Vector3(0.235f, 1.27f, 0), new Vector3(0.012f, 0.07f, 0.13f), "badge-white", new Color(0.94f, 0.95f, 0.94f));
                    Box(badge, "Armband cross", new Vector3(0.242f, 1.27f, 0), new Vector3(0.006f, 0.045f, 0.016f), "badge-red", new Color(0.82f, 0.16f, 0.15f));
                    Box(badge, "Armband cross", new Vector3(0.242f, 1.27f, 0), new Vector3(0.006f, 0.016f, 0.045f), "badge-red", new Color(0.82f, 0.16f, 0.15f));
                }
                postBadges[staff.Id] = badge; badgePosts[staff.Id] = post;
            }
            RefreshWatchLink(world);
        }

        void RefreshWatchLink(World world)
        {
            if (watchLink == null)
            {
                watchLink = new GameObject("Watch assignment link").AddComponent<LineRenderer>(); watchLink.transform.SetParent(transform, false);
                watchLink.sharedMaterial = Material("handle", Teal); watchLink.startWidth = watchLink.endWidth = 0.035f; watchLink.useWorldSpace = true; watchLink.positionCount = 2;
            }
            Actor officer = null, resident = null;
            foreach (StaffMember staff in world.Data.Staff)
            {
                PersonnelFile file = world.FileFor(staff);
                if (file == null || file.Duty != "Watch" || (staff.Id != selectedResidentId && file.WatchId != selectedResidentId)) continue;
                if (actors.TryGetValue(staff.Id, out officer) && actors.TryGetValue(file.WatchId, out resident) &&
                    officer.Root.gameObject.activeInHierarchy && resident.Root.gameObject.activeInHierarchy) break;
                officer = resident = null;
            }
            bool show = officer != null && resident != null;
            watchLink.gameObject.SetActive(show);
            if (!show) return;
            watchLink.SetPosition(0, officer.Root.position + Vector3.up * 1.9f);
            watchLink.SetPosition(1, resident.Root.position + Vector3.up * 1.9f);
        }
    }
}
