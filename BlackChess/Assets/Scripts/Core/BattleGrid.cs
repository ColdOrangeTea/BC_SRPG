using System.Collections.Generic;
using BlackChess.SRPG.Units;
using UnityEngine;

namespace BlackChess.SRPG.Core
{
    /// <summary>
    /// 戰場棋盤。負責：
    ///   1. 保存所有 Tile (地形資料)。
    ///   2. 提供格子座標 &lt;-&gt; 世界座標 的轉換 (給角色定位、滑鼠點選用)。
    ///   3. 提供鄰居查詢 (四面向) 給 Dijkstra 尋路使用。
    ///   4. 管理格子上單位/物件的登記 (誰站在哪一格)。
    ///
    /// 使用方式：把這個腳本掛在一個空物件上，設定寬高與格子大小，
    /// 於 Awake 產生棋盤 (或你也可以用 Tilemap 自行填地形，再呼叫 SetTileType)。
    /// </summary>
    public class BattleGrid : MonoBehaviour
    {
        [Header("棋盤尺寸 (格)")]
        public int width = 12;
        public int height = 12;

        [Header("每格的世界大小 (Unity 單位)")]
        public float cellSize = 1f;

        [Header("預設地形 (產生棋盤時填入每一格)")]
        public TileType defaultTileType;

        // 以座標為 Key 的字典。用字典而非二維陣列，是為了讓之後做不規則棋盤 / 動態擴充更容易。
        private readonly Dictionary<GridCoord, Tile> _tiles = new Dictionary<GridCoord, Tile>();

        public IReadOnlyDictionary<GridCoord, Tile> Tiles => _tiles;

        private void Awake()
        {
            if (_tiles.Count == 0)
                BuildGrid();
        }

        /// <summary>依 width/height 產生一張都是 defaultTileType 的棋盤。</summary>
        public void BuildGrid()
        {
            _tiles.Clear();
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var coord = new GridCoord(x, y);
                _tiles[coord] = new Tile(coord, defaultTileType);
            }
        }

        public bool InBounds(GridCoord c) => _tiles.ContainsKey(c);

        public Tile GetTile(GridCoord c) => _tiles.TryGetValue(c, out var t) ? t : null;

        /// <summary>覆寫某格地形 (例如用開關把水閘打開，把「深水」變成「地板」時使用)。</summary>
        public void SetTileType(GridCoord c, TileType type)
        {
            var tile = GetTile(c);
            if (tile != null) tile.type = type;
        }

        // ---------- 座標轉換 ----------

        /// <summary>格子座標 → 世界座標 (格子中心)。2D 用 XY 平面。</summary>
        public Vector3 CoordToWorld(GridCoord c)
        {
            return transform.position + new Vector3((c.x + 0.5f) * cellSize, (c.y + 0.5f) * cellSize, 0f);
        }

        /// <summary>世界座標 → 格子座標 (供滑鼠點選 / 拖曳判定)。</summary>
        public GridCoord WorldToCoord(Vector3 world)
        {
            Vector3 local = world - transform.position;
            return new GridCoord(Mathf.FloorToInt(local.x / cellSize), Mathf.FloorToInt(local.y / cellSize));
        }

        // ---------- 尋路支援 ----------

        /// <summary>回傳某格四面向、且在棋盤內的鄰居。Dijkstra 會呼叫它展開節點。</summary>
        public IEnumerable<Tile> GetNeighbors(GridCoord c)
        {
            foreach (var dir in GridCoord.Directions)
            {
                var n = c + dir;
                if (_tiles.TryGetValue(n, out var tile))
                    yield return tile;
            }
        }

        // ---------- 單位登記 ----------

        /// <summary>把單位登記到某格 (會先把它從舊格移除)。移動系統會呼叫它同步棋盤狀態。</summary>
        public void PlaceUnit(Unit unit, GridCoord c)
        {
            if (unit == null) return;
            // 從舊格移除
            var old = GetTile(unit.Coord);
            if (old != null && old.occupant == unit) old.occupant = null;

            var tile = GetTile(c);
            if (tile != null)
            {
                tile.occupant = unit;
                unit.SetCoordInternal(c);
                unit.transform.position = CoordToWorld(c);
            }
        }

        /// <summary>單位死亡/撤退時，把它從棋盤移除。</summary>
        public void RemoveUnit(Unit unit)
        {
            if (unit == null) return;
            var tile = GetTile(unit.Coord);
            if (tile != null && tile.occupant == unit) tile.occupant = null;
        }
    }
}
