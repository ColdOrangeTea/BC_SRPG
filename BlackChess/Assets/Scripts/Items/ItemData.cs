using UnityEngine;

namespace BlackChess.SRPG.Items
{
    public enum ItemEffectType
    {
        /// <summary>回復 HP。</summary>
        Heal,
        /// <summary>回復 MP。</summary>
        RestoreMP,
        /// <summary>暫時提升攻擊等 (預留，可自行擴充效果套用邏輯)。</summary>
        Buff,
    }

    /// <summary>
    /// 道具定義 (資料資產)。決定撿起後能做什麼。
    /// 建立方式：專案視窗 → 右鍵 → Create → BlackChess/SRPG/Item
    /// </summary>
    [CreateAssetMenu(fileName = "Item", menuName = "BlackChess/SRPG/Item")]
    public class ItemData : ScriptableObject
    {
        public string itemName = "Potion";
        [TextArea] public string description;
        public Sprite icon;

        [Header("效果")]
        public ItemEffectType effectType = ItemEffectType.Heal;
        [Tooltip("效果數值，例如回復量。")]
        public int amount = 5;

        [Tooltip("使用後是否消耗 (從背包移除)。")]
        public bool consumable = true;
    }
}
