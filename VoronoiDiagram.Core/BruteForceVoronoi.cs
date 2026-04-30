using System;
using System.Collections.Generic;
using VoronoiDiagram.Core.DataStructures;

namespace VoronoiDiagram.Core
{
    /// <summary>
    /// Brute-force Voronoi diagram via half-plane intersection (Observation 7.1, p.149).
    ///
    /// For each site pi: V(pi) = intersection of n-1 half-planes h(pi, pj), j != i.
    /// Each cell is a convex polygon computed by clipping against successive bisectors.
    /// This yields an O(n^2 log n) algorithm — slower than Fortune's but simpler and robust.
    /// Used here as a reference implementation for verification and fallback.
    ///
    /// The Sutherland-Hodgman polygon clipping algorithm is used to compute each cell:
    /// start with a large bounding box, then clip it against each bisector half-plane.
    /// </summary>
    public class BruteForceVoronoi
    {
        private readonly List<Site> _sites;

        public List<CellResult> Cells { get; } = new List<CellResult>();

        public BruteForceVoronoi(List<Point> sites)
        {
            _sites = new List<Site>();
            for (int i = 0; i < sites.Count; i++)
                _sites.Add(new Site(i, sites[i]));
        }

        /// <summary>
        /// Computes all Voronoi cells by half-plane intersection.
        /// Each cell starts as a large bounding box and is clipped against each bisector.
        /// </summary>
        public void Compute(double bound = 1000)
        {
            // Initial clipping polygon: large axis-aligned square
            var initialPoly = new List<Point>
            {
                new Point(-bound, -bound),
                new Point(bound, -bound),
                new Point(bound, bound),
                new Point(-bound, bound)
            };

            for (int i = 0; i < _sites.Count; i++)
            {
                Site pi = _sites[i];
                var cellPoly = new List<Point>(initialPoly);

                // Clip against the bisector half-plane for each other site pj
                for (int j = 0; j < _sites.Count; j++)
                {
                    if (i == j) continue;

                    Site pj = _sites[j];
                    cellPoly = ClipPolygon(cellPoly, pi, pj);

                    if (cellPoly.Count == 0) break; // Cell is empty (shouldn't happen for valid inputs)
                }

                Cells.Add(new CellResult(pi, cellPoly));
            }
        }

        /// <summary>
        /// Sutherland-Hodgman clip: retains only the portion of the polygon that lies
        /// in the half-plane h(pi, pj) — i.e., closer to pi than to pj.
        /// The clipping boundary is the perpendicular bisector of segment pij.
        /// </summary>
        private static List<Point> ClipPolygon(List<Point> poly, Site pi, Site pj)
        {
            var output = new List<Point>();

            if (poly.Count == 0) return output;

            // Bisector: the line equidistant from pi and pj.
            // Half-plane h(pi, pj): points q where dist(q, pi) < dist(q, pj).
            // This is equivalent to: (q - midpoint) . (pi - pj) > 0
            // Or: 2*q.(pi - pj) > |pi|^2 - |pj|^2

            Point a = pi.Location;
            Point b = pj.Location;

            double ax = a.X, ay = a.Y;
            double bx = b.X, by = b.Y;

            // Half-plane: (ax-bx)*qx + (ay-by)*qy > 0.5*(ax^2+ay^2 - bx^2-by^2)
            double nx = ax - bx;  // Normal direction toward pi's side
            double ny = ay - by;
            double c = 0.5 * (ax * ax + ay * ay - bx * bx - by * by);

            Point S = poly[poly.Count - 1]; // Start with last vertex
            bool sInside = IsInside(S, nx, ny, c);

            foreach (Point E in poly)
            {
                bool eInside = IsInside(E, nx, ny, c);

                if (eInside)
                {
                    if (!sInside)
                    {
                        // Edge crosses the bisector — compute intersection
                        Point I = ComputeIntersection(S, E, nx, ny, c);
                        output.Add(I);
                    }
                    output.Add(E);
                }
                else if (sInside)
                {
                    // Edge exits the half-plane — compute intersection
                    Point I = ComputeIntersection(S, E, nx, ny, c);
                    output.Add(I);
                }

                S = E;
                sInside = eInside;
            }

            return output;
        }

