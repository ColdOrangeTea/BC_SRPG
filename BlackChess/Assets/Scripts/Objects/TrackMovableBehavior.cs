using System.Collections;
using System.Collections.Generic;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Units;
using UnityEngine;

namespace BlackChess.SRPG.Objects
{
    /// <summary>
    /// 「軌道移動」功能 (需求書：在指定地形軌道上可移動、可搭載人的礦車)。
    ///   - 只能停在 trackTiles 清單裡的格子 (軌道)。
    ///   - 可搭載一名乘客單位，礦車移動時把乘客一起帶著走。
    ///
    /// 軌道格可在 Inspector 手動指定，或呼叫 SetTrack 以程式化設定 (例如用某個地形類型自動掃出軌道)。
    /// </summary>
    public class TrackMovableBehavior : ObjectBehavior
    {
        [Tooltip("這台礦車能行駛的軌道格 (座標)。")]
        public List<GridCoord> trackTiles = new List<GridCoord>();

        [Tooltip("目前搭載的乘客 (可為空)。")]
        public Unit passenger;

        public void SetTrack(IEnumerable<GridCoord> tiles)
        {
            trackTiles = new List<GridCoord>(tiles);
        }

        public bool IsOnTrack(GridCoord c) => trackTiles.Contains(c);

        /// <summary>讓單位登上礦車 (需與礦車同格或相鄰，由呼叫端判定)。</summary>
        public void Board(Unit unit) => passenger = unit;

        public void Unboard() => passenger = null;

        /// <summary>
        /// 檢查能否往 direction 推進一格 (不實際移動)。可行則回傳 true 並輸出目的地格。
        /// 目的地必須是軌道、在棋盤內、地形可走、且沒被非乘客的單位/其他物件擋住。
        /// </summary>
        public bool CanAdvance(GridCoord direction, out GridCoord target)
        {
            target = default;
            var grid = Obj.Grid;
            if (grid == null) return false;

            target = Obj.Coord + direction;
            if (!IsOnTrack(target)) return false;           // 目的地不是軌道
            var tile = grid.GetTile(target);
            if (tile == null || !tile.IsTerrainWalkable) return false;
            if (tile.interactable != null && tile.interactable != Obj) return false;
            // 目的地若有非乘客的其他單位擋著，不能前進。
            if (tile.occupant != null && tile.occupant != passenger) return false;
            return true;
        }

        /// <summary>
        /// 立即推進一格 (瞬移，無表演)。載有乘客時乘客一起移到同格。回傳是否成功。
        /// </summary>
        public bool TryAdvance(GridCoord direction)
        {
            if (!CanAdvance(direction, out var target)) return false;

            Obj.MoveTo(target);
            if (passenger != null)
                Obj.Grid.PlaceUnit(passenger, target); // 乘客跟著移動 (與車同格)
            return true;
        }

        /// <summary>
        /// 推進一格並播放「一格一格移動」的表演 (協程)。礦車與車上乘客會同步平滑移動。
        /// 供玩家確認指令後 StartCoroutine 等待動畫完成；無法前進則立即結束。
        /// </summary>
        public IEnumerator AdvanceAnimated(GridCoord direction)
        {
            if (!CanAdvance(direction, out var target)) yield break;

            // 乘客的邏輯佔格先更新到目的地 (視覺位置隨後由動畫接管，不會出現瞬移)。
            Transform passengerTr = passenger != null ? passenger.transform : null;
            if (passenger != null) Obj.Grid.PlaceUnit(passenger, target);

            yield return Obj.MoveToAnimated(target, passengerTr);
        }
    }
}
