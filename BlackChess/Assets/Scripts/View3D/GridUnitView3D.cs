using System.Collections.Generic;
using BlackChess.SRPG.Core;
using UnityEngine;

namespace BlackChess.SRPG.View3D
{
    /// <summary>
    /// 3D 棋盤上的單位外觀 (只管「站在哪、佔幾格、面向哪」，不含戰鬥邏輯)。
    ///
    /// 建議的物件階層 (由 Tools/SRPG 3D/單位 Prefab 產生器 自動建立)：
    ///   Unit_xxx            ← 本腳本。位置 = 佔據格子的中心 (多格單位取整塊中心)
    ///     └ Visual          ← BillboardSprite (腳底樞紐，負責面向鏡頭)
    ///         └ Spine       ← SkeletonAnimation 等 2D 美術 (只負責縮放與左右翻轉)
    ///     └ Footprint       ← 執行期產生的佔格標示 (可關閉)
    ///
    /// 多格單位：<see cref="footprint"/> 填入相對錨點格的偏移，(0,0) 為錨點 (左下格)。
    ///   2x2 → (0,0)(1,0)(0,1)(1,1)；橫 1x3 → (0,0)(1,0)(2,0)；也可以是 L 形等不規則形狀。
    /// </summary>
    public class GridUnitView3D : MonoBehaviour
    {
        [Header("棋盤")]
        [Tooltip("所在棋盤。留空會在 Awake 自動尋找場景中的 Board3D。")]
        public Board3D board;

        [Tooltip("錨點格座標 (footprint 偏移 (0,0) 所在的格子)。")]
        public GridCoord anchor;

        [Tooltip("佔據的格子 (相對錨點的偏移)。一般角色只要 (0,0)。")]
        public List<Vector2Int> footprint = new List<Vector2Int> { Vector2Int.zero };

        [Header("外觀")]
        [Tooltip("美術的根節點 (翻轉左右時會把它的 scale.x 取負)。通常是 Visual/Spine。")]
        public Transform artRoot;

        [Tooltip("美術預設面向右方嗎？ (Spine 角色多半預設面右)")]
        public bool artFacesRight = true;

        [Tooltip("目前是否面向右方。")]
        public bool facingRight = true;

        [Header("佔格標示")]
        [Tooltip("是否在腳下畫出佔據的格子 (測試大型單位時很好用)。")]
        public bool showFootprint = true;

        [Tooltip("佔格標示用的材質 (半透明)。留空則不畫。")]
        public Material footprintMaterial;

        public Color footprintColor = new Color(0.2f, 0.8f, 1f, 0.35f);

        [Header("移動")]
        [Tooltip("MoveTo 時的移動速度 (格/秒)。")]
        public float moveSpeedCellsPerSec = 4f;

        private Transform _footprintRoot;
        private Vector3 _moveTarget;
        private bool _moving;

        public bool IsMoving => _moving;

        private void Awake()
        {
            if (board == null) board = FindAnyObjectByType<Board3D>();
            if (board == null)
            {
                Debug.LogWarning($"[GridUnitView3D] {name} 找不到 Board3D，無法定位到棋盤上。", this);
                enabled = false;
                return;
            }
            if (footprint == null || footprint.Count == 0)
                footprint = new List<Vector2Int> { Vector2Int.zero };
            if (artRoot == null)
                Debug.LogWarning($"[GridUnitView3D] {name} 沒有指定 artRoot，左右翻轉將不會生效。", this);
        }

        private void Start()
        {
            SnapToGrid();
            SetFacing(facingRight);
            BuildFootprintMarkers();
        }

        private void Update()
        {
            if (!_moving) return;
            float speed = moveSpeedCellsPerSec * board.cellSize;
            transform.position = Vector3.MoveTowards(transform.position, _moveTarget, speed * Time.deltaTime);
            if ((transform.position - _moveTarget).sqrMagnitude < 1e-6f)
            {
                transform.position = _moveTarget;
                _moving = false;
            }
        }

