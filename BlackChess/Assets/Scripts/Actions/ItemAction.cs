using BlackChess.SRPG.Core;
using BlackChess.SRPG.Items;
using BlackChess.SRPG.Units;

namespace BlackChess.SRPG.Actions
{
    /// <summary>
    /// 「道具」相關行動：撿取場地道具、以及使用背包中的道具。
    /// </summary>
    public static class ItemAction
    {
        /// <summary>嘗試撿起單位可及範圍內、某一格上的道具。回傳是否成功。</summary>
        public static bool TryPickUp(BattleGrid grid, Unit unit, GridCoord itemCoord)
        {
            var tile = grid.GetTile(itemCoord);
            if (tile == null || tile.item == null) return false;
            return tile.item.PickUp(unit);
        }

        /// <summary>撿起單位「腳下 / 可及範圍內」最近的一個道具 (移動後常見的便利呼叫)。</summary>
        public static bool TryPickUpNearby(BattleGrid grid, Unit unit)
        {
            // 先看腳下，再看四面向鄰格 (即需求書的「站在格上或周圍四格」)。
            var onTile = grid.GetTile(unit.Coord);
            if (onTile?.item != null && onTile.item.PickUp(unit)) return true;

            foreach (var dir in GridCoord.Directions)
            {
                var tile = grid.GetTile(unit.Coord + dir);
                if (tile?.item != null && tile.item.CanBePickedUpBy(unit) && tile.item.PickUp(unit))
                    return true;
            }
            return false;
        }

        /// <summary>使用背包中的道具，套用效果到 target (通常是自己或友軍)。</summary>
        public static bool Use(Unit user, ItemData item, Unit target)
        {
            if (user == null || item == null || target == null) return false;
            if (user.HasActed) return false;
            if (!user.Inventory.Has(item)) return false;

            ApplyEffect(item, target);

            if (item.consumable) user.Inventory.Remove(item);
            user.MarkActed();
            return true;
        }

        private static void ApplyEffect(ItemData item, Unit target)
        {
            switch (item.effectType)
            {
                case ItemEffectType.Heal:
                    target.Heal(item.amount);
                    break;
                case ItemEffectType.RestoreMP:
                    target.Stats.currentMP = System.Math.Min(target.Stats.maxMP, target.Stats.currentMP + item.amount);
                    break;
                case ItemEffectType.Buff:
                    // 預留：可在此加上暫時性增益的套用邏輯。
                    break;
            }
        }
    }
}
