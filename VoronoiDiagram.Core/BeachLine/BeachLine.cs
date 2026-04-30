using System;
using VoronoiDiagram.Core.DataStructures;
using VoronoiDiagram.Core.Events;

namespace VoronoiDiagram.Core.BeachLineTree
{
    /// <summary>
    /// Binary search tree representing the beach line in Fortune's algorithm.
    ///
    /// Structure (per p.155):
    /// - Leaves correspond to parabolic arcs, ordered left-to-right by x-coordinate
    /// - Internal nodes represent breakpoints between adjacent arc groups
    ///   "A breakpoint is stored at an internal node by an ordered tuple of sites <pi, pj>"
    ///
    /// The tree enables O(log n) search for the arc above a new site.
    /// Each internal node caches its defining sites (LeftSite/RightSite) and stores
    /// a pointer to the half-edge being traced by this breakpoint (p.155).
    ///
    /// Arcs also maintain a doubly-linked list (Prev/Next) for O(1) neighbor lookup.
    /// </summary>
    public class BeachLine
    {
        private BeachTreeNode? _root;

        // ---- Search operations ----

        /// <summary>
        /// Finds the arc on the beach line vertically above a given point.
        /// Navigates by comparing query x-coordinate against breakpoint x-coordinates.
        /// </summary>
        public BeachArcNode? FindArcAbove(Point query, double sweepY)
        {
            return FindArc(_root, query.X, sweepY);
        }

        private static BeachArcNode? FindArc(BeachTreeNode? node, double x, double sweepY)
        {
            if (node == null || node.IsLeaf)
                return node?.Arc;

            // Breakpoint at this internal node is between LeftSite and RightSite (cached).
            double breakX = GetBreakpointX(node.LeftSite, node.RightSite, sweepY);

            if (x < breakX)
                return FindArc(node.Left!, x, sweepY);
            else
                return FindArc(node.Right!, x, sweepY);
        }

        // ---- Insertion: split an existing arc and insert a new one between the pieces ----

        /// <summary>
        /// Inserts a new arc into the beach line. Called during site events (p.158).
        /// The original `existingArc` is reused as the LEFT piece of the split, preserving
        /// object identity so circle events referencing it still find it in the tree.
        /// Returns the RIGHT piece (new BeachArcNode with same site) for circle event detection.
        /// </summary>
        public BeachArcNode InsertArc(BeachArcNode newArc, BeachArcNode existingArc)
        {
            if (_root == null)
            {
                _root = new BeachTreeNode(newArc);
                return newArc;
            }

            if (existingArc.CircleEvent != null)
                existingArc.CircleEvent.Invalidate();

            BeachArcNode rightPiece = new BeachArcNode(existingArc.Site);

            // Update doubly-linked list: existingArc -> newArc -> rightPiece
            BeachArcNode? oldNext = existingArc.Next;
            existingArc.Next = newArc;
            newArc.Prev = existingArc;
            newArc.Next = rightPiece;
            rightPiece.Prev = newArc;
            rightPiece.Next = oldNext;
            if (oldNext != null)
                oldNext.Prev = rightPiece;

            // Recursive insertion maintains BST invariant and parent pointers correctly.
            _root = InsertArcRecursive(_root, existingArc, newArc, rightPiece);
            return rightPiece;
        }

