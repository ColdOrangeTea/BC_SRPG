using BlackChess.SRPG.Core;
using BlackChess.SRPG.Items;
using BlackChess.SRPG.Units;
using UnityEngine;

namespace BlackChess.SRPG.Objects
{
    /// <summary>
    /// 「可被破壞」功能 (例如需求書中「打掉後掉落補血物品的瓶子」)。
    /// 有自己的 HP，被攻擊時扣血，歸零時破壞並在原地生成掉落道具。
    /// 與 PushableBehavior 掛在同一物件上，就成了「可推、也可破壞的補給箱」。
    /// </summary>
    public class BreakableBehavior : ObjectBehavior
    {
        [Header("耐久")]
        [Min(1)] public int maxHP = 5;
        public int currentHP = 5;

        [Header("掉落物")]
        [Tooltip("破壞時要生成的道具 (例如補血藥水)。可留空表示不掉落。")]
        public FieldItem dropPrefab;

        [Tooltip("破壞後是否從棋盤移除此物件。")]
        public bool destroyOnBreak = true;

        public bool IsBroken => currentHP <= 0;

        /// <summary>受到攻擊。回傳是否因此被破壞。</summary>
        public bool TakeDamage(int amount)
        {
            if (IsBroken) return false;
            currentHP -= Mathf.Max(0, amount);
            if (currentHP <= 0)
            {
                Break();
                return true;
            }
            return false;
        }

        private void Break()
        {
            var grid = Obj.Grid;
            var coord = Obj.Coord;

            if (destroyOnBreak) Obj.RemoveFromGrid();

            if (dropPrefab != null && grid != null)
            {
                var drop = Instantiate(dropPrefab);
                drop.PlaceOnGrid(grid, coord);
            }

            if (destroyOnBreak)
                gameObject.SetActive(false);
        }
    }
}
