using System.Collections.Generic;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Items;
using BlackChess.SRPG.Objects;
using BlackChess.SRPG.Pathfinding;
using BlackChess.SRPG.Units;

namespace BlackChess.SRPG.Actions
{
    /// <summary>
    /// 「攻擊」行動。RNG 決定攻擊距離：目標與攻擊者的曼哈頓距離 &lt;= RNG。
    /// 除了距離外，還要「視線暢通」才打得到 —— 這點與移動範圍不同：
    ///   • 移動：會被敵方單位與擋路物件卡住。
    ///   • 攻擊：只被『敵方單位 / 擋視線的地形與物件』擋住；同勢力(友軍)單位不會擋攻擊。
    /// 可攻擊敵對單位，也可攻擊「可破壞物件」(例如打破瓶子取得補血道具)。
    /// </summary>
    public static class AttackAction
    {
        /// <summary>attacker 與 targetCoord 是否在射程 (曼哈頓距離) 內。只看距離，不看視線。</summary>
        public static bool InRange(Unit attacker, GridCoord targetCoord)
        {
            return attacker.Coord.ManhattanDistanceTo(targetCoord) <= attacker.Stats.rng;
        }

        /// <summary>attacker 到 targetCoord 的攻擊視線是否暢通 (同勢力友軍不擋、敵方與障礙物會擋)。</summary>
        public static bool HasLineOfSight(BattleGrid grid, Unit attacker, GridCoord targetCoord)
        {
            return LineOfSight.HasLineOfSight(grid, attacker.Coord, targetCoord, attacker);
        }

        /// <summary>attacker 是否能攻擊到 targetCoord (射程內 + 視線暢通)。</summary>
        public static bool CanReach(BattleGrid grid, Unit attacker, GridCoord targetCoord)
        {
            return InRange(attacker, targetCoord) && HasLineOfSight(grid, attacker, targetCoord);
        }

        /// <summary>是否可攻擊此單位 (在射程內、視線暢通、且為敵對)。</summary>
        public static bool CanAttack(BattleGrid grid, Unit attacker, Unit target)
        {
            if (attacker == null || target == null || !target.IsAlive) return false;
            if (attacker.HasActed) return false;
            if (!attacker.IsHostileTo(target)) return false;
            return CanReach(grid, attacker, target.Coord);
        }

        /// <summary>對單位發動攻擊，回傳造成的傷害。</summary>
        public static int Execute(BattleGrid grid, Unit attacker, Unit target)
        {
            if (!CanAttack(grid, attacker, target)) return 0;

            int damage = attacker.Stats.atk;
            target.TakeDamage(damage);
            attacker.MarkActed();
            return damage;
        }

        /// <summary>
        /// 未來的「範圍攻擊 / 技能」判定某單位是否會被 attacker 的攻擊影響：
        /// 敵人一定會；同陣營夥伴取決於 attacker 的 attackAffectsAllies 設定 (無差別大範圍傷害等)。
        /// </summary>
        public static bool AttackAffects(Unit attacker, Unit target)
        {
            if (attacker == null) return false;
            return HitPolicy.HarmAffects(attacker.faction, target, attacker.Stats.attackAffectsAllies);
        }

        /// <summary>是否可攻擊此物件 (具「可破壞」功能、尚未破壞、在射程內且視線暢通)。</summary>
        public static bool CanAttackObject(BattleGrid grid, Unit attacker, InteractableObject obj)
        {
            if (attacker == null || obj == null || attacker.HasActed) return false;
            var breakable = obj.GetBehavior<BreakableBehavior>();
            if (breakable == null || breakable.IsBroken) return false;
            return CanReach(grid, attacker, obj.Coord);
        }

        /// <summary>攻擊可破壞物件 (例如瓶子)。回傳是否命中。</summary>
        public static bool ExecuteOnObject(BattleGrid grid, Unit attacker, InteractableObject obj)
            => ExecuteOnObject(grid, attacker, obj, out _);

        /// <summary>
        /// 攻擊可破壞物件。回傳是否命中；drop 為物件被打壞時生成的掉落道具 (沒破壞或不掉落則為 null)，
        /// 供呼叫端接著執行「自動撿起」流程。
        /// </summary>
        public static bool ExecuteOnObject(BattleGrid grid, Unit attacker, InteractableObject obj, out FieldItem drop)
        {
            drop = null;
            if (!CanAttackObject(grid, attacker, obj)) return false;

            obj.GetBehavior<BreakableBehavior>().TakeDamage(attacker.Stats.atk, out drop);
            attacker.MarkActed();
            return true;
        }

        /// <summary>列出 attacker 射程 + 視線內、所有可攻擊的「可破壞物件」(供玩家 UI 高亮)。</summary>
        public static List<InteractableObject> GetObjectTargetsInRange(BattleGrid grid, Unit attacker)
        {
            var result = new List<InteractableObject>();
            if (grid == null || attacker == null) return result;
            foreach (var kv in grid.Tiles)
            {
                var obj = kv.Value.interactable;
                if (obj != null && CanAttackObject(grid, attacker, obj))
                    result.Add(obj);
            }
            return result;
        }

        /// <summary>
        /// 列出 attacker 目前射程 + 視線內、所有可攻擊的敵方單位 (供玩家 UI 高亮或 AI 選目標)。
        /// </summary>
        public static List<Unit> GetTargetsInRange(BattleGrid grid, Unit attacker, IEnumerable<Unit> allUnits)
        {
            var result = new List<Unit>();
            foreach (var u in allUnits)
                if (CanAttack(grid, attacker, u))
                    result.Add(u);
            return result;
        }
    }
}
