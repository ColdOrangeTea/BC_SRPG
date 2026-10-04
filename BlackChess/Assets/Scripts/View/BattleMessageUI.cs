using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackChess.SRPG.View
{
    /// <summary>
    /// 戰鬥中提示文字 (uGUI)。使用你的兩個 prefab：
    ///   • UI_BattleMessageCanva — ScreenSpaceOverlay Canvas，提示訊息的畫布。
    ///   • UI_BattleMsgCell      — 單則提示訊息 (黑底 + TMP 文字)，每則訊息生成一個。
    ///
    /// 兩個 prefab 於執行時從 Resources/UI 載入 (Assets/SRPG/Resources/UI/)；
    /// 也可在 Inspector 直接指定覆寫。
    ///
    /// 任何系統想顯示提示 (例如「獲得道具」) 只要呼叫 BattleMessageUI.Show("...")：
    /// 訊息會在畫面上方置中出現、停留一段時間後淡出，多則訊息會自動往下排列。
    /// </summary>
    public class BattleMessageUI : MonoBehaviour
    {
        private const string CanvaResourcePath = "UI/UI_BattleMessageCanva";
        private const string CellResourcePath = "UI/UI_BattleMsgCell";

        [Header("Prefab (留空則自動從 Resources/UI 載入同名資產)")]
        [SerializeField] private GameObject canvaPrefab; // UI_BattleMessageCanva
        [SerializeField] private GameObject cellPrefab;  // UI_BattleMsgCell

        [Header("節奏 (秒)")]
        [Min(0f)] public float fadeInTime = 0.15f;
        [Min(0f)] public float displayTime = 1.6f;
        [Min(0f)] public float fadeOutTime = 0.35f;

        [Header("排版")]
        [Tooltip("訊息區距離畫面頂端的距離 (像素)。")]
        public float topOffset = 70f;
        [Tooltip("多則訊息之間的垂直間距 (像素)。")]
        public float spacing = 6f;

        private static BattleMessageUI _instance;
        private GameObject _canvasInstance;
        private RectTransform _msgRoot;

        /// <summary>全域入口。場景中沒有實例時會自動建立一個。</summary>
        public static BattleMessageUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<BattleMessageUI>();
                    if (_instance == null)
                        _instance = new GameObject("BattleMessageUI (runtime)").AddComponent<BattleMessageUI>();
                }
                return _instance;
            }
        }

        /// <summary>顯示一則戰鬥提示文字 (例如「獲得道具『藥水』」)。</summary>
        public static void Show(string text) => Instance.Spawn(text);

        private void Awake()
        {
            if (_instance == null) _instance = this;
            if (canvaPrefab == null) canvaPrefab = Resources.Load<GameObject>(CanvaResourcePath);
            if (cellPrefab == null) cellPrefab = Resources.Load<GameObject>(CellResourcePath);
        }

        // ---------------- 生成訊息 ----------------

        private void Spawn(string text)
        {
            if (!EnsureCanvas()) return;

            GameObject cell;
            if (cellPrefab != null)
            {
                cell = Instantiate(cellPrefab, _msgRoot);
                cell.SetActive(true);
                var txt = FindDeep(cell.transform, "Txt")?.GetComponent<TMP_Text>();
                if (txt != null) txt.text = text;
            }
            else
            {
                // 沒有 prefab 時的保底：純文字訊息。
                cell = new GameObject("BattleMsg (fallback)", typeof(RectTransform));
                cell.transform.SetParent(_msgRoot, false);
                var tmp = cell.AddComponent<TextMeshProUGUI>();
                tmp.text = text;
                tmp.fontSize = 28f;
                tmp.alignment = TextAlignmentOptions.Center;
                ((RectTransform)cell.transform).sizeDelta = new Vector2(400f, 40f);
            }

            var group = cell.GetComponent<CanvasGroup>();
            if (group == null) group = cell.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            StartCoroutine(CellLifetime(cell, group));
        }

        /// <summary>單則訊息的生命週期：淡入 → 停留 → 淡出 → 銷毀。</summary>
        private IEnumerator CellLifetime(GameObject cell, CanvasGroup group)
        {
            if (fadeInTime > 0f)
                for (float t = 0f; t < 1f; t += Time.deltaTime / fadeInTime)
                {
                    if (cell == null) yield break;
                    group.alpha = t;
                    yield return null;
                }
            if (cell == null) yield break;
            group.alpha = 1f;

            yield return new WaitForSeconds(displayTime);

            if (fadeOutTime > 0f)
                for (float t = 0f; t < 1f; t += Time.deltaTime / fadeOutTime)
                {
                    if (cell == null) yield break;
                    group.alpha = 1f - t;
                    yield return null;
                }
            if (cell != null) Destroy(cell);
        }

        // ---------------- Canvas / 容器 ----------------

        private bool EnsureCanvas()
        {
            if (_msgRoot != null) return true;

            if (canvaPrefab != null)
            {
                _canvasInstance = Instantiate(canvaPrefab);
                _canvasInstance.name = "UI_BattleMessageCanva (runtime)";
            }
            else
            {
                // 保底：自行建立一個 Overlay Canvas。
                _canvasInstance = new GameObject("UI_BattleMessageCanva (fallback)");
                var canvas = _canvasInstance.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = _canvasInstance.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            // 訊息容器：貼在畫面頂端置中，多則訊息由 VerticalLayoutGroup 往下排。
            var rootGO = new GameObject("MsgRoot", typeof(RectTransform));
            _msgRoot = (RectTransform)rootGO.transform;
            _msgRoot.SetParent(_canvasInstance.transform, false);
            _msgRoot.anchorMin = new Vector2(0.5f, 1f);
            _msgRoot.anchorMax = new Vector2(0.5f, 1f);
            _msgRoot.pivot = new Vector2(0.5f, 1f);
            _msgRoot.anchoredPosition = new Vector2(0f, -topOffset);

            var layout = rootGO.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = rootGO.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return true;
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
    }
}
