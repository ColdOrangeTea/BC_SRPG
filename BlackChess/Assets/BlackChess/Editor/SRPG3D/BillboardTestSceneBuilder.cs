/// <summary>
/// 【Billboard 測試場景產生器】
/// 工具說明：
///   一鍵建立「3D 格子棋盤 + Spine 角色 Billboard」的測試場景，產出：
///     • Assets/Prefabs/View3D/Units/ 下的測試單位 Prefab (Spine：Assets/Spine2D/Test_Stickfigute_SkeletonData.asset，身高依格子邊長等比)
///         - Unit_Test_Stickfigute      (1x1，我方，面右)
///         - Unit_Enemy_1x1             (1x1，敵方，面左)
///         - Unit_BigEnemy_2x2          (2x2 大型敵人，身高 2.8 格)
///         - Unit_BigEnemy_3x1          (3x1 橫向大型敵人，身高 2.2 格)
///     • Assets/Materials/View3D/FootprintMarker.mat (佔格標示材質)
///     • Assets/Scenes/BillboardTest.unity
///         - Board3D (以 Assets/Prefabs/GridCell/GridCell_Gress.prefab 鋪 10x7 格，照 Prefab 原尺寸擺放，
///           格子邊長 = 模型實際邊長、單位站在 Prefab 內的 Pos_Obj 高度)
///         - Main Camera + BattleCameraRig3D (透視 FOV 25、俯角 55)
///         - Directional Light、BillboardTestDemo (點選移動、切換 Billboard 模式)
///
/// 使用方式：
///   Unity 上方選單 → Tools/SRPG 3D/建立 Billboard 測試場景 (Build Billboard Test Scene)
///   完成後場景自動開啟，按 Play 即可測試 (操作說明顯示在畫面左上)。
///
/// 可重複執行：會覆蓋更新既有產出（GUID 不變，引用不會斷）。
/// 此工具是由 A_Good_Ink 使用 AI 生成的工具。
/// </summary>
using System.Collections.Generic;
using System.Text;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Samples;
using BlackChess.SRPG.View3D;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BlackChess.SRPG.EditorTools
{
    public static class BillboardTestSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/BillboardTest.unity";
        private const string TileModelPath = "Assets/Prefabs/GridCell/GridCell_Gress.prefab";
        private const string SkeletonAPath = "Assets/Spine2D/Test_Stickfigute_SkeletonData.asset";

        private static readonly Color AllyColor = new Color(0.2f, 0.8f, 1f, 0.35f);
        private static readonly Color EnemyColor = new Color(1f, 0.3f, 0.25f, 0.35f);

        [MenuItem("Tools/SRPG 3D/建立 Billboard 測試場景 (Build Billboard Test Scene)")]
        public static void Build() => Build(true);

        /// <summary>interactive = false 時不跳任何對話框 (給自動化/批次呼叫)，結果寫入 Console。</summary>
        public static string Build(bool interactive)
        {
            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "已取消";

            var tile = AssetDatabase.LoadAssetAtPath<GameObject>(TileModelPath);
            var skelA = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(SkeletonAPath);
            if (tile == null || skelA == null)
            {
                return Notify(interactive, "Billboard 測試場景",
                    $"缺少必要資源：\n{(tile == null ? TileModelPath + "\n" : "")}{(skelA == null ? SkeletonAPath : "")}");
            }

            var report = new StringBuilder();

            // ---------- 1. 場景 + 棋盤 (先建棋盤，才知道格子邊長) ----------
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            // NewScene 會卸載未被引用的資源，先前載入的參考可能失效 → 重新載入。
            tile = AssetDatabase.LoadAssetAtPath<GameObject>(TileModelPath);
            skelA = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(SkeletonAPath);

            var boardGo = new GameObject("Board3D");
            var board = boardGo.AddComponent<Board3D>();
            board.width = 10;
            board.height = 7;
            board.tilePrefab = tile;
            board.tileGap = 0f;
            board.MatchCellSizeToTile(); // 尊重美術調好的 Prefab 尺寸：cellSize = 模型邊長、不自動縮放
            board.Build();
            float cell = board.cellSize;

            // ---------- 2. 單位 Prefab ----------
            var pAlly = MakeUnit(skelA, "Unit_Test_Stickfigute", 1.5f, 1, 1, true, AllyColor, cell, report);
            var pEnemy = MakeUnit(skelA, "Unit_Enemy_1x1", 1.5f, 1, 1, false, EnemyColor, cell, report);
            var pBig = MakeUnit(skelA, "Unit_BigEnemy_2x2", 2.8f, 2, 2, false, EnemyColor, cell, report);
            var pWide = MakeUnit(skelA, "Unit_BigEnemy_3x1", 2.2f, 3, 1, false, EnemyColor, cell, report);
            if (pAlly == null) return Notify(interactive, "Billboard 測試場景", report.ToString());

            var unitsRoot = new GameObject("Units").transform;
            Place(pAlly, unitsRoot, board, new GridCoord(2, 2), true);
            Place(pAlly, unitsRoot, board, new GridCoord(1, 4), true);
            Place(pEnemy, unitsRoot, board, new GridCoord(5, 5), false);
            Place(pBig, unitsRoot, board, new GridCoord(6, 1), false);
            if (pWide != null) Place(pWide, unitsRoot, board, new GridCoord(3, 0), false);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
                cam.nearClipPlane = 0.1f * cell;
                cam.farClipPlane = 200f * cell;
                var rig = cam.GetComponent<BattleCameraRig3D>();
                if (rig == null) rig = cam.gameObject.AddComponent<BattleCameraRig3D>();
                rig.board = board;
                rig.focusPoint = board.Center;
                // 相機距離/平移速度以「格」為單位設計，依格子邊長等比放大。
                rig.zoom = 16f * cell;
                rig.minZoom = 6f * cell;
                rig.maxZoom = 40f * cell;
                rig.wheelZoomSpeed = 2f * cell;
                rig.keyPanSpeed = 8f * cell;
                rig.Apply(); // 編輯期就擺好相機，Scene/Game 視窗打開即可預覽
            }

            var light = Object.FindAnyObjectByType<Light>();
            if (light != null) light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var demo = new GameObject("BillboardTestDemo").AddComponent<BillboardTestDemo>();
            demo.board = board;
            demo.cameraRig = cam != null ? cam.GetComponent<BattleCameraRig3D>() : null;

            SpineUnitPrefabBuilder.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.AppendLine(ScenePath);
            report.AppendLine(SpineUnitPrefabBuilder.FootprintMaterialPath);
            return Notify(interactive, "Billboard 測試場景 完成", "產出 / 更新：\n" + report + "\n按 Play 開始測試。");
        }

        private static string Notify(bool interactive, string title, string body)
        {
            if (interactive) EditorUtility.DisplayDialog(title, body, "OK");
            else Debug.Log($"[{title}] {body}");
            return body;
        }

        private static GameObject MakeUnit(SkeletonDataAsset sda, string name, float height, int w, int d,
                                           bool faceRight, Color color, float cellSize, StringBuilder report)
        {
            var s = new SpineUnitPrefabBuilder.Settings
            {
                skeletonData = sda,
                prefabName = name,
                heightInCells = height,
                cellSize = cellSize,
                footprintWidth = w,
                footprintDepth = d,
                startAnimation = "Idle",
                artFacesRight = true,
                facingRight = faceRight,
                footprintColor = color,
            };
            var prefab = SpineUnitPrefabBuilder.BuildUnitPrefab(s, out string msg);
            report.AppendLine(prefab != null ? $"{SpineUnitPrefabBuilder.DefaultOutputFolder}/{name}.prefab" : "失敗：" + msg);
            return prefab;
        }

        private static void Place(GameObject prefab, Transform parent, Board3D board, GridCoord anchor, bool faceRight)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            var view = go.GetComponent<GridUnitView3D>();
            view.board = board;
            view.anchor = anchor;
            view.facingRight = faceRight;
            view.SnapToGrid();
            view.SetFacing(faceRight);
        }
    }
}
