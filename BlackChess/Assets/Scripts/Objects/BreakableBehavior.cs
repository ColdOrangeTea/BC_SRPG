using BlackChess.SRPG.Core;
using BlackChess.SRPG.Items;
using BlackChess.SRPG.Units;
using UnityEngine;

namespace BlackChess.SRPG.Objects
{
    /// <summary>
    /// 「可被破壞」功能 (例如需求書中「打掉後掉落補血物品的瓶子」)。
    /// 有自己的 HP，可被玩家選定為攻擊目標；被攻擊時扣血，歸零時破壞並在原地生成掉落道具
    /// (掉落後由 BattleManager 觸發「自動撿起」流程)。
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

        [Tooltip("沒有指定 dropPrefab 時，可直接指定道具資料，破壞時自動生成一個 FieldItem。")]
        public ItemData dropData;

        [Tooltip("破壞後是否從棋盤移除此物件。")]
        public bool destroyOnBreak = true;

        public bool IsBroken => currentHP <= 0;

        /// <summary>受到攻擊。回傳是否因此被破壞。</summary>
        public bool TakeDamage(int amount) => TakeDamage(amount, out _);

        /// <summary>受到攻擊。回傳是否因此被破壞；drop 為破壞時生成的掉落道具 (可能為 null)。</summary>
        public bool TakeDamage(int amount, out FieldItem drop)
        {
            drop = null;
            if (IsBroken) return false;
            currentHP -= Mathf.Max(0, amount);
            if (currentHP <= 0)
            {
                drop = Break();
                return true;
            }
            return false;
        }

        /// <summary>破壞：從棋盤移除並生成掉落道具。回傳生成的道具 (可能為 null)。</summary>
        private FieldItem Break()
        {
            var grid = Obj.Grid;
            var coord = Obj.Coord;

            if (destroyOnBreak) Obj.RemoveFromGrid();

            FieldItem drop = null;
            if (grid != null)
            {
                if (dropPrefab != null)
                {
                    drop = Instantiate(dropPrefab);
                    drop.gameObject.SetActive(true); // 範本可能是停用中的物件
                    drop.PlaceOnGrid(grid, coord);
                }
                else if (dropData != null)
                {
                    drop = FieldItem.Spawn(dropData, grid, coord);
                }
            }

            if (destroyOnBreak)
                gameObject.SetActive(false);

            return drop;
        }
    }
}
