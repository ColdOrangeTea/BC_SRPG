/// <summary>
/// 【SRPG 3D 單位 Prefab 產生器】
/// 工具說明：
///   把 Spine 角色 (SkeletonDataAsset) 包裝成可放在 3D 棋盤上的單位 Prefab，自動完成：
///     • Billboard 結構：Unit (GridUnitView3D) → Visual (BillboardSprite，腳底樞紐) → Spine (SkeletonAnimation)
///     • 依「身高 (以格為單位)」自動縮放 Spine，換任何大小的美術都能一致地站在格子上
///     • 佔格 (footprint)：寬 × 深 的矩形，例如 1x1 一般角色、2x2 大型敵人；不規則形狀可在 Prefab 上再編輯
///     • 預設播放的動畫 (Idle 等)、美術預設面向、佔格標示材質
///   產出位置：預設 &lt;BlackChess 根&gt;/Prefabs/View3D/Units/Unit_&lt;名稱&gt;.prefab
///   (根資料夾由 BlackChessEditorPaths 依本工具所在位置推算，目前為 Assets/BlackChess)
///
/// 使用方式：
///   Unity 上方選單 → Tools/SRPG 3D/單位 Prefab 產生器 (Spine Unit Prefab Builder)
///   1. 指定 SkeletonDataAsset (例如 char_002_amiya_SkeletonData.asset)
///   2. 調整身高、佔格大小、預設動畫 → 按「產生 / 更新 Prefab」
///
/// 可重複執行：會覆蓋更新既有產出（GUID 不變，引用不會斷）。
/// 此工具是由 A_Good_Ink 使用 AI 生成的工具。
/// </summary>
using System.Collections.Generic;
using System.IO;
using BlackChess.SRPG.View3D;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

namespace BlackChess.SRPG.EditorTools
{
    public class SpineUnitPrefabBuilder : EditorWindow
    {
        // 路徑一律相對 BlackChess 根資料夾，資料夾搬家/改名後不用改程式。
        public static string DefaultOutputFolder => BlackChessEditorPaths.Combine("Prefabs/View3D/Units");
        public static string FootprintMaterialPath => BlackChessEditorPaths.Combine("Materials/View3D/FootprintMarker.mat");
        public static string SpineFolder => BlackChessEditorPaths.Combine("Spine2D");

        /// <summary>產生單位 Prefab 需要的所有參數 (場景產生器也會用到)。</summary>
        public class Settings
        {
            public SkeletonDataAsset skeletonData;
            public string prefabName;
            /// <summary>輸出資料夾。留空 = <see cref="DefaultOutputFolder"/>。</summary>
            public string outputFolder;
            public float heightInCells = 1.5f;
            /// <summary>棋盤格子邊長 (世界單位)。角色世界身高 = heightInCells × cellSize。</summary>
            public float cellSize = 1f;
            public int footprintWidth = 1;
            public int footprintDepth = 1;
            public string startAnimation = "Idle";
            public bool artFacesRight = true;
            public bool facingRight = true;
            public Color footprintColor = new Color(0.2f, 0.8f, 1f, 0.35f);
            public BillboardSprite.Mode billboardMode = BillboardSprite.Mode.UprightCompensated;
        }

        private SkeletonDataAsset _skeletonData;
        private string _prefabName = "";
        private string _outputFolder; // ScriptableObject 建構時不能呼叫 AssetDatabase，於 OnEnable 帶入預設值
        private float _heightInCells = 1.5f;
        private float _cellSize = 1f;
        private int _footprintWidth = 1;
        private int _footprintDepth = 1;
        private int _animIndex;
        private bool _artFacesRight = true;
        private bool _facingRight = true;
        private Color _footprintColor = new Color(0.2f, 0.8f, 1f, 0.35f);
        private BillboardSprite.Mode _mode = BillboardSprite.Mode.UprightCompensated;
        private string _log = "";

