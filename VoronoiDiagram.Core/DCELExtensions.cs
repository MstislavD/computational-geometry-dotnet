using System;
using System.Collections.Generic;
using VoronoiDiagram.Core.DataStructures;

namespace VoronoiDiagram.Core
{
    /// <summary>
    /// Extension methods for converting DCEL output into visualization-friendly formats.
    /// The DCEL stores the diagram as a pointer-based structure (half-edge rings), while
    /// the UI expects ordered vertex lists per cell. This converter bridges the two representations.
    /// </summary>
    public static class DCELExtensions
    {
        /// <summary>
        /// Converts the DCEL into a list of CellResult objects compatible with BruteForceVoronoi output.
        /// For each site, traverses its cell's half-edge boundary ring to collect vertices in order.
        /// Falls back to angular sorting if the ring is incomplete (degenerate cases).
        /// </summary>
        public static List<CellResult> ToCellResults(this DCEL dcel)
        {
            var results = new List<CellResult>();

            foreach (var cell in dcel.Cells)
            {
                if (cell.Site == null) continue; // Skip the outer/bounding box cell

                var vertices = ExtractCellVertices(cell, dcel);
                results.Add(new CellResult(cell.Site, vertices));
            }

            return results;
        }

        /// <summary>
        /// Extracts ordered vertex list for a single cell by traversing its half-edge ring.
        /// </summary>
        private static List<Point> ExtractCellVertices(Cell cell, DCEL dcel)
        {
            var vertices = new List<Point>();

            if (cell.BoundaryEdge == null)
            {
                // No boundary edge set — try to find one by scanning all half-edges.
                HalfEdge? start = FindBoundaryEdge(cell, dcel);
                if (start != null)
                    cell.BoundaryEdge = start;
                else
                    return vertices; // Empty cell
            }

            // Traverse the half-edge ring: each edge's Origin is a vertex of the cell.
            HalfEdge? edge = cell.BoundaryEdge;
            int safetyCounter = 0;
            int maxIterations = dcel.HalfEdges.Count + 1;

            do
            {
                if (edge.Origin != null)
                    vertices.Add(edge.Origin.Location);

                edge = edge.Next;
                safetyCounter++;
            } while (edge != null && edge != cell.BoundaryEdge && safetyCounter < maxIterations);

            // If the ring traversal didn't produce a valid polygon, try fallback approach.
            if (vertices.Count < 3)
                vertices = FallbackExtractVertices(cell, dcel);

            // Final fallback: use RecordedVertices from circle events + co-circular discovery.
            if (vertices.Count < 3 && cell.RecordedVertices.Count >= 2)
            {
                vertices = new List<Point>(cell.RecordedVertices);
                Point centroid = ComputeCentroid(vertices);
                vertices.Sort((a, b) =>
                    Math.Atan2(a.Y - centroid.Y, a.X - centroid.X).CompareTo(
                        Math.Atan2(b.Y - centroid.Y, b.X - centroid.X)));
            }

            return vertices;
        }

        /// <summary>
        /// Finds any half-edge that bounds the given cell by scanning all edges in the DCEL.
        /// </summary>
        private static HalfEdge? FindBoundaryEdge(Cell cell, DCEL dcel)
        {
            foreach (var he in dcel.HalfEdges)
            {
                if (he.Face == cell)
                    return he;
            }
            return null;
        }

        /// <summary>
        /// Fallback: collect all vertices incident to this cell's half-edges and sort them angularly.
        /// Used when the Next/Prev ring isn't fully linked (degenerate cases or incomplete construction).
        /// </summary>
        private static List<Point> FallbackExtractVertices(Cell cell, DCEL dcel)
        {
            var vertexSet = new HashSet<string>(); // Dedup by coordinate string
            var vertices = new List<Point>();

            foreach (var he in dcel.HalfEdges)
            {
                if (he.Face == cell && he.Origin != null)
                {
                    string key = $"{he.Origin.Location.X:F4},{he.Origin.Location.Y:F4}";
                    if (!vertexSet.Contains(key))
                    {
                        vertexSet.Add(key);
                        vertices.Add(he.Origin.Location);
                    }
                }
            }

            // Sort angularly around the cell's centroid for a proper polygon order.
            if (vertices.Count >= 3)
            {
                Point centroid = ComputeCentroid(vertices);
                vertices.Sort((a, b) =>
                    Math.Atan2(a.Y - centroid.Y, a.X - centroid.X).CompareTo(
                        Math.Atan2(b.Y - centroid.Y, b.X - centroid.X)));
            }

            return vertices;
        }

        /// <summary>
        /// Computes the arithmetic mean of all points (centroid).
        /// </summary>
        private static Point ComputeCentroid(List<Point> vertices)
        {
            double sumX = 0, sumY = 0;
            foreach (var v in vertices)
            {
                sumX += v.X;
                sumY += v.Y;
            }
            return new Point(sumX / vertices.Count, sumY / vertices.Count);
        }

        /// <summary>
        /// Extracts edge list from the DCEL for visualization.
        /// Each Voronoi edge is a segment between two vertices on the bisector of two sites.
        /// </summary>
        public static List<EdgeResult> ToEdgeResults(this DCEL dcel)
        {
            var edges = new List<EdgeResult>();
            var seen = new HashSet<string>();
            var processed = new HashSet<HalfEdge>(System.Collections.Generic.EqualityComparer<HalfEdge>.Default);

            foreach (var he in dcel.HalfEdges)
            {
                if (he.Origin == null || he.Destination == null) continue;
                if (he.IsInfinite) continue;

                // Only process one direction per edge pair to avoid duplicates.
                if (!processed.Add(he)) continue;

                var vertices = new List<Point> { he.Origin.Location, he.Destination.Location };

                // Determine the two sites this edge separates.
                Site? siteA = he.Face?.Site;
                Site? siteB = he.Twin?.Face?.Site;

                if (siteA != null && siteB != null)
                {
                    int idMin = Math.Min(siteA.Id, siteB.Id);
                    int idMax = Math.Max(siteA.Id, siteB.Id);
                    string key = $"{idMin}-{idMax}";

                    if (!seen.Contains(key))
                    {
                        seen.Add(key);
                        edges.Add(new EdgeResult(siteA, siteB, vertices));
                    }
                }
            }

            return edges;
        }
    }
}
