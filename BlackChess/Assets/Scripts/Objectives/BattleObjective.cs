using BlackChess.SRPG.Battle;
using UnityEngine;

namespace BlackChess.SRPG.Objectives
{
    /// <summary>戰鬥目標的評估結果。</summary>
    public enum ObjectiveState
    {
        InProgress,
        Achieved, // 達成 → 戰鬥勝利/結束
        Failed,   // 失敗 → 戰鬥失敗
    }

    /// <summary>
    /// 戰鬥目標基底 (需求書：一場戰鬥有目標，如殲滅所有敵人、撤退到指定地點，達成即結束)。
    /// 做成抽象 MonoBehaviour，讓不同目標可掛在場景中、於 Inspector 設定條件。
    /// BattleManager 每次行動後會呼叫 Evaluate 檢查戰鬥是否該結束。
    /// </summary>
    public abstract class BattleObjective : MonoBehaviour
    {
        [Tooltip("目標說明文字 (顯示給玩家)。")]
        [TextArea] public string description = "達成目標";

        /// <summary>評估目前戰鬥狀態。</summary>
        public abstract ObjectiveState Evaluate(BattleContext context);
    }
}
