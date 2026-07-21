using System.Linq;
using BlackChess.SRPG.Battle;
using BlackChess.SRPG.Units;
using UnityEngine;

namespace BlackChess.SRPG.Objectives
{
    /// <summary>
    /// 「殲滅」目標：把指定敵對陣營 (teamId) 全部消滅即勝利；
    /// 若我方 (protectTeamId) 全滅則失敗。
    /// </summary>
    public class AnnihilateObjective : BattleObjective
    {
        [Tooltip("要被殲滅的敵方陣營 teamId。")]
        public int enemyTeamId = 1;

        [Tooltip("我方陣營 teamId；此陣營全滅則戰鬥失敗。")]
        public int protectTeamId = 0;

        public override ObjectiveState Evaluate(BattleContext context)
        {
            bool anyEnemyAlive = context.Factions
                .Where(f => f.teamId == enemyTeamId)
                .Any(f => !f.IsDefeated);

            bool anyAllyAlive = context.Factions
                .Where(f => f.teamId == protectTeamId)
                .Any(f => !f.IsDefeated);

            if (!anyAllyAlive) return ObjectiveState.Failed;
            if (!anyEnemyAlive) return ObjectiveState.Achieved;
            return ObjectiveState.InProgress;
        }
    }
}
