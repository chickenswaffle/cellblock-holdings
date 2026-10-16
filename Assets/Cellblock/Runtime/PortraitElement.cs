using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // A painted ID-photo portrait generated from a persistent FaceSpec. Drawn with Painter2D, so it
    // needs no textures and costs nothing until the element is repainted.
    public sealed class PortraitElement : VisualElement
    {
        static readonly Color[] SkinTones = { new Color(0.36f, 0.24f, 0.17f), new Color(0.47f, 0.31f, 0.22f), new Color(0.62f, 0.43f, 0.31f),
            new Color(0.74f, 0.55f, 0.41f), new Color(0.84f, 0.67f, 0.53f), new Color(0.92f, 0.77f, 0.65f) };
        static readonly Color[] HairColors = { new Color(0.09f, 0.08f, 0.07f), new Color(0.22f, 0.15f, 0.10f), new Color(0.40f, 0.27f, 0.16f),
            new Color(0.62f, 0.45f, 0.25f), new Color(0.66f, 0.66f, 0.64f) };
        FaceSpec face;
        Color uniform = new Color(0.19f, 0.27f, 0.33f), accent = new Color(0.82f, 0.70f, 0.42f);
        int stripes;
        string kind = "Officer";

        public PortraitElement()
        {
            AddToClassList("portrait");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Show(FaceSpec value, string role, string post, int rankLevel)
        {
            face = value; kind = string.IsNullOrEmpty(post) ? role : post; stripes = role == "Officer" && post != "Warden" ? rankLevel : 0;
            uniform = kind == "Warden" ? new Color(0.15f, 0.16f, 0.19f) : kind == "Nurse" ? new Color(0.53f, 0.71f, 0.79f) : kind == "Caseworker" ? new Color(0.36f, 0.45f, 0.41f) :
                kind == "Technician" ? new Color(0.30f, 0.39f, 0.44f) : new Color(0.19f, 0.27f, 0.33f);
            accent = kind == "Warden" ? new Color(0.78f, 0.62f, 0.30f) : kind == "Nurse" ? new Color(0.86f, 0.22f, 0.20f) : new Color(0.82f, 0.70f, 0.42f);
            MarkDirtyRepaint();
        }

        static void Ellipse(Painter2D p, Vector2 c, float rx, float ry)
        {
            const float k = 0.5522848f;
            p.BeginPath();
            p.MoveTo(new Vector2(c.x + rx, c.y));
            p.BezierCurveTo(new Vector2(c.x + rx, c.y + k * ry), new Vector2(c.x + k * rx, c.y + ry), new Vector2(c.x, c.y + ry));
            p.BezierCurveTo(new Vector2(c.x - k * rx, c.y + ry), new Vector2(c.x - rx, c.y + k * ry), new Vector2(c.x - rx, c.y));
            p.BezierCurveTo(new Vector2(c.x - rx, c.y - k * ry), new Vector2(c.x - k * rx, c.y - ry), new Vector2(c.x, c.y - ry));
            p.BezierCurveTo(new Vector2(c.x + k * rx, c.y - ry), new Vector2(c.x + rx, c.y - k * ry), new Vector2(c.x + rx, c.y));
            p.ClosePath();
        }
        static void FillEllipse(Painter2D p, Vector2 c, float rx, float ry, Color color) { p.fillColor = color; Ellipse(p, c, rx, ry); p.Fill(); }
        static void FillRect(Painter2D p, float x, float y, float w, float h, Color color)
        {
            p.fillColor = color; p.BeginPath(); p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(x + w, y)); p.LineTo(new Vector2(x + w, y + h)); p.LineTo(new Vector2(x, y + h)); p.ClosePath(); p.Fill();
        }
        static void Line(Painter2D p, Vector2 a, Vector2 b, float width, Color color)
        {
            p.strokeColor = color; p.lineWidth = width; p.lineCap = LineCap.Round; p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
        }

        void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (face == null || r.width < 8 || r.height < 8) return;
            Painter2D p = context.painter2D;
            float w = r.width, h = r.height, s = Mathf.Min(w, h * 0.86f);
            Vector2 c = new Vector2(r.x + w / 2, r.y + h * 0.42f);
            Color skin = SkinTones[Mathf.Clamp(face.Skin, 0, SkinTones.Length - 1)], hair = HairColors[Mathf.Clamp(face.HairColor, 0, HairColors.Length - 1)];
            Color shade = Color.Lerp(skin, Color.black, 0.22f), dark = new Color(0.10f, 0.09f, 0.09f);
            // Backdrop: a plain ID-photo wall with a height chart line.
            FillRect(p, r.x, r.y, w, h, new Color(0.73f, 0.76f, 0.75f));
            Line(p, new Vector2(r.x, r.y + h * 0.18f), new Vector2(r.x + w, r.y + h * 0.18f), 1, new Color(0.62f, 0.65f, 0.64f));
            // Shoulders and uniform.
            float shoulders = s * (0.40f + face.Build * 0.04f);
            FillEllipse(p, new Vector2(c.x, r.y + h * 1.02f), shoulders, h * 0.30f, uniform);
            FillRect(p, c.x - s * 0.07f, c.y + s * 0.24f, s * 0.14f, s * 0.16f, shade);
            if (kind == "Warden")
            {
                // Shirt collar and tie under a suit jacket.
                FillRect(p, c.x - s * 0.09f, c.y + s * 0.36f, s * 0.18f, s * 0.20f, new Color(0.92f, 0.92f, 0.90f));
                p.fillColor = new Color(0.45f, 0.12f, 0.14f); p.BeginPath(); p.MoveTo(new Vector2(c.x - s * 0.035f, c.y + s * 0.38f)); p.LineTo(new Vector2(c.x + s * 0.035f, c.y + s * 0.38f));
                p.LineTo(new Vector2(c.x + s * 0.05f, c.y + s * 0.58f)); p.LineTo(new Vector2(c.x - s * 0.05f, c.y + s * 0.58f)); p.ClosePath(); p.Fill();
            }
            else
            {
                p.fillColor = Color.Lerp(uniform, Color.black, 0.25f); p.BeginPath(); p.MoveTo(new Vector2(c.x - s * 0.12f, c.y + s * 0.36f)); p.LineTo(new Vector2(c.x, c.y + s * 0.48f));
                p.LineTo(new Vector2(c.x + s * 0.12f, c.y + s * 0.36f)); p.LineTo(new Vector2(c.x + s * 0.10f, c.y + s * 0.62f)); p.LineTo(new Vector2(c.x - s * 0.10f, c.y + s * 0.62f)); p.ClosePath(); p.Fill();
                if (kind == "Nurse") { FillRect(p, c.x + s * 0.17f, c.y + s * 0.47f, s * 0.08f, s * 0.025f, accent); FillRect(p, c.x + s * 0.198f, c.y + s * 0.442f, s * 0.025f, s * 0.08f, accent); }
                else FillRect(p, c.x - s * 0.24f, c.y + s * 0.46f, s * 0.07f, s * 0.09f, accent);
            }
            for (int i = 0; i < stripes; i++)
                foreach (int side in new[] { -1, 1 })
                    Line(p, new Vector2(c.x + side * s * 0.27f, c.y + s * (0.42f + i * 0.045f)), new Vector2(c.x + side * s * 0.37f, c.y + s * (0.45f + i * 0.045f)), Mathf.Max(1.5f, s * 0.022f), accent);
            // Head and ears.
            float hx = s * 0.21f, hy = s * 0.27f;
            if (face.Hair == 3) FillEllipse(p, new Vector2(c.x, c.y + s * 0.06f), hx * 1.18f, hy * 1.12f, hair);
            if (face.Hair == 4) FillEllipse(p, new Vector2(c.x, c.y - s * 0.07f), hx * 1.38f, hy * 1.08f, hair);
            FillEllipse(p, new Vector2(c.x - hx * 0.98f, c.y + s * 0.01f), s * 0.035f, s * 0.055f, shade);
            FillEllipse(p, new Vector2(c.x + hx * 0.98f, c.y + s * 0.01f), s * 0.035f, s * 0.055f, shade);
            FillEllipse(p, c, hx, hy, skin);
            // Hair styles: buzz, short, side part, long, curly and a bun.
            switch (face.Hair)
            {
                case 0: FillEllipse(p, new Vector2(c.x, c.y - hy * 0.72f), hx * 0.92f, hy * 0.32f, Color.Lerp(skin, hair, 0.45f)); break;
                case 1: FillEllipse(p, new Vector2(c.x, c.y - hy * 0.70f), hx * 1.02f, hy * 0.42f, hair); break;
                case 2:
                    FillEllipse(p, new Vector2(c.x, c.y - hy * 0.70f), hx * 1.04f, hy * 0.44f, hair);
                    Line(p, new Vector2(c.x - hx * 0.35f, c.y - hy * 1.05f), new Vector2(c.x - hx * 0.25f, c.y - hy * 0.62f), Mathf.Max(1, s * 0.012f), Color.Lerp(hair, skin, 0.4f));
                    break;
                case 3: FillEllipse(p, new Vector2(c.x, c.y - hy * 0.66f), hx * 1.08f, hy * 0.50f, hair); break;
                case 4: FillEllipse(p, new Vector2(c.x, c.y - hy * 0.68f), hx * 1.2f, hy * 0.52f, hair); break;
                default:
                    FillEllipse(p, new Vector2(c.x, c.y - hy * 0.72f), hx * 1.0f, hy * 0.40f, hair);
                    FillEllipse(p, new Vector2(c.x, c.y - hy * 1.12f), hx * 0.42f, hy * 0.24f, hair); break;
            }
            // Brows and eyes.
            float eyeY = c.y - hy * 0.05f, eyeX = hx * 0.42f, eyeR = s * (face.Eyes == 2 ? 0.032f : 0.026f), eyeH = face.Eyes == 1 ? eyeR * 0.55f : eyeR;
            float browW = Mathf.Max(1.2f, s * (face.Brows == 1 ? 0.024f : 0.014f));
            foreach (int side in new[] { -1, 1 })
            {
                float lift = face.Brows == 2 ? -s * 0.012f : 0;
                Line(p, new Vector2(c.x + side * eyeX * 0.55f, eyeY - s * 0.06f), new Vector2(c.x + side * eyeX * 1.45f, eyeY - s * 0.065f + lift), browW, Color.Lerp(hair, dark, 0.3f));
                FillEllipse(p, new Vector2(c.x + side * eyeX, eyeY), eyeR * 1.35f, eyeH * 1.1f, new Color(0.95f, 0.94f, 0.91f));
                FillEllipse(p, new Vector2(c.x + side * eyeX, eyeY), eyeR * 0.75f, Mathf.Min(eyeR * 0.75f, eyeH), new Color(0.20f, 0.15f, 0.11f));
                if (face.Lines >= 1) Line(p, new Vector2(c.x + side * eyeX * 1.6f, eyeY + s * 0.01f), new Vector2(c.x + side * eyeX * 1.85f, eyeY + s * 0.025f), 1, shade);
            }
            // Nose, mouth and age lines.
            Line(p, new Vector2(c.x, eyeY + s * 0.03f), new Vector2(c.x - s * 0.018f, c.y + hy * 0.32f), Mathf.Max(1, s * 0.012f), shade);
            Line(p, new Vector2(c.x - s * 0.018f, c.y + hy * 0.32f), new Vector2(c.x + s * 0.02f, c.y + hy * 0.34f), Mathf.Max(1, s * 0.012f), shade);
            Line(p, new Vector2(c.x - hx * 0.32f, c.y + hy * 0.56f), new Vector2(c.x + hx * 0.32f, c.y + hy * 0.56f), Mathf.Max(1.2f, s * 0.016f), Color.Lerp(shade, new Color(0.45f, 0.2f, 0.2f), 0.4f));
            if (face.Lines >= 2) foreach (int side in new[] { -1, 1 })
                Line(p, new Vector2(c.x + side * hx * 0.38f, c.y + hy * 0.30f), new Vector2(c.x + side * hx * 0.50f, c.y + hy * 0.58f), 1, shade);
            // Facial hair.
            if (face.Beard == 1) FillEllipse(p, new Vector2(c.x, c.y + hy * 0.62f), hx * 0.78f, hy * 0.36f, new Color(hair.r, hair.g, hair.b, 0.28f));
            if (face.Beard == 2) Line(p, new Vector2(c.x - hx * 0.36f, c.y + hy * 0.47f), new Vector2(c.x + hx * 0.36f, c.y + hy * 0.47f), Mathf.Max(2, s * 0.03f), hair);
            if (face.Beard == 3)
            {
                FillEllipse(p, new Vector2(c.x, c.y + hy * 0.72f), hx * 0.86f, hy * 0.40f, hair);
                Line(p, new Vector2(c.x - hx * 0.28f, c.y + hy * 0.56f), new Vector2(c.x + hx * 0.28f, c.y + hy * 0.56f), Mathf.Max(1.2f, s * 0.016f), Color.Lerp(skin, dark, 0.5f));
            }
            // Glasses.
            if (face.Glasses)
            {
                p.strokeColor = dark; p.lineWidth = Mathf.Max(1, s * 0.012f);
                foreach (int side in new[] { -1, 1 }) { Ellipse(p, new Vector2(c.x + side * eyeX, eyeY), eyeR * 2.2f, eyeR * 1.7f); p.Stroke(); }
                Line(p, new Vector2(c.x - eyeX + eyeR * 2.2f, eyeY), new Vector2(c.x + eyeX - eyeR * 2.2f, eyeY), Mathf.Max(1, s * 0.012f), dark);
            }
        }
    }
}
