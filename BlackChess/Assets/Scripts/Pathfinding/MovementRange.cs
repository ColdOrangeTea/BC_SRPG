using System.Collections.Generic;
using BlackChess.SRPG.Core;

namespace BlackChess.SRPG.Pathfinding
{
    /// <summary>
    /// Dijkstra 計算後的成果：從起點出發、在移動力預算內能到達的所有格子。
    ///   - costSoFar：每格的「最少累積移動成本」。
    ///   - cameFrom ：每格是「從哪一格走過來的」，用來反推完整路徑。
    /// </summary>
    public class MovementRange
    {
        public readonly GridCoord origin;

        /// <summary>可到達格子 → 抵達該格所需的最少移動成本。</summary>
        public readonly Dictionary<GridCoord, int> costSoFar = new Dictionary<GridCoord, int>();

        /// <summary>可到達格子 → 前一格 (路徑反推用)。</summary>
        public readonly Dictionary<GridCoord, GridCoord> cameFrom = new Dictionary<GridCoord, GridCoord>();

        public MovementRange(GridCoord origin)
        {
            this.origin = origin;
        }

        /// <summary>這格是否在移動範圍內 (可停留的目的地清單由 Pathfinder 過濾後放進來)。</summary>
        public bool CanReach(GridCoord c) => costSoFar.ContainsKey(c);

        public int GetCost(GridCoord c) => costSoFar.TryGetValue(c, out var cost) ? cost : int.MaxValue;

        /// <summary>
        /// 反推從起點到 target 的完整路徑 (含起點與終點)。
        /// 從終點沿著 cameFrom 一路回溯到起點，再反轉即可。若 target 不可達則回傳 null。
        /// </summary>
        public List<GridCoord> GetPathTo(GridCoord target)
        {
            if (!CanReach(target)) return null;

            var path = new List<GridCoord>();
            var current = target;
            path.Add(current);
            while (current != origin)
            {
                current = cameFrom[current];
                path.Add(current);
            }
            path.Reverse();
            return path;
        }

        /// <summary>回傳所有可到達的格子。</summary>
        public IEnumerable<GridCoord> ReachableCoords => costSoFar.Keys;
    }
}
