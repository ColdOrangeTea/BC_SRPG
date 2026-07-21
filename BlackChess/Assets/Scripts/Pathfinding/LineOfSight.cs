using System.Collections.Generic;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Units;
using UnityEngine;

namespace BlackChess.SRPG.Pathfinding
{
    /// <summary>
    /// 攻擊「視線 (Line of Sight)」判定。與「移動範圍」不同：移動會被敵方單位與擋路物件卡住，
    /// 但攻擊只在意「攻擊者與目標之間的直線上有沒有東西擋住」。
    ///
    /// 阻擋規則 (攻擊者與目標「兩端點本身」不算)：
    ///   - 擋視線的地形 (TileType.blocksLineOfSight，如高牆)          → 擋
    ///   - 擋視線的物件 (InteractableObject.blocksLineOfSight)        → 擋
    ///   - 敵方單位                                                  → 擋
    ///   - 同勢力(友軍)單位、以及攻擊者自己                          → 不擋 (可越過友軍射擊)
    /// </summary>
    public static class LineOfSight
    {
        /// <summary>attacker 從 from 這一格，到 to 這一格之間是否「視線暢通」(可攻擊)。</summary>
        public static bool HasLineOfSight(BattleGrid grid, GridCoord from, GridCoord to, Unit attacker)
        {
            if (grid == null) return true; // 沒有棋盤資訊時不做阻擋 (退化為純距離)

            foreach (var cell in Line(from, to))
            {
                if (cell == from || cell == to) continue; // 端點 (攻擊者/目標所在格) 不算阻擋
                if (BlocksSight(grid.GetTile(cell), attacker)) return false;
            }
            return true;
        }

        /// <summary>這一格是否會擋住 attacker 的攻擊視線。</summary>
        private static bool BlocksSight(Tile tile, Unit attacker)
        {
            if (tile == null) return true;                                        // 出界視同被擋
            if (tile.type != null && tile.type.blocksLineOfSight) return true;    // 高牆等地形
            if (tile.interactable != null && tile.interactable.blocksLineOfSight) return true; // 擋視線物件

            var occ = tile.occupant;
            // 敵方單位擋線；攻擊者自己與同勢力友軍透明 (可越過射擊)。
            if (occ != null && occ != attacker && occ.IsHostileTo(attacker)) return true;
            return false;
        }

        /// <summary>
        /// 「超覆蓋 (supercover)」直線走訪：列出從 a 到 b 的線段所經過的每一格 (含兩端點)。
        /// 用整數運算逐步逼近理想直線，正好穿過格子角落時斜向同時前進一步。
        /// </summary>
        public static IEnumerable<GridCoord> Line(GridCoord a, GridCoord b)
        {
            int dx = b.x - a.x;
            int dy = b.y - a.y;
            int nx = Mathf.Abs(dx);
            int ny = Mathf.Abs(dy);
            int signX = dx > 0 ? 1 : -1;
            int signY = dy > 0 ? 1 : -1;

            int px = a.x;
            int py = a.y;
            yield return new GridCoord(px, py);

            int ix = 0, iy = 0;
            while (ix < nx || iy < ny)
            {
                // 比較「下一步走橫向」與「下一步走縱向」誰比較貼近理想直線。
                int decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx;
                if (decision == 0)      { px += signX; py += signY; ix++; iy++; } // 正好穿過角落 → 斜走
                else if (decision < 0)  { px += signX; ix++; }                    // 往橫向前進
                else                    { py += signY; iy++; }                    // 往縱向前進
                yield return new GridCoord(px, py);
            }
        }
    }
}
