using System.Collections.Generic;
using BlackChess.SRPG.Core;
using UnityEngine;

namespace BlackChess.SRPG.View3D
{
    /// <summary>
    /// 3D 戰棋棋盤 (XZ 平面)。負責：
    ///   1. 依寬高把「格子模型 Prefab」鋪滿棋盤 (地形美術可隨時替換)。
    ///   2. 兩種擺放方式：
    ///      • autoFitTile = true：自動把格子模型縮放/對齊到 <see cref="cellSize"/>，頂面高度 = 0 (換任何模型都不必改數值)。
    ///      • autoFitTile = false：照 Prefab 原樣擺放 (尊重美術調整好的尺寸)，單位站立高度取 Prefab 內的
    ///        站位點 (<see cref="standPointName"/>，例如 Pos_Obj)；沒有站位點則取模型頂面。
    ///        可用 <see cref="MatchCellSizeToTile"/> 讓 cellSize 等於模型的實際邊長。
    ///   3. 提供 格子座標 ↔ 世界座標 的轉換 (單位站位、滑鼠點選用)。
    ///
    /// 座標約定：GridCoord.x → 世界 +X，GridCoord.y → 世界 +Z；單位站立高度 = 棋盤物件的 Y + <see cref="standHeight"/>。
    /// 此類別只管「外觀與座標」，地形成本與擋路規則交給尋路模組 (BlackChess.SRPG.Pathfinding.IPathGrid / TileTypePathGrid)，之後可接上。
    /// </summary>
    public class Board3D : MonoBehaviour
    {
        [Header("棋盤尺寸 (格)")]
        [Min(1)] public int width = 8;
        [Min(1)] public int height = 6;

        [Header("格子")]
        [Tooltip("每格在世界中的邊長 (Unity 單位)。單位站位、Billboard 大小都以此為基準。")]
        [Min(0.01f)] public float cellSize = 1f;

        [Tooltip("格子模型 Prefab (例如 GridCell_Gress)。")]
        public GameObject tilePrefab;

        [Tooltip("自動把模型的水平尺寸縮放成 cellSize，並把頂面對齊到 Y=0。關閉則照 Prefab 原樣擺放 (原點對格子中心)。")]
        public bool autoFitTile = true;

        [Tooltip("autoFitTile 關閉時：Prefab 內代表「單位站立點」的子物件名稱。找不到則用模型頂面。")]
        public string standPointName = "Pos_Obj";

        [Tooltip("單位站立面相對棋盤物件的高度。Build 時自動計算 (autoFitTile 時為 0)。")]
        public float standHeight;

        [Tooltip("格與格之間的間隙比例 (0 = 緊貼)。做出明日方舟那種格線感。")]
        [Range(0f, 0.3f)] public float tileGap = 0.04f;

        [Tooltip("模型厚度要不要跟著等比縮放。關閉時厚度維持 cellSize × thicknessRatio。")]
        public bool keepModelProportion = true;

        [Tooltip("keepModelProportion 關閉時使用：格子厚度 = cellSize × 此值。")]
        [Min(0.01f)] public float thicknessRatio = 0.25f;

        private const string TileRootName = "Tiles";

        private readonly Dictionary<GridCoord, Transform> _cells = new Dictionary<GridCoord, Transform>();

        public IReadOnlyDictionary<GridCoord, Transform> Cells => _cells;

        private void Awake()
        {
            // 場景裡已經有編輯期產生的格子 → 只重建索引；沒有才在執行期產生。
            RebuildIndex();
            if (_cells.Count == 0) Build();
        }

        // ---------- 產生 ----------

        /// <summary>清除並重新鋪滿格子。可在編輯期 (右鍵元件 → Build) 或執行期呼叫。</summary>
        [ContextMenu("Build (重新鋪格子)")]
        public void Build()
        {
            if (tilePrefab == null)
            {
                Debug.LogWarning($"[Board3D] {name} 沒有指定 tilePrefab，無法鋪格子。", this);
                return;
            }

            Transform root = GetOrCreateTileRoot();
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            _cells.Clear();

            if (autoFitTile) standHeight = 0f;
            bool standMeasured = autoFitTile;

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var coord = new GridCoord(x, y);
                var cell = new GameObject(CellName(coord)).transform;
                cell.SetParent(root, false);
                cell.position = CellOrigin(coord);

                var model = InstantiateTile(cell);
                if (model == null) { _cells[coord] = cell; continue; }
                if (autoFitTile) FitModelToCell(model.transform, cell);
                else if (!standMeasured)
                {
                    standHeight = MeasureStandHeight(model.transform, cell);
                    standMeasured = true;
                }
                _cells[coord] = cell;
            }
        }

        /// <summary>照原樣擺放時，量出單位站立點相對格子原點的高度。</summary>
        private float MeasureStandHeight(Transform model, Transform cell)
        {
            var sp = FindDeep(model, standPointName);
            if (sp != null) return cell.InverseTransformPoint(sp.position).y;
            if (TryGetRendererBounds(model, out Bounds b)) return b.max.y - cell.position.y;
            Debug.LogWarning($"[Board3D] 格子 {model.name} 沒有站位點 {standPointName} 也沒有 Renderer，站立高度設為 0。", model);
            return 0f;
        }

