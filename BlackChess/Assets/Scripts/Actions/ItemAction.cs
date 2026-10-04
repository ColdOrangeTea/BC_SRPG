using System.Collections;
using System.Collections.Generic;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Items;
using BlackChess.SRPG.Units;

namespace BlackChess.SRPG.Actions
{
    /// <summary>
    /// 「道具」相關行動：撿取場地道具、以及使用背包中的道具。
    ///
    /// 使用規則 (依 ItemData 設定)：
    ///   • 關鍵道具 (KeyItem)：帶在身上、不能使用。
    ///   • 補品 (Supply)：對我方治療/增益。以使用角色為中心 useRange 格內指定目標格，
    ///     再以該格為中心向外 effectRadius 格內生效 (SelfCentered / ThrowAtTile 規則相同，僅語意與演出不同)。
    ///   • 傷害道具 (Damage)：對敵人傷害/減益，範圍規則同上；
    ///     affectsAllies = true 時範圍會無差別波及同陣營夥伴 (統一由 HitPolicy 判定)。
    /// </summary>
    public static class ItemAction
    {
        // ---------------- 撿取 ----------------

        /// <summary>嘗試撿起單位可及範圍內、某一格上的道具 (瞬間、無演出)。回傳是否成功。</summary>
        public static bool TryPickUp(BattleGrid grid, Unit unit, GridCoord itemCoord)
        {
            var tile = grid.GetTile(itemCoord);
            if (tile == null || tile.item == null) return false;
            return tile.item.PickUp(unit);
        }

        /// <summary>撿起單位「腳下 / 可及範圍內」最近的一個道具 (瞬間、無演出)。</summary>
        public static bool TryPickUpNearby(BattleGrid grid, Unit unit)
        {
            var item = FindPickableNearby(grid, unit);
            return item != null && item.PickUp(unit);
        }

        /// <summary>找出單位腳下或周圍四格內、可撿取的第一個道具 (沒有則回傳 null)。</summary>
        public static FieldItem FindPickableNearby(BattleGrid grid, Unit unit)
        {
            if (grid == null || unit == null) return null;

            // 先看腳下，再看四面向鄰格 (即需求書的「站在格上或周圍四格」)。
            var onTile = grid.GetTile(unit.Coord);
            if (onTile?.item != null && onTile.item.CanBePickedUpBy(unit)) return onTile.item;

            foreach (var dir in GridCoord.Directions)
            {
                var tile = grid.GetTile(unit.Coord + dir);
                if (tile?.item != null && tile.item.CanBePickedUpBy(unit)) return tile.item;
            }
            return null;
        }

        /// <summary>
        /// 撿起單位可及範圍內的所有道具 (協程、含完整撿取演出：道具飛向角色淡出 → 提示文字)。
        /// 供移動結束後呼叫。
        /// </summary>
        public static IEnumerator PickUpNearbyAnimated(BattleGrid grid, Unit unit)
        {
            FieldItem item;
            while ((item = FindPickableNearby(grid, unit)) != null)
                yield return item.PickUpAnimated(unit);
        }

        // ---------------- 使用 (範圍版) ----------------

        /// <summary>此單位目前能否使用該道具 (不看目標格)。關鍵道具永遠 false。</summary>
        public static bool CanUse(Unit user, ItemData item)
        {
            if (user == null || item == null) return false;
            if (!item.IsUsable) return false;      // 關鍵道具不能用
            if (user.HasActed) return false;
            return user.Inventory.Has(item);
        }

        /// <summary>
        /// targetCoord 是否為該道具的合法使用目標格。
        /// 兩種 targeting 模式規則相同：以使用角色為中心、useRange 格內的格子皆可指定。
        /// </summary>
        public static bool IsValidTarget(BattleGrid grid, Unit user, ItemData item, GridCoord targetCoord)
        {
            if (!CanUse(user, item)) return false;
            if (grid == null || !grid.InBounds(targetCoord)) return false;
            return user.Coord.ManhattanDistanceTo(targetCoord) <= item.useRange;
        }

        /// <summary>列出該道具可指定的所有目標格 (供 UI 高亮「以使用角色為中心的使用範圍」)。</summary>
        public static List<GridCoord> GetUsableCoords(BattleGrid grid, Unit user, ItemData item)
        {
            var result = new List<GridCoord>();
            if (grid == null || user == null || item == null) return result;

            foreach (var kv in grid.Tiles)
                if (user.Coord.ManhattanDistanceTo(kv.Key) <= item.useRange)
                    result.Add(kv.Key);
            return result;
        }

        /// <summary>
        /// 列出以 center 為中心、effectRadius 內「實際會被此道具影響」的單位。
        /// 有害 (傷害/減益) / 有益 (治療/增益) 與「是否波及同陣營」統一由 HitPolicy 判定。
        /// </summary>
        public static List<Unit> GetAffectedUnits(BattleGrid grid, Unit user, ItemData item, GridCoord center)
        {
            var result = new List<Unit>();
            if (grid == null || user == null || item == null) return result;

            foreach (var kv in grid.Tiles)
            {
                var target = kv.Value.occupant;
                if (target == null) continue;
                if (kv.Key.ManhattanDistanceTo(center) > item.effectRadius) continue;

                bool affected = item.IsHarmful
                    ? HitPolicy.HarmAffects(user.faction, target, item.affectsAllies)
                    : HitPolicy.BenefitAffects(user, target, item.affectsAllies);
                if (affected) result.Add(target);
            }
            return result;
        }

        /// <summary>
        /// 對目標格使用道具：以指定的目標格為效果中心，
        /// 向外 effectRadius 格內的合法目標套用效果。成功時消耗道具並結束該單位主行動。
        /// </summary>
        public static bool UseAt(BattleGrid grid, Unit user, ItemData item, GridCoord targetCoord)
        {
            if (!IsValidTarget(grid, user, item, targetCoord)) return false;

            foreach (var target in GetAffectedUnits(grid, user, item, targetCoord))
                ApplyEffect(item, target);

            if (item.consumable) user.Inventory.Remove(item);
            user.MarkActed();
            return true;
        }

        // ---------------- 使用 (單體版，向後相容) ----------------

        /// <summary>使用背包中的道具，套用效果到單一 target。關鍵道具無法使用。</summary>
        public static bool Use(Unit user, ItemData item, Unit target)
        {
            if (!CanUse(user, item) || target == null) return false;

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
                case ItemEffectType.Damage:
                    target.TakeDamage(item.amount);
                    break;
                case ItemEffectType.Debuff:
                    // 預留：可在此加上暫時性減益的套用邏輯。
                    break;
            }
        }
    }
}
