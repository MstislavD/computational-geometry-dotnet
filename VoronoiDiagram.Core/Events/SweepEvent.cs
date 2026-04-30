namespace VoronoiDiagram.Core.Events
{
    /// <summary>
    /// Base class for sweep line events in Fortune's algorithm.
    /// Events are processed in order of decreasing y-coordinate (sweep moves top to bottom).
    /// The event queue Q is a priority queue where "priority = largest y-coordinate" (p.157).
    /// </summary>
    public abstract class SweepEvent
    {
        /// <summary>
        /// The y-coordinate of the sweep line position when this event fires.
        /// This determines the event's priority in the queue.
        /// </summary>
        public double YCoordinate { get; protected set; }

        /// <summary>
        /// Whether this event has been invalidated (false alarm).
        /// A circle event becomes a false alarm if the defining triple of arcs
        /// is disrupted by another event before the circle event fires (p.156).
        /// </summary>
        public bool IsInvalidated { get; protected set; }

        public virtual void Invalidate() => IsInvalidated = true;

        public override string ToString() => $"{GetType().Name} at y={YCoordinate:F2}";
    }
}
