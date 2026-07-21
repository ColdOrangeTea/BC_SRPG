using System;
using UnityEngine;

namespace BlackChess.SRPG.Units
{
    /// <summary>
    /// 單位的數值。需求書指定的六項核心屬性：HP / MP / ATK / RNG / MOV / SPD。
    /// 用 [Serializable] 一般類別，讓它能直接顯示在 Unit 的 Inspector，也方便存檔。
    /// </summary>
    [Serializable]
    public class UnitStats
    {
        [Header("生命 / 魔力")]
        [Min(1)] public int maxHP = 10;
        public int currentHP = 10;
        [Min(0)] public int maxMP = 0;
        public int currentMP = 0;

        [Header("戰鬥")]
        [Tooltip("攻擊力：造成傷害的基礎值。")]
        [Min(0)] public int atk = 3;

        [Tooltip("射程 (RNG)：攻擊能觸及的曼哈頓距離。1=近戰、2以上=遠程。")]
        [Min(1)] public int rng = 1;

        [Header("行動")]
        [Tooltip("移動力 (MOV)：一回合最多能消耗的移動成本。走一般地板可走 MOV 格，黏液減半。")]
        [Min(0)] public int mov = 4;

        [Tooltip("速度 (SPD)：同勢力所有單位的 SPD 總和決定勢力行動先後。")]
        [Min(0)] public int spd = 2;

        public bool IsAlive => currentHP > 0;

        /// <summary>戰鬥開始 / 復活時把 HP、MP 補滿。</summary>
        public void FullRestore()
        {
            currentHP = maxHP;
            currentMP = maxMP;
        }

        public void ClampVitals()
        {
            currentHP = Mathf.Clamp(currentHP, 0, maxHP);
            currentMP = Mathf.Clamp(currentMP, 0, maxMP);
        }
    }
}
