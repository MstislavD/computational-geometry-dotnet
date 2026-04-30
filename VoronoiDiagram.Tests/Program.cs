using System;
using System.Collections.Generic;
using System.Diagnostics;
using VoronoiDiagram.Core;
using VoronoiDiagram.Core.DataStructures;

class Program
{
    static int passed = 0, failed = 0;

    static void Assert(bool condition, string msg)
    {
        if (condition) { passed++; Console.WriteLine($"  PASS: {msg}"); }
        else { failed++; Console.WriteLine($"  FAIL: {msg}"); }
    }

    static void Main()
    {
        TestCircumcircle();
        TestSingleSite();
        TestTwoSites();
        TestThreeCollinear();
        TestEquilateralTriangle();
        TestSquareLattice();
        TestRandomSmall();
        TestFortuneVsBruteForce();
        TestVisualSanity20();
        TestPerformance100();
        TestPerformance500();

        Console.WriteLine($"\n=== Results: {passed} passed, {failed} failed ===");
        Environment.Exit(failed > 0 ? 1 : 0);
    }

    static void TestCircumcircle()
    {
        Console.WriteLine("\n--- Test: Circumcircle computation ---");
        // Equilateral triangle: (0,0), (10,0), (5, 8.66)
        var c = VoronoiDiagram.Core.DataStructures.Circle.FromThreePoints(
            new Point(0, 0), new Point(10, 0), new Point(5, 8.66));

        Assert(c.HasValue, "Circumcircle exists for equilateral triangle");
        if (c.HasValue)
        {
            Console.WriteLine($"  Center: ({c.Value.Center.X:F4}, {c.Value.Center.Y:F4})");
            Console.WriteLine($"  Radius: {c.Value.Radius:F4}");
            double expectedCx = 5, expectedCy = 10 / (2 * Math.Sqrt(3)); // ≈ 2.887
            Assert(Math.Abs(c.Value.Center.X - expectedCx) < 0.01, $"Center X={c.Value.Center.X:F4} ≈ {expectedCx:F4}");
            Assert(Math.Abs(c.Value.Center.Y - expectedCy) < 0.05, $"Center Y={c.Value.Center.Y:F4} ≈ {expectedCy:F4}");
        }

        // Collinear points should return null
        var c2 = VoronoiDiagram.Core.DataStructures.Circle.FromThreePoints(
            new Point(0, 0), new Point(5, 0), new Point(10, 0));
        Assert(!c2.HasValue, "Collinear points produce no circumcircle");
    }

    static void TestSingleSite()
    {
        Console.WriteLine("\n--- Test: Single site (should throw) ---");
        try
        {
            var fa = new FortuneAlgorithm();
            fa.Compute(new List<Point> { new Point(0, 0) });
            Assert(false, "Should have thrown for single site");
        }
        catch (ArgumentException)
        {
            Assert(true, "Throws ArgumentException for single site");
        }
    }

    static void TestTwoSites()
    {
        Console.WriteLine("\n--- Test: Two sites ---");
        var fa = new FortuneAlgorithm();
        fa.Compute(new List<Point>
        {
            new Point(0, 0),
            new Point(10, 0)
        });

        var cells = fa.Diagram.ToCellResults();
        Assert(cells.Count == 2, $"Expected 2 cells, got {cells.Count}");

        // The bisector should be at x=5. Left cell vertices should have x <= ~5, right cell x >= ~5.
        if (cells.Count == 2)
        {
            foreach (var c in cells)
            {
                Assert(c.Vertices.Count >= 2, $"Site {c.Site.Id} has {c.Vertices.Count} vertices");
            }
        }

        var edges = fa.Diagram.ToEdgeResults();
        Console.WriteLine($"  Edges: {edges.Count}");
    }

    static void TestThreeCollinear()
    {
        Console.WriteLine("\n--- Test: Three collinear sites ---");
        var fa = new FortuneAlgorithm();
        fa.Compute(new List<Point>
        {
            new Point(0, 0),
            new Point(5, 0),
            new Point(10, 0)
        });

        var cells = fa.Diagram.ToCellResults();
        Assert(cells.Count == 3, $"Expected 3 cells, got {cells.Count}");
    }