        private static Transform FindDeep(Transform root, string childName)
        {
            if (string.IsNullOrEmpty(childName)) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == childName) return t;
            return null;
        }

        /// <summary>
        /// 讓 cellSize 等於格子 Prefab 的實際水平邊長 (取 X/Z 較大者)，並改成照原樣擺放。
        /// 美術已在 Prefab 調好尺寸時使用；之後記得重新 Build。
        /// </summary>
        [ContextMenu("Match Cell Size To Tile (cellSize = 模型邊長)")]
        public void MatchCellSizeToTile()
        {
            if (tilePrefab == null) { Debug.LogWarning($"[Board3D] {name} 沒有指定 tilePrefab。", this); return; }
            var temp = Instantiate(tilePrefab);
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (TryGetRendererBounds(temp.transform, out Bounds b) && Mathf.Max(b.size.x, b.size.z) > 0f)
            {
                cellSize = Mathf.Max(b.size.x, b.size.z);
                autoFitTile = false;
            }
            else Debug.LogWarning($"[Board3D] {tilePrefab.name} 沒有可用的 Renderer，無法量測邊長。", this);
            if (Application.isPlaying) Destroy(temp); else DestroyImmediate(temp);
        }

        private GameObject InstantiateTile(Transform parent)
        {
#if UNITY_EDITOR
            // 編輯期用 PrefabUtility 生成，保留 Prefab 連結 (之後改 Prefab 會同步)。
            if (!Application.isPlaying)
            {
                var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(tilePrefab, parent);
                if (go != null) return go;
            }
#endif
            return Instantiate(tilePrefab, parent);
        }

        /// <summary>依 Renderer 的世界包圍盒，把模型縮放/位移成「頂面中心 = cell 原點」。</summary>
        private void FitModelToCell(Transform model, Transform cell)
        {
            model.localPosition = Vector3.zero;
            if (!TryGetRendererBounds(model, out Bounds b) || b.size.x <= 0f || b.size.z <= 0f)
            {
                Debug.LogWarning($"[Board3D] 格子模型 {model.name} 沒有可用的 Renderer，略過自動對齊。", model);
                return;
            }

            float target = cellSize * (1f - tileGap);
            float sx = target / b.size.x;
            float sz = target / b.size.z;
            float sy = keepModelProportion ? (sx + sz) * 0.5f : (cellSize * thicknessRatio) / Mathf.Max(b.size.y, 0.0001f);
            // 模型可能帶旋轉 (Blender 匯出常見 -90° X)，所以在 cell 空間縮放：先包一層 Fit 節點。
            var fit = new GameObject("Fit").transform;
            fit.SetParent(cell, false);
            model.SetParent(fit, true);
            fit.localScale = new Vector3(sx, sy, sz);

            // 縮放後重新量，把頂面中心移到 cell 原點。
            TryGetRendererBounds(fit, out b);
            fit.position += cell.position - new Vector3(b.center.x, b.max.y, b.center.z);
        }

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return found;
        }

        private Transform GetOrCreateTileRoot()
        {
            var root = transform.Find(TileRootName);
            if (root == null)
            {
                root = new GameObject(TileRootName).transform;
                root.SetParent(transform, false);
            }
            return root;
        }

        /// <summary>從場景既有的子物件重建「座標 → 格子」索引 (編輯期已鋪好的格子)。</summary>
        public void RebuildIndex()
        {
            _cells.Clear();
            var root = transform.Find(TileRootName);
            if (root == null) return;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var coord = new GridCoord(x, y);
                var cell = root.Find(CellName(coord));
                if (cell != null) _cells[coord] = cell;
            }
        }

        private static string CellName(GridCoord c) => $"Cell_{c.x}_{c.y}";

        // ---------- 座標轉換 ----------

        public bool InBounds(GridCoord c) => c.x >= 0 && c.y >= 0 && c.x < width && c.y < height;

        /// <summary>格子座標 → 世界座標 (單位站立點：格子中心、站立高度)。</summary>
        public Vector3 CoordToWorld(GridCoord c) => CellOrigin(c) + Vector3.up * standHeight;

        /// <summary>格子物件本身的位置 (格子中心、棋盤高度)。</summary>
        private Vector3 CellOrigin(GridCoord c)
        {
            return transform.position + new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);
        }

        /// <summary>單位站立面的世界 Y (滑鼠射線、相機拖曳用的地面高度)。</summary>
        public float SurfaceY => transform.position.y + standHeight;

        /// <summary>世界座標 → 格子座標 (只看 XZ)。</summary>
        public GridCoord WorldToCoord(Vector3 world)
        {
            Vector3 local = world - transform.position;
            return new GridCoord(Mathf.FloorToInt(local.x / cellSize), Mathf.FloorToInt(local.z / cellSize));
        }

        /// <summary>滑鼠射線 → 打到的格子 (與格子頂面高度的水平面求交)。</summary>
        public bool TryRaycastCell(Ray ray, out GridCoord coord)
        {
            coord = default;
            var plane = new Plane(Vector3.up, new Vector3(0f, SurfaceY, 0f));
            if (!plane.Raycast(ray, out float enter)) return false;
            coord = WorldToCoord(ray.GetPoint(enter));
            return InBounds(coord);
        }

        /// <summary>棋盤中心的世界座標 (相機對焦用)。</summary>
        public Vector3 Center => transform.position + new Vector3(width * cellSize * 0.5f, standHeight, height * cellSize * 0.5f);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            float gy = standHeight + 0.01f;
            for (int x = 0; x <= width; x++)
                Gizmos.DrawLine(transform.position + new Vector3(x * cellSize, gy, 0f),
                                transform.position + new Vector3(x * cellSize, gy, height * cellSize));
            for (int y = 0; y <= height; y++)
                Gizmos.DrawLine(transform.position + new Vector3(0f, gy, y * cellSize),
                                transform.position + new Vector3(width * cellSize, gy, y * cellSize));
        }
    }
}
