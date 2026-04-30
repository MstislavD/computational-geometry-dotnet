namespace VoronoiDiagram.Core.DataStructures
{
    /// <summary>
    /// Half-edge in the Doubly-Connected Edge List (DCEL).
    /// Each edge of the Voronoi diagram is split into two directed half-edges.
    /// This rich pointer structure enables efficient traversal:
    /// - Twin: the opposite-directed half-edge on the same geometric edge
    /// - Next/Prev: adjacent half-edges along the same face boundary
    /// - Origin: the vertex this half-edge starts from
    /// - Face: the cell (face) to the left of this half-edge
    ///
    /// In Fortune's algorithm, breakpoints on the beach line trace out Voronoi edges.
    /// Each breakpoint corresponds to an internal node in the beach line tree and
    /// stores a pointer to one of the two half-edges being traced (p.155).
    /// </summary>
    public class HalfEdge
    {
        // The vertex where this half-edge originates
        public Vertex? Origin { get; internal set; }

        // The face (Voronoi cell) to the left of this directed edge
        public Cell? Face { get; internal set; }

        // The twin half-edge: same geometric edge, opposite direction
        public HalfEdge? Twin { get; internal set; }

        // Next and previous half-edges in the face boundary ring
        public HalfEdge? Next { get; internal set; }
        public HalfEdge? Prev { get; internal set; }

        /// <summary>
        /// The destination vertex. For half-infinite edges (half-lines), this is null
        /// until the bounding box is attached (Algorithm step 7, p.157).
        /// </summary>
        public Vertex? Destination => Twin?.Origin;

        // Internal flag to mark half-edges as infinite rays during construction
        internal bool IsInfinite { get; set; }

        /// <summary>
        /// Returns the geometric direction vector of this edge.
        /// Used for computing bounding box intersections and visualization.
        /// </summary>
        public Point Direction => Origin != null && Destination != null
            ? new Point(Destination.Location.X - Origin.Location.X,
                        Destination.Location.Y - Origin.Location.Y)
            : Point.Zero;

        public override string ToString()
        {
            var originStr = Origin != null ? Origin.Location.ToString() : "null";
            var destStr = Destination != null ? Destination.Location.ToString() : "null";
            return $"HalfEdge({originStr} -> {destStr})";
        }
    }
}
