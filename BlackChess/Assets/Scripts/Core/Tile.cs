using BlackChess.SRPG.Items;
using BlackChess.SRPG.Objects;
using BlackChess.SRPG.Units;

namespace BlackChess.SRPG.Core
{
    /// <summary>
    /// 棋盤上「一格」的執行時期資料。
    /// 它同時記錄了：這格是什麼地形、上面站著誰(Unit)、放了什麼機關物件(Object)、有沒有掉落的道具(Item)。
    ///
    /// 佔格規則：
    ///   - Unit         → 佔格，一格最多一個單位。
    ///   - Object 物件  → 視 blocksMovement 而定，例如箱子擋路、開關不擋路。
    ///   - Item 道具    → 不佔格，玩家可以站上去或穿越 (需求書明確要求)。
    /// </summary>
    public class Tile
    {
        public readonly GridCoord coord;
        public TileType type;

        /// <summary>目前站在這格上的單位 (沒有則為 null)。</summary>
        public Unit occupant;

        /// <summary>放在這格上的互動物件 (箱子/礦車/開關等)。</summary>
        public InteractableObject interactable;

        /// <summary>掉在這格上的場地道具。道具不佔格，可與單位共存。</summary>
        public FieldItem item;

        public Tile(GridCoord coord, TileType type)
        {
            this.coord = coord;
            this.type = type;
        }

        /// <summary>進入這格要花多少移動力 (由地形決定)。</summary>
        public int EnterCost => type != null ? type.moveCost : 1;

        /// <summary>地形本身是否可通行 (不看單位/物件)。</summary>
        public bool IsTerrainWalkable => type != null && type.isWalkable;

        /// <summary>
        /// 對「正在移動的 mover」而言，這格能不能「經過」。
        /// 允許經過的條件：地形可走、沒有擋路的物件、且沒有敵對單位擋著
        /// (自己或友軍可以穿過，敵人擋路則不可穿過 — 這是常見戰棋規則，可自行調整)。
        /// </summary>
        public bool IsPassableFor(Unit mover)
        {
            if (!IsTerrainWalkable) return false;
            if (interactable != null && interactable.BlocksMovement) return false;
            if (occupant != null && occupant != mover)
            {
                // 敵對單位擋路 → 不可穿過；友方單位 → 可穿過但不可停留。
                if (mover == null || occupant.IsHostileTo(mover)) return false;
            }
            return true;
        }

        /// <summary>對 mover 而言，這格能不能「停下來」。停留比經過更嚴格：不能有任何其他單位。</summary>
        public bool IsStoppableFor(Unit mover)
        {
            if (!IsPassableFor(mover)) return false;
            if (occupant != null && occupant != mover) return false;
            return true;
        }
    }
}
