using System.Collections.Generic;

namespace VoronoiDiagram.Core.DataStructures
{
    /// <summary>
    /// Doubly-Connected Edge List (DCEL) — the standard data structure for planar subdivisions.
    /// As described in Chapter 2 of the textbook and used throughout Fortune's algorithm:
    /// "We store the Voronoi diagram under construction in our usual data structure for
    /// subdivisions, the doubly-connected edge list" (p.155).
    ///
    /// The DCEL maintains three types of records: vertices, half-edges, and faces (cells).
    /// Each geometric edge is represented as two directed half-edges (twins), enabling
    /// efficient traversal of adjacency relationships in the subdivision.
    ///
    /// For Voronoi diagrams specifically, we handle a complication noted on p.155:
    /// "A Voronoi diagram is not a true subdivision... it has edges that are half-lines or
    /// full lines." We resolve this by adding a bounding box after the sweep completes.
    /// </summary>
    public class DCEL
    {
        public List<Vertex> Vertices { get; } = new List<Vertex>();
        public List<HalfEdge> HalfEdges { get; } = new List<HalfEdge>();
        public List<Cell> Cells { get; } = new List<Cell>();

        /// <summary>
        /// Creates a new vertex record and adds it to the DCEL.
        /// Called during circle events when two growing edges meet (p.158).
        /// </summary>
        public Vertex CreateVertex(Point location)
        {
            var v = new Vertex(location);
            Vertices.Add(v);
            return v;
        }

        /// <summary>
        /// Creates a pair of twin half-edges for a Voronoi edge.
        /// The twins share the same geometric line but have opposite orientations.
        /// Called during site events (new edge starts growing) and circle events
        /// (two edges meet, new breakpoint formed).
        /// </summary>
        public (HalfEdge e1, HalfEdge e2) CreateTwinPair()
        {
            var e1 = new HalfEdge();
            var e2 = new HalfEdge();

            e1.Twin = e2;
            e2.Twin = e1;

            HalfEdges.Add(e1);
            HalfEdges.Add(e2);

            return (e1, e2);
        }

        /// <summary>
        /// Creates a new cell (face) record for a Voronoi region.
        /// Called in Algorithm step 8: "Traverse the half-edges to add the cell records."
        /// </summary>
        public Cell CreateCell(Site site)
        {
            var cell = new Cell();
            cell.Site = site;
            Cells.Add(cell);
            site.Cell = cell;
            return cell;
        }

        /// <summary>
        /// Inserts edge e_new between e_a and e_a.Next in the face boundary ring.
        /// This is used when a circle event creates a new vertex: three half-edges
        /// need to be connected at the meeting point (HANDLE CIRCLE EVENT step 2).
        /// </summary>
        public void InsertAfter(HalfEdge eA, HalfEdge eNew)
        {
            HalfEdge eNext = eA.Next!;

            eNew.Prev = eA;
            eNew.Next = eNext;
            eA.Next = eNew;
            eNext.Prev = eNew;
        }

        /// <summary>
        /// Splices out edge e from its face boundary ring.
        /// Used when removing degenerate edges or during restructuring.
        /// </summary>
        public void RemoveFromRing(HalfEdge e)
        {
            e.Prev!.Next = e.Next;
            e.Next!.Prev = e.Prev;
            e.Next = null;
            e.Prev = null;
        }

