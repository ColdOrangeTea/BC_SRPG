using System.Collections;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Units;
using BlackChess.SRPG.View;
using UnityEngine;

namespace BlackChess.SRPG.Items
{
    /// <summary>
    /// 掉在戰場上的道具。需求書規則：
    ///   - 不佔格：單位可以站在同一格、也可以直接穿過。
    ///   - 撿取範圍：站在道具所在格，或在道具「周圍四格 (上下左右)」時，都能選擇撿起。
    ///     → 也就是曼哈頓距離 &lt;= pickupRange (預設 1)。
    ///
    /// 撿取流程 (PickUpAnimated)：靠近道具 → 撿取演出 (道具往角色身上靠近並淡出)
    /// → 用 UI_BattleMsgCell 顯示「獲得道具」提示文字 → 結束。
    /// </summary>
    public class FieldItem : MonoBehaviour
    {
        public ItemData data;

        [Tooltip("撿取範圍 (曼哈頓距離)。1 = 站在格上或上下左右四格皆可撿。")]
        [Min(0)] public int pickupRange = 1;

        [Header("撿取演出")]
        [Tooltip("道具飛向角色並淡出所花的秒數。設 0 則瞬間完成。")]
        [Min(0f)] public float pickupFlyDuration = 0.35f;

        public GridCoord Coord { get; private set; }
        private BattleGrid _grid;

        /// <summary>把道具登記到棋盤上的某一格 (道具登記在 tile.item，但不佔格)。</summary>
        public void PlaceOnGrid(BattleGrid grid, GridCoord coord)
        {
            _grid = grid;
            Coord = coord;
            var tile = grid.GetTile(coord);
            if (tile != null) tile.item = this;
            transform.position = grid.CoordToWorld(coord);
        }

        /// <summary>用道具資料在棋盤上直接生成一個場地道具 (有 icon 時自動附上 SpriteRenderer)。</summary>
        public static FieldItem Spawn(ItemData data, BattleGrid grid, GridCoord coord)
        {
            var go = new GameObject($"FieldItem_{(data != null ? data.itemName : "Unknown")}");
            var item = go.AddComponent<FieldItem>();
            item.data = data;

            if (data != null && data.icon != null)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = data.icon;
                sr.sortingOrder = 10;
            }

            item.PlaceOnGrid(grid, coord);
            return item;
        }

        /// <summary>某單位是否在可撿取範圍內。</summary>
        public bool CanBePickedUpBy(Unit unit)
        {
            if (unit == null) return false;
            return unit.Coord.ManhattanDistanceTo(Coord) <= pickupRange;
        }

        /// <summary>由 unit 撿起 (瞬間、無演出)：加進背包並從棋盤移除。</summary>
        public bool PickUp(Unit unit)
        {
            if (!CanBePickedUpBy(unit)) return false;

            unit.Inventory.Add(data);
            RemoveFromTile();
            gameObject.SetActive(false);
            return true;
        }

        /// <summary>
        /// 由 unit 撿起 (含演出的完整流程，協程)：
        ///   1. 道具立即加入背包並從棋盤移除 (邏輯先落定，演出途中不會被重複撿取)。
        ///   2. 撿取演出：道具往角色身上靠近，同時淡出消失。
        ///   3. 用 UI_BattleMsgCell 顯示「獲得道具」提示文字。
        /// </summary>
        public IEnumerator PickUpAnimated(Unit unit, bool showMessage = true)
        {
            if (!CanBePickedUpBy(unit)) yield break;

            unit.Inventory.Add(data);
            RemoveFromTile();

            yield return FlyToAndFade(unit.transform);

            if (showMessage && data != null)
                BattleMessageUI.Show($"{unit.unitName} 獲得道具「{data.itemName}」");

            gameObject.SetActive(false);
        }

        private void RemoveFromTile()
        {
            if (_grid == null) return;
            var tile = _grid.GetTile(Coord);
            if (tile != null && tile.item == this) tile.item = null;
        }

        /// <summary>撿取演出：往目標 (角色) 平滑靠近、逐漸縮小並淡出 (Sprite 有 alpha 時同時降低透明度)。</summary>
        private IEnumerator FlyToAndFade(Transform target)
        {
            float dur = pickupFlyDuration;
            Vector3 startPos = transform.position;
            Vector3 startScale = transform.localScale;

            var sprites = GetComponentsInChildren<SpriteRenderer>(true);
            var baseColors = new Color[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) baseColors[i] = sprites[i].color;

            if (dur > 0f)
            {
                for (float t = 0f; t < 1f; t += Time.deltaTime / dur)
                {
                    float k = Mathf.SmoothStep(0f, 1f, t);
                    Vector3 to = target != null ? target.position : startPos;
                    transform.position = Vector3.Lerp(startPos, to, k);
                    transform.localScale = startScale * (1f - k); // 縮小：沒有 Sprite 的物件也看得出淡出感
                    for (int i = 0; i < sprites.Length; i++)
                    {
                        var c = baseColors[i];
                        c.a *= 1f - k;
                        sprites[i].color = c;
                    }
                    yield return null;
                }
            }

            // 還原外觀 (物件隨後會被停用；若日後重新啟用/物件池回收，外觀才不會壞掉)。
            transform.localScale = startScale;
            for (int i = 0; i < sprites.Length; i++) sprites[i].color = baseColors[i];
        }
    }
}
