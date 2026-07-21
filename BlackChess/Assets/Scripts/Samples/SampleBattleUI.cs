using System.Collections;
using System.Collections.Generic;
using BlackChess.SRPG.Actions;
using BlackChess.SRPG.Battle;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Items;
using BlackChess.SRPG.Objectives;
using BlackChess.SRPG.Pathfinding;
using BlackChess.SRPG.Units;
using BlackChess.SRPG.View;
using UnityEngine;

namespace BlackChess.SRPG.Samples
{
    /// <summary>
    /// 【範例】最精簡的戰鬥畫面 + 玩家操作。
    /// 為了「不依賴任何美術資源、換到任何渲染管線都能顯示」，這裡刻意用 OnGUI 直接把棋盤、
    /// 單位、可移動範圍畫成色塊 —— 正式專案請改用 Sprite / Tilemap / UI，邏輯呼叫方式完全相同。
    ///
    /// 操作方式：
    ///   • 左鍵點自己的單位 → 選取，於旁邊彈出指令選單 (攻擊 / 移動 / 待機 / 道具)。
    ///   • 攻擊 → 進入選目標模式，點紅色高亮的敵人發動攻擊。
    ///   • 移動 → 進入選格模式，點藍色可移動範圍移動。
    ///   • 道具 → 展開一行道具列表，滑鼠 hover 或方向鍵/滾輪選取 (高光淡入淡出)，Enter/點擊使用。
    ///   • 待機 → 該單位防禦並結束行動。   Esc → 取消目前選取/選單。
    ///   • Enter / E → 結束玩家勢力回合，換 AI 行動。
    ///   • 左鍵拖曳 → 平移視野；滾輪 → 縮放 (由 BattleCameraController 處理)。
    ///
    /// 它只透過 BattleManager 的 Player* 公開方法下令，示範玩家端該怎麼接。
    /// </summary>
    public class SampleBattleUI : MonoBehaviour
    {
        private enum Mode { Idle, Menu, MoveTargeting, AttackTargeting, ItemList }

        private SampleBattleSetup _setup;
        private BattleCameraController _view;
        private BattleGrid Grid => _setup != null ? _setup.Grid : null;
        private BattleManager Manager => _setup != null ? _setup.Manager : null;

        private Unit _selected;
        private Mode _mode = Mode.Idle;
        private readonly HashSet<GridCoord> _reachable = new HashSet<GridCoord>();
        private readonly List<Unit> _attackTargets = new List<Unit>();
        private bool _busy;

        // 道具列表狀態
        private int _itemIndex;    // 目前選取的道具索引
        private int _itemScroll;   // 目前可視範圍的第一個道具索引
        private const int ItemVisible = 4; // 一次顯示幾個道具欄位

        // 戰況面板收合
        private bool _hudCollapsed;

        // 點擊 vs 拖曳：記錄按下當下是否在 UI 上
        private bool _pressOverUI;

        // 供「滑鼠是否停在 UI 上」判定用：每次 OnGUI 重建各面板的螢幕矩形 (GUI 座標)
        private readonly List<Rect> _uiRects = new List<Rect>();

        private Texture2D _tex;   // 1x1 白貼圖，配合 GUI.color 畫任意顏色方塊
        private GUIStyle _label;

        private void Awake()
        {
            _setup = FindFirstObjectByType<SampleBattleSetup>();
            _view = FindFirstObjectByType<BattleCameraController>();
            _tex = new Texture2D(1, 1);
            _tex.SetPixel(0, 0, Color.white);
            _tex.Apply();
        }

        // ---------------- 輸入 ----------------
        private void Update()
        {
            // 不論何時都先更新「滑鼠是否在 UI 上」，讓視野控制知道要不要讓路。
            UpdatePointerOverUI();

            if (Manager == null || Grid == null) return;
            if (_view == null) _view = FindFirstObjectByType<BattleCameraController>();

            // 戰鬥結束 / 非玩家回合 → 收起選取 (但仍允許縮放拖曳視野)。
            if (Manager.Result != ObjectiveState.InProgress ||
                !Manager.WaitingForPlayer || Manager.CurrentFaction == null)
            {
                ResetSelection();
                return;
            }
            if (_busy) return;

            HandleKeyboard();
            HandleWorldClick();
        }