        /// <summary>
        /// Recursively finds the leaf containing existingArc and replaces it with a 3-leaf subtree.
        /// Returns the (possibly rebuilt) root of the modified subtree.
        /// </summary>
        private BeachTreeNode InsertArcRecursive(BeachTreeNode node, BeachArcNode existingArc,
                                                  BeachArcNode newArc, BeachArcNode rightPiece)
        {
            if (node.IsLeaf && node.Arc == existingArc)
            {
                // Found the target leaf — replace with 3-leaf subtree.
                BeachTreeNode leafL = new BeachTreeNode(existingArc);
                BeachTreeNode leafN = new BeachTreeNode(newArc);
                BeachTreeNode leafR = new BeachTreeNode(rightPiece);

                BeachTreeNode bp2 = new BeachTreeNode(leafN, leafR, newArc.Site, rightPiece.Site);
                return new BeachTreeNode(leafL, bp2, existingArc.Site, newArc.Site);
            }

            if (node.IsLeaf)
                return node; // Not the target — shouldn't happen for valid inputs

            // Recurse into correct child.
            BeachTreeNode leftChild = node.Left!;
            BeachTreeNode rightChild = node.Right!;

            // Check which subtree contains the existingArc leaf.
            if (ContainsLeaf(leftChild, existingArc))
            {
                BeachTreeNode newLeft = InsertArcRecursive(leftChild, existingArc, newArc, rightPiece);
                if (newLeft != leftChild)
                {
                    // Left subtree was modified — rebuild this node with updated cached sites.
                    node.Left = newLeft;
                    newLeft.Parent = node;
                    UpdateCachedSites(node);
                }
            }
            else
            {
                BeachTreeNode newRight = InsertArcRecursive(rightChild, existingArc, newArc, rightPiece);
                if (newRight != rightChild)
                {
                    node.Right = newRight;
                    newRight.Parent = node;
                    UpdateCachedSites(node);
                }
            }

            return node;
        }

        /// <summary>
        /// Checks whether a subtree contains a leaf with the given arc.
        /// </summary>
        private static bool ContainsLeaf(BeachTreeNode node, BeachArcNode arc)
        {
            if (node.IsLeaf)
                return node.Arc == arc;

            // Use cached sites to narrow search: leftmost of left subtree vs rightmost of right subtree.
            // The arc we're looking for should be in the subtree whose range contains it.
            BeachArcNode? leftMost = GetLeftmostLeaf(node.Left!);
            if (leftMost == arc) return true;

            BeachArcNode? rightMost = GetRightmostLeaf(node.Right!);
            // If the arc is between leftmost and rightmost, check both subtrees.
            // For efficiency, we check the left subtree first (common case).
            var found = FindLeafInSubtree(node.Left!, arc);
            if (found != null) return true;

            found = FindLeafInSubtree(node.Right!, arc);
            return found != null;
        }

        private static BeachArcNode? FindLeafInSubtree(BeachTreeNode node, BeachArcNode arc)
        {
            if (node.IsLeaf)
                return node.Arc == arc ? arc : null;

            var result = FindLeafInSubtree(node.Left!, arc);
            if (result != null) return result;
            return FindLeafInSubtree(node.Right!, arc);
        }

        // ---- Removal: delete an arc (circle event) ----

        /// <summary>
        /// Removes an arc from the beach line. Called during circle events (p.158).
        /// When the middle arc disappears, its left and right neighbors become adjacent.
        /// </summary>
        public void RemoveArc(BeachArcNode arc)
        {
            if (_root == null || _root.IsLeaf && _root.Arc == arc)
            {
                _root = null;
                return;
            }

            // Update doubly-linked list: bypass the removed arc
            if (arc.Prev != null)
                arc.Prev.Next = arc.Next;
            if (arc.Next != null)
                arc.Next.Prev = arc.Prev;
            arc.Prev = null;
            arc.Next = null;

            // Recursive removal: replace the leaf with its sibling subtree.
            _root = RemoveArcRecursive(_root, arc);
        }

        /// <summary>
        /// Recursively finds and removes a leaf containing the given arc.
        /// Returns the (possibly rebuilt) root of the modified subtree, or null if tree becomes empty.
        /// </summary>
        private BeachTreeNode? RemoveArcRecursive(BeachTreeNode node, BeachArcNode arc)
        {
            if (node.IsLeaf && node.Arc == arc)
            {
                // Removing the only leaf — shouldn't reach here due to caller's guard.
                return null;
            }

            if (node.IsLeaf)
                return node; // Not found

            BeachTreeNode leftChild = node.Left!;
            BeachTreeNode rightChild = node.Right!;

            if (leftChild.IsLeaf && leftChild.Arc == arc)
            {
                // Removing left child — replace with right subtree.
                return rightChild;
            }

            if (rightChild.IsLeaf && rightChild.Arc == arc)
            {
                // Removing right child — replace with left subtree.
                return leftChild;
            }

            // Recurse into correct child.
            BeachTreeNode? newLeft = RemoveArcRecursive(leftChild, arc);
            if (newLeft != null && newLeft != leftChild)
            {
                node.Left = newLeft;
                newLeft.Parent = node;
                UpdateCachedSites(node);
                return node;
            }

            BeachTreeNode? newRight = RemoveArcRecursive(rightChild, arc);
            if (newRight != null && newRight != rightChild)
            {
                node.Right = newRight;
                newRight.Parent = node;
                UpdateCachedSites(node);
                return node;
            }

            // If a child became null after removal, promote the other child.
            if (newLeft == null)
                return rightChild;
            if (newRight == null)
                return leftChild;

            return node;
        }

