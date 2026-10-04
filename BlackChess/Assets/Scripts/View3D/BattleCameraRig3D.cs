using UnityEngine;

namespace BlackChess.SRPG.View3D
{
    /// <summary>
    /// 明日方舟風格的斜俯視戰鬥相機 (3D 棋盤用)。掛在 Main Camera 上。
    ///
    /// 相機以「對焦點 + 水平角 yaw + 俯角 pitch + 距離」描述，每幀在 LateUpdate 套用。
    /// 建議設定：透視、FOV 20~30 (FOV 小 = 透視變形小，接近明日方舟的感覺)、俯角 45~60。
    /// 也可切成正交 (orthographic)，完全沒有透視變形。
    ///
    /// 操作 (測試用)：
    ///   • 左鍵拖曳 / WASD → 平移   • 滾輪 → 縮放   • Q / E → 旋轉水平角 (驗證 Billboard 效果)
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-100)]
    public class BattleCameraRig3D : MonoBehaviour
    {
        [Header("對焦")]
        [Tooltip("對焦的棋盤。留空會自動尋找；有設定時開場會對焦到棋盤中心。")]
        public Board3D board;
        public Vector3 focusPoint;

        [Header("角度")]
        [Tooltip("水平旋轉角 (0 = 從 -Z 往 +Z 看)。")]
        public float yaw = 0f;
        [Tooltip("俯角 (0 = 水平看，90 = 正上方往下看)。明日方舟約 50~60。")]
        [Range(10f, 89f)] public float pitch = 55f;

        [Header("鏡頭")]
        [Tooltip("勾選 = 正交相機 (無透視變形)。")]
        public bool orthographic = false;
        [Tooltip("透視時的視野角。越小越接近正交，Billboard 的透視變形越小。")]
        [Range(5f, 60f)] public float fieldOfView = 25f;
        [Tooltip("透視：相機到對焦點的距離；正交：orthographicSize。滾輪調整的就是這個值。")]
        public float zoom = 16f;
        public float minZoom = 6f;
        public float maxZoom = 40f;

        [Header("操作")]
        public bool enableInput = true;
        public float wheelZoomSpeed = 2f;
        public float keyPanSpeed = 8f;
        public float yawKeySpeed = 90f;
        [Tooltip("滑鼠位移超過此像素才算拖曳 (避免誤觸點選)。")]
        public float dragThresholdPixels = 6f;

        /// <summary>本次左鍵按壓是否被判定為拖曳 (點選邏輯可讀它來忽略點擊)。</summary>
        public bool DraggedThisPress { get; private set; }

        private Camera _cam;
        private Vector2 _pressStart;
        private Vector3 _dragGrabWorld;
        private bool _pressing;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (board == null) board = FindAnyObjectByType<Board3D>();
            if (board != null) focusPoint = board.Center;
            Apply();
        }

        private void LateUpdate()
        {
            if (enableInput) HandleInput();
            Apply();
        }

        /// <summary>依目前參數更新相機位置/角度。編輯器工具也會呼叫它來預先擺好相機。</summary>
        public void Apply()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            _cam.orthographic = orthographic;
            if (orthographic)
            {
                _cam.orthographicSize = zoom * 0.35f;
                transform.position = focusPoint - rot * Vector3.forward * 50f;
            }
            else
            {
                _cam.fieldOfView = fieldOfView;
                transform.position = focusPoint - rot * Vector3.forward * zoom;
            }
            transform.rotation = rot;
        }

        private void HandleInput()
        {
            // 縮放
            float scroll = Input.mouseScrollDelta.y;
            if (!Mathf.Approximately(scroll, 0f)) zoom -= scroll * wheelZoomSpeed;

            // 旋轉
            if (Input.GetKey(KeyCode.Q)) yaw += yawKeySpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.E)) yaw -= yawKeySpeed * Time.deltaTime;

            // 鍵盤平移 (以相機水平方向為準)
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += fwd;
            if (Input.GetKey(KeyCode.S)) move -= fwd;
            if (Input.GetKey(KeyCode.D)) move += right;
            if (Input.GetKey(KeyCode.A)) move -= right;
            focusPoint += move * (keyPanSpeed * Time.deltaTime);

            // 左鍵拖曳平移：讓按下時抓住的地面點一直黏在游標下
            if (Input.GetMouseButtonDown(0))
            {
                _pressing = true;
                DraggedThisPress = false;
                _pressStart = Input.mousePosition;
                TryGroundPoint(Input.mousePosition, out _dragGrabWorld);
            }
            if (_pressing && Input.GetMouseButton(0))
            {
                if (!DraggedThisPress && Vector2.Distance(_pressStart, Input.mousePosition) > dragThresholdPixels)
                    DraggedThisPress = true;
                if (DraggedThisPress)
                {
                    Apply(); // 先用目前 focus 更新相機，再算游標下的地面點
                    if (TryGroundPoint(Input.mousePosition, out Vector3 now))
                        focusPoint += _dragGrabWorld - now;
                }
            }
            if (Input.GetMouseButtonUp(0)) _pressing = false;
        }

        private bool TryGroundPoint(Vector3 screenPos, out Vector3 world)
        {
            world = default;
            float groundY = board != null ? board.SurfaceY : 0f;
            var plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
            var ray = _cam.ScreenPointToRay(screenPos);
            if (!plane.Raycast(ray, out float enter)) return false;
            world = ray.GetPoint(enter);
            return true;
        }
    }
}
