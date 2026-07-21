using UnityEngine;

namespace BlackChess.SRPG.View
{
    /// <summary>
    /// 戰鬥視野控制。掛在 Main Camera (需正交 orthographic) 上即可，不需其他場景設定：
    ///   • 滾輪 → 縮放視野 (改變正交尺寸，夾在 min/max 之間)。
    ///   • 螢幕左下角的 Slider → 也能拖動控制縮放尺寸 (與滾輪同步)。
    ///   • 左鍵按住拖曳 → 平移視野。
    ///
    /// 為避免「拖曳視野」誤觸場上物件/單位的點選判定：
    ///   • 只有滑鼠位移超過 <see cref="dragThresholdPixels"/> 才算「拖曳」(<see cref="DraggedThisPress"/>)；
    ///     UI 端 (SampleBattleUI) 會讀這個旗標，若這次左鍵按壓是拖曳就不當成點擊。
    ///   • 若按壓的起點落在 UI 之上 (<see cref="PointerOverUI"/> 由 UI 每幀寫入)，整個按壓都不平移，
    ///     縮放也會停用，交給 UI 自行處理 (例如捲動道具列表)。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BattleCameraController : MonoBehaviour
    {
        [Header("縮放 (正交尺寸，越小畫面越近)")]
        [Tooltip("最小正交尺寸：畫面能拉到多近。")]
        public float minZoom = 2.5f;
        [Tooltip("最大正交尺寸：畫面能拉到多遠。")]
        public float maxZoom = 14f;
        [Tooltip("滾輪每一刻改變的尺寸量。")]
        public float wheelZoomSpeed = 1.2f;

        [Header("拖曳平移")]
        [Tooltip("滑鼠位移超過此像素才判定為『拖曳視野』(而非點擊選取)。")]
        public float dragThresholdPixels = 6f;

        [Header("縮放 Slider (螢幕左下角)")]
        public bool showZoomSlider = true;
        [Tooltip("面板左下角的螢幕座標 (x 為左邊界；y 若 <= 0 則自動貼在畫面底部)。")]
        public Vector2 sliderPanelOffset = new Vector2(14f, 0f);
        public float sliderWidth = 190f;

        /// <summary>本次左鍵按壓是否已被判定為『拖曳視野』。UI 讀它來抑制點擊。</summary>
        public bool DraggedThisPress { get; private set; }

        /// <summary>由 UI 每幀寫入：滑鼠是否停在戰鬥選單/面板等 UI 之上。為 true 時本腳本不平移、不縮放。</summary>
        public static bool PointerOverUI;

        private Camera _cam;
        private float _zoom;
        private Vector3 _dragOriginWorld;
        private Vector2 _pressStartPixel;
        private bool _pressing;
        private bool _pressBlocked; // 這次按壓起點在 UI 上 → 全程不平移
        private Rect _sliderPanelRect;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (!_cam.orthographic) _cam.orthographic = true;
            // 尊重場景既有的取景 (SampleBattleSetup.FrameCamera 設定的尺寸) 當成「固定的預設視野」，
            // 只把它夾進允許的縮放範圍內。
            _zoom = Mathf.Clamp(_cam.orthographicSize, minZoom, maxZoom);
            _cam.orthographicSize = _zoom;
        }

        private void Update()
        {
            HandleWheelZoom();
            HandleDragPan();
        }

        // ---------- 縮放 ----------
        private void HandleWheelZoom()
        {
            if (PointerOverUI || OverSliderPanel(MouseGuiPos())) return;
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Approximately(scroll, 0f)) return;
            SetZoom(_zoom - scroll * wheelZoomSpeed);
        }

        private void SetZoom(float value)
        {
            _zoom = Mathf.Clamp(value, minZoom, maxZoom);
            _cam.orthographicSize = _zoom;
        }

        // ---------- 拖曳平移 ----------
        private void HandleDragPan()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _pressing = true;
                DraggedThisPress = false;
                _pressStartPixel = Input.mousePosition;
                _pressBlocked = PointerOverUI || OverSliderPanel(MouseGuiPos());
                _dragOriginWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
            }

            if (_pressing && Input.GetMouseButton(0) && !_pressBlocked)
            {
                if (!DraggedThisPress &&
                    Vector2.Distance(_pressStartPixel, Input.mousePosition) > dragThresholdPixels)
                    DraggedThisPress = true;

                if (DraggedThisPress)
                {
                    // 讓「按下時抓住的世界點」持續黏在游標下：算出目前游標對應的世界點與原點差值後補正相機。
                    Vector3 now = _cam.ScreenToWorldPoint(Input.mousePosition);
                    Vector3 diff = _dragOriginWorld - now;
                    diff.z = 0f;
                    transform.position += diff;
                }
            }

            if (Input.GetMouseButtonUp(0))
                _pressing = false;
        }

        // ---------- Slider ----------
        private void OnGUI()
        {
            if (!showZoomSlider || _cam == null) return;

            float panelW = sliderWidth + 26f;
            float panelH = 58f;
            float x = sliderPanelOffset.x;
            float y = sliderPanelOffset.y > 0f ? sliderPanelOffset.y : Screen.height - panelH - 14f;
            _sliderPanelRect = new Rect(x, y, panelW, panelH);

            GUILayout.BeginArea(_sliderPanelRect, GUI.skin.box);
            GUILayout.Label("視野縮放 (左近 / 右遠)");
            float newZoom = GUILayout.HorizontalSlider(_zoom, minZoom, maxZoom, GUILayout.Width(sliderWidth));
            if (!Mathf.Approximately(newZoom, _zoom)) SetZoom(newZoom);
            GUILayout.EndArea();
        }

        // ---------- 小工具 ----------
        private bool OverSliderPanel(Vector2 guiPos) => showZoomSlider && _sliderPanelRect.Contains(guiPos);

        /// <summary>把 Input.mousePosition (左下為原點、y 向上) 轉成 GUI 座標 (左上為原點、y 向下)。</summary>
        private static Vector2 MouseGuiPos() =>
            new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
    }
}
