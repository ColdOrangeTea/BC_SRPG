using BlackChess.SRPG.Battle;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Units;

namespace BlackChess.SRPG.AI
{
    /// <summary>
    /// 進攻型 AI：鎖定最近的敵人，朝它移動，若移動後進入射程就攻擊。
    /// 敵人或盟軍都可使用；盟軍設成進攻型就會主動幫玩家清怪。
    /// </summary>
    public class AggressiveAI : UnitAI
    {
        public override UnitPlan DecideAction(BattleContext context)
        {
            var self = Self;
            var target = context.NearestEnemy(self);
            if (target == null) return UnitPlan.DoNothing;

            var grid = context.Grid;
            var plan = new UnitPlan();

            // 若原地就打得到，直接攻擊，不必移動。
            if (AITactics.CanAttackFrom(self.Coord, self, target))
            {
                plan.attack = true;
                plan.attackTarget = target;
                return plan;
            }

            // 否則走到「最靠近目標」的落腳點。
            GridCoord step = AITactics.BestStepToward(grid, self, target.Coord);
            if (step != self.Coord)
            {
                plan.move = true;
                plan.moveTarget = step;
            }

            // 移動到新位置後，若能攻擊就攻擊。
            if (AITactics.CanAttackFrom(step, self, target))
            {
                plan.attack = true;
                plan.attackTarget = target;
            }
            else if (!plan.move)
            {
                plan.wait = true; // 走不動又打不到 → 待機防禦
            }

            return plan;
        }
    }
}
