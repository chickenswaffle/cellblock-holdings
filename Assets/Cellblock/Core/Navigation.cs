using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // A navigation lattice is an internal approximation; editable geometry is not restricted to its grid.
    public sealed class Navigation
    {
        const float Cell = 0.65f, Clearance = 0.18f;
        readonly int width = (int)(Geometry.SiteX * 2 / Cell) + 1;
        readonly int height = (int)(Geometry.SiteY * 2 / Cell) + 1;
        readonly List<Segment> barriers;
        readonly Dictionary<long, bool> links = new Dictionary<long, bool>();
        public Navigation(IEnumerable<Wall> walls, IEnumerable<Fixture> fixtures = null)
        {
            barriers = walls.SelectMany(w => Geometry.Segments(w, true)).ToList();
            if (fixtures != null)
                foreach (Fixture fixture in fixtures)
                {
                    var points = Geometry.Footprint(fixture);
                    for (int i = 0; i < points.Count; i++) barriers.Add(new Segment(points[i], points[(i + 1) % points.Count]));
                }
        }
        public bool Clear(Point a, Point b)
        {
            if (Math.Abs(a.X) >= Geometry.SiteX || Math.Abs(a.Y) >= Geometry.SiteY ||
                Math.Abs(b.X) >= Geometry.SiteX || Math.Abs(b.Y) >= Geometry.SiteY) return false;
            var travel = new Segment(a, b);
            foreach (Segment barrier in barriers)
                if (Geometry.SegmentDistance(travel, barrier) <= Clearance) return false;
            return true;
        }
        Point Position(int index) { return new Point(index % width * Cell - Geometry.SiteX, index / width * Cell - Geometry.SiteY); }
        int Closest(Point p)
        {
            int x = (int)Math.Round((p.X + Geometry.SiteX) / Cell), y = (int)Math.Round((p.Y + Geometry.SiteY) / Cell);
            int best = -1;
            float distance = float.MaxValue;
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int px = x + dx, py = y + dy;
                    if (px < 0 || py < 0 || px >= width || py >= height) continue;
                    int index = py * width + px;
                    float d = Geometry.Distance(p, Position(index));
                    if (d < distance && Clear(p, Position(index))) { best = index; distance = d; }
                }
            return best;
        }
        bool Link(int a, int b)
        {
            long key = (long)Math.Min(a, b) * width * height + Math.Max(a, b);
            bool clear;
            if (!links.TryGetValue(key, out clear)) { clear = Clear(Position(a), Position(b)); links[key] = clear; }
            return clear;
        }
        public List<Point> Find(Point from, Point to)
        {
            if (Clear(from, to)) return new List<Point> { to };
            int start = Closest(from), goal = Closest(to);
            if (start < 0 || goal < 0) return null;
            int count = width * height;
            var cost = new float[count]; var previous = new int[count]; var closed = new bool[count];
            for (int i = 0; i < count; i++) { cost[i] = float.MaxValue; previous[i] = -1; }
            var open = new MinHeap(); cost[start] = 0; open.Add(start, 0);
            while (open.Count > 0)
            {
                int current = open.Pop();
                if (closed[current]) continue;
                if (current == goal)
                {
                    var path = new List<Point> { to };
                    for (int node = goal; node != start; node = previous[node]) path.Add(Position(node));
                    path.Add(Position(start)); path.Reverse();
                    return path;
                }
                closed[current] = true;
                int x = current % width, y = current / width;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        int next = ny * width + nx;
                        if (closed[next] || !Link(current, next)) continue;
                        float candidate = cost[current] + (dx == 0 || dy == 0 ? Cell : Cell * 1.41421356f);
                        if (candidate >= cost[next]) continue;
                        cost[next] = candidate; previous[next] = current;
                        open.Add(next, candidate + Geometry.Distance(Position(next), Position(goal)));
                    }
            }
            return null;
        }

        sealed class MinHeap
        {
            readonly List<Tuple<int, float>> items = new List<Tuple<int, float>>();
            public int Count { get { return items.Count; } }
            public void Add(int id, float priority)
            {
                items.Add(Tuple.Create(id, priority)); int i = items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (items[parent].Item2 <= items[i].Item2) break;
                    var swap = items[parent]; items[parent] = items[i]; items[i] = swap; i = parent;
                }
            }
            public int Pop()
            {
                int result = items[0].Item1;
                items[0] = items[items.Count - 1]; items.RemoveAt(items.Count - 1); int i = 0;
                while (i * 2 + 1 < items.Count)
                {
                    int child = i * 2 + 1;
                    if (child + 1 < items.Count && items[child + 1].Item2 < items[child].Item2) child++;
                    if (items[i].Item2 <= items[child].Item2) break;
                    var swap = items[i]; items[i] = items[child]; items[child] = swap; i = child;
                }
                return result;
            }
        }
    }
}
