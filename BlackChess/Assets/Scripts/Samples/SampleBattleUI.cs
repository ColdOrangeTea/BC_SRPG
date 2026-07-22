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
    /// 棋盤、單位、範圍高亮與指令選單用 OnGUI 畫 (不依賴美術資源)；道具列表則改用真正的 uGUI
    /// (你的 BattleItemCanva / UI_ItemCell prefab)，由 <see cref="BattleItemListUI"/> 管理。
    ///
    /// 操作方式：
    ///   • 左鍵點自己的單位 → 選取，於旁邊彈出指令選單 (攻擊 / 移動 / 待機 / 道具)。選單可用滑鼠拖曳移動。
    ///   • 選了某個行動後，指令選單會收起；按 Esc 取消該行動則再度顯示。
    ///   • 攻擊 → 點紅框敵人；移動 → 點藍色範圍；道具 → 開啟捲動道具列表；待機 → 防禦並結束。
    ///   • Enter / E → 結束玩家勢力回合。   Esc → 取消目前選取/選單。
    ///   • 左鍵拖曳空白處 → 平移視野；滾輪 → 縮放 (由 BattleCameraController 處理)。
    /// </summary>
    public class SampleBattleUI : MonoBehaviour
    {
        private enum Mode { Idle, Menu, MoveTargeting, AttackTargeting, ItemList }

        private const int MenuWindowId = 9701;

        private SampleBattleSetup _setup;
        private BattleCameraController _view;
        private BattleItemListUI _itemList;
        private BattleGrid Grid => _setup != null ? _setup.Grid : null;
        private BattleManager Manager => _setup != null ? _setup.Manager : null;

        private Unit _selected;
        private Mode _mode = Mode.Idle;
        private readonly HashSet<GridCoord> _reachable = new HashSet<GridCoord>();
        private readonly List<Unit> _attackTargets = new List<Unit>();
        private bool _busy;

        // 指令選單 (可拖曳的視窗)
        private Rect _menuRect;
        private bool _menuPlaced;

        // 戰況面板收合
        private bool _hudCollapsed;

        // 點擊 vs 拖曳：記錄按下當下是否在 UI 上
        private bool _pressOverUI;

        // 供「滑鼠是否停在 UI 上」判定用：每次 OnGUI 重建各面板的螢幕矩形 (GUI 座標)
        private readonly List<Rect> _uiRects = new List<Rect>();

        private Texture2D _tex;   // 1x1 白貼圖，配合 GUI.color 畫任意顏色方塊
        private GUIStyle _label;
        private GUIStyle _menuTitle;

        private void Awake()
        {
            _setup = FindFirstObjectByType<SampleBattleSetup>();
            _view = FindFirstObjectByType<BattleCameraController>();
            _itemList = GetComponent<BattleItemListUI>();
            if (_itemList == null) _itemList = gameObject.AddComponent<BattleItemListUI>();

            _tex = new Texture2D(1, 1);
            _tex.SetPixel(0, 0, Color.white);
            _tex.Apply();
        }

        // ---------------- 輸入 ----------------
        private void Update()
        {
            UpdatePointerOverUI(); // 隨時更新，讓視野控制知道要不要讓路

            if (Manager == null || Grid == null) return;
            if (_view == null) _view = FindFirstObjectByType<BattleCameraController>();

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
            // 道具列表開啟時，鍵盤交給列表自己處理 (方向鍵/Enter/Esc)。
            if (_itemList != null && _itemList.IsOpen) return;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.E))
            {
                Manager.EndPlayerFactionTurn();
                ResetSelection();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_mode == Mode.MoveTargeting || _mode == Mode.AttackTargeting) _mode = Mode.Menu;
                else ResetSelection();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space) && _selected != null && !_selected.HasActed && _mode == Mode.Menu)
                DoWait();
        }

        private void HandleWorldClick()
        {
            // 道具列表開啟時，點擊交給 uGUI (EventSystem)。
            if (_itemList != null && _itemList.IsOpen) return;

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

        private void CancelTargetingOrReselect(Tile tile)
        {
            if (tile != null && tile.occupant != null && tile.occupant != _selected &&
                tile.occupant.faction == Manager.CurrentFaction && !tile.occupant.HasFinishedTurn)
                Select(tile.occupant);
            else
                _mode = Mode.Menu; // 回到指令選單 (選單重新顯示)
        }

        // ---------------- 玩家行動 ----------------
        private void Select(Unit unit)
        {
            _selected = unit;
            _mode = Mode.Menu;
            _menuPlaced = false; // 換單位 → 選單重新貼到新單位旁
            RefreshReachable();
        }

        private IEnumerator DoMove(Unit unit, GridCoord target)
        {
            _busy = true;
            yield return Manager.PlayerMove(unit, target); // 內含 Dijkstra 找路 + 移動動畫 + 撿道具
            _busy = false;
            _mode = Mode.Menu;
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

        private void OpenItemList()
        {
            if (_selected == null || _itemList == null) return;
            _mode = Mode.ItemList; // 選單收起，改顯示道具列表
            _itemList.Open(_selected, OnItemChosen, OnItemCancelled);
        }

        private void OnItemChosen(ItemData item)
        {
            if (_selected != null && item != null)
                Manager.PlayerUseItem(_selected, item, _selected); // 範例：對自己使用 (回血/回魔)

            _itemList.Close();
            _mode = Mode.Menu;
            RefreshReachable();
            if (_selected != null && _selected.HasFinishedTurn) ResetSelection();
        }

        private void OnItemCancelled()
        {
            _itemList.Close();
            _mode = Mode.Menu; // 取消 → 回到指令選單
        }

        private void ResetSelection()
        {
            if (_itemList != null && _itemList.IsOpen) _itemList.Close();
            _selected = null;
            _mode = Mode.Idle;
            _menuPlaced = false;
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
            if (_itemList != null && _itemList.IsOpen) { BattleCameraController.PointerOverUI = true; return; }

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
            _menuTitle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold };
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
            DrawTargetingHint();
        }

        // ---------------- 戰況面板 (可收合) ----------------
        private void DrawHud()
        {
            if (_hudCollapsed)
            {
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
            GUILayout.Label("左鍵: 選單位 → 指令選單 (可拖曳)");
            GUILayout.Label("左鍵拖曳: 平移視野   滾輪: 縮放");
            GUILayout.Label("Enter/E: 結束我方回合   Esc: 取消");
            GUILayout.EndArea();
        }

        // ---------------- 單位指令選單 (可拖曳視窗；只在 Menu 模式顯示) ----------------
        private void DrawUnitMenu()
        {
            if (_selected == null || _mode != Mode.Menu) return;

            if (!_menuPlaced) { PlaceMenu(); _menuPlaced = true; }

            _menuRect = GUI.Window(MenuWindowId, _menuRect, DrawMenuWindow, GUIContent.none);
            _menuRect.x = Mathf.Clamp(_menuRect.x, 0f, Screen.width - _menuRect.width);
            _menuRect.y = Mathf.Clamp(_menuRect.y, 0f, Screen.height - _menuRect.height);
            _uiRects.Add(_menuRect);
        }

        private void PlaceMenu()
        {
            const float mw = 108f;
            float mh = MenuHeight();
            var ur = CellRect(_selected.Coord);
            float mx = ur.xMax + 8f;
            float my = ur.y;
            if (mx + mw > Screen.width) mx = ur.x - mw - 8f; // 右邊放不下 → 改放左邊
            mx = Mathf.Clamp(mx, 0f, Screen.width - mw);
            my = Mathf.Clamp(my, 0f, Screen.height - mh);
            _menuRect = new Rect(mx, my, mw, mh);
        }

        private const float MenuTitleH = 20f, MenuBtnH = 28f, MenuGap = 5f;
        private static float MenuHeight() => MenuTitleH + 4 * MenuBtnH + 5 * MenuGap;

        private void DrawMenuWindow(int id)
        {
            float w = _menuRect.width;
            GUI.Label(new Rect(0, 2, w, MenuTitleH - 2), "≡ 行動", _menuTitle);

            float bx = MenuGap, bw = w - MenuGap * 2f, by = MenuTitleH + MenuGap;

            if (MenuButton(new Rect(bx, by, bw, MenuBtnH), "攻擊", HasAnyAttackTarget()))
            {
                RecomputeAttackTargets();
                _mode = _attackTargets.Count > 0 ? Mode.AttackTargeting : Mode.Menu;
            }
            by += MenuBtnH + MenuGap;

            if (MenuButton(new Rect(bx, by, bw, MenuBtnH), "移動", !_selected.HasMoved))
            {
                RefreshReachable();
                _mode = Mode.MoveTargeting;
            }
            by += MenuBtnH + MenuGap;

            if (MenuButton(new Rect(bx, by, bw, MenuBtnH), "待機", !_selected.HasActed))
                DoWait();
            by += MenuBtnH + MenuGap;

            if (MenuButton(new Rect(bx, by, bw, MenuBtnH), "道具", !_selected.HasActed && _selected.Inventory.Count > 0))
                OpenItemList();

            // 只讓標題列可拖曳，避免拖動時誤觸按鈕。
            GUI.DragWindow(new Rect(0, 0, w, MenuTitleH));
        }

        private bool MenuButton(Rect r, string text, bool enabled)
        {
            bool prev = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(r, text);
            GUI.enabled = prev;
            return clicked;
        }

        private void DrawTargetingHint()
        {
            if (_selected == null) return;
            if (_mode != Mode.MoveTargeting && _mode != Mode.AttackTargeting) return;

            var ur = CellRect(_selected.Coord);
            var hint = new Rect(Mathf.Clamp(ur.x, 0, Screen.width - 220), Mathf.Clamp(ur.yMax + 4, 0, Screen.height - 24), 220, 22);
            _uiRects.Add(hint);
            GUI.Box(hint, _mode == Mode.MoveTargeting ? "點藍色格移動 (Esc 取消)" : "點紅框敵人攻擊 (Esc 取消)");
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
