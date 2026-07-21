using System.Collections;
using System.Collections.Generic;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Pathfinding;
using BlackChess.SRPG.Units;

namespace BlackChess.SRPG.Actions
{
    /// <summary>
    /// 「移動」行動。以 Dijkstra 算出的移動範圍為依據，把單位移動到目標格。
    /// 一回合限用一次 (由 Unit.HasMoved 控制)。
    /// </summary>
    public static class MoveAction
    {
        /// <summary>目標格是否為合法的可停留移動終點。</summary>
        public static bool CanMoveTo(BattleGrid grid, Unit unit, GridCoord target, out MovementRange range, out List<GridCoord> path)
        {
            range = Pathfinder.ComputeMovementRange(grid, unit.Coord, unit.Stats.mov, unit);
            path = null;

            if (unit.HasMoved) return false;
            if (!range.CanReach(target)) return false;

            var tile = grid.GetTile(target);
            if (tile == null || !tile.IsStoppableFor(unit)) return false;

            path = range.GetPathTo(target);
            return path != null;
        }

        /// <summary>
        /// 執行移動 (協程)。沿路徑平滑移動，結束後標記 HasMoved。
        /// 移動途中會順帶「經過」道具 —— 是否自動撿取由呼叫端決定 (見 ItemAction)。
        /// </summary>
        public static IEnumerator Execute(BattleGrid grid, Unit unit, List<GridCoord> path)
        {
            yield return unit.MoveAlongPath(grid, path);
            unit.MarkMoved();
        }
    }
}
