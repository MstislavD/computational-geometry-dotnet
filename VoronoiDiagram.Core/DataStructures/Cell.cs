using System.Collections.Generic;

namespace VoronoiDiagram.Core.DataStructures
{
    /// <summary>
    /// A face (Voronoi cell) in the Doubly-Connected Edge List.
    /// Each cell V(pi) corresponds to one site pi and contains all points q such that
    /// dist(q, pi) < dist(q, pj) for every other site pj (Observation 7.1).
    /// Equivalently: V(pi) = intersection of half-planes h(pi, pj) for j != i.
    /// Each cell is a convex polygonal region bounded by at most n-1 edges.
    /// </summary>
    public class Cell
    {
        // The site that owns this Voronoi cell
        public Site? Site { get; internal set; }

        // One of the half-edges on the boundary of this cell
        public HalfEdge? BoundaryEdge { get; internal set; }

        // Vertices recorded directly during circle events — avoids Origin overwrite issues.
        public List<Point> RecordedVertices { get; } = new List<Point>();

        /// <summary>
        /// Collects all vertices of this cell in order by traversing the half-edge ring.
        /// The cell is convex (Observation 7.1), so these form a convex polygon.
        /// </summary>
        public List<Point> GetVertices()
        {
            if (RecordedVertices.Count > 0) return new List<Point>(RecordedVertices);

            var vertices = new List<Point>();

            if (BoundaryEdge == null) return vertices;

            HalfEdge? edge = BoundaryEdge;
            do
            {
                if (edge.Origin != null)
                    vertices.Add(edge.Origin.Location);

                edge = edge.Next;
            } while (edge != null && edge != BoundaryEdge);

            return vertices;
        }

        public override string ToString() => $"Cell for Site[{Site?.Id ?? -1}]";
    }
}
