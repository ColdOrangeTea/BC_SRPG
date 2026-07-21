using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BlackChess.SRPG.Units
{
    /// <summary>單位由誰控制。</summary>
    public enum FactionControl
    {
        /// <summary>玩家操作 (玩家回合可自由選擇單位行動)。</summary>
        Player,
        /// <summary>由 AI 自動行動 (盟軍或敵人都用這個，差別在 AI 行為腳本)。</summary>
        AI,
    }

    /// <summary>
    /// 勢力 (陣營)。需求書要求：可能不只兩個勢力 (玩家 / 盟軍 / 敵人…)，
    /// 每個勢力的所有單位 SPD 總和決定回合行動先後。
    ///
    /// 敵我關係用 teamId 判定：
    ///   - teamId 相同 → 友方 (例如 玩家 與 盟軍 都設 teamId = 0)。
    ///   - teamId 不同 → 敵對 (例如 敵人 設 teamId = 1)。
    /// 這樣就能表達「玩家+盟軍」對抗「敵人」，或多方混戰 (每方不同 teamId)。
    /// </summary>
    public class Faction : MonoBehaviour
    {
        [Header("身分")]
        public string factionName = "Player";
        public FactionControl control = FactionControl.Player;

        [Tooltip("陣營編號。相同 = 友方，不同 = 敵對。玩家與盟軍通常設成同一個。")]
        public int teamId = 0;

        [Tooltip("回合中 UI / 提示用的代表色。")]
        public Color factionColor = Color.blue;

        private readonly List<Unit> _units = new List<Unit>();
        public IReadOnlyList<Unit> Units => _units;

        public void Register(Unit unit)
        {
            if (unit != null && !_units.Contains(unit))
                _units.Add(unit);
        }

        public void Unregister(Unit unit) => _units.Remove(unit);

        /// <summary>還活著的單位。</summary>
        public IEnumerable<Unit> LivingUnits => _units.Where(u => u != null && u.IsAlive);

        /// <summary>本勢力所有存活單位的 SPD 總和 —— 這是決定勢力行動順序的關鍵值。</summary>
        public int TotalSpeed => LivingUnits.Sum(u => u.Stats.spd);

        /// <summary>是否全滅 (殲滅類目標會用到)。</summary>
        public bool IsDefeated => !LivingUnits.Any();

        public bool IsFriendlyTo(Faction other) => other != null && other.teamId == teamId;
        public bool IsHostileTo(Faction other) => other != null && other.teamId != teamId;
    }
}
