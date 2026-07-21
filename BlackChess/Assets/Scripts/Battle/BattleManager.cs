using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BlackChess.SRPG.Actions;
using BlackChess.SRPG.AI;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Objectives;
using BlackChess.SRPG.Units;
using UnityEngine;

namespace BlackChess.SRPG.Battle
{
    /// <summary>
    /// 戰鬥總管。把所有系統串起來，跑「大回合 → 各勢力依 SPD 順序行動 → 檢查目標」的主迴圈。
    ///
    /// 迴圈骨架：
    ///   while (目標尚未分出勝負)
    ///     每個大回合 (Round)：
    ///       1. 依各勢力 SPD 總和排出勢力順序 (TurnOrderSystem)。
    ///       2. 逐一讓每個勢力行動：
    ///            - AI 勢力：每個單位跑自己的 UnitAI，自動執行移動/攻擊/待機。
    ///            - 玩家勢力：交給玩家操作，等玩家呼叫 EndPlayerFactionTurn 才換手。
    ///       3. 每次單位行動後檢查戰鬥目標 → 若已達成/失敗則結束。
    ///
    /// 玩家操作透過本類別提供的 Player* 方法驅動 (可由你的點擊 UI 呼叫)。
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        [Header("場景參照")]
        public BattleGrid grid;
        [Tooltip("參戰的所有勢力 (玩家 / 盟軍 / 敵人…)。")]
        public List<Faction> factions = new List<Faction>();
        [Tooltip("這場戰鬥的目標 (可多個)。")]
        public List<BattleObjective> objectives = new List<BattleObjective>();

        [Header("節奏")]
        [Tooltip("AI 每個動作之間的停頓秒數 (讓玩家看得清楚)。")]
        public float aiActionDelay = 0.35f;

        [Header("通用 Prefab 設定")]
        [Tooltip("開啟後，未在 Inspector 指定的 grid / factions / objectives 會在開戰時自動從場景收集。\n" +
                 "這讓本物件可做成『放到任何戰鬥地圖都能用』的通用回合管理 Prefab —— Prefab 無法引用場景實例，改為執行時自動尋找。")]
        public bool autoCollectFromScene = true;

        [Tooltip("開啟後，開戰時會把每個單位放到其 Unit.startCoord 指定的格子。")]
        public bool autoPlaceUnits = true;

        [Tooltip("是否自動開始戰鬥 (Start 時)。若想由劇情/過場手動觸發，關閉後改呼叫 StartBattle()。")]
        public bool autoStart = true;

        // ---- 執行時期狀態 ----
        public int Round { get; private set; }
        public Faction CurrentFaction { get; private set; }
        public bool WaitingForPlayer { get; private set; }
        public ObjectiveState Result { get; private set; } = ObjectiveState.InProgress;

        private BattleContext _context;
        private bool _playerFactionEnded;
        private bool _battleOver;

        // ---- 事件 (給 UI / 音效 掛鉤) ----
        public event Action OnBattleStart;
        public event Action<int> OnRoundStart;
        public event Action<Faction> OnFactionTurnStart;
        public event Action<Faction> OnFactionTurnEnd;
        public event Action<ObjectiveState> OnBattleEnd;

        public BattleContext Context => _context;

        private void Start()
        {
            if (autoStart) StartBattle();
        }

        public void StartBattle()
        {
            if (autoCollectFromScene) CollectSceneReferences();
            if (autoPlaceUnits) PlaceAllUnits();

            _context = new BattleContext(grid, factions);
            foreach (var f in factions)
                foreach (var u in f.Units)
                    u.OnDied += HandleUnitDied;

            OnBattleStart?.Invoke();
            StartCoroutine(BattleLoop());
        }

        /// <summary>
        /// 通用 Prefab 的關鍵：把這場地圖的 grid / factions / objectives 從場景自動抓進來。
        /// 只有在 Inspector 沒指定時才自動填，所以你仍可手動覆寫個別引用。
        /// </summary>
        private void CollectSceneReferences()
        {
            if (grid == null)
                grid = FindFirstObjectByType<BattleGrid>();

            if (factions == null || factions.Count == 0)
                factions = FindObjectsByType<Faction>(FindObjectsSortMode.None)
                    .OrderBy(f => f.factionName).ToList();

            if (objectives == null || objectives.Count == 0)
                objectives = FindObjectsByType<BattleObjective>(FindObjectsSortMode.None).ToList();
        }

        /// <summary>把每個單位放到自己的 startCoord。讓關卡設計者只要在單位上填座標即可佈陣。</summary>
        private void PlaceAllUnits()
        {
            if (grid == null) return;
            foreach (var f in factions)
                foreach (var u in f.Units)
                    if (u != null && u.placeAtStartCoord)
                        grid.PlaceUnit(u, u.startCoord);
        }

        private void HandleUnitDied(Unit u)
        {
            grid.RemoveUnit(u);
            CheckObjectives();
        }

        // ---------- 主迴圈 ----------

