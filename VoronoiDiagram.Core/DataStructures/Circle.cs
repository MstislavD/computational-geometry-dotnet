using System;

namespace VoronoiDiagram.Core.DataStructures
{
    /// <summary>
    /// A circle defined by center and radius.
    /// In Fortune's algorithm, a "circle event" occurs when the sweep line reaches
    /// the bottom of an empty circle passing through three sites that define
    /// consecutive arcs on the beach line (Theorem 7.4).
    /// </summary>
    public readonly struct Circle
    {
        public Point Center { get; }
        public double Radius { get; }

        public Circle(Point center, double radius)
        {
            Center = center;
            Radius = radius;
        }

        /// <summary>
        /// The lowest point on the circle — this is where the sweep line triggers the event.
        /// For a circle event at sites pi, pj, pk: the vertex of Vor(P) lies at Center,
        /// and the circle's bottom touches the sweep line at (Center.X, Center.Y + Radius).
        /// </summary>
        public Point BottomPoint => new Point(Center.X, Center.Y + Radius);

        /// <summary>
        /// Computes the circumcircle of three non-collinear points.
        /// Uses the perpendicular bisector intersection method:
        /// The center is equidistant from all three points (Theorem 7.4 proof).
        /// </summary>
        public static Circle? FromThreePoints(Point a, Point b, Point c)
        {
            // Check for collinearity via cross product of vectors AB and AC
            double ax = b.X - a.X;
            double ay = b.Y - a.Y;
            double bx = c.X - a.X;
            double by = c.Y - a.Y;

            double det = ax * by - ay * bx;

            // Collinear points do not define a circle — no circle event possible
            if (Math.Abs(det) < 1e-12)
                return null;

            // Circumcenter formula via perpendicular bisector intersection.
            // Derived from solving the system: |C - A|^2 = |C - B|^2 = |C - C|^2
            double a2 = ax * ax + ay * ay;
            double b2 = bx * bx + by * by;

            double cx = a.X + (by * a2 - ay * b2) / (2.0 * det);
            double cy = a.Y + (ax * b2 - bx * a2) / (2.0 * det);

            Point center = new Point(cx, cy);
            double radius = center.Distance(a);

            return new Circle(center, radius);
        }
    }
}
