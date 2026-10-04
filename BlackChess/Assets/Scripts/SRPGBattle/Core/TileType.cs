using UnityEngine;

namespace BlackChess.SRPG.Core
{
    /// <summary>
    /// 地形種類 (資料資產)。用 ScriptableObject 讓你能在 Unity 編輯器裡建立多種地形，
    /// 例如：草地(cost=1)、黏液(cost=2 → 等於移動力砍半)、岩漿/牆壁(不可通行)。
    ///
    /// 建立方式：專案視窗 → 右鍵 → Create → BlackChess/SRPG/Tile Type
    ///
    /// 移動成本設計說明：
    ///   - 一般地板 moveCost = 1
    ///   - 黏液地板 moveCost = 2，因為 MOV 預算在進入這格時被扣 2，
    ///     等於同樣的 MOV 只能走一半的黏液格 → 實現「移動力/2」的效果。
    ///   - 障礙物 isWalkable = false，Dijkstra 會直接略過，代表「不能走」。
    /// </summary>
    [CreateAssetMenu(fileName = "TileType", menuName = "BlackChess/SRPG/Tile Type")]
    public class TileType : ScriptableObject
    {
        [Tooltip("地形顯示名稱")]
        public string typeName = "Ground";

        [Tooltip("是否可被單位通行/停留。false = 障礙物 (牆、深水、岩漿等)")]
        public bool isWalkable = true;

        [Tooltip("進入此格所消耗的移動力。1=一般地板，2=黏液(等於移動力砍半)。至少為 1。")]
        [Min(1)]
        public int moveCost = 1;

        [Tooltip("是否阻擋攻擊/視線 (高牆之類)。目前預設不阻擋，可供之後的射程/視線判定使用。")]
        public bool blocksLineOfSight = false;

        [Tooltip("編輯器 / Debug 時在格子上畫出的顏色。")]
        public Color debugColor = new Color(0.6f, 0.6f, 0.6f, 0.35f);
    }
}