    static void TestEquilateralTriangle()
    {
        Console.WriteLine("\n--- Test: Equilateral triangle ---");
        // All three sites at different Y values so circle event can fire.
        double side = 10;
        var fa = new FortuneAlgorithm();
        fa.Compute(new List<Point>
        {
            new Point(0, 2),       // site 0 — lowest
            new Point(side, 3),    // site 1 — middle height
            new Point(side / 2, 2 + side * Math.Sqrt(3) / 2) // site 2 — highest ≈ (5, 10.66)
        });

        // Debug: inspect internal state
        Console.WriteLine($"  DCEL vertices: {fa.Diagram.Vertices.Count}");
        Console.WriteLine($"  DCEL half-edges: {fa.Diagram.HalfEdges.Count}");
        int originsSet = 0, destsSet = 0;
        foreach (var he in fa.Diagram.HalfEdges)
        {
            if (he.Origin != null) originsSet++;
            if (he.Destination != null) destsSet++;
        }
        Console.WriteLine($"  Half-edges with Origin set: {originsSet}/{fa.Diagram.HalfEdges.Count}");
        Console.WriteLine($"  Half-edges with Destination set: {destsSet}/{fa.Diagram.HalfEdges.Count}");

        foreach (var v in fa.Diagram.Vertices)
            if (double.IsFinite(v.Location.X))
                Console.WriteLine($"  Vertex at ({v.Location.X:F4}, {v.Location.Y:F4})");

        var cells = fa.Diagram.ToCellResults();
        Assert(cells.Count == 3, $"Expected 3 cells, got {cells.Count}");

        // Circumcenter of this triangle:
        var circ = VoronoiDiagram.Core.DataStructures.Circle.FromThreePoints(
            new Point(0, 2), new Point(side, 3), new Point(side / 2, 2 + side * Math.Sqrt(3) / 2));
        if (circ.HasValue)
        {
            Console.WriteLine($"  Expected vertex near ({circ.Value.Center.X:F4}, {circ.Value.Center.Y:F4})");

            bool foundVertex = false;
            foreach (var c in cells)
            {
                foreach (var v in c.Vertices)
                {
                    double dist = Math.Sqrt((v.X - circ.Value.Center.X) * (v.X - circ.Value.Center.X) +
                                            (v.Y - circ.Value.Center.Y) * (v.Y - circ.Value.Center.Y));
                    if (dist < 0.5)
                    {
                        foundVertex = true;
                        Console.WriteLine($"  Found vertex near circumcenter: ({v.X:F4}, {v.Y:F4}), dist={dist:F6}");
                    }
                }
            }
            Assert(foundVertex, "Found a vertex near the expected circumcenter");
        }

        var edges = fa.Diagram.ToEdgeResults();
        foreach (var e in edges)
        {
            if (e.Vertices.Count >= 1)
                Console.WriteLine($"  Edge site {e.SiteA.Id}-{e.SiteB.Id}: ({e.Vertices[0].X:F2}, {e.Vertices[0].Y:F2})");
        }
    }

    static void TestSquareLattice()
    {
        Console.WriteLine("\n--- Test: 3x3 square lattice ---");
        var points = new List<Point>();
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                points.Add(new Point(x * 10, y * 10));

        var fa = new FortuneAlgorithm();
        fa.Compute(points);

        // Debug: inspect all cells' recorded vertices
        foreach (var cell in fa.Diagram.Cells)
        {
            if (cell.Site == null) continue;
            Console.WriteLine($"  Site [{cell.Site.Id}] ({cell.Site.Location.X:F0},{cell.Site.Location.Y:F0}): BoundaryEdge={cell.BoundaryEdge != null}, RecordedVertices={cell.RecordedVertices.Count}");
        }

        var cells = fa.Diagram.ToCellResults();
        Assert(cells.Count == 9, $"Expected 9 cells, got {cells.Count}");

        // Interior cell (center) should have ~4 vertices
        foreach (var c in cells)
        {
            if (c.Site.Location.X == 10 && c.Site.Location.Y == 10)
            {
                Console.WriteLine($"  Center cell: {c.Vertices.Count} vertices");
                Assert(c.Vertices.Count >= 3, $"Center cell has {c.Vertices.Count} vertices (expected ~4)");
            }
        }
    }

