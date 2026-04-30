using System;
using System.Collections.Generic;
using VoronoiDiagram.Core.BeachLineTree;
using VoronoiDiagram.Core.DataStructures;
using VoronoiDiagram.Core.Events;

namespace VoronoiDiagram.Core
{
    public class FortuneAlgorithm
    {
        private readonly BeachLine _beachLine;
        private readonly EventQueue _eventQueue;
        private DCEL _dcel;

        // Maps ordered site pair (minId, maxId) to the active edge fragment being traced.
        private readonly Dictionary<(int, int), EdgeFragment> _edgeFragments;

        // All fragments ever created — used during finalization to reconstruct cell boundaries.
        private List<EdgeFragment> _allFragments;

        public DCEL Diagram => _dcel;
        public List<Site> Sites { get; private set; } = new List<Site>();

        public FortuneAlgorithm()
        {
            _beachLine = new BeachLine();
            _eventQueue = new EventQueue();
            _dcel = new DCEL();
            _edgeFragments = new Dictionary<(int, int), EdgeFragment>();
            _allFragments = new List<EdgeFragment>();
        }

        public void Compute(List<Point> points)
        {
            _beachLine.Clear();
            _eventQueue.Clear();
            _dcel = new DCEL();
            _edgeFragments.Clear();
            _allFragments.Clear();
            Sites.Clear();

            if (points.Count < 2)
                throw new ArgumentException("Need at least 2 sites for a Voronoi diagram.");

            for (int i = 0; i < points.Count; i++)
            {
                Sites.Add(new Site(i, points[i]));
            }

            foreach (var site in Sites)
            {
                _eventQueue.Insert(new SiteEvent(site));
            }

            Sweep();
            FinalizeDiagram();
        }

        private void Sweep()
        {
            while (!_eventQueue.IsEmpty)
            {
                SweepEvent? evt = _eventQueue.RemoveHighest();
                if (evt == null) continue;

                if (evt is SiteEvent siteEvt)
                {
                    HandleSiteEvent(siteEvt);
                }
                else if (evt is CircleEvent circleEvt)
                {
                    HandleCircleEvent(circleEvt);
                }
            }
        }

        private void HandleSiteEvent(SiteEvent evt)
        {
            Site site = evt.Site;

            BeachArcNode? existingArc = _beachLine.FindArcAbove(site.Location, site.Location.Y + 1e-6);

            if (existingArc == null)
            {
                var firstArc = new BeachArcNode(site);
                _beachLine.InsertArc(firstArc, firstArc);
                _dcel.CreateCell(site);
                return;
            }

            var oldNeighbors = _beachLine.GetNeighbors(existingArc);

            if (existingArc.CircleEvent != null)
            {
                existingArc.CircleEvent.Invalidate();
                existingArc.CircleEvent = null;
            }

            var newArc = new BeachArcNode(site);
            BeachArcNode rightPiece = _beachLine.InsertArc(newArc, existingArc);

            // Ensure cells exist for both sites.
            if (existingArc.Site.Cell == null) _dcel.CreateCell(existingArc.Site);
            if (site.Cell == null) _dcel.CreateCell(site);

            var (e1, e2) = _dcel.CreateTwinPair();

            Site leftSite = existingArc.Site.Location.X <= site.Location.X ? existingArc.Site : site;
            Site rightSite = leftSite == existingArc.Site ? site : existingArc.Site;

            e1.Face = leftSite.Cell!;
            e2.Face = rightSite.Cell!;

            if (leftSite.Cell!.BoundaryEdge == null)
                leftSite.Cell.BoundaryEdge = e1;
            if (rightSite.Cell!.BoundaryEdge == null)
                rightSite.Cell.BoundaryEdge = e2;

            int idA = Math.Min(existingArc.Site.Id, site.Id);
            int idB = Math.Max(existingArc.Site.Id, site.Id);

            var frag = new EdgeFragment(
                Sites[idA], Sites[idB],
                Sites[idA] == leftSite ? e1 : e2,
                Sites[idB] == rightSite ? e2 : e1);

            _edgeFragments[(idA, idB)] = frag;
            _allFragments.Add(frag);

            if (oldNeighbors.left != null)
                TryAddCircleEvent(oldNeighbors.left.Site, existingArc.Site, site, existingArc);

            if (oldNeighbors.right != null)
                TryAddCircleEvent(site, rightPiece.Site, oldNeighbors.right.Site, rightPiece);
        }