        private void HandleKeyboard()
        {
            // 結束我方回合
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.E))
            {
                if (_mode == Mode.ItemList) { UseSelectedItem(); return; } // 道具列表中 Enter = 使用
                Manager.EndPlayerFactionTurn();
                ResetSelection();
                return;
            }

            if (_mode == Mode.ItemList)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow)) MoveItemSelection(-1);
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow)) MoveItemSelection(+1);
                else if (Input.GetKeyDown(KeyCode.Escape)) _mode = Mode.Menu;
                return;
            }

            // 取消：選單/選目標 → 收回選取
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_mode == Mode.MoveTargeting || _mode == Mode.AttackTargeting) _mode = Mode.Menu;
                else ResetSelection();
                return;
            }

            // 快捷鍵：空白鍵待機
            if (Input.GetKeyDown(KeyCode.Space) && _selected != null && !_selected.HasActed)
                DoWait();
        }

        private void HandleWorldClick()
        {
            if (Input.GetMouseButtonDown(0))
                _pressOverUI = BattleCameraController.PointerOverUI;

            if (!Input.GetMouseButtonUp(0)) return;

            // 這次左鍵是「拖曳視野」或「點在 UI 上」→ 不當成場上點擊。
            bool dragged = _view != null && _view.DraggedThisPress;
            if (dragged || _pressOverUI || BattleCameraController.PointerOverUI) return;

            var coord = MouseToCoord();
            if (!Grid.InBounds(coord)) { if (_mode == Mode.Menu || _mode == Mode.Idle) ResetSelection(); return; }
            var tile = Grid.GetTile(coord);

            switch (_mode)
            {
                case Mode.Idle:
                case Mode.Menu:
                    TrySelectAt(tile);
                    break;

                case Mode.MoveTargeting:
                    if (_selected != null && !_selected.HasMoved && _reachable.Contains(coord))
                        StartCoroutine(DoMove(_selected, coord));
                    else
                        CancelTargetingOrReselect(tile);
                    break;

                case Mode.AttackTargeting:
                    if (_selected != null && tile.occupant != null && _attackTargets.Contains(tile.occupant))
                        DoAttack(tile.occupant);
                    else
                        CancelTargetingOrReselect(tile);
                    break;
            }
        }

        private void TrySelectAt(Tile tile)
        {
            if (tile != null && tile.occupant != null &&
                tile.occupant.faction == Manager.CurrentFaction && !tile.occupant.HasFinishedTurn)
                Select(tile.occupant);
            else
                ResetSelection();
        }

        /// <summary>選目標模式下點到別處：若點到另一個我方可動單位就改選它，否則退回指令選單。</summary>
        private void CancelTargetingOrReselect(Tile tile)
        {
            if (tile != null && tile.occupant != null && tile.occupant != _selected &&
                tile.occupant.faction == Manager.CurrentFaction && !tile.occupant.HasFinishedTurn)
                Select(tile.occupant);
            else
                _mode = Mode.Menu;
        }

        // ---------------- 玩家行動 ----------------
        private void Select(Unit unit)
        {
            _selected = unit;
            _mode = Mode.Menu;
            RefreshReachable();
        }

        private IEnumerator DoMove(Unit unit, GridCoord target)
        {
            _busy = true;
            yield return Manager.PlayerMove(unit, target); // 內含 Dijkstra 找路 + 移動動畫 + 撿道具
            _busy = false;
            _mode = Mode.Menu;   // 移動後回到選單 (可能還能攻擊/用道具)
            RefreshReachable();
            if (_selected != null && _selected.HasFinishedTurn) ResetSelection();
        }

        private void DoAttack(Unit target)
        {
            Manager.PlayerAttack(_selected, target);
            _mode = Mode.Menu;
            RefreshReachable();
            if (_selected != null && _selected.HasFinishedTurn) ResetSelection();
        }

        private void DoWait()
        {
            Manager.PlayerWait(_selected);
            ResetSelection();
        }

        private void UseSelectedItem()
        {
            if (_selected == null) return;
            var items = _selected.Inventory.Items;
            if (_itemIndex < 0 || _itemIndex >= items.Count) return;

            var item = items[_itemIndex];
            Manager.PlayerUseItem(_selected, item, _selected); // 範例：對自己使用 (回血/回魔)
            _mode = Mode.Menu;
            RefreshReachable();
            if (_selected.HasFinishedTurn) ResetSelection();
        }

        private void MoveItemSelection(int delta)
        {
            if (_selected == null) return;
            int count = _selected.Inventory.Count;
            if (count == 0) return;
            _itemIndex = Mathf.Clamp(_itemIndex + delta, 0, count - 1);
            EnsureItemVisible(count);
        }

        private void EnsureItemVisible(int count)
        {
            if (_itemIndex < _itemScroll) _itemScroll = _itemIndex;
            if (_itemIndex > _itemScroll + ItemVisible - 1) _itemScroll = _itemIndex - ItemVisible + 1;
            _itemScroll = Mathf.Clamp(_itemScroll, 0, Mathf.Max(0, count - ItemVisible));
        }

        private void ResetSelection()
        {
            _selected = null;
            _mode = Mode.Idle;
            _reachable.Clear();
            _attackTargets.Clear();
        }

        private void RefreshReachable()
        {
            _reachable.Clear();
            if (_selected == null || _selected.HasMoved) return;

            var range = Pathfinder.ComputeMovementRange(Grid, _selected.Coord, _selected.Stats.mov, _selected);
            foreach (var c in Pathfinder.GetStoppableCells(Grid, range, _selected))
                _reachable.Add(c);
        }

        private void RecomputeAttackTargets()
        {
            _attackTargets.Clear();
            if (_selected == null) return;
            foreach (var f in Manager.factions)
            {
                if (f == null) continue;
                foreach (var u in f.LivingUnits)
                    if (AttackAction.CanAttack(Grid, _selected, u))
                        _attackTargets.Add(u);
            }
        }

        private bool HasAnyAttackTarget()
        {
            if (_selected == null || _selected.HasActed) return false;
            foreach (var f in Manager.factions)
            {
                if (f == null) continue;
                foreach (var u in f.LivingUnits)
                    if (AttackAction.CanAttack(Grid, _selected, u)) return true;
            }
            return false;
        }

        private GridCoord MouseToCoord()
        {
            var cam = Camera.main;
            float zDist = Mathf.Abs(cam.transform.position.z);
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, zDist));
            return Grid.WorldToCoord(world);
        }

        /// <summary>用上一幀 OnGUI 蒐集到的面板矩形，判斷滑鼠是否停在 UI 上，寫回視野控制。</summary>
        private void UpdatePointerOverUI()
        {
            Vector2 gui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            bool over = false;
            for (int i = 0; i < _uiRects.Count; i++)
                if (_uiRects[i].Contains(gui)) { over = true; break; }
            BattleCameraController.PointerOverUI = over;
        }

        // ---------------- 繪製 (OnGUI) ----------------
        private void OnGUI()
        {
            if (Grid == null) return;
            _label ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, wordWrap = true };
            _uiRects.Clear();

            // 地形
            foreach (var kv in Grid.Tiles)
            {
                var rect = CellRect(kv.Key);
                var type = kv.Value.type;
                DrawBox(rect, type != null ? type.debugColor : Color.gray);
            }

            // 可移動範圍高亮 (只在選移動格時顯示)
            if (_mode == Mode.MoveTargeting)
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

            // 可攻擊目標高亮 (選攻擊目標時)
            if (_mode == Mode.AttackTargeting)
                foreach (var t in _attackTargets)
                    if (t != null && t.IsAlive)
                        DrawBorder(Shrink(CellRect(t.Coord), 2), new Color(1f, 0.3f, 0.3f), 3f);

            DrawHud();
            DrawUnitMenu();
            DrawItemList();
        }

        // ---------------- 戰況面板 (可收合) ----------------
        private void DrawHud()
        {
            if (_hudCollapsed)
            {
                // 收合時：左上角側邊欄按鈕，按一下展開。
                var tab = new Rect(6, 6, 96, 28);
                _uiRects.Add(tab);
                if (GUI.Button(tab, "▶ 戰況")) _hudCollapsed = false;
                return;
            }

            var area = new Rect(10, 10, 330, 238);
            _uiRects.Add(area);
            GUILayout.BeginArea(area, GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>BlackChess SRPG 範例戰鬥</b>");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("—", GUILayout.Width(26), GUILayout.Height(20))) _hudCollapsed = true;
            GUILayout.EndHorizontal();

            if (Manager != null)
            {
                GUILayout.Label($"回合 Round: {Manager.Round}");
                GUILayout.Label($"目前勢力: {(Manager.CurrentFaction != null ? Manager.CurrentFaction.factionName : "-")}");

                if (Manager.Result == ObjectiveState.Achieved) GUILayout.Label("<color=#7CFF7C><b>勝利！殲滅所有敵人</b></color>");
                else if (Manager.Result == ObjectiveState.Failed) GUILayout.Label("<color=#FF7C7C><b>戰敗…</b></color>");
                else if (Manager.WaitingForPlayer) GUILayout.Label("<color=#7CC8FF>你的回合 — 選取單位下令</color>");
                else GUILayout.Label("AI 行動中…");
            }
            GUILayout.Space(6);
            GUILayout.Label("左鍵: 選單位 → 指令選單 (攻擊/移動/待機/道具)");
            GUILayout.Label("左鍵拖曳: 平移視野   滾輪: 縮放");
            GUILayout.Label("Enter/E: 結束我方回合   Esc: 取消");
            GUILayout.EndArea();
        }

        // ---------------- 單位指令選單 ----------------
        private void DrawUnitMenu()
        {
            if (_selected == null || _mode == Mode.ItemList) return;

            const float mw = 96f, bh = 28f, gap = 5f;
            float mh = 4 * bh + 5 * gap + 4f;

            var ur = CellRect(_selected.Coord);
            float mx = ur.xMax + 8f;
            float my = ur.y;
            if (mx + mw > Screen.width) mx = ur.x - mw - 8f;   // 右邊放不下 → 改放左邊
            mx = Mathf.Clamp(mx, 0f, Screen.width - mw);
            my = Mathf.Clamp(my, 0f, Screen.height - mh);

            var menu = new Rect(mx, my, mw, mh);
            _uiRects.Add(menu);
            GUI.Box(menu, GUIContent.none);

            float bx = mx + gap, bw = mw - gap * 2f, by = my + gap;

            // 攻擊
            if (MenuButton(new Rect(bx, by, bw, bh), "攻擊", HasAnyAttackTarget()))
            {
                RecomputeAttackTargets();
                _mode = _attackTargets.Count > 0 ? Mode.AttackTargeting : Mode.Menu;
            }
            by += bh + gap;

            // 移動
            if (MenuButton(new Rect(bx, by, bw, bh), "移動", !_selected.HasMoved))
            {
                RefreshReachable();
                _mode = Mode.MoveTargeting;
            }
            by += bh + gap;

            // 待機
            if (MenuButton(new Rect(bx, by, bw, bh), "待機", !_selected.HasActed))
                DoWait();
            by += bh + gap;

            // 道具
            if (MenuButton(new Rect(bx, by, bw, bh), "道具", !_selected.HasActed && _selected.Inventory.Count > 0))
            {
                _itemIndex = 0; _itemScroll = 0;
                _mode = Mode.ItemList;
            }

            // 選目標模式時給個提示
            if (_mode == Mode.MoveTargeting || _mode == Mode.AttackTargeting)
            {
                var hint = new Rect(mx, my + mh + 4f, Mathf.Max(mw, 150f), 22f);
                _uiRects.Add(hint);
                GUI.Box(hint, _mode == Mode.MoveTargeting ? "點藍色格移動 (Esc取消)" : "點紅框敵人攻擊 (Esc取消)");
            }
        }

        private bool MenuButton(Rect r, string text, bool enabled)
        {
            bool prev = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(r, text);
            GUI.enabled = prev;
            return clicked;
        }

        // ---------------- 道具列表 (一行、可捲動、高光淡入淡出) ----------------
        private void DrawItemList()
        {
            if (_selected == null || _mode != Mode.ItemList) return;

            var items = _selected.Inventory.Items;
            int count = items.Count;

            const float slotW = 118f, slotH = 66f, pad = 8f, arrowW = 26f;
            float panelW = arrowW * 2f + ItemVisible * slotW + pad * 2f + 4f;
            float panelH = slotH + 44f;
            float px = (Screen.width - panelW) * 0.5f;
            float py = Screen.height - panelH - 90f; // 留出底部縮放 Slider 的空間

            var panel = new Rect(px, py, panelW, panelH);
            _uiRects.Add(panel);
            GUI.Box(panel, GUIContent.none);

            var title = new Rect(px, py + 4f, panelW, 20f);
            GUI.Label(title, "道具 — 方向鍵/滾輪選擇，Enter 或點擊使用，Esc 返回", _label);

            Event e = Event.current;

            // 面板上滾輪 → 移動選取 (視野縮放已因 PointerOverUI 停用)
            if (e.type == EventType.ScrollWheel && panel.Contains(e.mousePosition))
            {
                MoveItemSelection(e.delta.y > 0f ? 1 : -1);
                e.Use();
            }

            float slotY = py + 28f;

            // 左箭頭
            var leftArrow = new Rect(px + pad, slotY, arrowW, slotH);
            if (GUI.Button(leftArrow, "◀") && count > 0) MoveItemSelection(-1);

            // 道具欄位
            float startX = px + pad + arrowW + 2f;
            for (int slot = 0; slot < ItemVisible; slot++)
            {
                int index = _itemScroll + slot;
                var r = new Rect(startX + slot * slotW, slotY, slotW - 4f, slotH);

                if (index < 0 || index >= count)
                {
                    DrawBox(r, new Color(0f, 0f, 0f, 0.15f)); // 空欄位
                    continue;
                }

                var item = items[index];
                bool hovered = r.Contains(e.mousePosition);
                if (hovered) _itemIndex = index; // 滑鼠 hover 即選取

                // 底色
                DrawBox(r, new Color(0.12f, 0.12f, 0.16f, 0.85f));

                // 高光淡入淡出 (選取中的欄位)
                if (index == _itemIndex)
                {
                    float a = 0.25f + 0.35f * Mathf.PingPong(Time.unscaledTime * 2f, 1f);
                    DrawBox(r, new Color(1f, 0.85f, 0.3f, a));
                    DrawBorder(r, new Color(1f, 0.9f, 0.4f), 2f);
                }

                DrawLabel(new Rect(r.x, r.y + 6f, r.width, r.height - 12f),
                    $"{item.itemName}\n{EffectText(item)}", Color.white);

                // 點擊使用
                if (hovered && e.type == EventType.MouseDown && e.button == 0)
                {
                    _itemIndex = index;
                    UseSelectedItem();
                    e.Use();
                }
            }

            // 右箭頭
            var rightArrow = new Rect(px + panelW - pad - arrowW, slotY, arrowW, slotH);
            if (GUI.Button(rightArrow, "▶") && count > 0) MoveItemSelection(1);
        }

        private static string EffectText(ItemData item)
        {
            switch (item.effectType)
            {
                case ItemEffectType.Heal: return $"回復HP {item.amount}";
                case ItemEffectType.RestoreMP: return $"回復MP {item.amount}";
                default: return "增益";
            }
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
