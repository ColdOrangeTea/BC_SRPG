using UnityEngine;

namespace BlackChess.SRPG.Items
{
    /// <summary>
    /// 道具大分類。
    ///   Supply  — 補品：對我方成員治療/增益。
    ///   Damage  — 傷害道具：對敵人傷害/減益。
    ///   KeyItem — 關鍵道具：帶在身上、不能使用 (劇情/任務用)。
    /// (Supply 放第 0 個，讓既有的藥水資產在升級後預設仍是補品。)
    /// </summary>
    public enum ItemCategory
    {
        /// <summary>補品：治療/增益我方。</summary>
        Supply,
        /// <summary>傷害道具：傷害/減益敵人。</summary>
        Damage,
        /// <summary>關鍵道具：不能使用。</summary>
        KeyItem,
    }

    /// <summary>
    /// 道具的使用方式 (補品/傷害道具皆適用)。
    /// 兩種模式都是「以使用角色為中心 useRange 格內選一格 → 以該格為中心對 effectRadius 範圍生效」，
    /// 差別在語意與表現 (投擲型未來可加投擲軌跡等演出)。
    /// </summary>
    public enum ItemTargetingMode
    {
        /// <summary>以該使用角色為中心產生使用範圍，在範圍內指定目標格後生效 (例如遞補品給旁邊的夥伴)。</summary>
        SelfCentered,
        /// <summary>指定 useRange 內的某一格「投擲」，以該格為中心對 effectRadius 範圍生效。</summary>
        ThrowAtTile,
    }

    public enum ItemEffectType
    {
        /// <summary>回復 HP。</summary>
        Heal,
        /// <summary>回復 MP。</summary>
        RestoreMP,
        /// <summary>暫時提升攻擊等 (預留，可自行擴充效果套用邏輯)。</summary>
        Buff,
        /// <summary>造成 HP 傷害 (傷害道具用)。</summary>
        Damage,
        /// <summary>減益 (預留，可自行擴充效果套用邏輯)。</summary>
        Debuff,
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

        [Header("分類")]
        [Tooltip("補品=治療/增益我方；傷害道具=傷害/減益敵人；關鍵道具=帶在身上不能用。")]
        public ItemCategory category = ItemCategory.Supply;

        [Header("效果")]
        public ItemEffectType effectType = ItemEffectType.Heal;
        [Tooltip("效果數值，例如回復量 / 傷害量。")]
        public int amount = 5;

        [Tooltip("使用後是否消耗 (從背包移除)。")]
        public bool consumable = true;

        [Header("使用範圍 (格數，關鍵道具不適用)")]
        [Tooltip("SelfCentered=以使用角色為中心的使用範圍內選目標格；ThrowAtTile=指定某一格投擲。")]
        public ItemTargetingMode targeting = ItemTargetingMode.SelfCentered;

        [Tooltip("使用範圍：以使用角色為中心、可指定目標格的最遠格數 (曼哈頓距離)。0 = 只能指定自己所在格。")]
        [Range(0, 10)] public int useRange = 0;

        [Tooltip("效果範圍：以效果中心格向外擴散的格數 (曼哈頓距離)。0 = 只影響中心那一格。")]
        [Range(0, 10)] public int effectRadius = 0;

        [Header("是否會影響同陣營夥伴的血量")]
        [Tooltip("補品：true = 也能治療範圍內的我方夥伴；false = 只作用在使用者自己。\n" +
                 "傷害道具：true = 範圍傷害會無差別波及同陣營夥伴 (如炸藥)；false = 只會影響敵人。")]
        public bool affectsAllies = true;

        /// <summary>此道具能否被「使用」(關鍵道具只能帶在身上)。</summary>
        public bool IsUsable => category != ItemCategory.KeyItem;

        /// <summary>此道具的效果是否為有害 (傷害/減益)。</summary>
        public bool IsHarmful => effectType == ItemEffectType.Damage || effectType == ItemEffectType.Debuff;
    }
}