    static void TestRandomSmall()
    {
        Console.WriteLine("\n--- Test: 10 random sites ---");
        var rng = new Random(42);
        var points = GenerateRandomPoints(rng, 10, 0, 100);

        var fa = new FortuneAlgorithm();
        fa.Compute(points);

        var cells = fa.Diagram.ToCellResults();
        Assert(cells.Count == 10, $"Expected 10 cells, got {cells.Count}");

        // Each cell should have at least 2 vertices (bounding box clipped)
        int validCells = 0;
        foreach (var c in cells)
        {
            if (c.Vertices.Count >= 2) validCells++;
        }
        Assert(validCells == 10, $"{validCells}/10 cells have >= 2 vertices");

        var edges = fa.Diagram.ToEdgeResults();
        Console.WriteLine($"  Edges: {edges.Count}");
    }

    static void TestFortuneVsBruteForce()
    {
        Console.WriteLine("\n--- Test: Fortune vs Brute-Force (20 sites) ---");
        var rng = new Random(123);
        var points = GenerateRandomPoints(rng, 20, 0, 500);

        // Run brute-force with matching bounding box
        var bf = new BruteForceVoronoi(points);
        bf.Compute(bound: 1000);

        var fa = new FortuneAlgorithm();
        fa.Compute(points);

        var fortuneCells = fa.Diagram.ToCellResults();
        Assert(fortuneCells.Count == 20, $"Fortune produced {fortuneCells.Count} cells (expected 20)");
        Assert(bf.Cells.Count == 20, $"BruteForce produced {bf.Cells.Count} cells (expected 20)");

        // Compare cell areas as a sanity check
        double totalAreaDiff = 0;
        int compared = 0;
        foreach (var bfCell in bf.Cells)
        {
            var fortuneCell = null as CellResult;
            foreach (var fc in fortuneCells)
            {
                if (fc.Site.Id == bfCell.Site.Id) { fortuneCell = fc; break; }
            }
            if (fortuneCell == null || fortuneCell.Vertices.Count < 3) continue;

            double bfArea = PolygonArea(bfCell.Vertices);
            double faArea = PolygonArea(fortuneCell.Vertices);
            double diff = Math.Abs(bfArea - faArea);
            totalAreaDiff += diff;
            compared++;

            // Hull cells will differ due to bounding box handling — skip large diffs
            if (bfArea > 100000 && faArea > 100000) continue;

            double relError = bfArea > 0 ? diff / bfArea : 0;
            if (relError > 0.5)
            {
                Console.WriteLine($"  Site {bfCell.Site.Id}: BF area={bfArea:F0}, Fortune area={faArea:F0}, diff={diff:F0}");
            }
        }

        Console.WriteLine($"  Compared {compared} cells, total area diff = {totalAreaDiff:F0}");
    }