        private void HandleCircleEvent(CircleEvent evt)
        {
            if (evt.MiddleArcNode == null || evt.MiddleArcNode.CircleEvent != evt)
            {
                return;
            }

            BeachArcNode disappearingArc = evt.MiddleArcNode;
            Site leftSite = evt.LeftSite;
            Site middleSite = evt.MiddleSite;
            Site rightSite = evt.RightSite;

            var neighbors = _beachLine.GetNeighbors(disappearingArc);
            BeachArcNode? leftNeighbor = neighbors.left;
            BeachArcNode? rightNeighbor = neighbors.right;

            if (leftNeighbor?.CircleEvent != null)
                leftNeighbor.CircleEvent.Invalidate();
            if (rightNeighbor?.CircleEvent != null)
                rightNeighbor.CircleEvent.Invalidate();

            _beachLine.RemoveArc(disappearingArc);

            Point vLoc = evt.VertexLocation;
            if (double.IsNaN(vLoc.X) || double.IsNaN(vLoc.Y) ||
                double.IsInfinity(vLoc.X) || double.IsInfinity(vLoc.Y))
            {
                return;
            }
            Vertex v = _dcel.CreateVertex(vLoc);

            Cell? cellLeft = leftSite.Cell ?? _dcel.CreateCell(leftSite);
            Cell? cellMiddle = middleSite.Cell ?? _dcel.CreateCell(middleSite);
            Cell? cellRight = rightSite.Cell ?? _dcel.CreateCell(rightSite);

            RecordVertexOnCell(cellLeft, vLoc);
            RecordVertexOnCell(cellMiddle, vLoc);
            RecordVertexOnCell(cellRight, vLoc);

            // End edge (leftSite, middleSite) — record vertex on its fragment.
            int idPiPjMin = Math.Min(leftSite.Id, middleSite.Id);
            int idPiPjMax = Math.Max(leftSite.Id, middleSite.Id);
            if (_edgeFragments.TryGetValue((idPiPjMin, idPiPjMax), out var frag1))
            {
                frag1.Vertices.Add(vLoc);
                _edgeFragments.Remove((idPiPjMin, idPiPjMax));
            }

            // End edge (middleSite, rightSite) — record vertex on its fragment.
            int idPjPkMin = Math.Min(middleSite.Id, rightSite.Id);
            int idPjPkMax = Math.Max(middleSite.Id, rightSite.Id);
            if (_edgeFragments.TryGetValue((idPjPkMin, idPjPkMax), out var frag2))
            {
                frag2.Vertices.Add(vLoc);
                _edgeFragments.Remove((idPjPkMin, idPjPkMax));
            }

            // Create new fragment for surviving breakpoint (leftSite | rightSite).
            var (eNew1, eNew2) = _dcel.CreateTwinPair();
            int idPiPkMin = Math.Min(leftSite.Id, rightSite.Id);
            int idPiPkMax = Math.Max(leftSite.Id, rightSite.Id);

            Site siteA = Sites[idPiPkMin];
            Site siteB = Sites[idPiPkMax];

            var eNewA = siteA == leftSite ? eNew1 : eNew2;
            var eNewB = siteB == rightSite ? eNew2 : eNew1;

            eNewA.Face = cellLeft;
            eNewB.Face = cellRight;

            if (cellLeft.BoundaryEdge == null)
                cellLeft.BoundaryEdge = eNewA;
            if (cellRight.BoundaryEdge == null)
                cellRight.BoundaryEdge = eNewB;

            var newFrag = new EdgeFragment(siteA, siteB, eNewA, eNewB);
            newFrag.Vertices.Add(vLoc);
            _edgeFragments[(idPiPkMin, idPiPkMax)] = newFrag;
            _allFragments.Add(newFrag);

            if (leftNeighbor != null)
            {
                var ln = _beachLine.GetNeighbors(leftNeighbor);
                if (ln.left != null && ln.right != null)
                    TryAddCircleEvent(ln.left.Site, leftNeighbor.Site, ln.right.Site, leftNeighbor);
            }

            if (rightNeighbor != null)
            {
                var rn = _beachLine.GetNeighbors(rightNeighbor);
                if (rn.left != null && rn.right != null)
                    TryAddCircleEvent(rn.left.Site, rightNeighbor.Site, rn.right.Site, rightNeighbor);
            }
        }

