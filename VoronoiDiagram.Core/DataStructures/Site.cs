namespace VoronoiDiagram.Core.DataStructures
{
    /// <summary>
    /// A point site in the Voronoi diagram.
    /// Each site has a unique ID for tracking and an associated Voronoi cell.
    /// Per the textbook (p.148): "Let P = {p1, p2, ..., pn} be a set of n distinct points."
    /// </summary>
    public class Site
    {
        public int Id { get; }
        public Point Location { get; }

        // Reference to the Voronoi cell for this site (set after computation)
        public Cell? Cell { get; internal set; }

        public Site(int id, Point location)
        {
            Id = id;
            Location = location;
        }

        public override string ToString() => $"Site[{Id}] at {Location}";
    }
}
