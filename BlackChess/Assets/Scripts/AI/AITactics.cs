using BlackChess.SRPG.Actions;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Pathfinding;
using BlackChess.SRPG.Units;

namespace BlackChess.SRPG.AI
{
    /// <summary>
    /// AI 共用的戰術小工具。各種 AI 行為都會用到「找一格盡量靠近目標」「找射程內的攻擊目標」等基本盤算，
    /// 抽成靜態函式共用，避免每個行為腳本重寫一遍。
    /// </summary>
    public static class AITactics
    {
        /// <summary>
        /// 在 self 的移動範圍內，找一格「離 destination 最近、且能停留」的落腳點。
        /// 用 Dijkstra 算出可達範圍後，挑曼哈頓距離目標最小的可停留格。
        /// 若已經無法更靠近 (或本來就在原地最好)，回傳目前所在格。
        /// </summary>
        public static GridCoord BestStepToward(BattleGrid grid, Unit self, GridCoord destination)
        {
            var range = Pathfinder.ComputeMovementRange(grid, self.Coord, self.Stats.mov, self);

            GridCoord best = self.Coord;
            int bestDist = self.Coord.ManhattanDistanceTo(destination);

            foreach (var coord in range.ReachableCoords)
            {
                var tile = grid.GetTile(coord);
                if (tile == null || !tile.IsStoppableFor(self)) continue;

                int dist = coord.ManhattanDistanceTo(destination);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = coord;
                }
            }
            return best;
        }

        /// <summary>
        /// 從 fromCoord 這一格出發，射程內且視線暢通是否打得到 target。
        /// 視線判定與攻擊一致：敵方單位與擋視線的地形/物件會擋，同勢力友軍不擋。
        /// </summary>
        public static bool CanAttackFrom(BattleGrid grid, GridCoord fromCoord, Unit self, Unit target)
        {
            if (target == null || !target.IsAlive) return false;
            if (fromCoord.ManhattanDistanceTo(target.Coord) > self.Stats.rng) return false;
            return LineOfSight.HasLineOfSight(grid, fromCoord, target.Coord, self);
        }
    }
}
