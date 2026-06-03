using System;

namespace FoundersLands.Simulation.Pathfinding
{
    /// <summary>
    /// Binary min-heap of (priority, node) used by A* and Dijkstra. Stable: equal priorities
    /// break on node index, so searches are deterministic.
    /// </summary>
    internal sealed class MinHeap
    {
        private float[] _key;
        private int[] _val;
        private int _count;

        public MinHeap(int capacity)
        {
            if (capacity < 16) capacity = 16;
            _key = new float[capacity];
            _val = new int[capacity];
        }

        public int Count { get { return _count; } }

        public void Push(float key, int val)
        {
            if (_count == _key.Length)
            {
                Array.Resize(ref _key, _count * 2);
                Array.Resize(ref _val, _count * 2);
            }
            int i = _count++;
            _key[i] = key;
            _val[i] = val;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (!Less(i, p)) break;
                Swap(i, p);
                i = p;
            }
        }

        public int Pop()
        {
            int result = _val[0];
            _count--;
            _key[0] = _key[_count];
            _val[0] = _val[_count];
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1;
                int r = l + 1;
                int smallest = i;
                if (l < _count && Less(l, smallest)) smallest = l;
                if (r < _count && Less(r, smallest)) smallest = r;
                if (smallest == i) break;
                Swap(i, smallest);
                i = smallest;
            }
            return result;
        }

        private bool Less(int a, int b)
        {
            if (_key[a] < _key[b]) return true;
            if (_key[a] > _key[b]) return false;
            return _val[a] < _val[b];
        }

        private void Swap(int a, int b)
        {
            float k = _key[a]; _key[a] = _key[b]; _key[b] = k;
            int v = _val[a]; _val[a] = _val[b]; _val[b] = v;
        }
    }
}