        // ---- Neighbor lookup (O(1) via doubly-linked list) ----

        /// <summary>
        /// Gets the left and right neighbors of an arc in O(1).
        /// Uses the embedded Prev/Next linked list maintained during insert/remove.
        /// </summary>
        public (BeachArcNode? left, BeachArcNode? right) GetNeighbors(BeachArcNode arc)
        {
            return (arc.Prev, arc.Next);
        }

        // ---- Half-edge management on breakpoints ----

        /// <summary>
        /// Sets the half-edge being traced by a breakpoint between two sites.
        /// Per p.155: "Every internal node ν has a pointer to a half-edge."
        /// </summary>
        public void SetEdge(Site leftSite, Site rightSite, HalfEdge edge)
        {
            var node = FindBreakpointNode(_root, leftSite, rightSite);
            if (node != null)
                node.Edge = edge;
        }

        /// <summary>
        /// Gets the half-edge being traced by a breakpoint between two sites.
        /// </summary>
        public HalfEdge? GetEdge(Site leftSite, Site rightSite)
        {
            return FindBreakpointNode(_root, leftSite, rightSite)?.Edge;
        }

        private static BeachTreeNode? FindBreakpointNode(BeachTreeNode? node, Site leftSite, Site rightSite)
        {
            if (node == null || node.IsLeaf) return null;

            if (node.LeftSite == leftSite && node.RightSite == rightSite)
                return node;

            var result = FindBreakpointNode(node.Left!, leftSite, rightSite);
            if (result != null) return result;

            return FindBreakpointNode(node.Right!, leftSite, rightSite);
        }

        // ---- Tree manipulation helpers ----

        /// <summary>
        /// Finds the immediate parent internal node of a leaf containing the given arc,
        /// along with the sibling subtree. Returns null if not found.
        /// </summary>
        private static (BeachTreeNode parent, BeachTreeNode? sibling)? FindLeafContext(
            BeachTreeNode node, BeachArcNode arc)
        {
            if (node.IsLeaf) return null;

            BeachTreeNode leftChild = node.Left!;
            BeachTreeNode rightChild = node.Right!;

            // Check direct children first
            if (leftChild.IsLeaf && leftChild.Arc == arc)
                return (node, rightChild);

            if (rightChild.IsLeaf && rightChild.Arc == arc)
                return (node, leftChild);

            // Recurse into subtrees
            var result = FindLeafContext(leftChild, arc);
            if (result != null) return result;

            return FindLeafContext(rightChild, arc);
        }

        /// <summary>
        /// Recomputes cached LeftSite/RightSite for a node by finding its leftmost and rightmost leaves.
        /// </summary>
        private static void UpdateCachedSites(BeachTreeNode node)
        {
            if (node.IsLeaf) return;

            BeachArcNode leftMost = GetLeftmostLeaf(node.Left!);
            BeachArcNode rightMost = GetRightmostLeaf(node.Right!);

            node.LeftSite = leftMost.Site;
            node.RightSite = rightMost.Site;
        }

        /// <summary>
        /// Updates cached sites from a given node up to the root.
        /// </summary>
        private void UpdateCachedSitesUpToRoot(BeachTreeNode node)
        {
            BeachTreeNode? current = node;
            while (current != null)
            {
                if (!current.IsLeaf)
                    UpdateCachedSites(current);

                current = current.Parent;
            }
        }

        // ---- Utility operations ----

        /// <summary>
        /// Gets all arcs in left-to-right order using the doubly-linked list.
        /// </summary>
        public System.Collections.Generic.List<BeachArcNode> GetAllArcs()
        {
            var arcs = new System.Collections.Generic.List<BeachArcNode>();

            if (_root == null) return arcs;

            BeachArcNode? first = GetLeftmostLeaf(_root);
            BeachArcNode? current = first;
            while (current != null)
            {
                arcs.Add(current);
                current = current.Next;
            }

            return arcs;
        }

