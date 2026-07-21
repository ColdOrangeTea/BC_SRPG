using BlackChess.SRPG.Core;
using BlackChess.SRPG.Units;
using UnityEngine;

namespace BlackChess.SRPG.Items
{
    /// <summary>
    /// 掉在戰場上的道具。需求書規則：
    ///   - 不佔格：單位可以站在同一格、也可以直接穿過。
    ///   - 撿取範圍：站在道具所在格，或在道具「周圍四格 (上下左右)」時，都能選擇撿起。
    ///     → 也就是曼哈頓距離 &lt;= pickupRange (預設 1)。
    /// </summary>
    public class FieldItem : MonoBehaviour
    {
        public ItemData data;

        [Tooltip("撿取範圍 (曼哈頓距離)。1 = 站在格上或上下左右四格皆可撿。")]
        [Min(0)] public int pickupRange = 1;

        public GridCoord Coord { get; private set; }
        private BattleGrid _grid;

        /// <summary>把道具登記到棋盤上的某一格 (道具登記在 tile.item，但不佔格)。</summary>
        public void PlaceOnGrid(BattleGrid grid, GridCoord coord)
        {
            _grid = grid;
            Coord = coord;
            var tile = grid.GetTile(coord);
            if (tile != null) tile.item = this;
            transform.position = grid.CoordToWorld(coord);
        }

        /// <summary>某單位是否在可撿取範圍內。</summary>
        public bool CanBePickedUpBy(Unit unit)
        {
            if (unit == null) return false;
            return unit.Coord.ManhattanDistanceTo(Coord) <= pickupRange;
        }

        /// <summary>由 unit 撿起：加進背包並從棋盤移除。</summary>
        public bool PickUp(Unit unit)
        {
            if (!CanBePickedUpBy(unit)) return false;

            unit.Inventory.Add(data);
            if (_grid != null)
            {
                var tile = _grid.GetTile(Coord);
                if (tile != null && tile.item == this) tile.item = null;
            }
            gameObject.SetActive(false);
            return true;
        }
    }
}
