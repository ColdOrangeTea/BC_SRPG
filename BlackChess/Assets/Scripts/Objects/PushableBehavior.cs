using System.Collections;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Units;

namespace BlackChess.SRPG.Objects
{
    /// <summary>
    /// 「可被推動」功能 (例如需求書中「透過特殊指令移動的箱子」)。
    /// 推動規則：往指定方向推一格，目的地必須在棋盤內、地形可走、且沒有其他單位/物件。
    /// </summary>
    public class PushableBehavior : ObjectBehavior
    {
        /// <summary>
        /// 檢查能否往 direction 推動一格 (不實際移動)。可推則回傳 true 並輸出目的地格。
        /// direction 應為 GridCoord.Directions 之一 (四面向)。
        /// </summary>
        public bool CanPush(GridCoord direction, out GridCoord target)
        {
            target = default;
            var grid = Obj.Grid;
            if (grid == null) return false;

            target = Obj.Coord + direction;
            var tile = grid.GetTile(target);
            if (tile == null) return false;                 // 出界
            if (!tile.IsTerrainWalkable) return false;      // 撞牆
            if (tile.occupant != null) return false;        // 有人擋著
            if (tile.interactable != null) return false;    // 有其他物件擋著
            return true;
        }

        /// <summary>
        /// 立即推動一格 (瞬移，無表演)。回傳是否成功。
        /// </summary>
        public bool TryPush(Unit pusher, GridCoord direction)
        {
            if (!CanPush(direction, out var target)) return false;
            Obj.MoveTo(target);
            return true;
        }

        /// <summary>
        /// 推動一格並播放「一格一格移動」的表演 (協程，供玩家確認指令後 StartCoroutine 等待完成)。
        /// 若無法推動則立即結束、不做任何事。
        /// </summary>
        public IEnumerator PushAnimated(Unit pusher, GridCoord direction)
        {
            if (!CanPush(direction, out var target)) yield break;
            yield return Obj.MoveToAnimated(target);
        }
    }
}