        [MenuItem("Tools/SRPG 3D/單位 Prefab 產生器 (Spine Unit Prefab Builder)")]
        public static void Open() => GetWindow<SpineUnitPrefabBuilder>("Spine 單位 Prefab");

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_outputFolder)) _outputFolder = DefaultOutputFolder;

            // 場景裡有 Board3D 就沿用它的格子邊長，產出的角色大小才會跟棋盤一致。
            var board = FindAnyObjectByType<Board3D>();
            if (board != null) _cellSize = board.cellSize;

            // 預設帶入專案內第一個 SkeletonDataAsset，方便直接試用。
            if (_skeletonData == null)
            {
                string folder = AssetDatabase.IsValidFolder(SpineFolder) ? SpineFolder : BlackChessEditorPaths.Root;
                var guids = AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { folder });
                if (guids.Length > 0)
                    _skeletonData = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("來源", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _skeletonData = (SkeletonDataAsset)EditorGUILayout.ObjectField(
                new GUIContent("SkeletonDataAsset", "Spine 匯入後產生的 *_SkeletonData.asset"), _skeletonData, typeof(SkeletonDataAsset), false);
            if (EditorGUI.EndChangeCheck()) _animIndex = 0;

            string[] anims = GetAnimationNames(_skeletonData);
            if (anims.Length > 0)
            {
                if (_animIndex == 0) _animIndex = Mathf.Max(0, System.Array.IndexOf(anims, "Idle"));
                _animIndex = EditorGUILayout.Popup(new GUIContent("預設動畫", "進場景後循環播放的動畫"), Mathf.Clamp(_animIndex, 0, anims.Length - 1), anims);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("外觀", EditorStyles.boldLabel);
            _heightInCells = EditorGUILayout.Slider(new GUIContent("身高 (格)", "角色在世界中的高度，以格子邊長為 1。一般角色約 1.3~1.6，大型敵人可給 2.5 以上。"), _heightInCells, 0.3f, 6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                _cellSize = EditorGUILayout.FloatField(new GUIContent("格子邊長 (世界單位)", "棋盤 Board3D 的 cellSize。角色世界身高 = 身高(格) × 此值。"), _cellSize);
                if (GUILayout.Button(new GUIContent("從場景讀取", "讀取目前場景中 Board3D 的 cellSize"), GUILayout.Width(80)))
                {
                    var board = FindAnyObjectByType<Board3D>();
                    if (board != null) _cellSize = board.cellSize;
                    else _log = "目前場景中沒有 Board3D。";
                }
            }
            _cellSize = Mathf.Max(0.01f, _cellSize);
            _artFacesRight = EditorGUILayout.Toggle(new GUIContent("美術預設面右", "Spine 原始動畫中角色是否面向右方"), _artFacesRight);
            _facingRight = EditorGUILayout.Toggle(new GUIContent("初始面向右", "放進場景時面向右方"), _facingRight);
            _mode = (BillboardSprite.Mode)EditorGUILayout.EnumPopup(new GUIContent("Billboard 模式", "推薦 UprightCompensated"), _mode);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("佔格", EditorStyles.boldLabel);
            _footprintWidth = EditorGUILayout.IntSlider(new GUIContent("寬 (X 格)", "左右方向佔幾格"), _footprintWidth, 1, 5);
            _footprintDepth = EditorGUILayout.IntSlider(new GUIContent("深 (Z 格)", "前後方向佔幾格"), _footprintDepth, 1, 5);
            _footprintColor = EditorGUILayout.ColorField(new GUIContent("佔格標示顏色", "腳下半透明方塊的顏色"), _footprintColor);
            EditorGUILayout.HelpBox("不規則形狀 (L 形、十字等) 請產生後在 Prefab 的 GridUnitView3D → footprint 清單手動編輯。", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("輸出", EditorStyles.boldLabel);
            _outputFolder = EditorGUILayout.TextField(new GUIContent("輸出資料夾", "Prefab 存放位置"), _outputFolder);
            string defaultName = _skeletonData != null ? GuessBaseName(_skeletonData) : "";
            _prefabName = EditorGUILayout.TextField(new GUIContent("Prefab 名稱", $"留空 = Unit_{defaultName}"), _prefabName);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_skeletonData == null))
            {
                if (GUILayout.Button("產生 / 更新 Prefab", GUILayout.Height(30)))
                {
                    var s = new Settings
                    {
                        skeletonData = _skeletonData,
                        prefabName = string.IsNullOrWhiteSpace(_prefabName) ? "Unit_" + defaultName : _prefabName.Trim(),
                        outputFolder = _outputFolder,
                        heightInCells = _heightInCells,
                        cellSize = _cellSize,
                        footprintWidth = _footprintWidth,
                        footprintDepth = _footprintDepth,
                        startAnimation = anims.Length > 0 ? anims[Mathf.Clamp(_animIndex, 0, anims.Length - 1)] : "",
                        artFacesRight = _artFacesRight,
                        facingRight = _facingRight,
                        footprintColor = _footprintColor,
                        billboardMode = _mode,
                    };
                    var prefab = BuildUnitPrefab(s, out string msg);
                    _log = msg;
                    if (prefab != null) EditorGUIUtility.PingObject(prefab);
                }
            }
            if (!string.IsNullOrEmpty(_log)) EditorGUILayout.HelpBox(_log, MessageType.Info);
        }

        // ---------- 核心 (可被其他工具呼叫) ----------

        /// <summary>依設定產生或就地更新單位 Prefab。失敗回傳 null 並在 message 說明原因。</summary>
        public static GameObject BuildUnitPrefab(Settings s, out string message)
        {
            if (s == null || s.skeletonData == null) { message = "未指定 SkeletonDataAsset。"; return null; }
            var data = s.skeletonData.GetSkeletonData(true);
            if (data == null) { message = $"{s.skeletonData.name} 讀取失敗，請確認 Spine 版本 (需 4.2) 與 atlas 是否正確。"; return null; }

            string outputFolder = string.IsNullOrWhiteSpace(s.outputFolder) ? DefaultOutputFolder : s.outputFolder.Trim().TrimEnd('/');
            EnsureFolder(outputFolder);
            string path = $"{outputFolder}/{s.prefabName}.prefab";
            var footprintMat = EnsureFootprintMaterial();

            // 1) 根：GridUnitView3D
            var root = new GameObject(s.prefabName);
            var view = root.AddComponent<GridUnitView3D>();

            // 2) Visual：Billboard 腳底樞紐
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var billboard = visual.AddComponent<BillboardSprite>();
            billboard.mode = s.billboardMode;

            // 3) Spine：SkeletonAnimation
            var spine = SkeletonAnimation.NewSkeletonAnimationGameObject(s.skeletonData);
            spine.gameObject.name = "Spine";
            spine.transform.SetParent(visual.transform, false);
            float rawHeight = Mathf.Max(data.Height * s.skeletonData.scale, 0.0001f);
            // 世界身高 = 身高(格) × 格子邊長，換不同大小的格子美術時角色比例不變。
            float k = s.heightInCells * Mathf.Max(0.01f, s.cellSize) / rawHeight;
            spine.transform.localScale = new Vector3(k, k, k);
            // Spine 骨架原點 (data.Y) 不一定剛好在腳底，往上補正讓腳底貼齊 Visual 原點。
            spine.transform.localPosition = new Vector3(0f, -data.Y * s.skeletonData.scale * k, 0f);

            var so = new SerializedObject(spine);
            var animProp = so.FindProperty("_animationName");
            if (animProp != null && !string.IsNullOrEmpty(s.startAnimation) && data.FindAnimation(s.startAnimation) != null)
                animProp.stringValue = s.startAnimation;
            var loopProp = so.FindProperty("loop");
            if (loopProp != null) loopProp.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            var mr = spine.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            // 4) 單位設定
            view.artRoot = spine.transform;
            view.artFacesRight = s.artFacesRight;
            view.facingRight = s.facingRight;
            view.footprintMaterial = footprintMat;
            view.footprintColor = s.footprintColor;
            view.footprint = new List<Vector2Int>();
            for (int z = 0; z < s.footprintDepth; z++)
            for (int x = 0; x < s.footprintWidth; x++)
                view.footprint.Add(new Vector2Int(x, z));

            // 5) 存檔 (同路徑覆蓋 → GUID 不變)
            bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            if (!ok || prefab == null) { message = $"儲存 Prefab 失敗：{path}"; return null; }
            message = $"{(existed ? "已更新" : "已產生")}：{path}\n身高 {s.heightInCells} 格 (格邊長 {s.cellSize})、佔格 {s.footprintWidth}x{s.footprintDepth}、動畫 {s.startAnimation}";
            return prefab;
        }

        /// <summary>佔格標示用的半透明材質 (Sprites/Default，支援頂點/材質顏色與透明)。</summary>
        public static Material EnsureFootprintMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(FootprintMaterialPath);
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[SpineUnitPrefabBuilder] 找不到 Sprites/Default shader，佔格標示將不會顯示。");
                return mat;
            }
            if (mat == null)
            {
                EnsureFolder(Path.GetDirectoryName(FootprintMaterialPath).Replace('\\', '/'));
                mat = new Material(shader) { name = "FootprintMarker" };
                AssetDatabase.CreateAsset(mat, FootprintMaterialPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        public static string[] GetAnimationNames(SkeletonDataAsset sda)
        {
            var data = sda != null ? sda.GetSkeletonData(true) : null;
            if (data == null) return new string[0];
            var list = new List<string>();
            foreach (var a in data.Animations) list.Add(a.Name);
            return list.ToArray();
        }

        /// <summary>char_002_amiya_SkeletonData → char_002_amiya</summary>
        public static string GuessBaseName(SkeletonDataAsset sda)
        {
            string n = sda.name;
            const string suffix = "_SkeletonData";
            return n.EndsWith(suffix) ? n.Substring(0, n.Length - suffix.Length) : n;
        }

        /// <summary>逐層建立資料夾 (轉呼叫 BlackChessEditorPaths.EnsureFolder，保留給既有呼叫端)。</summary>
        public static void EnsureFolder(string folder) => BlackChessEditorPaths.EnsureFolder(folder);
    }
}
