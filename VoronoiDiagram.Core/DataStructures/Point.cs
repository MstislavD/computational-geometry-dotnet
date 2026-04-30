using System;

namespace VoronoiDiagram.Core.DataStructures
{
    /// <summary>
    /// Immutable 2D point with arithmetic operators.
    /// Serves as the foundational type for all geometric computations in Fortune's algorithm.
    /// </summary>
    public readonly struct Point : IEquatable<Point>
    {
        public double X { get; }
        public double Y { get; }

        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Origin point (0, 0).</summary>
        public static Point Zero => new Point(0, 0);

        public static bool operator ==(Point a, Point b) => a.Equals(b);
        public static bool operator !=(Point a, Point b) => !a.Equals(b);

        // Vector subtraction: this - other
        public static Point operator -(Point a, Point b) => new Point(a.X - b.X, a.Y - b.Y);

        // Vector addition
        public static Point operator +(Point a, Point b) => new Point(a.X + b.X, a.Y + b.Y);

        // Scalar multiplication
        public static Point operator *(Point p, double s) => new Point(p.X * s, p.Y * s);
        public static Point operator *(double s, Point p) => new Point(p.X * s, p.Y * s);

        // Scalar division
        public static Point operator /(Point p, double s) => new Point(p.X / s, p.Y / s);

        /// <summary>
        /// Squared Euclidean distance — avoids costly sqrt for comparisons.
        /// </summary>
        public double DistanceSquared(Point other) =>
            (X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y);

        public double Distance(Point other) => Math.Sqrt(DistanceSquared(other));

        public bool Equals(Point other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is Point p && Equals(p);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X:F2}, {Y:F2})";

        /// <summary>
        /// Epsilon-based equality for floating-point robustness.
        /// Critical in Fortune's algorithm where computed intersection points
        /// may differ slightly from input coordinates due to rounding.
        /// </summary>
        public bool AlmostEquals(Point other, double epsilon = 1e-9) =>
            Math.Abs(X - other.X) < epsilon && Math.Abs(Y - other.Y) < epsilon;
    }
}
