using System.Collections.Generic;

namespace VoronoiDiagram.Core.DataStructures
{
    /// <summary>
    /// Tracks a single Voronoi edge between two adjacent sites during the sweep.
    ///
    /// Each fragment corresponds to one breakpoint on the beach line. As the sweep progresses,
    /// circle events add vertices to the fragment's vertex list in sweep order (highest Y first).
    /// At finalization, fragments are assembled into proper cell boundary rings.
    /// </summary>
    public class EdgeFragment
    {
        public Site SiteA { get; }
        public Site SiteB { get; }

        /// <summary>Half-edge in SiteA's cell (Face = SiteA.Cell).</summary>
        public HalfEdge HalfEdgeA { get; }

        /// <summary>Half-edge in SiteB's cell (Face = SiteB.Cell).</summary>
        public HalfEdge HalfEdgeB { get; }

        /// <summary>Vertices along this edge, ordered from highest Y to lowest Y (sweep order).</summary>
        public List<Point> Vertices { get; } = new List<Point>();

        public EdgeFragment(Site siteA, Site siteB, HalfEdge heA, HalfEdge heB)
        {
            SiteA = siteA;
            SiteB = siteB;
            HalfEdgeA = heA;
            HalfEdgeB = heB;
        }
    }
}
