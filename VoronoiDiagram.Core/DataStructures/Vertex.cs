namespace VoronoiDiagram.Core.DataStructures
{
    /// <summary>
    /// Vertex in the Doubly-Connected Edge List (DCEL).
    /// Represents a point where two or more Voronoi edges meet.
    /// Per Theorem 7.4: a vertex q is where the largest empty circle CP(q)
    /// contains three or more sites on its boundary.
    /// </summary>
    public class Vertex
    {
        public Point Location { get; }

        // Pointer to one of the half-edges originating from this vertex
        public HalfEdge? IncidentEdge { get; internal set; }

        public Vertex(Point location)
        {
            Location = location;
        }

        public override string ToString() => $"Vertex at {Location}";
    }
}
