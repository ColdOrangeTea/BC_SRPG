using System.Collections.Generic;
using System.Linq;
using BlackChess.SRPG.Units;

namespace BlackChess.SRPG.Battle
{
    /// <summary>
    /// 回合順序系統。需求書規則：
    ///   每個勢力所有存活單位的 SPD 總和，決定「勢力」的行動先後 (總和大的先動)。
    ///   例：玩家 SPD 合=8 &gt; 盟軍=6 &gt; 敵人=4 → 行動順序 玩家 → 盟軍 → 敵人。
    ///
    /// 注意：這裡排序的是「勢力」而非個別單位。輪到某勢力時，
    ///   - 玩家勢力：玩家可自由選擇任一可行動單位輪流下令。
    ///   - AI 勢力：依序讓每個單位跑各自的 AI。
    /// 每一「大回合 (Round)」開始時重新計算一次，因為單位可能陣亡導致 SPD 總和改變。
    /// </summary>
    public static class TurnOrderSystem
    {
        /// <summary>依 SPD 總和由大到小回傳本回合的勢力行動順序 (已排除全滅勢力)。</summary>
        public static List<Faction> BuildOrder(IEnumerable<Faction> factions)
        {
            return factions
                .Where(f => f != null && !f.IsDefeated)
                .OrderByDescending(f => f.TotalSpeed)
                .ThenBy(f => f.factionName) // SPD 相同時以名稱穩定排序，避免順序抖動
                .ToList();
        }
    }
}
