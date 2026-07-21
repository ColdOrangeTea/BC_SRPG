using System.Collections.Generic;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Objects;
using BlackChess.SRPG.Units;

namespace BlackChess.SRPG.Actions
{
    /// <summary>
    /// 「攻擊」行動。RNG 決定攻擊範圍：目標與攻擊者的曼哈頓距離 &lt;= RNG 即可攻擊。
    /// 可攻擊敵對單位，也可攻擊「可破壞物件」(例如打破瓶子取得補血道具)。
    /// </summary>
    public static class AttackAction
    {
        /// <summary>attacker 是否能攻擊到 targetCoord (只看距離)。</summary>
        public static bool InRange(Unit attacker, GridCoord targetCoord)
        {
            return attacker.Coord.ManhattanDistanceTo(targetCoord) <= attacker.Stats.rng;
        }

        /// <summary>是否可攻擊此單位 (在射程內且為敵對)。</summary>
        public static bool CanAttack(Unit attacker, Unit target)
        {
            if (attacker == null || target == null || !target.IsAlive) return false;
            if (attacker.HasActed) return false;
            if (!attacker.IsHostileTo(target)) return false;
            return InRange(attacker, target.Coord);
        }

        /// <summary>對單位發動攻擊，回傳造成的傷害。</summary>
        public static int Execute(Unit attacker, Unit target)
        {
            if (!CanAttack(attacker, target)) return 0;

            int damage = attacker.Stats.atk;
            target.TakeDamage(damage);
            attacker.MarkActed();
            return damage;
        }

        /// <summary>攻擊可破壞物件 (例如瓶子)。回傳是否命中。</summary>
        public static bool ExecuteOnObject(Unit attacker, InteractableObject obj)
        {
            if (attacker == null || obj == null || attacker.HasActed) return false;
            if (!InRange(attacker, obj.Coord)) return false;

            var breakable = obj.GetBehavior<BreakableBehavior>();
            if (breakable == null) return false;

            breakable.TakeDamage(attacker.Stats.atk);
            attacker.MarkActed();
            return true;
        }

        /// <summary>
        /// 列出 attacker 目前射程內、所有可攻擊的敵方單位 (供玩家 UI 高亮或 AI 選目標)。
        /// </summary>
        public static List<Unit> GetTargetsInRange(Unit attacker, IEnumerable<Unit> allUnits)
        {
            var result = new List<Unit>();
            foreach (var u in allUnits)
                if (CanAttack(attacker, u))
                    result.Add(u);
            return result;
        }
    }
}
