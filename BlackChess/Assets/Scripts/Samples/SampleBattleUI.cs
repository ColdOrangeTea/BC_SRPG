using System.Collections;
using System.Collections.Generic;
using BlackChess.SRPG.Actions;
using BlackChess.SRPG.Battle;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Objectives;
using BlackChess.SRPG.Pathfinding;
using BlackChess.SRPG.Units;
using UnityEngine;

namespace BlackChess.SRPG.Samples
{
    /// <summary>
    /// 【範例】最精簡的戰鬥畫面 + 玩家操作。
    /// 為了「不依賴任何美術資源、換到任何渲染管線都能顯示」，這裡刻意用 OnGUI 直接把棋盤、
    /// 單位、可移動範圍畫成色塊 —— 正式專案請改用 Sprite / Tilemap / UI，邏輯呼叫方式完全相同。
    ///
    /// 操作方式：
    ///   • 左鍵點自己的單位 → 選取 (顯示可移動範圍)
    ///   • 左鍵點藍色範圍 → 移動；點射程內敵人 → 攻擊
    ///   • 空白鍵 → 選取單位待機 (防禦)
    ///   • Enter / E → 結束玩家勢力回合，換 AI 行動
    ///
    /// 它只透過 BattleManager 的 Player* 公開方法下令，示範玩家端該怎麼接。
    /// </summary>
    public class SampleBattleUI : MonoBehaviour
    {
        private SampleBattleSetup _setup;
        private BattleGrid Grid => _setup != null ? _setup.Grid : null;
        private BattleManager Manager => _setup != null ? _setup.Manager : null;

        private Unit _selected;
        private readonly HashSet<GridCoord> _reachable = new HashSet<GridCoord>();
        private bool _busy;

        private Texture2D _tex;   // 1x1 白貼圖，配合 GUI.color 畫任意顏色方塊
        private GUIStyle _label;

        private void Awake()
        {
            _setup = FindFirstObjectByType<SampleBattleSetup>();
            _tex = new Texture2D(1, 1);
            _tex.SetPixel(0, 0, Color.white);
            _tex.Apply();
        }

