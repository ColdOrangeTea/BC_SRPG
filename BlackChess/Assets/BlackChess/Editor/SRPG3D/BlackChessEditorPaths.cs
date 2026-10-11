/// <summary>
/// 【BlackChess Editor 路徑工具】
/// 工具說明：
///   讓 Editor 工具不必寫死 "Assets/..." 路徑，資料夾搬家、改名後工具仍可正常運作：
///     • 專案根資料夾 (<see cref="Root"/>)：由本腳本自己的位置往上推算。
///       本腳本放在 &lt;Root&gt;/Editor/SRPG3D/ 下，所以往上兩層就是 Root (目前為 Assets/BlackChess)。
///       整個 BlackChess 資料夾搬到別處或改名，Root 會跟著變。
///     • 輸出位置：一律用 <see cref="Combine"/> 組出「相對 Root」的路徑 (例如 Root/Prefabs/View3D/Units)。
///     • 輸入資源：用 <see cref="FindAsset{T}"/> 依「檔名 + 類型」搜尋整個專案，不依賴所在資料夾。
///
/// 使用方式：供其他 Editor 工具呼叫 (BillboardTestSceneBuilder、SpineUnitPrefabBuilder)，本身沒有選單。
/// 注意：本腳本若移出 &lt;Root&gt;/Editor/SRPG3D/ 這種兩層結構，請同步調整 <see cref="LevelsAboveRoot"/>。
///
/// 可重複執行：純路徑計算，不產生任何資產。
/// 此工具是由 A_Good_Ink 使用 AI 生成的工具。
/// </summary>
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BlackChess.SRPG.EditorTools
{
    public static class BlackChessEditorPaths
    {
        /// <summary>本腳本所在資料夾往上幾層是 Root (SRPG3D → Editor → Root = 2)。</summary>
        private const int LevelsAboveRoot = 2;

        /// <summary>找不到本腳本時的備援根目錄。</summary>
        private const string FallbackRoot = "Assets";

        private static string _root;

        /// <summary>專案根資料夾 (例如 "Assets/BlackChess")，結尾不含斜線。</summary>
        public static string Root
        {
            get
            {
                if (string.IsNullOrEmpty(_root) || !AssetDatabase.IsValidFolder(_root))
                    _root = ResolveRoot();
                return _root;
            }
        }

        /// <summary>組出相對 Root 的路徑，例如 Combine("Prefabs/View3D/Units") → "Assets/BlackChess/Prefabs/View3D/Units"。</summary>
        public static string Combine(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return Root;
            return $"{Root}/{relativePath.Trim('/', '\\').Replace('\\', '/')}";
        }

        /// <summary>
        /// 依檔名 (不含副檔名) + 類型在整個專案搜尋資源。多個同名時優先取 Root 底下的那個，並在 Console 提示。
        /// 找不到回傳 null。
        /// </summary>
        public static T FindAsset<T>(string fileName) where T : Object
        {
            string path = FindAssetPath<T>(fileName);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        /// <summary>同 <see cref="FindAsset{T}"/>，但回傳路徑 (找不到回傳 null)。</summary>
        public static string FindAssetPath<T>(string fileName) where T : Object
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            var matches = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets($"{fileName} t:{typeof(T).Name}"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                // FindAssets 是部分比對，這裡只留檔名完全相同的。
                if (Path.GetFileNameWithoutExtension(p) == fileName) matches.Add(p);
            }
            if (matches.Count == 0) return null;

            string chosen = matches.Find(p => p.StartsWith(Root + "/")) ?? matches[0];
            if (matches.Count > 1)
                Debug.LogWarning($"[BlackChessEditorPaths] 找到 {matches.Count} 個名為 {fileName} 的 {typeof(T).Name}，使用：{chosen}\n" +
                                 string.Join("\n", matches));
            return chosen;
        }

        /// <summary>逐層建立資料夾 (例如 Assets/BlackChess/Prefabs/View3D/Units)。</summary>
        public static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
            {
                Debug.LogError($"[BlackChessEditorPaths] 無法建立資料夾：{folder}");
                return;
            }
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static string ResolveRoot()
        {
            foreach (var guid in AssetDatabase.FindAssets($"{nameof(BlackChessEditorPaths)} t:MonoScript"))
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(scriptPath) != nameof(BlackChessEditorPaths)) continue;

                string dir = Path.GetDirectoryName(scriptPath)?.Replace('\\', '/');
                for (int i = 0; i < LevelsAboveRoot && !string.IsNullOrEmpty(dir); i++)
                    dir = Path.GetDirectoryName(dir)?.Replace('\\', '/');

                if (!string.IsNullOrEmpty(dir) && AssetDatabase.IsValidFolder(dir)) return dir;
            }
            Debug.LogWarning($"[BlackChessEditorPaths] 無法從腳本位置推算根資料夾，改用 {FallbackRoot}。");
            return FallbackRoot;
        }
    }
}
