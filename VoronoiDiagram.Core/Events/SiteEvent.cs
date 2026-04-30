using VoronoiDiagram.Core.DataStructures;

namespace VoronoiDiagram.Core.Events
{
    /// <summary>
    /// A site event: fired when the sweep line encounters a new input point.
    /// "The only way in which a new arc can appear on the beach line is through a site event" (Lemma 7.6).
    ///
    /// At a site event:
    /// - A new parabolic arc appears on the beach line at the site's x-position
    /// - Two new breakpoints are created, beginning to trace a Voronoi edge
    /// - The new edge is initially disconnected from the rest of the diagram
    ///   (it becomes connected later when it meets another edge at a circle event)
    /// </summary>
    public class SiteEvent : SweepEvent
    {
        public Site Site { get; }

        public SiteEvent(Site site)
        {
            Site = site;
            YCoordinate = site.Location.Y;
        }

        public override string ToString() => $"SiteEvent[{Site.Id}] at y={YCoordinate:F2}";
    }
}
