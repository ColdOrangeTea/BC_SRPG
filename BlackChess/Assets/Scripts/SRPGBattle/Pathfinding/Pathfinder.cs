using System;
using System.Collections.Generic;
using BlackChess.SRPG.Core;

namespace BlackChess.SRPG.Pathfinding
{
    /// <summary>
    /// 尋路核心。使用 Dijkstra 演算法計算「移動範圍」與「最短路徑」。
    /// 只依賴 <see cref="IPathGrid"/>，不綁任何單位/物件類別，可直接接到任何棋盤上。
    ///
    /// ─────────────────────────────────────────────────────────────
    ///  為什麼用 Dijkstra 而不是 BFS？
    /// ─────────────────────────────────────────────────────────────
    ///  BFS 只適用「每一步成本都相同」的情況。但地形有不同成本：
    ///      一般地板 = 1、黏液 = 2 (移動力砍半)、障礙 = 不可走。
    ///  當「進入不同格子的成本不一樣」時，BFS 找到的「格數最少」不等於「成本最少」，
    ///  必須改用 Dijkstra —— 它每次都從優先佇列取出「目前累積成本最小」的格子來展開，
    ///  因此第一次確定某格的成本時，那個成本一定是最小的 (這就是 Dijkstra 的正確性保證)。
    ///
    ///  演算法流程 (計算移動範圍)：
    ///    1. 起點成本 = 0，放進最小堆積。
    ///    2. 取出成本最小的格子 cur。
    ///    3. 看它的四個鄰居 next：
    ///         newCost = cost[cur] + (進入 next 的成本)
    ///         若 next 可通過、newCost &lt;= 移動力預算 (MOV)，且比之前記錄的更低，
    ///         就更新 cost[next]、記錄 cameFrom[next] = cur、放回堆積。
    ///    4. 重複 2~3 直到堆積清空。最後 cost 字典裡的所有格子就是「走得到的範圍」。
    ///
    ///  複雜度：O(E log V)。四面向下每格最多 4 條邊，一次計算數百格也沒問題。
    /// ─────────────────────────────────────────────────────────────
    /// </summary>
    public static class Pathfinder
    {
        /// <summary>
        /// 計算從 origin 出發、在 movePoints (MOV) 移動力內能走到的所有格子。
        /// </summary>
        /// <param name="grid">棋盤 (決定範圍與每格進入成本)</param>
        /// <param name="origin">起點 (通常是單位目前所在格)</param>
        /// <param name="movePoints">移動力預算 (單位的 MOV)</param>
        public static MovementRange ComputeMovementRange(IPathGrid grid, GridCoord origin, int movePoints)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            var range = new MovementRange(origin);
            range.costSoFar[origin] = 0;

            var frontier = new MinHeap<GridCoord>();
            frontier.Push(origin, 0);

            while (frontier.Count > 0)
            {
                var current = frontier.Pop();
                int currentCost = range.costSoFar[current];

                foreach (var dir in GridCoord.Directions)
                {
                    var next = current + dir;
                    if (!grid.InBounds(next)) continue;

                    int enter = grid.GetEnterCost(next);
                    if (enter < 0) continue;                 // 不可通過

                    int newCost = currentCost + Math.Max(1, enter);
                    if (newCost > movePoints) continue;      // 超出移動力預算

                    // 只有在「第一次到達」或「找到更便宜的路」時才更新。
                    if (!range.costSoFar.TryGetValue(next, out int existing) || newCost < existing)
                    {
                        range.costSoFar[next] = newCost;
                        range.cameFrom[next] = current;
                        frontier.Push(next, newCost);
                    }
                }
            }

            return range;
        }

        /// <summary>
        /// 從移動範圍中篩出「可以實際停下來」的格子 (例如排除有友軍站著、只能穿越的格子)。
        /// canStop 為 null 時全部可停。UI 高亮可走範圍時用這個。
        /// </summary>
        public static List<GridCoord> GetStoppableCells(MovementRange range, Func<GridCoord, bool> canStop)
        {
            var result = new List<GridCoord>();
            if (range == null) return result;
            foreach (var coord in range.ReachableCoords)
                if (canStop == null || canStop(coord))
                    result.Add(coord);
            return result;
        }

        /// <summary>
        /// 計算從 origin 到 target 的最短路徑 (含起終點)，忽略移動力上限。
        /// 常用於 AI「朝目標前進」時先算整條路，再依 MOV 截斷。若無路可通回傳 null。
        /// </summary>
        /// <param name="allowBlockedTarget">
        /// true = 目標格本身不可通過也能納入 (例如目標格站著敵人，用來算「走到敵人旁」的路)。
        /// </param>
        public static List<GridCoord> FindPath(IPathGrid grid, GridCoord origin, GridCoord target, bool allowBlockedTarget = false)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (origin == target) return new List<GridCoord> { origin };
            if (!grid.InBounds(target)) return null;

            var costSoFar = new Dictionary<GridCoord, int> { [origin] = 0 };
            var cameFrom = new Dictionary<GridCoord, GridCoord>();
            var frontier = new MinHeap<GridCoord>();
            frontier.Push(origin, 0);

            bool found = false;
            while (frontier.Count > 0)
            {
                var current = frontier.Pop();
                if (current == target) { found = true; break; }

                foreach (var dir in GridCoord.Directions)
                {
                    var next = current + dir;
                    if (!grid.InBounds(next)) continue;

                    int enter = grid.GetEnterCost(next);
                    if (enter < 0)
                    {
                        if (!(allowBlockedTarget && next == target)) continue;
                        enter = 1;
                    }

                    int newCost = costSoFar[current] + Math.Max(1, enter);
                    if (!costSoFar.TryGetValue(next, out int existing) || newCost < existing)
                    {
                        costSoFar[next] = newCost;
                        cameFrom[next] = current;
                        frontier.Push(next, newCost);
                    }
                }
            }

            if (!found) return null;

            var path = new List<GridCoord> { target };
            var c = target;
            while (c != origin)
            {
                c = cameFrom[c];
                path.Add(c);
            }
            path.Reverse();
            return path;
        }
    }
}
