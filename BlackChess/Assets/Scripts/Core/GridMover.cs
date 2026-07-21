using System.Collections;
using UnityEngine;

namespace BlackChess.SRPG.Core
{
    /// <summary>
    /// 共用的「格到格」平滑移動工具。棋盤上任何會移動的物件 (單位 / 推箱子 / 礦車…)
    /// 都透過它把 transform 從一格平滑補間到相鄰的下一格，做出「一格一格移動」的表演。
    ///
    /// 邏輯上的佔格 (誰站在哪一格) 由呼叫端負責更新；這裡只純粹處理視覺位置補間。
    /// 可一次帶多個 transform (例如礦車與車上乘客同步移動)，null 會自動略過。
    /// </summary>
    public static class GridMover
    {
        /// <summary>
        /// 把一組 transform 從 from 平滑移到 to，耗時 duration 秒。
        /// duration &lt;= 0 則直接瞬移到定位 (等同關閉動畫)。
        /// </summary>
        public static IEnumerator MoveStep(Vector3 from, Vector3 to, float duration, params Transform[] transforms)
        {
            if (duration <= 0f)
            {
                Apply(transforms, to);
                yield break;
            }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                Apply(transforms, Vector3.Lerp(from, to, Mathf.Clamp01(t)));
                yield return null;
            }
            Apply(transforms, to); // 收尾對齊，避免補間殘留誤差
        }

        private static void Apply(Transform[] transforms, Vector3 position)
        {
            if (transforms == null) return;
            foreach (var tr in transforms)
                if (tr != null) tr.position = position;
        }
    }
}
