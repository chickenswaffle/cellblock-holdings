using System.Collections.Generic;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        sealed class RoutineProps { public Transform Book, Notebook, Pencil, LeftWeight, RightWeight; }
        readonly Dictionary<Actor, RoutineProps> routineProps = new Dictionary<Actor, RoutineProps>();
        readonly List<Actor> retiredRoutineProps = new List<Actor>();
        int routinePruneFrame;
        World routinePropWorld;
        void FaceRoutine(Actor actor, Vector3 target)
        {
            Vector3 direction = target - actor.Root.position; direction.y = 0;
            if (direction.sqrMagnitude > .01f) actor.Root.rotation = Quaternion.Slerp(actor.Root.rotation, Quaternion.LookRotation(direction), 1 - Mathf.Exp(-Time.unscaledDeltaTime * 9));
        }
        void FaceRoutineObject(Actor actor, Resident resident, World world)
        { Fixture f = world.Data.Fixtures.Find(x => x.Id == resident.ActivityFixture); if (f != null) FaceRoutine(actor, V(f.Position)); }
        bool RoutinePartnerReady(Resident resident, World world)
        {
            Actor other; Resident partner = world.Data.Residents.Find(r => r.Id == resident.ActivityPartner);
            return partner != null && actors.TryGetValue(resident.ActivityPartner, out other) && other.Root != null && other.Speed < .06f &&
                Vector3.Distance(other.Root.position, V(partner.Position, .14f)) < .25f;
        }
        Transform RoutineBook(Transform hand, string name, Color cover)
        {
            Transform book = new GameObject(name).transform; book.SetParent(hand, false); book.localPosition = new Vector3(.015f, .02f, .085f); book.localRotation = Quaternion.Euler(12, 0, 0);
            Box(book, "Open pages", Vector3.zero, new Vector3(.30f, .027f, .21f), "book-paper", new Color(.88f, .85f, .72f));
            Box(book, "Book cover", new Vector3(0, -.021f, 0), new Vector3(.31f, .012f, .22f), "book-cover-" + name, cover);
            Box(book, "Spine", new Vector3(0, .002f, 0), new Vector3(.015f, .03f, .21f), "book-spine", new Color(.57f, .51f, .39f));
            for (int side = -1; side <= 1; side += 2)
                for (int line = 0; line < 6; line++) Box(book, "Printed line", new Vector3(side * .078f, .0145f, -.07f + line * .027f), new Vector3(.108f, .0015f, .003f), "book-ink", new Color(.31f, .32f, .30f));
            Combine(book); return book;
        }
        Transform RoutineWeight(Transform hand)
        {
            Transform weight = new GameObject("Hand weight / exercise equipment").transform; weight.SetParent(hand, false); weight.localPosition = new Vector3(0, .015f, .015f);
            Transform bar = Cylinder(weight, "Grip", Vector3.zero, new Vector3(.035f, .17f, .035f), "weight-steel", new Color(.43f, .47f, .49f)); bar.localRotation = Quaternion.Euler(0, 0, 90);
            foreach (float side in new[] { -1f, 1f })
            { Transform plate = Cylinder(weight, "Weight plate", new Vector3(side * .17f, 0, 0), new Vector3(.14f, .045f, .14f), "weight-rubber", new Color(.13f, .16f, .17f)); plate.localRotation = Quaternion.Euler(0, 0, 90); }
            Combine(weight); return weight;
        }
        void HideRoutineProps(Actor actor)
        {
            RoutineProps p; if (!routineProps.TryGetValue(actor, out p)) return;
            HideRoutineProp(p.Book); HideRoutineProp(p.Notebook); HideRoutineProp(p.Pencil); HideRoutineProp(p.LeftWeight); HideRoutineProp(p.RightWeight);
        }
        void HideRoutineProp(Transform prop) { if (prop != null) prop.gameObject.SetActive(false); }
        void RefreshRoutineProps(Actor actor, LifePose pose, float weight, World world)
        {
            if (routinePropWorld != world) { routineProps.Clear(); routinePropWorld = world; }
            if (Time.frameCount - routinePruneFrame > 120)
            {
                routinePruneFrame = Time.frameCount; retiredRoutineProps.Clear();
                foreach (var pair in routineProps) if (pair.Key.Root == null) retiredRoutineProps.Add(pair.Key);
                foreach (Actor retired in retiredRoutineProps) routineProps.Remove(retired);
            }
            RoutineProps p; bool needed = pose == LifePose.Read || pose == LifePose.Study || pose == LifePose.Exercise;
            if (!routineProps.TryGetValue(actor, out p)) { if (!needed) return; p = new RoutineProps(); routineProps.Add(actor, p); }
            HideRoutineProps(actor); if (weight < .45f) return;
            if (pose == LifePose.Read)
            { if (p.Book == null) p.Book = RoutineBook(actor.Hands[0], "Reading book", new Color(.18f, .27f, .32f)); p.Book.gameObject.SetActive(true); }
            else if (pose == LifePose.Study)
            {
                if (p.Notebook == null) p.Notebook = RoutineBook(actor.Hands[0], "Course notebook", new Color(.38f, .25f, .17f));
                if (p.Pencil == null)
                { p.Pencil = new GameObject("Writing pencil").transform; p.Pencil.SetParent(actor.Hands[1], false); p.Pencil.localPosition = new Vector3(0, .015f, .045f);
                    Box(p.Pencil, "Pencil", Vector3.zero, new Vector3(.008f, .14f, .008f), "pencil", new Color(.69f, .48f, .18f)); Combine(p.Pencil); }
                p.Notebook.gameObject.SetActive(true); p.Pencil.gameObject.SetActive(true);
            }
            else if (pose == LifePose.Exercise)
            { if (p.LeftWeight == null) p.LeftWeight = RoutineWeight(actor.Hands[0]); if (p.RightWeight == null) p.RightWeight = RoutineWeight(actor.Hands[1]); p.LeftWeight.gameObject.SetActive(true); p.RightWeight.gameObject.SetActive(true); }
        }
#if UNITY_EDITOR
        public bool PreviewRoutinePose(string id, string pose)
        {
            Actor a; Look l;
            return actors.TryGetValue(id, out a) && a.Root != null && looks.TryGetValue(a, out l) &&
                l.Pose.ToString() == pose && l.Weight > .95f && a.Speed < .06f;
        }
        public int PreviewRoutinePropsVisible
        { get { int count = 0; foreach (var p in routineProps.Values) foreach (Transform t in new[] { p.Book, p.Notebook, p.Pencil, p.LeftWeight, p.RightWeight }) if (t != null && t.gameObject.activeInHierarchy) count++; return count; } }
#endif
    }
}