        private void TryAddCircleEvent(Site leftSite, Site middleSite, Site rightSite, BeachArcNode? middleArc = null)
        {
            Circle? circle = Circle.FromThreePoints(
                leftSite.Location,
                middleSite.Location,
                rightSite.Location);

            if (!circle.HasValue) return;

            // The event fires at the bottom of the circumcircle. It must be below the middle site
            // for the breakpoints to converge (the middle arc must shrink). Left/right sites may
            // be lower — they just need to already be on the beach line when the event fires.
            double eventY = circle.Value.Center.Y - circle.Value.Radius;

            if (eventY >= middleSite.Location.Y - 1e-9)
            {
                return; // Breakpoints diverge — no valid convergence
            }

            // Three consecutive arcs on the beach line guarantee an empty circumcircle (Lemma 7.8).

            if (middleArc == null)
            {
                middleArc = FindMiddleArc(middleSite, leftSite);
                if (middleArc == null) return;
            }

            if (middleArc.CircleEvent != null)
                middleArc.CircleEvent.Invalidate();

            var circleEvt = new CircleEvent(
                circle.Value.Center,
                circle.Value,
                leftSite,
                middleSite,
                rightSite,
                middleArc);

            middleArc.CircleEvent = circleEvt;
            _eventQueue.Insert(circleEvt);
        }

        private BeachArcNode? FindMiddleArc(Site middleSite, Site leftSite)
        {
            foreach (var arc in _beachLine.GetAllArcs())
            {
                if (arc.Site != middleSite) continue;

                var neighbors = _beachLine.GetNeighbors(arc);
                if (neighbors.left?.Site == leftSite || neighbors.right?.Site == leftSite)
                    return arc;
            }
            return null;
        }

        private void FinalizeDiagram()
        {
            foreach (var site in Sites)
            {
                if (site.Cell == null)
                    _dcel.CreateCell(site);
            }

            BuildCellRingsFromFragments();
        }