        // ---------------- 輸入 ----------------
        private void Update()
        {
            if (_busy || Manager == null || Grid == null) return;
            if (!Manager.WaitingForPlayer || Manager.CurrentFaction == null) { _selected = null; return; }

            // 結束回合 / 待機
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E))
            {
                Manager.EndPlayerFactionTurn();
                _selected = null; _reachable.Clear();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Space) && _selected != null)
            {
                Manager.PlayerWait(_selected);
                _selected = null; _reachable.Clear();
                return;
            }

            if (!Input.GetMouseButtonDown(0)) return;

            var coord = MouseToCoord();
            if (!Grid.InBounds(coord)) return;
            var tile = Grid.GetTile(coord);

            // 1) 射程內有敵人 → 攻擊
            if (_selected != null && tile.occupant != null && AttackAction.CanAttack(_selected, tile.occupant))
            {
                Manager.PlayerAttack(_selected, tile.occupant);
                RefreshReachable();
                return;
            }

            // 2) 點到藍色可移動範圍 → 移動
            if (_selected != null && !_selected.HasMoved && _reachable.Contains(coord))
            {
                StartCoroutine(DoMove(_selected, coord));
                return;
            }

            // 3) 點到自己可行動的單位 → 選取
            if (tile.occupant != null && tile.occupant.faction == Manager.CurrentFaction && !tile.occupant.HasFinishedTurn)
            {
                Select(tile.occupant);
                return;
            }

            _selected = null; _reachable.Clear();
        }

        private IEnumerator DoMove(Unit unit, GridCoord target)
        {
            _busy = true;
            yield return Manager.PlayerMove(unit, target); // 內含 Dijkstra 找路 + 移動動畫 + 撿道具
            _busy = false;
            RefreshReachable(); // 移動後仍可攻擊，重算高亮
        }

        private void Select(Unit unit)
        {
            _selected = unit;
            RefreshReachable();
        }

        private void RefreshReachable()
        {
            _reachable.Clear();
            if (_selected == null || _selected.HasMoved) return;

            var range = Pathfinder.ComputeMovementRange(Grid, _selected.Coord, _selected.Stats.mov, _selected);
            foreach (var c in Pathfinder.GetStoppableCells(Grid, range, _selected))
                _reachable.Add(c);
        }

        private GridCoord MouseToCoord()
        {
            var cam = Camera.main;
            float zDist = Mathf.Abs(cam.transform.position.z);
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, zDist));
            return Grid.WorldToCoord(world);
        }

        // ---------------- 繪製 (OnGUI) ----------------
        private void OnGUI()
        {
            if (Grid == null) return;
            _label ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, wordWrap = true };

            // 地形
            foreach (var kv in Grid.Tiles)
            {
                var rect = CellRect(kv.Key);
                var type = kv.Value.type;
                DrawBox(rect, type != null ? type.debugColor : Color.gray);
            }

            // 可移動範圍高亮
            foreach (var c in _reachable)
                DrawBox(CellRect(c), new Color(0.2f, 0.5f, 1f, 0.35f));

            // 道具 / 物件
            foreach (var kv in Grid.Tiles)
            {
                var tile = kv.Value;
                var rect = CellRect(kv.Key);
                if (tile.item != null) DrawLabel(rect, "道具", Color.yellow);
                else if (tile.interactable != null) DrawLabel(rect, "瓶子", new Color(0.8f, 0.6f, 0.3f));
            }

            // 單位
            foreach (var f in Manager != null ? Manager.factions : new List<Faction>())
            {
                if (f == null) continue;
                foreach (var u in f.LivingUnits)
                {
                    var rect = CellRect(u.Coord);
                    var col = f.factionColor;
                    if (u.HasFinishedTurn) col *= 0.5f; // 已行動完 → 變暗
                    DrawBox(Shrink(rect, 6), col);
                    if (u == _selected) DrawBorder(Shrink(rect, 4), Color.white);
                    DrawLabel(rect, $"{u.unitName}\n{u.Stats.currentHP}/{u.Stats.maxHP}", Color.black);
                }
            }

            DrawHud();
        }

        private void DrawHud()
        {
            GUILayout.BeginArea(new Rect(10, 10, 320, 220), GUI.skin.box);
            GUILayout.Label("<b>BlackChess SRPG 範例戰鬥</b>");
            if (Manager != null)
            {
                GUILayout.Label($"回合 Round: {Manager.Round}");
                GUILayout.Label($"目前勢力: {(Manager.CurrentFaction != null ? Manager.CurrentFaction.factionName : "-")}");

                if (Manager.Result == ObjectiveState.Achieved) GUILayout.Label("<color=#7CFF7C><b>勝利！殲滅所有敵人</b></color>");
                else if (Manager.Result == ObjectiveState.Failed) GUILayout.Label("<color=#FF7C7C><b>戰敗…</b></color>");
                else if (Manager.WaitingForPlayer) GUILayout.Label("<color=#7CC8FF>你的回合 — 請下令</color>");
                else GUILayout.Label("AI 行動中…");
            }
            GUILayout.Space(6);
            GUILayout.Label("左鍵: 選單位 / 移動(藍) / 攻擊(射程內敵人)");
            GUILayout.Label("空白鍵: 待機(防禦)   Enter/E: 結束我方回合");
            GUILayout.EndArea();
        }

        // ---------------- 繪圖小工具 ----------------
        /// <summary>把某格轉成螢幕 (GUI) 矩形。</summary>
        private Rect CellRect(GridCoord c)
        {
            var cam = Camera.main;
            Vector3 center = cam.WorldToScreenPoint(Grid.CoordToWorld(c));
            float px = (cam.WorldToScreenPoint(Grid.CoordToWorld(c + new GridCoord(1, 0))) - cam.WorldToScreenPoint(Grid.CoordToWorld(c))).x;
            float size = Mathf.Abs(px);
            float x = center.x - size * 0.5f;
            float y = (Screen.height - center.y) - size * 0.5f; // 螢幕 y 上下翻轉
            return new Rect(x, y, size, size);
        }

        private void DrawBox(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, _tex);
            GUI.color = old;
        }

        private void DrawBorder(Rect r, Color c, float t = 2f)
        {
            DrawBox(new Rect(r.x, r.y, r.width, t), c);
            DrawBox(new Rect(r.x, r.yMax - t, r.width, t), c);
            DrawBox(new Rect(r.x, r.y, t, r.height), c);
            DrawBox(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        private void DrawLabel(Rect r, string text, Color c)
        {
            var old = _label.normal.textColor; _label.normal.textColor = c;
            GUI.Label(r, text, _label);
            _label.normal.textColor = old;
        }

        private Rect Shrink(Rect r, float pad) => new Rect(r.x + pad, r.y + pad, r.width - pad * 2, r.height - pad * 2);
    }
}