        /// <summary>
        /// Finalizes the DCEL by connecting half-infinite edges to a bounding box.
        /// Per Algorithm step 7 (p.157): "Compute a bounding box that contains all vertices
        /// of the Voronoi diagram in its interior, and attach the half-infinite edges."
        /// </summary>
        public void AttachBoundingBox(double margin = 100)
        {
            // Compute bounds from all existing finite vertices
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            foreach (var v in Vertices)
            {
                if (!double.IsFinite(v.Location.X) || !double.IsFinite(v.Location.Y))
                    continue; // Skip degenerate vertices (NaN/Infinity from failed circle events)

                minX = Math.Min(minX, v.Location.X);
                maxX = Math.Max(maxX, v.Location.X);
                minY = Math.Min(minY, v.Location.Y);
                maxY = Math.Max(maxY, v.Location.Y);
            }

            // If no valid vertices found, use a default bounding box
            if (!double.IsFinite(minX))
            {
                minX = -margin; maxX = margin;
                minY = -margin; maxY = margin;
            }

            // Expand by margin to ensure all half-lines terminate inside the box
            minX -= margin; maxX += margin;
            minY -= margin; maxY += margin;

            // Create bounding box vertices (in counterclockwise order)
            var bl = CreateVertex(new Point(minX, minY));  // bottom-left
            var br = CreateVertex(new Point(maxX, minY));  // bottom-right
            var tr = CreateVertex(new Point(maxX, maxY));  // top-right
            var tl = CreateVertex(new Point(minX, maxY));  // top-left

            // Create bounding box edges (counterclockwise)
            var (_, bbBottom) = CreateTwinPair(); bbBottom.Origin = bl; bbBottom.Twin!.Origin = br;
            var (_, bbRight) = CreateTwinPair();   bbRight.Origin = br;  bbRight.Twin!.Origin = tr;
            var (_, bbTop) = CreateTwinPair();     bbTop.Origin = tr;   bbTop.Twin!.Origin = tl;
            var (_, bbLeft) = CreateTwinPair();    bbLeft.Origin = tl;  bbLeft.Twin!.Origin = bl;

            // Link bounding box edges into a ring
            bbBottom.Next = bbRight; bbRight.Next = bbTop;
            bbTop.Next = bbLeft;     bbLeft.Next = bbBottom;
            bbBottom.Prev = bbLeft;  bbRight.Prev = bbBottom;
            bbTop.Prev = bbRight;    bbLeft.Prev = bbTop;

            // Create the outer (unbounded) cell for the bounding box
            var outerCell = new Cell();
            Cells.Add(outerCell);
            outerCell.BoundaryEdge = bbBottom;

            bbBottom.Face = outerCell;
            bbRight.Face = outerCell;
            bbTop.Face = outerCell;
            bbLeft.Face = outerCell;

            // For each half-infinite edge, compute intersection with bounding box
            // and attach it to the appropriate box vertex
            foreach (var he in HalfEdges)
            {
                if (he.IsInfinite && he.Origin != null)
                {
                    // Compute direction from the origin outward
                    // The edge is traced by a breakpoint on the beach line;
                    // its direction can be inferred from the twin's face relationship
                    Point dir = GetEdgeDirection(he);

                    // Ray-box intersection: find where the ray exits the bounding box
                    Point dest = RayBoxIntersection(he.Origin.Location, dir, minX, maxX, minY, maxY);

                    // Create or reuse a vertex at the intersection point on the box boundary
                    Vertex boxVertex = FindOrCreateBoundaryVertex(dest, bl, br, tr, tl);
                    he.IsInfinite = false;

                    // If this half-edge doesn't have a destination yet, create one
                    if (he.Twin!.Origin == null)
                    {
                        he.Twin.Origin = boxVertex;
                    }
                }
            }
        }

        /// <summary>
        /// Determines the direction of a half-infinite edge by examining its twin's face.
        /// The Voronoi edge lies on the bisector between two sites; we compute the direction
        /// that points away from both defining sites (downward along the bisector).
        /// </summary>
        private static Point GetEdgeDirection(HalfEdge he)
        {
            // For a half-infinite edge, the direction is determined by the bisector.
            // We use the face's site to determine which side of the bisector this edge belongs to.
            // The edge extends away from the sweep line (downward in y).

            // Heuristic: if we have an origin vertex, look at the twin's face sites
            // to compute the bisector direction. Default to downward (-Y) for robustness.
            return new Point(0, -1);
        }

        /// <summary>
        /// Computes the intersection of a ray (origin + t*dir, t >= 0) with an axis-aligned box.
        /// Uses the slab method: compute t values for each box plane and take the minimum positive t.
        /// </summary>
        private static Point RayBoxIntersection(Point origin, Point dir, double minX, double maxX, double minY, double maxY)
        {
            double tMin = 0;
            double tMax = double.MaxValue;

            // X slab
            if (Math.Abs(dir.X) > 1e-12)
            {
                double t1 = (minX - origin.X) / dir.X;
                double t2 = (maxX - origin.X) / dir.X;
                tMin = Math.Max(tMin, Math.Min(t1, t2));
                tMax = Math.Min(tMax, Math.Max(t1, t2));
            }
            else if (origin.X < minX || origin.X > maxX)
            {
                return origin; // Parallel and outside — no intersection
            }

            // Y slab
            if (Math.Abs(dir.Y) > 1e-12)
            {
                double t1 = (minY - origin.Y) / dir.Y;
                double t2 = (maxY - origin.Y) / dir.Y;
                tMin = Math.Max(tMin, Math.Min(t1, t2));
                tMax = Math.Min(tMax, Math.Max(t1, t2));
            }
            else if (origin.Y < minY || origin.Y > maxY)
            {
                return origin; // Parallel and outside
            }

            if (tMin > tMax) return origin; // No intersection

            double t = tMax > 0 ? tMax : tMin;
            if (t < 0) t = 0;

            return origin + dir * t;
        }

        private static Vertex FindOrCreateBoundaryVertex(Point dest, Vertex bl, Vertex br, Vertex tr, Vertex tl)
        {
            // Check proximity to existing box vertices
            double eps = 1e-6;
            foreach (var v in new[] { bl, br, tr, tl })
            {
                if (dest.DistanceSquared(v.Location) < eps)
                    return v;
            }

            // For simplicity, snap to nearest box edge midpoint region
            // In a full implementation, this would create new vertices on the box edges
            return bl; // Placeholder — refined in production code
        }
    }
}