        /// <summary>
        /// Gets all breakpoints in left-to-right order for visualization.
        /// </summary>
        public System.Collections.Generic.List<(Site Left, Site Right, Point Location)> GetBreakpoints(double sweepY)
        {
            var bps = new System.Collections.Generic.List<(Site, Site, Point)>();
            CollectBreakpoints(_root, sweepY, bps);
            return bps;
        }

        private static void CollectBreakpoints(BeachTreeNode? node, double sweepY,
                                                System.Collections.Generic.List<(Site, Site, Point)> result)
        {
            if (node == null || node.IsLeaf) return;

            CollectBreakpoints(node.Left!, sweepY, result);

            double bx = GetBreakpointX(node.LeftSite, node.RightSite, sweepY);
            double by = GetParabolaY(bx, node.LeftSite.Location, sweepY);

            result.Add((node.LeftSite, node.RightSite, new Point(bx, by)));

            CollectBreakpoints(node.Right!, sweepY, result);
        }

        public void Clear() => _root = null;

        // ---- Static geometric computations ----

        /// <summary>
        /// Computes the x-coordinate of the breakpoint between two adjacent parabolic arcs.
        /// Equating parabola equations and solving for x (quadratic terms cancel):
        ///   x_bp = ((xi^2 - xj^2) + (yi^2 - yj^2) - 2*ys*(yi - yj)) / (2*(xi - xj))
        /// </summary>
        public static double GetBreakpointX(Site leftSite, Site rightSite, double sweepY)
        {
            Point a = leftSite.Location;
            Point b = rightSite.Location;

            if (Math.Abs(a.X - b.X) < 1e-12)
                return (a.X + b.X) / 2.0;

            double numerator = (a.X * a.X - b.X * b.X) + (a.Y * a.Y - b.Y * b.Y)
                              - 2.0 * sweepY * (a.Y - b.Y);
            double denominator = 2.0 * (a.X - b.X);

            return numerator / denominator;
        }

        /// <summary>
        /// Computes the y-coordinate of a parabola at a given x.
        /// Parabola with focus (fx, fy) and directrix y = ys:
        ///   y(x) = fy + (x - fx)^2 / (2 * (fy - ys))
        /// </summary>
        public static double GetParabolaY(double x, Point focus, double sweepY)
        {
            double d = focus.Y - sweepY;
            if (Math.Abs(d) < 1e-12) return focus.Y;
            return focus.Y + (x - focus.X) * (x - focus.X) / (2.0 * d);
        }

        private static BeachArcNode? GetLeftmostLeaf(BeachTreeNode node)
        {
            while (!node.IsLeaf)
                node = node.Left!;
            return node.Arc;
        }

        private static BeachArcNode? GetRightmostLeaf(BeachTreeNode node)
        {
            while (!node.IsLeaf)
                node = node.Right!;
            return node.Arc;
        }
    }

    /// <summary>
    /// Internal node of the beach line BST.
    /// Either a leaf (one parabolic arc) or an internal node (breakpoint between two subtrees).
    /// </summary>
    internal class BeachTreeNode
    {
        public BeachArcNode? Arc;           // Set for leaves only
        public BeachTreeNode? Left, Right;  // Set for internal nodes only
        public BeachTreeNode? Parent;

        // Cached sites defining this breakpoint (internal nodes only)
        public Site LeftSite;
        public Site RightSite;

        /// <summary>
        /// Half-edge being traced by this breakpoint on the beach line (p.155).
        /// </summary>
        public HalfEdge? Edge;

        // Leaf constructor
        public BeachTreeNode(BeachArcNode arc)
        {
            Arc = arc;
        }

        // Internal node constructor
        public BeachTreeNode(BeachTreeNode left, BeachTreeNode right, Site leftSite, Site rightSite)
        {
            Left = left;
            Right = right;
            left.Parent = this;
            right.Parent = this;
            LeftSite = leftSite;
            RightSite = rightSite;
        }

        public bool IsLeaf => Arc != null;
    }
}
