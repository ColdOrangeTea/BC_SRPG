using System.Collections.Generic;
using System.Linq;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Units;

namespace BlackChess.SRPG.Battle
{
    /// <summary>
    /// 一場戰鬥的共享資訊。傳給 AI 與戰鬥目標，讓它們能查詢棋盤、所有勢力與單位，
    /// 而不需要各自持有一堆引用。相當於戰鬥的「唯讀世界快照 + 查詢工具」。
    /// </summary>
    public class BattleContext
    {
        public BattleGrid Grid { get; }
        public IReadOnlyList<Faction> Factions { get; }

        public BattleContext(BattleGrid grid, IReadOnlyList<Faction> factions)
        {
            Grid = grid;
            Factions = factions;
        }

        /// <summary>所有勢力中仍存活的單位。</summary>
        public IEnumerable<Unit> AllLivingUnits =>
            Factions.SelectMany(f => f.LivingUnits);

        /// <summary>對 self 而言的敵方存活單位。</summary>
        public IEnumerable<Unit> EnemiesOf(Unit self) =>
            AllLivingUnits.Where(u => u.IsHostileTo(self));

        /// <summary>對 self 而言的友方存活單位 (含自己)。</summary>
        public IEnumerable<Unit> AlliesOf(Unit self) =>
            AllLivingUnits.Where(u => u.IsFriendlyTo(self));

        /// <summary>回傳距離 self 最近的敵方單位 (AI 常用)。</summary>
        public Unit NearestEnemy(Unit self)
        {
            Unit best = null;
            int bestDist = int.MaxValue;
            foreach (var e in EnemiesOf(self))
            {
                int d = self.Coord.ManhattanDistanceTo(e.Coord);
                if (d < bestDist) { bestDist = d; best = e; }
            }
            return best;
        }
    }
}
