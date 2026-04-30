using VoronoiDiagram.Core.BeachLineTree;
using VoronoiDiagram.Core.DataStructures;

namespace VoronoiDiagram.Core.Events
{
    /// <summary>
    /// A circle event: fired when an arc on the beach line shrinks to a point and disappears.
    /// "The only way in which an existing arc can disappear from the beach line is through a circle event" (Lemma 7.7).
    ///
    /// Geometrically, this occurs when three consecutive arcs α, α', α'' on the beach line,
    /// defined by sites pi, pj, pk respectively, converge such that their two breakpoints meet.
    /// At convergence: there exists a circle through pi, pj, pk whose lowest point touches the sweep line,
    /// and whose interior contains no other sites (Theorem 7.4). The center of this circle is a Voronoi vertex.
    ///
    /// Per p.156: "For every three consecutive arcs on the beach line that define a potential circle event,
    /// the potential event is stored in Q." However, not all potential events fire — some are false alarms
    /// when the defining triple is disrupted by an intervening site or circle event.
    /// </summary>
    public class CircleEvent : SweepEvent
    {
        /// <summary>
        /// The center of the circumcircle through the three defining sites.
        /// This point becomes a vertex in the Voronoi diagram when the event fires.
        /// </summary>
        public Point VertexLocation { get; }

        /// <summary>
        /// The circle that defines this event: passes through three sites, tangent to sweep line at bottom.
        /// </summary>
        public Circle Circle { get; }

        /// <summary>
        /// The three sites whose consecutive arcs on the beach line define this event.
        /// pj is the site defining the middle arc (the one that will disappear).
        /// </summary>
        public Site LeftSite   { get; }  // pi — defines left arc α
        public Site MiddleSite { get; }  // pj — defines disappearing arc α'
        public Site RightSite  { get; }  // pk — defines right arc α''

        /// <summary>
        /// Reference to the beach line node for the middle arc.
        /// Used to invalidate this event if the arc is removed before the event fires (false alarm detection).
        /// Per p.155: "Each leaf of T, representing an arc α, stores one pointer to a node in the event queue."
        /// </summary>
        public BeachArcNode? MiddleArcNode { get; }

        public CircleEvent(Point vertexLocation, Circle circle, Site leftSite, Site middleSite, Site rightSite,
                            BeachArcNode? middleArcNode)
        {
            VertexLocation = vertexLocation;
            Circle = circle;
            LeftSite = leftSite;
            MiddleSite = middleSite;
            RightSite = rightSite;
            MiddleArcNode = middleArcNode;

            // The event fires when the sweep line reaches the lowest point of the circumcircle.
            // Sweep moves from high Y to low Y, so "bottom" = smallest Y = Center.Y - Radius.
            YCoordinate = circle.Center.Y - circle.Radius;
        }

        public override string ToString() =>
            $"CircleEvent vertex={VertexLocation}, sites=[{LeftSite.Id},{MiddleSite.Id},{RightSite.Id}]";
    }
}
