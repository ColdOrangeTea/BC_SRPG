using System;
using UnityEngine;

namespace BlackChess.SRPG.Core
{
    /// <summary>
    /// 棋盤上的整數座標 (格子座標)。這是整個戰棋系統定位的基礎單位。
    /// 使用 struct (數值型別) 讓它可以當作字典的 Key、方便比較與複製，且不會產生 GC 負擔。
    /// 本系統採「四面向」移動，所以只提供上下左右四個方向。
    /// </summary>
    [Serializable]
    public struct GridCoord : IEquatable<GridCoord>
    {
        public int x;
        public int y;

        public GridCoord(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>四個正交方向 (右、左、上、下)，四面向移動只會用到這四個。</summary>
        public static readonly GridCoord[] Directions =
        {
            new GridCoord(1, 0),
            new GridCoord(-1, 0),
            new GridCoord(0, 1),
            new GridCoord(0, -1),
        };

        /// <summary>回傳與另一格的曼哈頓距離。四面向移動下，這就是「最少要走幾格」的下限，也是攻擊/技能範圍判定用的距離。</summary>
        public int ManhattanDistanceTo(GridCoord other)
        {
            return Mathf.Abs(x - other.x) + Mathf.Abs(y - other.y);
        }

        public static GridCoord operator +(GridCoord a, GridCoord b) => new GridCoord(a.x + b.x, a.y + b.y);
        public static GridCoord operator -(GridCoord a, GridCoord b) => new GridCoord(a.x - b.x, a.y - b.y);
        public static bool operator ==(GridCoord a, GridCoord b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(GridCoord a, GridCoord b) => !(a == b);

        public bool Equals(GridCoord other) => this == other;
        public override bool Equals(object obj) => obj is GridCoord other && Equals(other);
        public override int GetHashCode() => (x * 397) ^ y;
        public override string ToString() => $"({x}, {y})";
    }
}
