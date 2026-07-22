using System;
using BlackChess.SRPG.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackChess.SRPG.View
{
    /// <summary>
    /// 執行時掛在 UI_ItemCell 實例上：快取 icon / 名稱 / 底圖三個子物件，
    /// 負責顯示道具資料、選取高光 (淡入淡出) 與滑鼠 hover/點擊事件回報。
    ///
    /// UI_ItemCell 階層 (由你的 prefab 而來)：
    ///   UI_ItemCell
    ///     ├ bg              (Image，底圖 → 拿來做高光)
    ///     └ layout
    ///         ├ Icon        (Image)
    ///         └ Item_Name   (TextMeshProUGUI)
    /// </summary>
    public class BattleItemCell : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        private static readonly Color BaseColor = new Color(0f, 0f, 0f, 0.39f);
        private static readonly Color HiA = new Color(0.20f, 0.20f, 0.28f, 0.60f);
        private static readonly Color HiB = new Color(1f, 0.85f, 0.30f, 0.80f);

        private Image _bg;
        private Image _icon;
        private TMP_Text _name;
        private int _index;
        private Action<int> _onHover;
        private Action<int> _onClick;

        public void Init(ItemData item, int index, Action<int> onHover, Action<int> onClick)
        {
            _index = index;
            _onHover = onHover;
            _onClick = onClick;
            Cache();

            if (_name != null) _name.text = item != null ? item.itemName : "-";
            if (_icon != null && item != null && item.icon != null)
                _icon.sprite = item.icon; // 道具沒有 icon 時就保留 prefab 預設圖示

            SetHighlight(0f, false);
        }

        private void Cache()
        {
            if (_bg != null) return;
            _bg = transform.Find("bg")?.GetComponent<Image>();
            var layout = transform.Find("layout");
            if (layout != null)
            {
                _icon = layout.Find("Icon")?.GetComponent<Image>();
                _name = layout.Find("Item_Name")?.GetComponent<TMP_Text>();
            }
        }

        /// <summary>t = 0..1 高光強度 (由外部用 PingPong 傳入)；selected = 是否為目前選取欄位。</summary>
        public void SetHighlight(float t, bool selected)
        {
            if (_bg == null) return;
            _bg.color = selected ? Color.Lerp(HiA, HiB, t) : BaseColor;
        }

        public void OnPointerEnter(PointerEventData eventData) => _onHover?.Invoke(_index);
        public void OnPointerClick(PointerEventData eventData) => _onClick?.Invoke(_index);
    }
}
