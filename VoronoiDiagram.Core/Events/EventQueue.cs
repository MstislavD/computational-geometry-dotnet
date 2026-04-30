using System.Collections.Generic;

namespace VoronoiDiagram.Core.Events
{
    /// <summary>
    /// Priority queue for sweep line events, ordered by y-coordinate (descending).
    /// The algorithm processes events from top to bottom: "Remove the event with largest
    /// y-coordinate from Q" (Algorithm step 3, p.157).
    ///
    /// Implementation uses a binary heap for O(log n) insert and extract operations.
    /// Supports invalidation of circle events (false alarms) without removal;
    /// invalidated events are skipped during extraction.
    ///
    /// Per Lemma 7.9: "The primitive operations on the tree T and the event queue Q,
    /// such as inserting or deleting an element, take O(log n) time each."
    /// </summary>
    public class EventQueue
    {
        private readonly List<SweepEvent> _heap = new List<SweepEvent>();
        private int _insertCount = 0;

        public int Count => _heap.Count;
        public bool IsEmpty => _heap.Count == 0;

        /// <summary>
        /// Inserts an event into the priority queue. O(log n).
        /// All site events are known in advance and inserted at initialization.
        /// Circle events are discovered dynamically during the sweep (p.156).
        /// </summary>
        public void Insert(SweepEvent evt)
        {
            _heap.Add(evt);
            Swim(_heap.Count - 1);
            _insertCount++;

            // Periodically compact when invalidated events accumulate.
            if (_insertCount % 64 == 0 && _heap.Count > 128)
            {
                Compact();
            }
        }

        /// <summary>
        /// Removes and returns the event with the highest y-coordinate (topmost on screen).
        /// Skips any invalidated events. O(log n) amortized.
        /// Returns null if no valid events remain.
        /// </summary>
        public SweepEvent? RemoveHighest()
        {
            // Clean up invalidated events from the top
            while (_heap.Count > 0 && _heap[0].IsInvalidated)
            {
                RemoveRoot();
            }

            if (_heap.Count == 0)
                return null;

            SweepEvent result = _heap[0];
            RemoveRoot();
            return result;
        }

        /// <summary>
        /// Peeks at the highest-priority event without removing it.
        /// Useful for debugging and determining sweep line position.
        /// </summary>
        public SweepEvent? Peek()
        {
            while (_heap.Count > 0 && _heap[0].IsInvalidated)
            {
                RemoveRoot();
            }

            return _heap.Count > 0 ? _heap[0] : null;
        }

        private void Swim(int index)
        {
            // Higher y-coordinate = higher priority (sweep moves downward from top)
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (_heap[index].YCoordinate <= _heap[parent].YCoordinate)
                    break;

                Swap(index, parent);
                index = parent;
            }
        }

        private void Sink(int index)
        {
            int size = _heap.Count;
            while (true)
            {
                int largest = index;
                int left = 2 * index + 1;
                int right = 2 * index + 2;

                if (left < size && _heap[left].YCoordinate > _heap[largest].YCoordinate)
                    largest = left;

                if (right < size && _heap[right].YCoordinate > _heap[largest].YCoordinate)
                    largest = right;

                if (largest == index)
                    break;

                Swap(index, largest);
                index = largest;
            }
        }

        private void RemoveRoot()
        {
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);

            if (_heap.Count > 0)
                Sink(0);
        }

        private void Swap(int i, int j)
        {
            var temp = _heap[i];
            _heap[i] = _heap[j];
            _heap[j] = temp;
        }

        /// <summary>
        /// Removes all events from the queue. Used for resetting between computations.
        /// </summary>
        public void Clear()
        {
            _heap.Clear();
            _insertCount = 0;
        }

        /// <summary>
        /// Removes invalidated events from the heap and rebuilds the heap structure.
        /// Called periodically to prevent unbounded growth from stale circle events.
        /// </summary>
        private void Compact()
        {
            int beforeCount = _heap.Count;
            _heap.RemoveAll(e => e.IsInvalidated);

            if (_heap.Count == beforeCount) return; // Nothing removed

            // Rebuild heap in O(n) from the filtered list.
            Heapify();
        }

        /// <summary>
        /// Builds a valid max-heap from the current list in O(n) time (Floyd's algorithm).
        /// </summary>
        private void Heapify()
        {
            for (int i = _heap.Count / 2 - 1; i >= 0; i--)
            {
                Sink(i);
            }
        }
    }
}
