using System.Collections.Generic;
using System.Linq;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.View3D;
using UnityEngine;

namespace BlackChess.SRPG.Samples
{
    /// <summary>
    /// Billboard 測試場景的操作腳本 (只供測試，不是正式戰鬥流程)。
    ///   • 左鍵點單位 → 選取；再點空格 → 移動過去 (多格單位會檢查整塊是否放得下、是否與他人重疊)。
    ///   • 1 / 2 / 3 → 切換所有單位的 Billboard 模式 (CameraFacing / Upright / UprightCompensated) 比較差異。
    ///   • O → 切換 透視 / 正交 相機。
    ///   • Q / E 旋轉、WASD/左鍵拖曳 平移、滾輪 縮放 (由 BattleCameraRig3D 處理)。
    /// </summary>
    public class BillboardTestDemo : MonoBehaviour
    {
        [Tooltip("棋盤。留空自動尋找。")]
        public Board3D board;
        [Tooltip("相機控制。留空自動從 Camera.main 取得。")]
        public BattleCameraRig3D cameraRig;
        [Tooltip("選取中的單位，佔格標示顯示的顏色。")]
        public Color selectedColor = new Color(1f, 0.85f, 0.2f, 0.55f);

        private GridUnitView3D _selected;
        private Color _selectedOriginalColor;
        private BillboardSprite.Mode _mode = BillboardSprite.Mode.UprightCompensated;

        private void Awake()
        {
            if (board == null) board = FindAnyObjectByType<Board3D>();
            if (cameraRig == null && Camera.main != null) cameraRig = Camera.main.GetComponent<BattleCameraRig3D>();
            if (board == null) Debug.LogWarning("[BillboardTestDemo] 場景中找不到 Board3D。", this);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetAllMode(BillboardSprite.Mode.CameraFacing);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetAllMode(BillboardSprite.Mode.Upright);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetAllMode(BillboardSprite.Mode.UprightCompensated);
            if (Input.GetKeyDown(KeyCode.O) && cameraRig != null) cameraRig.orthographic = !cameraRig.orthographic;

            // 放開左鍵且不是拖曳 → 視為點擊
            if (Input.GetMouseButtonUp(0) && (cameraRig == null || !cameraRig.DraggedThisPress))
                HandleClick();
        }

        private void HandleClick()
        {
            var cam = Camera.main;
            if (board == null || cam == null) return;
            if (!board.TryRaycastCell(cam.ScreenPointToRay(Input.mousePosition), out GridCoord cell)) return;

            var units = FindObjectsByType<GridUnitView3D>(FindObjectsSortMode.None);
            var clicked = units.FirstOrDefault(u => u.OccupiedCells().Contains(cell));
            if (clicked != null) { Select(clicked); return; }
            if (_selected == null || _selected.IsMoving) return;

            // 點空格：把「點到的格子」當成多格單位的中心附近 → 錨點 = 點擊格 - footprint 中心偏移
            GridCoord anchor = AnchorForClickedCell(_selected, cell);
            if (CanPlace(_selected, anchor, units)) _selected.MoveTo(anchor);
            else Debug.Log($"[BillboardTestDemo] {_selected.name} 無法放在錨點 {anchor} (超出棋盤或與其他單位重疊)。");
        }

        private static GridCoord AnchorForClickedCell(GridUnitView3D unit, GridCoord clicked)
        {
            if (unit.footprint.Count == 0) return clicked;
            float cx = (float)unit.footprint.Average(o => o.x);
            float cy = (float)unit.footprint.Average(o => o.y);
            return new GridCoord(clicked.x - Mathf.FloorToInt(cx), clicked.y - Mathf.FloorToInt(cy));
        }

        private bool CanPlace(GridUnitView3D unit, GridCoord anchor, IEnumerable<GridUnitView3D> all)
        {
            var target = new HashSet<GridCoord>(unit.CellsIfAnchoredAt(anchor));
            if (target.Any(c => !board.InBounds(c))) return false;
            foreach (var other in all)
                if (other != unit && other.OccupiedCells().Any(target.Contains)) return false;
            return true;
        }

        private void Select(GridUnitView3D unit)
        {
            if (_selected != null) RecolorFootprint(_selected, _selectedOriginalColor);
            _selected = unit;
            _selectedOriginalColor = unit.footprintColor;
            RecolorFootprint(unit, selectedColor);
        }

        private static void RecolorFootprint(GridUnitView3D unit, Color color)
        {
            var root = unit.transform.Find("Footprint");
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.sharedMaterial.color = color;
        }

        private void SetAllMode(BillboardSprite.Mode mode)
        {
            _mode = mode;
            foreach (var b in FindObjectsByType<BillboardSprite>(FindObjectsSortMode.None)) b.mode = mode;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 430, 170), GUI.skin.box);
            GUILayout.Label("Billboard 測試");
            GUILayout.Label($"模式：{_mode}   (1 CameraFacing / 2 Upright / 3 UprightCompensated)");
            GUILayout.Label($"相機：{(cameraRig != null && cameraRig.orthographic ? "正交" : "透視")}   (O 切換)");
            GUILayout.Label("Q/E 旋轉　WASD/左鍵拖曳 平移　滾輪 縮放");
            GUILayout.Label("點單位選取 → 點空格移動");
            GUILayout.Label("選取中：" + (_selected != null ? $"{_selected.name}  錨點 {_selected.anchor}" : "無"));
            GUILayout.EndArea();
        }
    }
}
