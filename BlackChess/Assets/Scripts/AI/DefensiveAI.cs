using BlackChess.SRPG.Battle;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Units;
using UnityEngine;

namespace BlackChess.SRPG.AI
{
    /// <summary>
    /// 防禦型 AI：待在原地附近保持防禦，只有當敵人靠近到 engageRadius 內才出手。
    /// 打得到就打，打不到但敵人已進入警戒範圍才小幅移動迎擊，其餘時間待機 (防禦減傷)。
    /// 適合「守在原地、被動反擊」的單位。
    /// </summary>
    public class DefensiveAI : UnitAI
    {
        [Tooltip("警戒半徑：敵人進入此曼哈頓距離內才會主動接戰。")]
        public int engageRadius = 3;

        public override UnitPlan DecideAction(BattleContext context)
        {
            var self = Self;
            var target = context.NearestEnemy(self);

            // 沒有敵人，或最近敵人還在警戒圈外 → 原地待機防禦。
            if (target == null || self.Coord.ManhattanDistanceTo(target.Coord) > engageRadius)
                return UnitPlan.DoNothing;

            var plan = new UnitPlan();

            if (AITactics.CanAttackFrom(context.Grid, self.Coord, self, target))
            {
                plan.attack = true;
                plan.attackTarget = target;
                return plan;
            }

            // 敵人進圈但還打不到 → 小幅接近後嘗試攻擊。
            GridCoord step = AITactics.BestStepToward(context.Grid, self, target.Coord);
            if (step != self.Coord)
            {
                plan.move = true;
                plan.moveTarget = step;
            }

            if (AITactics.CanAttackFrom(context.Grid, step, self, target))
            {
                plan.attack = true;
                plan.attackTarget = target;
            }
            else if (!plan.move)
            {
                plan.wait = true;
            }

            return plan;
        }
    }
}
