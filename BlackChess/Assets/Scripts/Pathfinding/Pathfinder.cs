using System.Collections.Generic;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Units;

namespace BlackChess.SRPG.Pathfinding
{
    /// <summary>
    /// 尋路核心。使用 Dijkstra 演算法計算「移動範圍」與「最短路徑」。
    ///
    /// ─────────────────────────────────────────────────────────────
    ///  為什麼用 Dijkstra 而不是 BFS？
    /// ─────────────────────────────────────────────────────────────
    ///  BFS 只適用「每一步成本都相同」的情況。但本系統的地形有不同成本：
    ///      一般地板 = 1、黏液 = 2 (移動力砍半)、障礙 = 不可走。
    ///  當「進入不同格子的成本不一樣」時，BFS 找到的「格數最少」不等於「成本最少」，
    ///  必須改用 Dijkstra —— 它每次都從優先佇列取出「目前累積成本最小」的格子來展開，
    ///  因此第一次確定某格的成本時，那個成本一定是最小的 (這就是 Dijkstra 的正確性保證)。
    ///
    ///  演算法流程 (計算移動範圍)：
    ///    1. 起點成本 = 0，放進最小堆積。
    ///    2. 取出成本最小的格子 cur。
    ///    3. 看它的四個鄰居 next：
    ///         newCost = cost[cur] + (進入 next 的地形成本)
    ///         若 newCost <= 單位的移動力預算 (MOV)，且比之前記錄的更低，
    ///         就更新 cost[next]、記錄 cameFrom[next] = cur、放回堆積。
    ///    4. 重複 2~3 直到堆積清空。最後 cost 字典裡的所有格子就是「走得到的範圍」。
    ///
    ///  複雜度：O(E log V)。四面向下每格最多 4 條邊，非常快，一次計算數百格也沒問題。
    /// ─────────────────────────────────────────────────────────────
    /// </summary>
    public static class Pathfinder
    {
        /// <summary>
        /// 計算 mover 從 origin 出發、在 movePoints (MOV) 移動力內能走到的所有格子。
        /// 障礙物、擋路物件、敵方單位都會被自動排除；黏液等高成本地形會被正確計入。
        /// </summary>
        /// <param name="grid">棋盤</param>
        /// <param name="origin">起點 (通常是單位目前所在格)</param>
        /// <param name="movePoints">移動力預算 (單位的 MOV)</param>
        /// <param name="mover">正在移動的單位 (用來判定敵/我方擋路)。可為 null 表示純地形尋路。</param>
        public static MovementRange ComputeMovementRange(BattleGrid grid, GridCoord origin, int movePoints, Unit mover)
        {
            var range = new MovementRange(origin);
            range.costSoFar[origin] = 0;

            var frontier = new MinHeap<GridCoord>();
            frontier.Push(origin, 0);

            while (frontier.Count > 0)
            {
                var current = frontier.Pop();
                int currentCost = range.costSoFar[current];

                foreach (var neighborTile in grid.GetNeighbors(current))
                {
                    // 不能「經過」的格子直接跳過 (地形不可走 / 物件擋路 / 敵人擋路)。
                    if (!neighborTile.IsPassableFor(mover)) continue;

                    int newCost = currentCost + neighborTile.EnterCost;

                    // 超出移動力預算 → 走不到，跳過。
                    if (newCost > movePoints) continue;

                    // 只有在「第一次到達」或「找到更便宜的路」時才更新。
                    if (!range.costSoFar.TryGetValue(neighborTile.coord, out int existing) || newCost < existing)
                    {
                        range.costSoFar[neighborTile.coord] = newCost;
                        range.cameFrom[neighborTile.coord] = current;
                        frontier.Push(neighborTile.coord, newCost);
                    }
                }
            }

            return range;
        }

        /// <summary>
        /// 從移動範圍中，篩出「可以實際停下來」的格子清單 (排除有友軍站著、只能穿越的格子)。
        /// UI 高亮可走範圍時用這個。
        /// </summary>
        public static List<GridCoord> GetStoppableCells(BattleGrid grid, MovementRange range, Unit mover)
        {
            var result = new List<GridCoord>();
            foreach (var coord in range.ReachableCoords)
            {
                var tile = grid.GetTile(coord);
                if (tile != null && tile.IsStoppableFor(mover))
                    result.Add(coord);
            }
            return result;
        }

        /// <summary>
        /// 計算從 origin 到 target 的最短路徑 (含起終點)。此版本忽略移動力上限，
        /// 常用於 AI「朝目標前進」時先算整條路，再依 MOV 截斷。若無路可通回傳 null。
        /// </summary>
        public static List<GridCoord> FindPath(BattleGrid grid, GridCoord origin, GridCoord target, Unit mover)
        {
            if (origin == target) return new List<GridCoord> { origin };

            var costSoFar = new Dictionary<GridCoord, int> { [origin] = 0 };
            var cameFrom = new Dictionary<GridCoord, GridCoord>();
            var frontier = new MinHeap<GridCoord>();
            frontier.Push(origin, 0);

            bool found = false;
            while (frontier.Count > 0)
            {
                var current = frontier.Pop();
                if (current == target) { found = true; break; }

                foreach (var neighborTile in grid.GetNeighbors(current))
                {
                    // 允許「目標格」即使有單位/物件也能被納入，讓 AI 能算出「走到目標旁」的路。
                    bool isTarget = neighborTile.coord == target;
                    if (!isTarget && !neighborTile.IsPassableFor(mover)) continue;

                    int newCost = costSoFar[current] + neighborTile.EnterCost;
                    if (!costSoFar.TryGetValue(neighborTile.coord, out int existing) || newCost < existing)
                    {
                        costSoFar[neighborTile.coord] = newCost;
                        cameFrom[neighborTile.coord] = current;
                        frontier.Push(neighborTile.coord, newCost);
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
