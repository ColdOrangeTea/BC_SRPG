using System;
using System.Collections.Generic;

namespace BlackChess.SRPG.Pathfinding
{
    /// <summary>
    /// 最小堆積 (二元堆積) 實作的優先佇列。
    /// Dijkstra 需要「每次取出目前累積成本最小的節點」，用優先佇列可讓這個操作是 O(log n)，
    /// 整體複雜度 O(E log V)，遠比每次線性搜尋最小值 (O(V^2)) 有效率。
    ///
    /// Unity 內建沒有 PriorityQueue (.NET 6 才有，Unity 2023 的 C# 版本不一定可用)，故自己實作一個。
    /// </summary>
    public class MinHeap<T>
    {
        private readonly List<(T item, int priority)> _heap = new List<(T, int)>();

        public int Count => _heap.Count;

        public void Clear() => _heap.Clear();

        /// <summary>放入一個元素，priority 越小越先被取出 (代表累積移動成本越低越優先)。</summary>
        public void Push(T item, int priority)
        {
            _heap.Add((item, priority));
            SiftUp(_heap.Count - 1);
        }

        /// <summary>取出並移除目前成本最小的元素。</summary>
        public T Pop()
        {
            if (_heap.Count == 0) throw new InvalidOperationException("Heap is empty.");
            var root = _heap[0].item;
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);
            if (_heap.Count > 0) SiftDown(0);
            return root;
        }

        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (_heap[i].priority >= _heap[parent].priority) break;
                (_heap[i], _heap[parent]) = (_heap[parent], _heap[i]);
                i = parent;
            }
        }

        private void SiftDown(int i)
        {
            int n = _heap.Count;
            while (true)
            {
                int left = 2 * i + 1, right = 2 * i + 2, smallest = i;
                if (left < n && _heap[left].priority < _heap[smallest].priority) smallest = left;
                if (right < n && _heap[right].priority < _heap[smallest].priority) smallest = right;
                if (smallest == i) break;
                (_heap[i], _heap[smallest]) = (_heap[smallest], _heap[i]);
                i = smallest;
            }
        }
    }
}
