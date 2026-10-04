using System.Collections.Generic;
using BlackChess.SRPG.Core;

namespace BlackChess.SRPG.Pathfinding
{
    /// <summary>
    /// 現成的 <see cref="IPathGrid"/> 實作：寬 × 高的矩形棋盤，每格一個 <see cref="TileType"/> 決定成本，
    /// 另外可登記「暫時擋路的格子」(例如被單位或大型敵人佔據的格)。
    ///
    /// 用法：
    ///   var grid = new TileTypePathGrid(10, 7, groundType);
    ///   grid.SetTileType(new GridCoord(3, 2), slimeType);
    ///   grid.SetBlocked(enemyCoord, true);
    ///   var range = Pathfinder.ComputeMovementRange(grid, unitCoord, mov);
    /// </summary>
    public class TileTypePathGrid : IPathGrid
    {
        public int Width { get; }
        public int Height { get; }

        /// <summary>沒有特別設定地形的格子使用的地形。null 視為成本 1 的一般地板。</summary>
        public TileType defaultType;

        private readonly Dictionary<GridCoord, TileType> _types = new Dictionary<GridCoord, TileType>();
        private readonly HashSet<GridCoord> _blocked = new HashSet<GridCoord>();

        public TileTypePathGrid(int width, int height, TileType defaultType = null)
        {
            Width = width < 0 ? 0 : width;
            Height = height < 0 ? 0 : height;
            this.defaultType = defaultType;
        }

        public bool InBounds(GridCoord c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;

        public int GetEnterCost(GridCoord c)
        {
            if (!InBounds(c) || _blocked.Contains(c)) return -1;
            var type = GetTileType(c);
            if (type == null) return 1;
            return type.isWalkable ? type.moveCost : -1;
        }

        public TileType GetTileType(GridCoord c) => _types.TryGetValue(c, out var t) ? t : defaultType;

        /// <summary>設定某格地形 (傳 null 則改回 defaultType)。</summary>
        public void SetTileType(GridCoord c, TileType type)
        {
            if (type == null) _types.Remove(c);
            else _types[c] = type;
        }

        /// <summary>登記/取消某格暫時擋路 (例如單位佔據)。</summary>
        public void SetBlocked(GridCoord c, bool blocked)
        {
            if (blocked) _blocked.Add(c);
            else _blocked.Remove(c);
        }

        public bool IsBlocked(GridCoord c) => _blocked.Contains(c);

        public void ClearBlocked() => _blocked.Clear();
    }
}
