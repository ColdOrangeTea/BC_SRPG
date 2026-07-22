using System;
using System.Collections.Generic;
using BlackChess.SRPG.Items;
using BlackChess.SRPG.Units;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackChess.SRPG.View
{
    /// <summary>
    /// 戰鬥用「道具列表」UI (uGUI)。使用你的兩個 prefab：
    ///   • BattleItemCanva — 一個 ScreenSpaceOverlay Canvas，內含垂直捲動的 ScrollView。
    ///   • UI_ItemCell     — List 樣式的小長條 (icon + 名稱)，每個道具生成一個。
    ///
    /// 兩個 prefab 會在執行時從 Resources 載入 (已放到 Assets/SRPG/Resources/)；
    /// 也可在 Inspector 直接指定覆寫。
    ///
    /// 操作：方向鍵/滑鼠 hover 選取 (高光淡入淡出)、滾輪捲動、Enter 或點擊使用、Esc 取消。
    /// 由 <see cref="SampleBattleUI"/> 透過 Open/Close 驅動，使用與取消各以 callback 回報。
    /// </summary>
    public class BattleItemListUI : MonoBehaviour
    {
        [Header("Prefab (留空則自動從 Resources 載入同名資產)")]
        [SerializeField] private GameObject canvaPrefab; // BattleItemCanva
        [SerializeField] private GameObject cellPrefab;  // UI_ItemCell

        private GameObject _canvasInstance;
        private ScrollRect _scroll;
        private RectTransform _content;
        private TMP_Text _desc; // Txt_description：顯示選取道具的敘述
        private readonly List<BattleItemCell> _cells = new List<BattleItemCell>();

        private Unit _unit;
        private Action<ItemData> _onUse;
        private Action _onCancel;
        private int _index;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (canvaPrefab == null) canvaPrefab = Resources.Load<GameObject>("BattleItemCanva");
            if (cellPrefab == null) cellPrefab = Resources.Load<GameObject>("UI_ItemCell");
        }

        // ---------------- 開 / 關 ----------------
        public void Open(Unit unit, Action<ItemData> onUse, Action onCancel)
        {
            _unit = unit;
            _onUse = onUse;
            _onCancel = onCancel;
            _index = 0;

            EnsureEventSystem();
            if (!EnsureCanvas()) { Debug.LogWarning("[BattleItemListUI] 找不到 BattleItemCanva / UI_ItemCell prefab，無法顯示道具列表。"); return; }

            _canvasInstance.SetActive(true);
            Rebuild();
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
            if (_canvasInstance != null) _canvasInstance.SetActive(false);
        }

        // ---------------- 建立 / 重建 ----------------
        private bool EnsureCanvas()
        {
            if (_canvasInstance != null) return true;
            if (canvaPrefab == null) return false;

            _canvasInstance = Instantiate(canvaPrefab);
            _canvasInstance.name = "BattleItemCanva (runtime)";

            _scroll = _canvasInstance.GetComponentInChildren<ScrollRect>(true);
            if (_scroll == null || _scroll.content == null) return false;
            _content = _scroll.content;

            // 讓 Content 高度隨道具數量成長，捲動才會生效。
            if (_content.GetComponent<ContentSizeFitter>() == null)
            {
                var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            // 讓每個道具格的寬度自動貼齊清單寬度 (格子高度仍由 prefab 自己決定)。
            var layout = _content.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.childControlWidth = true;
                layout.childForceExpandWidth = true;
            }

            // 道具敘述文字 (Txt_description)：設定成「固定框內自動縮字＋換行」，超出由父物件 Mask 裁掉。
            _desc = FindDeep(_canvasInstance.transform, "Txt_description")?.GetComponent<TMP_Text>();
            if (_desc != null) ConfigureDescription(_desc);

            return cellPrefab != null;
        }

        private void Rebuild()
        {
            // 清掉舊的格子
            _cells.Clear();
            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);

            if (_unit == null) return;
            var items = _unit.Inventory.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var go = Instantiate(cellPrefab, _content);
                go.SetActive(true);
                var cell = go.GetComponent<BattleItemCell>();
                if (cell == null) cell = go.AddComponent<BattleItemCell>();
                cell.Init(items[i], i, OnHover, OnClick);
                _cells.Add(cell);
            }

            _index = Mathf.Clamp(_index, 0, Mathf.Max(0, items.Count - 1));
            ScrollToIndex();
            UpdateDescription();
        }

        /// <summary>把敘述文字設定成：固定框內自動縮小字級並換行，超出範圍交給父物件的 Mask 裁切。</summary>
        private static void ConfigureDescription(TMP_Text desc)
        {
            // 撐滿父物件(Mask)範圍，讓自動縮放有完整空間可用。
            var rt = desc.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            desc.textWrappingMode = TextWrappingModes.Normal;    // 開啟換行
            desc.overflowMode = TextOverflowModes.Truncate;      // 縮到最小仍放不下時，截斷於框內
            desc.enableAutoSizing = true;                        // 依框大小自動縮放字級
            desc.fontSizeMin = 8f;
            if (desc.fontSizeMax < desc.fontSize) desc.fontSizeMax = desc.fontSize;
        }

        private void UpdateDescription()
        {
            if (_desc == null) return;
            var items = _unit != null ? _unit.Inventory.Items : null;
            if (items == null || _index < 0 || _index >= items.Count) { _desc.text = string.Empty; return; }
            var item = items[_index];
            _desc.text = string.IsNullOrEmpty(item.description) ? item.itemName : item.description;
        }

        private static Transform FindDeep(Transform root, string childName)
        {
            if (root.name == childName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), childName);
                if (found != null) return found;
            }
            return null;
        }

        // ---------------- 輸入 / 高光 ----------------
        private void Update()
        {
            if (!IsOpen) return;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow)) MoveSelection(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow)) MoveSelection(+1);

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { UseCurrent(); return; }
            if (Input.GetKeyDown(KeyCode.Escape)) { _onCancel?.Invoke(); return; }

            // 選取欄位高光淡入淡出
            float t = Mathf.PingPong(Time.unscaledTime * 2f, 1f);
            for (int i = 0; i < _cells.Count; i++)
                _cells[i].SetHighlight(t, i == _index);
        }

        private void MoveSelection(int delta)
        {
            if (_cells.Count == 0) return;
            _index = Mathf.Clamp(_index + delta, 0, _cells.Count - 1);
            ScrollToIndex();
            UpdateDescription();
        }

        private void OnHover(int index)
        {
            _index = index; // 滑鼠移過即選取
            UpdateDescription();
        }

        private void OnClick(int index)
        {
            _index = index;
            UseCurrent();
        }

        private void UseCurrent()
        {
            if (_unit == null) return;
            var items = _unit.Inventory.Items;
            if (_index < 0 || _index >= items.Count) return;
            _onUse?.Invoke(items[_index]);
        }

        private void ScrollToIndex()
        {
            if (_scroll == null || _cells.Count <= 1) return;
            // 由上到下：index 0 → 頂端(1)，最後 → 底端(0)。
            float pos = 1f - (float)_index / (_cells.Count - 1);
            _scroll.verticalNormalizedPosition = Mathf.Clamp01(pos);
        }

        // ---------------- EventSystem ----------------
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.hideFlags = HideFlags.DontSave;
        }
    }
}
