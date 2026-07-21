using System.Collections.Generic;
using System.Linq;
using BlackChess.SRPG.Battle;
using BlackChess.SRPG.Core;
using UnityEngine;

namespace BlackChess.SRPG.Objectives
{
    /// <summary>
    /// 「撤退 / 抵達指定地點」目標：只要指定陣營有任一 (或全部) 單位站到目標格上，即達成。
    /// 需求書範例：撤退到指定地點。
    /// </summary>
    public class ReachLocationObjective : BattleObjective
    {
        [Tooltip("要抵達目標的陣營 teamId (通常是玩家)。")]
        public int reachingTeamId = 0;

        [Tooltip("目標格清單 (撤退點)。站到其中任一格即計數。")]
        public List<GridCoord> targetCells = new List<GridCoord>();

        [Tooltip("是否需要「全部」存活單位都抵達 (true)，或「任一」單位抵達即可 (false)。")]
        public bool requireAllUnits = false;

        public override ObjectiveState Evaluate(BattleContext context)
        {
            var units = context.Factions
                .Where(f => f.teamId == reachingTeamId)
                .SelectMany(f => f.LivingUnits)
                .ToList();

            if (units.Count == 0) return ObjectiveState.Failed; // 沒人可撤退了

            bool AtTarget(Units.Unit u) => targetCells.Contains(u.Coord);

            bool done = requireAllUnits ? units.All(AtTarget) : units.Any(AtTarget);
            return done ? ObjectiveState.Achieved : ObjectiveState.InProgress;
        }
    }
}