        /// <summary>
        /// Builds cell boundary rings from edge fragments collected during the sweep.
        /// For cells with insufficient vertices, falls back to brute-force half-plane intersection.
        /// </summary>
        private void BuildCellRingsFromFragments()
        {
            // Compute bounding box from all sites and finite Voronoi vertices.
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            foreach (var s in Sites)
            {
                minX = Math.Min(minX, s.Location.X);
                maxX = Math.Max(maxX, s.Location.X);
                minY = Math.Min(minY, s.Location.Y);
                maxY = Math.Max(maxY, s.Location.Y);
            }

            foreach (var frag in _allFragments)
            {
                foreach (var vp in frag.Vertices)
                {
                    if (!double.IsFinite(vp.X) || !double.IsFinite(vp.Y)) continue;
                    minX = Math.Min(minX, vp.X);
                    maxX = Math.Max(maxX, vp.X);
                    minY = Math.Min(minY, vp.Y);
                    maxY = Math.Max(maxY, vp.Y);
                }
            }

            double spanX = maxX - minX;
            double spanY = maxY - minY;
            double margin = Math.Max(Math.Max(spanX, spanY) * 0.5, 10);
            minX -= margin; maxX += margin;
            minY -= margin; maxY += margin;

            foreach (var cell in _dcel.Cells)
            {
                if (cell.Site == null) continue;
                Site mySite = cell.Site;

                // Collect all finite vertices from ALL fragments involving this site.
                var vertexSet = new HashSet<string>();
                var vertexList = new List<Point>();

                foreach (var frag in _allFragments)
                {
                    if (frag.SiteA != mySite && frag.SiteB != mySite) continue;

                    foreach (var vp in frag.Vertices)
                    {
                        string key = $"{vp.X:F10},{vp.Y:F10}";
                        if (!vertexSet.Contains(key))
                        {
                            vertexSet.Add(key);
                            vertexList.Add(vp);
                        }
                    }
                }

                // Cap truly infinite edges (remaining in _edgeFragments after sweep).
                foreach (var kvp in _edgeFragments)
                {
                    EdgeFragment frag = kvp.Value;
                    if (frag.SiteA != mySite && frag.SiteB != mySite) continue;

                    Site otherSite = frag.SiteA == mySite ? frag.SiteB : frag.SiteA;
                    Point sa = mySite.Location;
                    Point sb = otherSite.Location;
                    Point ab = new Point(sb.X - sa.X, sb.Y - sa.Y);
                    Point perp = new Point(-ab.Y, ab.X);

                    if (Math.Abs(perp.X) < 1e-12 && Math.Abs(perp.Y) < 1e-12) continue;

                    // Determine origin point for ray extension.
                    Point origin;
                    if (frag.Vertices.Count > 0)
                        origin = frag.Vertices[frag.Vertices.Count - 1]; // Last vertex (lowest Y)
                    else
                        origin = new Point((sa.X + sb.X) / 2, (sa.Y + sb.Y) / 2); // Midpoint on bisector

                    // Pick the direction that points AWAY from mySite's interior.
                    double distOrigin = origin.Distance(sa);
                    Point testPos = new Point(origin.X + perp.X, origin.Y + perp.Y);
                    bool positiveIsOutward = testPos.Distance(sa) > distOrigin + 1e-9;

                    int sign = positiveIsOutward ? 1 : -1;
                    Point dir = new Point(perp.X * sign, perp.Y * sign);

                    Point? boxPt = RayBoxIntersection(origin, dir, minX, maxX, minY, maxY);
                    if (boxPt.HasValue)
                    {
                        string key = $"{boxPt.Value.X:F10},{boxPt.Value.Y:F10}";
                        if (!vertexSet.Contains(key))
                        {
                            vertexSet.Add(key);
                            vertexList.Add(boxPt.Value);
                        }
                    }

                    // For fragments with 0 vertices (purely infinite from a site event),
                    // also extend in the opposite direction.
                    if (frag.Vertices.Count == 0)
                    {
                        sign = -sign;
                        dir = new Point(perp.X * sign, perp.Y * sign);

                        boxPt = RayBoxIntersection(origin, dir, minX, maxX, minY, maxY);
                        if (boxPt.HasValue)
                        {
                            string key = $"{boxPt.Value.X:F10},{boxPt.Value.Y:F10}";
                            if (!vertexSet.Contains(key))
                            {
                                vertexSet.Add(key);
                                vertexList.Add(boxPt.Value);
                            }
                        }
                    }
                }

                // Fallback: if fewer than 3 vertices, compute this cell via brute-force
                // half-plane intersection (Sutherland-Hodgman clipping against all bisectors).
                if (vertexList.Count < 3)
                {
                    var poly = new List<Point>
                    {
                        new Point(minX, minY), new Point(maxX, minY),
                        new Point(maxX, maxY), new Point(minX, maxY)
                    };
                    foreach (var other in Sites)
                    {
                        if (other == mySite) continue;
                        poly = ClipHalfPlane(poly, mySite.Location, other.Location);
                        if (poly.Count < 3) break;
                    }
                    if (poly.Count >= 3)
                        vertexList = poly;
                }

                if (vertexList.Count < 2) continue;

                // Sort vertices angularly around the site. Voronoi cells are star-shaped with
                // respect to their defining site, so this produces correct boundary order.
                Point center = mySite.Location;
                vertexList.Sort((va, vb) =>
                    Math.Atan2(va.Y - center.Y, va.X - center.X).CompareTo(
                        Math.Atan2(vb.Y - center.Y, vb.X - center.X)));

                // Clamp extreme vertices to bounding box.
                for (int i = 0; i < vertexList.Count; i++)
                {
                    var v = vertexList[i];
                    double cx = Math.Clamp(v.X, minX, maxX);
                    double cy = Math.Clamp(v.Y, minY, maxY);
                    if (!v.AlmostEquals(new Point(cx, cy)))
                        vertexList[i] = new Point(cx, cy);
                }

                // Build half-edge ring.
                var edges = new HalfEdge[vertexList.Count];
                for (int i = 0; i < vertexList.Count; i++)
                {
                    int next = (i + 1) % vertexList.Count;

                    var (e1, e2) = _dcel.CreateTwinPair();

                    Vertex vStart = FindOrCreateVertex(vertexList[i]);
                    Vertex vEnd = FindOrCreateVertex(vertexList[next]);

                    e1.Origin = vStart;
                    e1.Face = cell;
                    e2.Origin = vEnd;
                    e2.Face = null; // Outer face

                    edges[i] = e1;
                }

                for (int i = 0; i < edges.Length; i++)
                {
                    int next = (i + 1) % edges.Length;
                    edges[i].Next = edges[next];
                    edges[i].Prev = edges[(i - 1 + edges.Length) % edges.Length];
                }

                cell.BoundaryEdge = edges[0];
                cell.RecordedVertices.Clear();
                foreach (var vp in vertexList)
                    cell.RecordedVertices.Add(vp);
            }
        }

