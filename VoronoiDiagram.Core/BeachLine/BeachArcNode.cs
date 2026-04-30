using System;
using VoronoiDiagram.Core.DataStructures;

namespace VoronoiDiagram.Core.BeachLineTree
{
    /// <summary>
    /// Represents a single parabolic arc on the beach line.
    /// Each arc is defined by one site pi and the current sweep line position y_sweep.
    /// The parabola has focus at pi and directrix at y = y_sweep.
    ///
    /// Per p.152: "Every site pi above the sweep line defines a complete parabola βi.
    /// The beach line is the function that — for each x-coordinate — passes through
    /// the lowest point of all parabolas."
    ///
    /// The equation of parabola βi with focus (xi, yi) and directrix y = ys:
    ///   y = yi + (x - xi)^2 / (2 * (yi - ys))
    /// where the focal distance d = yi - ys > 0 since the site is above the sweep line.
    /// </summary>
    public class BeachArcNode
    {
        // The site that defines this parabolic arc
        public Site Site { get; }

        /// <summary>
        /// Pointer to a pending circle event where this arc will disappear.
        /// Per p.155: "Each leaf of T, representing an arc α, stores one pointer to a node
        /// in the event queue, namely, the node that represents the circle event in which α will disappear."
        /// Null if no circle event exists or hasn't been detected yet.
        /// </summary>
        public Events.CircleEvent? CircleEvent { get; internal set; }

        // ---- Internal tree linkage (maintained by BeachLine) ----

        // In-order predecessor and successor arcs on the beach line.
        // These form a doubly-linked list embedded within the BST, enabling O(1) neighbor lookup.
        internal BeachArcNode? Prev { get; set; }
        internal BeachArcNode? Next { get; set; }

        public BeachArcNode(Site site)
        {
            Site = site;
        }
    }
}
