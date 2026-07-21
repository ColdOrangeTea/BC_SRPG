using BlackChess.SRPG.Battle;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Units;
using UnityEngine;

namespace BlackChess.SRPG.AI
{
    /// <summary>
    /// 守衛型 AI：守護一個指定地點 (例如基地/城門)。
    ///   - 敵人進入 guardRadius → 在不脫離守備範圍太遠的前提下迎擊。
    ///   - 沒有威脅 → 回到守衛點待命。
    /// 需求書範例：盟軍「守護基地」。把 guardPoint 設成基地座標即可。
    /// </summary>
    public class GuardAI : UnitAI
    {
        [Tooltip("守衛的中心點 (格座標)。未設定時，會用單位開場所在格作為守衛點。")]
        public GridCoord guardPoint;
        public bool useStartPositionAsGuardPoint = true;

        [Tooltip("守備半徑：敵人進入此範圍才接戰；也限制自己不會追離守衛點太遠。")]
        public int guardRadius = 4;

        private bool _initialized;

        private void EnsureInit()
        {
            if (_initialized) return;
            if (useStartPositionAsGuardPoint) guardPoint = Self.Coord;
            _initialized = true;
        }

        public override UnitPlan DecideAction(BattleContext context)
        {
            EnsureInit();
            var self = Self;
            var grid = context.Grid;

            // 找守備範圍內最近的敵人。
            Unit threat = null;
            int bestDist = int.MaxValue;
            foreach (var e in context.EnemiesOf(self))
            {
                int dToGuard = e.Coord.ManhattanDistanceTo(guardPoint);
                if (dToGuard > guardRadius) continue; // 沒踏進守備圈，不理會
                int dToSelf = self.Coord.ManhattanDistanceTo(e.Coord);
                if (dToSelf < bestDist) { bestDist = dToSelf; threat = e; }
            }

            // 沒有威脅 → 回到守衛點。
            if (threat == null)
            {
                if (self.Coord == guardPoint) return UnitPlan.DoNothing;
                GridCoord back = AITactics.BestStepToward(grid, self, guardPoint);
                return back == self.Coord
                    ? UnitPlan.DoNothing
                    : new UnitPlan { move = true, moveTarget = back };
            }

            // 有威脅 → 迎擊。
            var plan = new UnitPlan();
            if (AITactics.CanAttackFrom(self.Coord, self, threat))
            {
                plan.attack = true;
                plan.attackTarget = threat;
                return plan;
            }

            GridCoord step = AITactics.BestStepToward(grid, self, threat.Coord);
            // 不追出守備範圍：若落腳點離守衛點太遠就放棄追擊，留在原地。
            if (step != self.Coord && step.ManhattanDistanceTo(guardPoint) <= guardRadius)
            {
                plan.move = true;
                plan.moveTarget = step;
            }

            GridCoord attackFrom = plan.move ? plan.moveTarget : self.Coord;
            if (AITactics.CanAttackFrom(attackFrom, self, threat))
            {
                plan.attack = true;
                plan.attackTarget = threat;
            }
            else if (!plan.move)
            {
                plan.wait = true;
            }

            return plan;
        }
    }
}