        /// <summary>
        /// Tests whether point q lies in the half-plane closer to pi than pj.
        /// Half-plane equation: nx*qx + ny*qy > c
        /// </summary>
        private static bool IsInside(Point q, double nx, double ny, double c)
        {
            return (nx * q.X + ny * q.Y) > c - 1e-9; // Epsilon tolerance for robustness
        }

        /// <summary>
        /// Computes the intersection of segment SE with the bisector line nx*x + ny*y = c.
        /// Parametric form: P(t) = S + t*(E - S), solve for t where P(t) lies on the line.
        /// </summary>
        private static Point ComputeIntersection(Point s, Point e, double nx, double ny, double c)
        {
            double dx = e.X - s.X;
            double dy = e.Y - s.Y;

            double denom = nx * dx + ny * dy;
            if (Math.Abs(denom) < 1e-12)
                return new Point((s.X + e.X) / 2, (s.Y + e.Y) / 2); // Parallel — use midpoint

            double t = (c - nx * s.X - ny * s.Y) / denom;
            t = Math.Clamp(t, 0, 1); // Clamp to segment bounds for numerical safety

            return new Point(s.X + t * dx, s.Y + t * dy);
        }

        /// <summary>
        /// Computes the Voronoi edges as shared boundaries between adjacent cells.
        /// Two cells are adjacent if they share an edge on their polygonal boundary.
        /// </summary>
        public List<EdgeResult> GetEdges()
        {
            var edges = new List<EdgeResult>();
            var seen = new HashSet<string>();

            for (int i = 0; i < Cells.Count; i++)
            {
                for (int j = i + 1; j < Cells.Count; j++)
                {
                    var shared = FindSharedEdge(Cells[i].Vertices, Cells[j].Vertices);
                    if (shared.Count >= 2)
                    {
                        string key = $"{i}-{j}";
                        if (!seen.Contains(key))
                        {
                            seen.Add(key);
                            edges.Add(new EdgeResult(Cells[i].Site, Cells[j].Site, shared));
                        }
                    }
                }
            }

            return edges;
        }

        /// <summary>
        /// Finds the shared boundary between two convex polygons.
        /// Compares edges of both polygons and returns vertices that appear in both.
        /// </summary>
        private static List<Point> FindSharedEdge(List<Point> polyA, List<Point> polyB)
        {
            var shared = new List<Point>();

            foreach (var pa in polyA)
            {
                foreach (var pb in polyB)
                {
                    if (pa.DistanceSquared(pb) < 1e-6) // Within tolerance
                    {
                        bool alreadyAdded = false;
                        foreach (var s in shared)
                        {
                            if (s.DistanceSquared(pa) < 1e-6)
                            {
                                alreadyAdded = true;
                                break;
                            }
                        }
                        if (!alreadyAdded && shared.Count < polyA.Count) // Avoid duplicates and degenerate cases
                            shared.Add(pa);
                    }
                }
            }

            return shared;
        }
    }

    /// <summary>
    /// Result of computing a single Voronoi cell.
    /// The vertices form a convex polygon (possibly unbounded, clipped to the bounding box).
    /// </summary>
    public class CellResult
    {
        public Site Site { get; }
        public List<Point> Vertices { get; }

        public CellResult(Site site, List<Point> vertices)
        {
            Site = site;
            Vertices = vertices;
        }
    }

    /// <summary>
    /// A Voronoi edge shared between two cells.
    /// The edge lies on the perpendicular bisector of the segment connecting the two sites.
    /// </summary>
    public class EdgeResult
    {
        public Site SiteA { get; }
        public Site SiteB { get; }
        public List<Point> Vertices { get; }

        public EdgeResult(Site siteA, Site siteB, List<Point> vertices)
        {
            SiteA = siteA;
            SiteB = siteB;
            Vertices = vertices;
        }
    }
}