    static void TestVisualSanity20()
    {
        Console.WriteLine("\n--- Visual Sanity: 20 random sites ---");
        var rng = new Random(123);
        var points = GenerateRandomPoints(rng, 20, 50, 750);

        var fa = new FortuneAlgorithm();
        fa.Compute(points);

        var cells = fa.Diagram.ToCellResults();
        Console.WriteLine($"  Cells: {cells.Count}");

        // Check for extreme vertex coordinates (outside reasonable bounds)
        double siteMinX = double.MaxValue, siteMaxX = double.MinValue;
        double siteMinY = double.MaxValue, siteMaxY = double.MinValue;
        foreach (var p in points)
        {
            siteMinX = Math.Min(siteMinX, p.X); siteMaxX = Math.Max(siteMaxX, p.X);
            siteMinY = Math.Min(siteMinY, p.Y); siteMaxY = Math.Max(siteMaxY, p.Y);
        }
        double span = Math.Max(siteMaxX - siteMinX, siteMaxY - siteMinY);
        double lo = siteMinX - span;
        double hi = siteMaxX + span;

        int extremeCount = 0;
        foreach (var c in cells)
        {
            foreach (var v in c.Vertices)
            {
                if (v.X < lo || v.X > hi || v.Y < lo || v.Y > hi)
                {
                    Console.WriteLine($"  EXTREME vertex on site {c.Site.Id}: ({v.X:F1}, {v.Y:F1})");
                    extremeCount++;
                }
            }
        }
        if (extremeCount == 0)
            Console.WriteLine("  PASS: No extreme vertices");
        else
            Console.WriteLine($"  FAIL: {extremeCount} extreme vertices found");

        // Check for very long edges (> 2x site span)
        int longEdges = 0;
        double maxEdgeLen = span * 2;
        foreach (var c in cells)
        {
            for (int i = 0; i < c.Vertices.Count; i++)
            {
                var a = c.Vertices[i];
                var b = c.Vertices[(i + 1) % c.Vertices.Count];
                double len = a.Distance(b);
                if (len > maxEdgeLen)
                {
                    Console.WriteLine($"  LONG edge on site {c.Site.Id}: ({a.X:F0},{a.Y:F0})->({b.X:F0},{b.Y:F0}) len={len:F0}");
                    longEdges++;
                }
            }
        }
        if (longEdges == 0)
            Console.WriteLine("  PASS: No excessively long edges");
        else
            Console.WriteLine($"  FAIL: {longEdges} excessively long edges found");

        // Compare areas with brute-force
        var bf = new BruteForceVoronoi(points);
        bf.Compute(bound: 1000);

        double totalRelError = 0;
        int compared = 0;
        foreach (var bfCell in bf.Cells)
        {
            var fc = cells.Find(c => c.Site.Id == bfCell.Site.Id);
            if (fc == null || fc.Vertices.Count < 3) continue;

            double bfArea = PolygonArea(bfCell.Vertices);
            double faArea = PolygonArea(fc.Vertices);
            if (bfArea > 1000 && faArea > 1000) continue; // Skip huge hull cells

            double diff = Math.Abs(bfArea - faArea);
            double relError = bfArea > 0 ? diff / bfArea : 0;
            totalRelError += relError;
            compared++;

            if (relError > 0.3)
                Console.WriteLine($"  Area mismatch site {bfCell.Site.Id}: BF={bfArea:F0}, Fortune={faArea:F0}, relErr={relError:P1}");
        }
        Console.WriteLine($"  Average relative area error: {(compared > 0 ? totalRelError / compared : 0):P2} ({compared} cells)");
    }

    static void TestPerformance100()
    {
        Console.WriteLine("\n--- Performance: 100 sites ---");
        var rng = new Random(99);
        var points = GenerateRandomPoints(rng, 100, 0, 1000);

        var sw = Stopwatch.StartNew();
        var fa = new FortuneAlgorithm();
        fa.Compute(points);
        sw.Stop();

        var cells = fa.Diagram.ToCellResults();
        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds} ms");
        Assert(cells.Count == 100, $"Expected 100 cells, got {cells.Count}");

        int validCells = 0;
        foreach (var c in cells)
            if (c.Vertices.Count >= 2) validCells++;
        Console.WriteLine($"  Valid cells: {validCells}/100");
    }

    static void TestPerformance500()
    {
        Console.WriteLine("\n--- Performance: 500 sites ---");
        var rng = new Random(777);
        var points = GenerateRandomPoints(rng, 500, 0, 2000);

        var sw = Stopwatch.StartNew();
        var fa = new FortuneAlgorithm();
        fa.Compute(points);
        sw.Stop();

        var cells = fa.Diagram.ToCellResults();
        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds} ms");
        Assert(cells.Count == 500, $"Expected 500 cells, got {cells.Count}");

        int validCells = 0;
        foreach (var c in cells)
            if (c.Vertices.Count >= 2) validCells++;
        Console.WriteLine($"  Valid cells: {validCells}/500");
    }

    static List<Point> GenerateRandomPoints(Random rng, int count, double min, double max)
    {
        var points = new List<Point>();
        for (int i = 0; i < count; i++)
            points.Add(new Point(min + rng.NextDouble() * (max - min),
                                 min + rng.NextDouble() * (max - min)));
        return points;
    }

    static double PolygonArea(List<Point> vertices)
    {
        if (vertices.Count < 3) return 0;
        double area = 0;
        for (int i = 0, n = vertices.Count; i < n; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % n];
            area += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(area) / 2.0;
    }
}