        private static Point? RayBoxIntersection(Point origin, Point dir, double minX, double maxX, double minY, double maxY)
        {
            if (Math.Abs(dir.X) < 1e-12 && Math.Abs(dir.Y) < 1e-12)
                return null;

            double tMin = 0;
            double tMax = double.MaxValue;

            if (Math.Abs(dir.X) > 1e-12)
            {
                double t1 = (minX - origin.X) / dir.X;
                double t2 = (maxX - origin.X) / dir.X;
                tMin = Math.Max(tMin, Math.Min(t1, t2));
                tMax = Math.Min(tMax, Math.Max(t1, t2));
            }
            else if (origin.X < minX || origin.X > maxX)
                return null;

            if (Math.Abs(dir.Y) > 1e-12)
            {
                double t1 = (minY - origin.Y) / dir.Y;
                double t2 = (maxY - origin.Y) / dir.Y;
                tMin = Math.Max(tMin, Math.Min(t1, t2));
                tMax = Math.Min(tMax, Math.Max(t1, t2));
            }
            else if (origin.Y < minY || origin.Y > maxY)
                return null;

            if (tMin > tMax) return null;

            double t = -1;
            if (tMin > 1e-9)
                t = tMin;
            else if (tMax > 1e-9)
                t = tMax;

            if (t < 1e-9) return null;

            return new Point(origin.X + dir.X * t, origin.Y + dir.Y * t);
        }

        private Vertex FindOrCreateVertex(Point location)
        {
            foreach (var v in _dcel.Vertices)
            {
                if (v.Location.AlmostEquals(location))
                    return v;
            }
            return _dcel.CreateVertex(location);
        }

        /// <summary>
        /// Clips a polygon against the half-plane closer to pi than pj.
        /// Sutherland-Hodgman algorithm: keeps only the portion of the polygon
        /// that lies on pi's side of the perpendicular bisector of pij.
        /// </summary>
        private static List<Point> ClipHalfPlane(List<Point> poly, Point pi, Point pj)
        {
            if (poly.Count == 0) return poly;

            double nx = pi.X - pj.X;
            double ny = pi.Y - pj.Y;
            double c = 0.5 * (pi.X * pi.X + pi.Y * pi.Y - pj.X * pj.X - pj.Y * pj.Y);

            bool IsInside(Point q) => (nx * q.X + ny * q.Y) > c - 1e-9;

            static Point IntersectPoint(Point s, Point e, double nx, double ny, double c)
            {
                double dx = e.X - s.X, dy = e.Y - s.Y;
                double denom = nx * dx + ny * dy;
                if (Math.Abs(denom) < 1e-12) return new Point((s.X + e.X) / 2, (s.Y + e.Y) / 2);
                double t = Math.Clamp((c - nx * s.X - ny * s.Y) / denom, 0, 1);
                return new Point(s.X + t * dx, s.Y + t * dy);
            }

            var output = new List<Point>();
            Point S = poly[poly.Count - 1];
            bool sIn = IsInside(S);

            foreach (Point E in poly)
            {
                bool eIn = IsInside(E);
                if (eIn)
                {
                    if (!sIn) output.Add(IntersectPoint(S, E, nx, ny, c));
                    output.Add(E);
                }
                else if (sIn)
                {
                    output.Add(IntersectPoint(S, E, nx, ny, c));
                }
                S = E; sIn = eIn;
            }

            return output;
        }

        private void RecordVertexOnCell(Cell cell, Point location)
        {
            foreach (var existing in cell.RecordedVertices)
            {
                if (existing.AlmostEquals(location)) return;
            }
            cell.RecordedVertices.Add(location);
        }
    }
}