        // ---------- 佔格 ----------

        /// <summary>目前佔據的所有格子座標 (世界棋盤座標)。</summary>
        public IEnumerable<GridCoord> OccupiedCells() => CellsIfAnchoredAt(anchor);

        /// <summary>若錨點放在 at，會佔據哪些格子 (給移動/放置前的合法性檢查用)。</summary>
        public IEnumerable<GridCoord> CellsIfAnchoredAt(GridCoord at)
        {
            foreach (var o in footprint) yield return new GridCoord(at.x + o.x, at.y + o.y);
        }

        /// <summary>錨點放在 at 時，整塊佔格的世界中心 (角色的站位點)。</summary>
        public Vector3 FootprintCenter(GridCoord at)
        {
            if (board == null) return transform.position;
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var c in CellsIfAnchoredAt(at)) { sum += board.CoordToWorld(c); n++; }
            return n > 0 ? sum / n : board.CoordToWorld(at);
        }

        // ---------- 擺放 / 移動 ----------

        /// <summary>立刻對齊到 anchor 所在位置。</summary>
        [ContextMenu("Snap To Grid (對齊格子)")]
        public void SnapToGrid()
        {
            if (board == null) board = FindAnyObjectByType<Board3D>();
            if (board == null) return;
            _moving = false;
            transform.position = FootprintCenter(anchor);
        }

        /// <summary>把錨點移到 target，以直線平滑移動 (測試用；正式版可改走 Pathfinder 的路徑)。</summary>
        public void MoveTo(GridCoord target)
        {
            if (board == null) return;
            Vector3 dest = FootprintCenter(target);
            float dx = dest.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.01f) SetFacing(dx > 0f);
            anchor = target;
            _moveTarget = dest;
            _moving = true;
        }

        /// <summary>設定面向 (左右)。只翻 artRoot，不影響 Billboard 的旋轉。</summary>
        public void SetFacing(bool right)
        {
            facingRight = right;
            if (artRoot == null) return;
            Vector3 s = artRoot.localScale;
            float abs = Mathf.Abs(s.x);
            s.x = (right == artFacesRight) ? abs : -abs;
            artRoot.localScale = s;
        }

        // ---------- 佔格標示 ----------

        /// <summary>依 footprint 產生腳下的半透明方塊標示 (footprint 改變後可再呼叫)。</summary>
        public void BuildFootprintMarkers()
        {
            if (_footprintRoot != null) Destroy(_footprintRoot.gameObject);
            if (!showFootprint || footprintMaterial == null || board == null) return;

            _footprintRoot = new GameObject("Footprint").transform;
            _footprintRoot.SetParent(transform, false);

            var mat = new Material(footprintMaterial) { color = footprintColor };
            Vector3 center = FootprintCenter(anchor);
            float size = board.cellSize * 0.9f;
            foreach (var c in OccupiedCells())
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.name = $"Mark_{c.x}_{c.y}";
                quad.transform.SetParent(_footprintRoot, false);
                // 相對本物件的位置 (本物件在佔格中心)；微微浮起避免與格子頂面 Z-fighting。
                quad.transform.localPosition = board.CoordToWorld(c) - center + Vector3.up * 0.01f;
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                quad.transform.localScale = new Vector3(size, size, 1f);
                var r = quad.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.sortingOrder = -32000; // 永遠畫在角色底下
            }
        }

        private void OnDrawGizmos()
        {
            var b = board != null ? board : FindAnyObjectByType<Board3D>();
            if (b == null || footprint == null) return;
            Gizmos.color = new Color(footprintColor.r, footprintColor.g, footprintColor.b, 0.9f);
            foreach (var o in footprint)
            {
                var c = new GridCoord(anchor.x + o.x, anchor.y + o.y);
                Gizmos.DrawWireCube(b.CoordToWorld(c) + Vector3.up * 0.02f, new Vector3(b.cellSize * 0.9f, 0.02f, b.cellSize * 0.9f));
            }
        }
    }
}
