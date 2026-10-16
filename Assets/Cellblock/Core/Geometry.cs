using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    [Serializable]
    public struct Point
    {
        public float X, Y;
        public Point(float x, float y) { X = x; Y = y; }
        public static Point operator +(Point a, Point b) { return new Point(a.X + b.X, a.Y + b.Y); }
        public static Point operator -(Point a, Point b) { return new Point(a.X - b.X, a.Y - b.Y); }
        public static Point operator *(Point a, float n) { return new Point(a.X * n, a.Y * n); }
        public float Length { get { return (float)Math.Sqrt(X * X + Y * Y); } }
        public override string ToString() { return X.ToString("0.0") + ", " + Y.ToString("0.0"); }
    }

    public struct Segment
    {
        public Point A, B;
        public Segment(Point a, Point b) { A = a; B = b; }
    }

    [Serializable]
    public sealed class Wall
    {
        public string Id = "";
        public Point A, B, Control;
        public bool Curved;
        public float Door = -1;
        public float DoorWidth = 1.6f;
        public int Finish;
        public Wall Copy()
        {
            return new Wall { Id = Id, A = A, B = B, Control = Control, Curved = Curved,
                Door = Door, DoorWidth = DoorWidth, Finish = Finish };
        }
        public Point At(float t)
        {
            return Curved ? A * ((1 - t) * (1 - t)) + Control * (2 * (1 - t) * t) + B * (t * t)
                : A + (B - A) * t;
        }
    }

    public static class Geometry
    {
        public const float SiteX = 30, SiteY = 24;
        public static float Dot(Point a, Point b) { return a.X * b.X + a.Y * b.Y; }
        public static float Cross(Point a, Point b) { return a.X * b.Y - a.Y * b.X; }
        public static float Distance(Point a, Point b) { return (a - b).Length; }
        public static float Clamp(float n, float lo, float hi) { return Math.Max(lo, Math.Min(hi, n)); }
        public static float Parameter(Point p, Point a, Point b)
        {
            Point d = b - a;
            return Dot(p - a, d) / Math.Max(0.000001f, Dot(d, d));
        }
        public static float DistanceToSegment(Point p, Point a, Point b)
        {
            return Distance(p, a + (b - a) * Clamp(Parameter(p, a, b), 0, 1));
        }
        public static bool Intersection(Segment a, Segment b, out float t, out float u)
        {
            Point d = a.B - a.A, e = b.B - b.A;
            float denominator = Cross(d, e);
            t = u = 0;
            if (Math.Abs(denominator) < 0.00001f) return false;
            t = Cross(b.A - a.A, e) / denominator;
            u = Cross(b.A - a.A, d) / denominator;
            return t >= -0.0001f && t <= 1.0001f && u >= -0.0001f && u <= 1.0001f;
        }
        public static float SegmentDistance(Segment a, Segment b)
        {
            float t, u;
            if (Intersection(a, b, out t, out u)) return 0;
            return Math.Min(Math.Min(DistanceToSegment(a.A, b.A, b.B), DistanceToSegment(a.B, b.A, b.B)),
                Math.Min(DistanceToSegment(b.A, a.A, a.B), DistanceToSegment(b.B, a.A, a.B)));
        }
        public static List<Point> Sample(Wall wall)
        {
            int count = wall.Curved ? Math.Max(12, (int)Math.Min(512, Math.Ceiling((Distance(wall.A, wall.Control) +
                Distance(wall.Control, wall.B)) / 0.4f))) : 1;
            var result = new List<Point>(count + 1);
            for (int i = 0; i <= count; i++) result.Add(wall.At(i / (float)count));
            return result;
        }
        public static float Length(Wall wall)
        {
            var points = Sample(wall);
            float length = 0;
            for (int i = 1; i < points.Count; i++) length += Distance(points[i - 1], points[i]);
            return length;
        }
        // The same sampled geometry defines visible walls, collision, and navigation.
        // Door is a fraction of arc length, including on curved walls.
        public static List<Segment> Segments(Wall wall, bool openings)
        {
            var points = Sample(wall);
            float length = Length(wall), cursor = 0;
            float from = wall.Door * length - wall.DoorWidth / 2;
            float to = wall.Door * length + wall.DoorWidth / 2;
            var result = new List<Segment>();
            for (int i = 1; i < points.Count; i++)
            {
                Point a = points[i - 1], b = points[i];
                float size = Distance(a, b), end = cursor + size;
                if (!openings || wall.Door < 0 || end <= from || cursor >= to)
                    result.Add(new Segment(a, b));
                else
                {
                    if (cursor < from) result.Add(new Segment(a, a + (b - a) * ((from - cursor) / size)));
                    if (end > to) result.Add(new Segment(a + (b - a) * ((to - cursor) / size), b));
                }
                cursor = end;
            }
            return result;
        }
        public static Point OnLength(Wall wall, float fraction)
        {
            var points = Sample(wall);
            float remaining = Length(wall) * Clamp(fraction, 0, 1);
            for (int i = 1; i < points.Count; i++)
            {
                float length = Distance(points[i - 1], points[i]);
                if (remaining <= length) return points[i - 1] + (points[i] - points[i - 1]) * (remaining / length);
                remaining -= length;
            }
            return wall.B;
        }
        public static float NearestFraction(Wall wall, Point p)
        {
            var points = Sample(wall);
            float best = float.MaxValue, distance = 0, nearest = 0, total = Length(wall);
            for (int i = 1; i < points.Count; i++)
            {
                Point a = points[i - 1], b = points[i];
                float t = Clamp(Parameter(p, a, b), 0, 1), length = Distance(a, b);
                float candidate = Distance(p, a + (b - a) * t);
                if (candidate < best) { best = candidate; nearest = distance + length * t; }
                distance += length;
            }
            return nearest / Math.Max(total, 0.001f);
        }
        public static Point Snap(Point p, IEnumerable<Wall> walls, bool grid)
        {
            Point best = p;
            float distance = 0.65f;
            foreach (Wall wall in walls)
            {
                var points = Sample(wall);
                for (int i = 1; i < points.Count; i++)
                {
                    Point a = points[i - 1], b = points[i];
                    Point on = a + (b - a) * Clamp(Parameter(p, a, b), 0, 1);
                    float d = Distance(p, on);
                    if (d < distance) { best = on; distance = d; }
                }
                foreach (Point end in new[] { wall.A, wall.B })
                    if (Distance(p, end) < 0.4f) return end;
            }
            if (distance < 0.65f) return best;
            return grid ? new Point((float)Math.Round(p.X * 2) / 2, (float)Math.Round(p.Y * 2) / 2) : p;
        }
        public static string Validate(Wall wall)
        {
            if (wall == null) return "The wall data is missing.";
            foreach (float value in new[] { wall.A.X, wall.A.Y, wall.B.X, wall.B.Y, wall.Control.X,
                wall.Control.Y, wall.Door, wall.DoorWidth })
                if (float.IsNaN(value) || float.IsInfinity(value)) return "Wall dimensions must be finite numbers.";
            var points = Sample(wall);
            foreach (Point p in points)
                if (float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsInfinity(p.X) || float.IsInfinity(p.Y) ||
                    Math.Abs(p.X) > SiteX || Math.Abs(p.Y) > SiteY) return "Keep the construction inside the property boundary.";
            float length = Length(wall);
            if (length < 0.65f || Distance(wall.A, wall.B) < 0.5f) return "Give this wall at least 0.65 m of length and distinct ends.";
            if (wall.Door >= 0 && (wall.Door > 1 || wall.DoorWidth < 1.2f ||
                wall.Door * length < wall.DoorWidth / 2 + 0.2f ||
                (1 - wall.Door) * length < wall.DoorWidth / 2 + 0.2f))
                return "Move the doorway farther from the wall ends, or lengthen the wall.";
            for (int i = 1; i < points.Count; i++)
                for (int j = i + 2; j < points.Count; j++)
                    if (SegmentDistance(new Segment(points[i - 1], points[i]), new Segment(points[j - 1], points[j])) < 0.001f)
                        return "This curve folds back onto itself. Move its bend handle.";
            return "";
        }
        public static bool Equivalent(Wall a, Wall b)
        {
            bool ends = (Distance(a.A, b.A) < 0.01f && Distance(a.B, b.B) < 0.01f) ||
                (Distance(a.A, b.B) < 0.01f && Distance(a.B, b.A) < 0.01f);
            return ends && a.Curved == b.Curved && (!a.Curved || Distance(a.Control, b.Control) < 0.01f);
        }
        // Subtract collinear shared boundaries. Prefab attachment must not duplicate a wall or its bill.
        public static List<Wall> Uncovered(Wall candidate, IEnumerable<Wall> existing)
        {
            var walls = existing.ToList();
            if (walls.Any(w => Equivalent(candidate, w))) return new List<Wall>();
            if (candidate.Curved) return new List<Wall> { candidate.Copy() };
            var intervals = new List<Tuple<float, float>> { Tuple.Create(0f, 1f) };
            Point delta = candidate.B - candidate.A;
            foreach (Wall wall in walls.Where(w => !w.Curved))
            {
                if (Math.Abs(Cross(delta, wall.A - candidate.A)) > 0.01f ||
                    Math.Abs(Cross(delta, wall.B - candidate.A)) > 0.01f) continue;
                float ta = Parameter(wall.A, candidate.A, candidate.B), tb = Parameter(wall.B, candidate.A, candidate.B);
                float lo = Math.Min(ta, tb), hi = Math.Max(ta, tb);
                var next = new List<Tuple<float, float>>();
                foreach (var interval in intervals)
                {
                    if (hi <= interval.Item1 || lo >= interval.Item2) { next.Add(interval); continue; }
                    if (lo > interval.Item1) next.Add(Tuple.Create(interval.Item1, lo));
                    if (hi < interval.Item2) next.Add(Tuple.Create(hi, interval.Item2));
                }
                intervals = next;
            }
            var result = new List<Wall>();
            foreach (var interval in intervals)
            {
                if ((interval.Item2 - interval.Item1) * delta.Length < 0.01f) continue;
                Wall part = candidate.Copy();
                part.Id = candidate.Id + "/" + result.Count;
                part.A = candidate.A + delta * interval.Item1;
                part.B = candidate.A + delta * interval.Item2;
                if (part.Door >= 0)
                {
                    float half = candidate.DoorWidth / (2 * delta.Length);
                    if (candidate.Door + half <= interval.Item1 || candidate.Door - half >= interval.Item2) part.Door = -1;
                    else if (candidate.Door - half < interval.Item1 || candidate.Door + half > interval.Item2)
                        throw new ArgumentException("The doorway crosses a shared-wall join. Move the opening or edit the shared wall.");
                    else part.Door = (candidate.Door - interval.Item1) / (interval.Item2 - interval.Item1);
                }
                result.Add(part);
            }
            return result;
        }

        public static float Area(IList<Point> polygon)
        {
            float sum = 0;
            for (int i = 0; i < polygon.Count; i++) sum += Cross(polygon[i], polygon[(i + 1) % polygon.Count]);
            return sum / 2;
        }
        // Build planar faces from the approved geometry, including split T and crossing junctions.
        public static List<List<Point>> Rooms(IEnumerable<Wall> walls)
        {
            var edges = walls.SelectMany(w => Segments(w, false)).ToList();
            var vertices = new List<Point>();
            var lookup = new Dictionary<string, int>();
            var neighbors = new Dictionary<int, HashSet<int>>();
            Func<Point, int> vertex = p =>
            {
                string key = Math.Round(p.X * 1000) + ":" + Math.Round(p.Y * 1000);
                int index;
                if (lookup.TryGetValue(key, out index)) return index;
                index = vertices.Count; vertices.Add(p); lookup.Add(key, index); neighbors[index] = new HashSet<int>();
                return index;
            };
            for (int i = 0; i < edges.Count; i++)
            {
                Segment edge = edges[i];
                var splits = new List<float> { 0, 1 };
                for (int j = 0; j < edges.Count; j++)
                {
                    if (i == j) continue;
                    float t, u;
                    if (Intersection(edge, edges[j], out t, out u)) splits.Add(Clamp(t, 0, 1));
                    foreach (Point end in new[] { edges[j].A, edges[j].B })
                        if (DistanceToSegment(end, edge.A, edge.B) < 0.001f)
                            splits.Add(Clamp(Parameter(end, edge.A, edge.B), 0, 1));
                }
                splits.Sort();
                for (int s = 1; s < splits.Count; s++)
                {
                    if (splits[s] - splits[s - 1] < 0.00001f) continue;
                    int a = vertex(edge.A + (edge.B - edge.A) * splits[s - 1]);
                    int b = vertex(edge.A + (edge.B - edge.A) * splits[s]);
                    if (a != b) { neighbors[a].Add(b); neighbors[b].Add(a); }
                }
            }
            var ordered = neighbors.ToDictionary(k => k.Key, k => k.Value.OrderBy(n =>
                Math.Atan2(vertices[n].Y - vertices[k.Key].Y, vertices[n].X - vertices[k.Key].X)).ToList());
            var visited = new HashSet<long>();
            var rooms = new List<List<Point>>();
            foreach (var pair in ordered)
                foreach (int end in pair.Value)
                {
                    int a = pair.Key, b = end, startA = a, startB = b;
                    var face = new List<Point>();
                    bool closed = false;
                    for (int step = 0; step <= edges.Count * 8 + 8; step++)
                    {
                        long key = ((long)a << 32) | (uint)b;
                        if (!visited.Add(key)) { closed = a == startA && b == startB; break; }
                        face.Add(vertices[a]);
                        List<int> around = ordered[b];
                        int next = around[(around.IndexOf(a) + around.Count - 1) % around.Count];
                        a = b; b = next;
                    }
                    if (closed && face.Count >= 3 && Area(face) > 0.25f) rooms.Add(face);
                }
            return rooms;
        }
        public static bool Contains(IList<Point> polygon, Point p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
                if ((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y) &&
                    p.X < (polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) /
                    (polygon[j].Y - polygon[i].Y) + polygon[i].X) inside = !inside;
            return inside;
        }
        public static List<Point> Footprint(Fixture fixture)
        {
            FixtureDefinition definition = FixtureCatalog.Get(fixture.Kind);
            if (definition == null) throw new ArgumentException("Unknown furnishing: " + fixture.Kind);
            float x = definition.Width / 2, y = definition.Depth / 2;
            double angle = -fixture.Rotation * Math.PI / 180;
            var result = new List<Point>();
            foreach (Point p in new[] { new Point(-x,-y), new Point(x,-y), new Point(x,y), new Point(-x,y) })
                result.Add(fixture.Position + new Point((float)(p.X * Math.Cos(angle) - p.Y * Math.Sin(angle)), (float)(p.X * Math.Sin(angle) + p.Y * Math.Cos(angle))));
            return result;
        }
        public static Point AccessPoint(Fixture fixture)
        {
            FixtureDefinition definition = FixtureCatalog.Get(fixture.Kind);
            if (definition == null) throw new ArgumentException("Unknown furnishing: " + fixture.Kind);
            double angle = -fixture.Rotation * Math.PI / 180;
            return fixture.Position + new Point((float)(definition.AccessX * Math.Cos(angle) - definition.AccessY * Math.Sin(angle)),
                (float)(definition.AccessX * Math.Sin(angle) + definition.AccessY * Math.Cos(angle)));
        }
        public static bool Overlap(IList<Point> a, IList<Point> b)
        {
            foreach (var polygon in new[] { a, b })
                for (int i = 0; i < polygon.Count; i++)
                {
                    Point delta = polygon[(i + 1) % polygon.Count] - polygon[i], axis = new Point(-delta.Y, delta.X);
                    float aMin = a.Min(p => Dot(p, axis)), aMax = a.Max(p => Dot(p, axis));
                    float bMin = b.Min(p => Dot(p, axis)), bMax = b.Max(p => Dot(p, axis));
                    if (aMax <= bMin + 0.02f || bMax <= aMin + 0.02f) return false;
                }
            return true;
        }
        // Trapezoid decomposition avoids overlapping floors for disconnected nested room boundaries.
        public static List<Point> FloorTriangles(List<List<Point>> rooms, int index)
        {
            var boundary = rooms[index];
            var children = rooms.Where(r => r != boundary && Math.Abs(Area(r)) < Math.Abs(Area(boundary)) && Contains(boundary, r[0])).ToList();
            var holes = children.Where(r => !children.Any(other => other != r && Math.Abs(Area(other)) > Math.Abs(Area(r)) && Contains(other, r[0]))).ToList();
            var loops = new List<List<Point>> { boundary }; loops.AddRange(holes);
            var levels = loops.SelectMany(r => r.Select(p => p.Y)).Distinct().OrderBy(y => y).ToList();
            var triangles = new List<Point>();
            for (int band = 1; band < levels.Count; band++)
            {
                float low = levels[band - 1], high = levels[band];
                if (high - low < 0.00001f) continue;
                float middle = (low + high) / 2;
                var crossings = new List<Segment>();
                foreach (var loop in loops)
                    for (int i = 0; i < loop.Count; i++)
                    {
                        Point a = loop[i], b = loop[(i + 1) % loop.Count];
                        if ((a.Y < middle && b.Y > middle) || (b.Y < middle && a.Y > middle)) crossings.Add(new Segment(a, b));
                    }
                Func<Segment, float, float> at = (edge, y) => edge.A.X + (edge.B.X - edge.A.X) * (y - edge.A.Y) / (edge.B.Y - edge.A.Y);
                crossings.Sort((a, b) => at(a, middle).CompareTo(at(b, middle)));
                for (int i = 1; i < crossings.Count; i += 2)
                {
                    Point a = new Point(at(crossings[i - 1], low), low), b = new Point(at(crossings[i], low), low);
                    Point c = new Point(at(crossings[i], high), high), d = new Point(at(crossings[i - 1], high), high);
                    if (Math.Abs(Cross(b - a, c - a)) > 0.00001f) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
                    if (Math.Abs(Cross(c - a, d - a)) > 0.00001f) { triangles.Add(a); triangles.Add(c); triangles.Add(d); }
                }
            }
            return triangles;
        }
        public static List<int> Triangulate(IList<Point> polygon)
        {
            var remaining = Enumerable.Range(0, polygon.Count).ToList();
            var triangles = new List<int>();
            int guard = polygon.Count * polygon.Count;
            while (remaining.Count > 2 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if (Cross(polygon[b] - polygon[a], polygon[c] - polygon[b]) <= 0.000001f) continue;
                    bool occupied = remaining.Any(n => n != a && n != b && n != c &&
                        Cross(polygon[b] - polygon[a], polygon[n] - polygon[a]) > 0.000001f &&
                        Cross(polygon[c] - polygon[b], polygon[n] - polygon[b]) > 0.000001f &&
                        Cross(polygon[a] - polygon[c], polygon[n] - polygon[c]) > 0.000001f);
                    if (occupied) continue;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    remaining.RemoveAt(i); clipped = true; break;
                }
                if (!clipped)
                {
                    // Remove only redundant collinear vertices; never fill an invalid polygon with a fan.
                    int redundant = remaining.FindIndex(b =>
                    {
                        int i = remaining.IndexOf(b);
                        Point a = polygon[remaining[(i + remaining.Count - 1) % remaining.Count]];
                        Point c = polygon[remaining[(i + 1) % remaining.Count]];
                        return Math.Abs(Cross(polygon[b] - a, c - polygon[b])) < 0.00001f;
                    });
                    if (redundant < 0) throw new InvalidOperationException("Room boundary could not be triangulated.");
                    remaining.RemoveAt(redundant);
                }
            }
            return triangles;
        }
    }
}
