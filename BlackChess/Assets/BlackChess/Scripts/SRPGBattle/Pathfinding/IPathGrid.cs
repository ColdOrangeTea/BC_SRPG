using BlackChess.SRPG.Core;

namespace BlackChess.SRPG.Pathfinding
{
    /// <summary>
    /// 尋路用的棋盤介面。Pathfinder 只透過它詢問「這格在不在棋盤內、進入要花多少移動力」，
    /// 不認識任何單位/物件/地形類別，所以任何棋盤 (2D、3D、測試用的純資料) 只要實作它就能尋路。
    ///
    /// 「誰擋路」的規則 (敵人擋路、友軍可穿越、箱子擋路…) 也在實作端決定：
    /// 不可通過就讓 <see cref="GetEnterCost"/> 回傳負數即可。
    /// </summary>
    public interface IPathGrid
    {
        /// <summary>座標是否在棋盤範圍內。</summary>
        bool InBounds(GridCoord c);

        /// <summary>
        /// 進入格子 c 所需的移動力 (至少 1)。回傳負數 (例如 -1) 代表不可通過。
        /// 例：一般地板 1、黏液 2 (等於移動力砍半)、牆壁 -1。
        /// </summary>
        int GetEnterCost(GridCoord c);
    }
}
