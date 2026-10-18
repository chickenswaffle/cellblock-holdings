using System.Collections.Generic;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        Transform CreateFaceMotion(Transform head, int seed, string skinName, Color skin)
        {
            const float eyeY = .023f;
            const int columns = 12, rows = 5;
            float width = .84f + seed % 5 * .020f;
            var overlay = new GameObject("Transient blink eyelids").transform;
            overlay.SetParent(head, false); overlay.localPosition = Vector3.up * eyeY;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int side = -1; side <= 1; side += 2)
            {
                int start = vertices.Count;
                for (int row = 0; row < rows; row++) for (int column = 0; column <= columns; column++)
                {
                    float x = Mathf.Lerp(-1, 1, column / (float)columns), y = Mathf.Lerp(-1, 1, row / (float)(rows - 1));
                    float halfHeight = .0068f * Mathf.Sqrt(Mathf.Max(0, 1 - x * x));
                    Vector3 point = HumanFacePoint(side * .041f * width + x * .0168f, eyeY + y * halfHeight, width, .008f);
                    // Two shallow lid ridges leave a restrained physical crease at full closure.
                    point.z += Mathf.Sin(Mathf.Abs(y) * Mathf.PI) * .0008f;
                    vertices.Add(point - Vector3.up * eyeY);
                }
                for (int row = 0; row < rows - 1; row++) for (int column = 0; column < columns; column++)
                {
                    int a = start + row * (columns + 1) + column, b = a + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + columns + 1);
                    triangles.Add(b); triangles.Add(b + columns + 1); triangles.Add(a + columns + 1);
                }
            }
            HumanMesh(overlay, "Paired eyelid overlay", vertices, triangles, skinName, skin);
            Combine(overlay);
            overlay.localScale = new Vector3(1, 0, 1); overlay.gameObject.SetActive(false);
            return overlay;
        }

        void AnimateFaceMotion(Actor actor, Stride stride)
        {
            if (actor.Blink == null) return;
            int seed = IdentitySeed(stride.Id);
            float period = 4.1f + seed % 30 * .1f;
            float duration = .160f + (seed >> 5) % 5 * .008f;
            float phase = Mathf.Repeat(stride.Clock + seed % 211 * .029f, period);
            float closed = 0;
            if (phase < duration)
            {
                float t = phase / duration;
                if (t < .32f) closed = Mathf.SmoothStep(0, 1, t / .32f);
                else if (t < .42f) closed = 1;
                else closed = 1 - Mathf.SmoothStep(0, 1, (t - .42f) / .58f);
            }
            bool active = closed > .015f;
            if (actor.Blink.gameObject.activeSelf != active) actor.Blink.gameObject.SetActive(active);
            actor.Blink.localScale = new Vector3(1, closed, 1);
        }
    }
}
