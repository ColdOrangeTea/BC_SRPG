using UnityEngine;

namespace BlackChess.SRPG.View3D
{
    /// <summary>
    /// Billboard (看板) 效果：讓 2D 美術 (Spine / Sprite) 在 3D 斜俯視相機下永遠正對鏡頭，不會被看成一張「斜躺的紙片」。
    ///
    /// 掛法：掛在「腳底樞紐」物件上 (原點 = 角色腳底)，2D 美術當它的子物件。
    /// 本腳本只轉這個物件的 rotation / localScale.y，不碰子物件，所以子物件的左右翻轉 (scale.x = -1) 不受影響。
    ///
    /// 三種模式：
    ///   • <see cref="Mode.CameraFacing"/>：完全平行於相機畫面。最不變形，但角色上半身會往後倒，
    ///     背後若有高台格可能被擋住 (穿模)。
    ///   • <see cref="Mode.Upright"/>：只繞 Y 軸轉，永遠直立。不會穿進後面的格子，但俯角越大角色越「矮扁」。
    ///   • <see cref="Mode.UprightCompensated"/> (推薦)：直立 + 依俯角把高度拉長補償，
    ///     畫面上看起來與 CameraFacing 幾乎一樣，同時保有直立不穿模的優點。
    ///
    /// 另外會依「腳底到相機的深度」設定 sortingOrder，讓前後排角色 (含大型單位) 的遮擋順序正確。
    /// 執行順序排在相機之後 (DefaultExecutionOrder 1000)，避免相機移動時抖動。
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class BillboardSprite : MonoBehaviour
    {
        public enum Mode
        {
            CameraFacing,
            Upright,
            UprightCompensated,
        }

        [Tooltip("Billboard 模式。推薦 UprightCompensated：不變形、也不會倒進後方的格子。")]
        public Mode mode = Mode.UprightCompensated;

        [Tooltip("要面對的相機。留空則自動使用 Camera.main。")]
        public Camera targetCamera;

        [Tooltip("UprightCompensated 的最大拉長倍率 (避免相機接近正俯視時無限拉長)。")]
        [Range(1f, 4f)] public float maxCompensation = 3f;

        [Header("前後排序")]
        [Tooltip("依深度自動設定底下所有 Renderer 的 sortingOrder (越近越後畫 = 蓋在上面)。")]
        public bool autoSortByDepth = true;

        [Tooltip("深度 1 單位對應多少 sortingOrder。數值越大排序越細。")]
        public float sortingPrecision = 100f;

        [Tooltip("額外的排序偏移 (例如特效要永遠蓋在角色上，可給正值)。")]
        public int sortingOffset;

        private Renderer[] _renderers;
        private float _baseScaleY = 1f;
        private bool _baseScaleCaptured;

        private void OnEnable()
        {
            CacheRenderers();
            CaptureBaseScale();
        }

        /// <summary>子物件的 Renderer 有增減時 (換裝、換角色) 呼叫，重新抓排序對象。</summary>
        public void CacheRenderers() => _renderers = GetComponentsInChildren<Renderer>(true);

        private void CaptureBaseScale()
        {
            if (_baseScaleCaptured) return;
            _baseScaleY = Mathf.Approximately(transform.localScale.y, 0f) ? 1f : transform.localScale.y;
            _baseScaleCaptured = true;
        }

        private void LateUpdate()
        {
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;
            Apply(cam);
        }

        /// <summary>立刻依相機更新朝向與排序 (例如瞬移後、或相機剛切換時可手動呼叫)。</summary>
        public void Apply(Camera cam)
        {
            Transform camT = cam.transform;
            float stretch = 1f;

            switch (mode)
            {
                case Mode.CameraFacing:
                    // 與相機同旋轉 → 圖面完全平行於畫面。
                    transform.rotation = camT.rotation;
                    break;

                case Mode.Upright:
                case Mode.UprightCompensated:
                {
                    // 只取相機前方在水平面的分量 → 只繞 Y 轉。
                    Vector3 flatForward = Vector3.ProjectOnPlane(camT.forward, Vector3.up);
                    if (flatForward.sqrMagnitude < 1e-6f) flatForward = Vector3.ProjectOnPlane(camT.up, Vector3.up);
                    transform.rotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);

                    if (mode == Mode.UprightCompensated)
                    {
                        // 直立的紙片在俯角 θ 下，畫面高度只剩 cosθ 倍 → 拉長 1/cosθ 補回來。
                        // 透視相機用「相機 → 角色」的實際視線算 θ，正交相機用相機前方。
                        Vector3 viewDir = cam.orthographic ? camT.forward : (transform.position - camT.position);
                        float pitch = Vector3.Angle(Vector3.ProjectOnPlane(viewDir, Vector3.up), viewDir);
                        float cos = Mathf.Cos(pitch * Mathf.Deg2Rad);
                        stretch = Mathf.Min(1f / Mathf.Max(cos, 0.0001f), maxCompensation);
                    }
                    break;
                }
            }

            Vector3 s = transform.localScale;
            s.y = _baseScaleY * stretch;
            transform.localScale = s;

            if (autoSortByDepth) ApplySorting(camT);
        }

        private void ApplySorting(Transform camT)
        {
            if (_renderers == null) CacheRenderers();
            // 以腳底 (本物件原點) 沿相機前方的深度排序：越遠 order 越小。
            float depth = Vector3.Dot(transform.position - camT.position, camT.forward);
            int order = Mathf.Clamp(Mathf.RoundToInt(-depth * sortingPrecision) + sortingOffset, -32000, 32000);
            foreach (var r in _renderers)
                if (r != null) r.sortingOrder = order;
        }

        private void OnDisable()
        {
            // 還原拉長，避免在編輯期關掉元件後 scale 留在被拉長的狀態。
            if (!_baseScaleCaptured) return;
            Vector3 s = transform.localScale;
            s.y = _baseScaleY;
            transform.localScale = s;
        }
    }
}