        private IEnumerator BattleLoop()
        {
            while (!_battleOver)
            {
                Round++;
                OnRoundStart?.Invoke(Round);

                // 每個大回合重算勢力順序 (單位可能已陣亡使 SPD 總和改變)。
                var order = TurnOrderSystem.BuildOrder(factions);

                foreach (var faction in order)
                {
                    if (_battleOver) break;
                    if (faction.IsDefeated) continue;

                    CurrentFaction = faction;
                    OnFactionTurnStart?.Invoke(faction);

                    // 回合開始：重置該勢力所有單位的行動旗標。
                    foreach (var u in faction.LivingUnits)
                        u.BeginTurn();

                    if (faction.control == FactionControl.Player)
                        yield return PlayerFactionTurn(faction);
                    else
                        yield return AIFactionTurn(faction);

                    OnFactionTurnEnd?.Invoke(faction);
                    CheckObjectives();
                    if (_battleOver) break;
                }
            }
        }

        // ---------- AI 勢力回合 ----------

        private IEnumerator AIFactionTurn(Faction faction)
        {
            // 對每個存活單位輪流執行其 AI 計畫。ToList 複製一份，避免行動中集合被改動。
            foreach (var unit in faction.LivingUnits.ToList())
            {
                if (_battleOver) break;
                if (unit == null || !unit.IsAlive) continue;

                var ai = unit.GetComponent<UnitAI>();
                if (ai == null)
                {
                    // 沒掛 AI 的單位就待機。
                    WaitAction.Execute(unit);
                    continue;
                }

                var plan = ai.DecideAction(_context);
                yield return ExecutePlan(unit, plan);
                if (aiActionDelay > 0f) yield return new WaitForSeconds(aiActionDelay);
                CheckObjectives();
            }
        }

        /// <summary>把 AI 的 UnitPlan 實際執行成移動 + 攻擊 / 待機。</summary>
        private IEnumerator ExecutePlan(Unit unit, UnitPlan plan)
        {
            if (plan.move && !unit.HasMoved)
            {
                if (MoveAction.CanMoveTo(grid, unit, plan.moveTarget, out _, out var path))
                    yield return MoveAction.Execute(grid, unit, path);
            }

            if (plan.attack && plan.attackTarget != null && !unit.HasActed)
            {
                AttackAction.Execute(unit, plan.attackTarget);
            }
            else if (plan.wait && !unit.HasActed)
            {
                WaitAction.Execute(unit);
            }
        }

        // ---------- 玩家勢力回合 ----------

        private IEnumerator PlayerFactionTurn(Faction faction)
        {
            WaitingForPlayer = true;
            _playerFactionEnded = false;

            // 等玩家自行結束回合，或所有單位都行動完畢。
            while (!_playerFactionEnded && !_battleOver)
            {
                if (faction.LivingUnits.All(u => u.HasFinishedTurn))
                    break;
                yield return null;
            }

            WaitingForPlayer = false;
        }

        /// <summary>玩家 UI 呼叫：結束目前玩家勢力的回合 (提前換手)。</summary>
        public void EndPlayerFactionTurn() => _playerFactionEnded = true;

        // ---------- 玩家操作 API (供點擊 UI 呼叫) ----------

        /// <summary>玩家指令：把單位移動到目標格。回傳協程 (可 StartCoroutine 等它跑完)。</summary>
        public IEnumerator PlayerMove(Unit unit, GridCoord target)
        {
            if (!WaitingForPlayer || unit.faction != CurrentFaction) yield break;
            if (MoveAction.CanMoveTo(grid, unit, target, out _, out var path))
            {
                yield return MoveAction.Execute(grid, unit, path);
                ItemAction.TryPickUpNearby(grid, unit); // 移動後自動嘗試撿腳邊道具
                CheckObjectives();
            }
        }

        /// <summary>玩家指令：以單位攻擊目標。</summary>
        public void PlayerAttack(Unit unit, Unit target)
        {
            if (!WaitingForPlayer || unit.faction != CurrentFaction) return;
            AttackAction.Execute(unit, target);
            CheckObjectives();
        }

        /// <summary>玩家指令：待機 (防禦)。</summary>
        public void PlayerWait(Unit unit)
        {
            if (!WaitingForPlayer || unit.faction != CurrentFaction) return;
            WaitAction.Execute(unit);
        }

        /// <summary>玩家指令：使用道具。</summary>
        public void PlayerUseItem(Unit unit, Items.ItemData item, Unit target)
        {
            if (!WaitingForPlayer || unit.faction != CurrentFaction) return;
            ItemAction.Use(unit, item, target);
        }

        // ---------- 目標判定 ----------

        private void CheckObjectives()
        {
            if (_battleOver) return;

            var state = EvaluateObjectives();
            if (state == ObjectiveState.InProgress) return;

            _battleOver = true;
            Result = state;
            OnBattleEnd?.Invoke(state);
        }

        /// <summary>
        /// 綜合所有目標：任一失敗 → 整場失敗；全部達成 → 勝利；否則進行中。
        /// (若沒有設定任何目標，預設維持進行中，可自行改成預設殲滅。)
        /// </summary>
        private ObjectiveState EvaluateObjectives()
        {
            if (objectives == null || objectives.Count == 0) return ObjectiveState.InProgress;

            bool allAchieved = true;
            foreach (var obj in objectives)
            {
                if (obj == null) continue;
                var s = obj.Evaluate(_context);
                if (s == ObjectiveState.Failed) return ObjectiveState.Failed;
                if (s != ObjectiveState.Achieved) allAchieved = false;
            }
            return allAchieved ? ObjectiveState.Achieved : ObjectiveState.InProgress;
        }
    }
}
