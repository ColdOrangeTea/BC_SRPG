using BlackChess.SRPG.Battle;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Units;
using UnityEngine;

namespace BlackChess.SRPG.AI
{
    /// <summary>
    /// 一個 AI 單位在自己這一手要做什麼的「計畫」。
    /// AI 只負責「決策」產出這個計畫，實際的移動動畫與攻擊由 BattleManager 執行，
    /// 決策與執行分離，方便測試與抽換行為。
    /// </summary>
    public struct UnitPlan
    {
        public bool move;            // 是否要移動
        public GridCoord moveTarget; // 移動目的地 (必須是可到達且可停留的格)

        public bool attack;          // 是否要攻擊
        public Unit attackTarget;    // 攻擊對象

        public bool wait;            // 是否待機 (防禦)

        public static UnitPlan DoNothing => new UnitPlan { wait = true };
    }

    /// <summary>
    /// AI 行為基底。盟軍與敵人共用；差別只在掛哪一種子類 (進攻 / 防禦 / 守衛…)。
    /// 需求書要求盟軍與敵人各有多種行為類型，這裡用策略模式：每種行為一個子類。
    /// 把對應腳本掛在 AI 單位的 GameObject 上，BattleManager 會 GetComponent 取用。
    /// </summary>
    [RequireComponent(typeof(Unit))]
    public abstract class UnitAI : MonoBehaviour
    {
        private Unit _self;
        public Unit Self => _self != null ? _self : (_self = GetComponent<Unit>());

        /// <summary>依目前戰況決定這一手的計畫。</summary>
        public abstract UnitPlan DecideAction(BattleContext context);
    }
}
