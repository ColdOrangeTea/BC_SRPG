using System;
using System.Collections;
using System.Collections.Generic;
using BlackChess.SRPG.Core;
using UnityEngine;

namespace BlackChess.SRPG.Units
{
    /// <summary>
    /// 棋盤上的「單位」(Unit)。玩家角色、盟軍、敵人都用這個腳本，差別只在：
    ///   - faction.control 是 Player 還是 AI
    ///   - AI 單位身上額外掛一個 IUnitAI 行為腳本 (由 BattleManager 讀取)
    ///
    /// 一個單位一回合的行動流程：可以「移動」一次 + 「一個主行動 (攻擊/待機/道具)」。
    /// HasMoved / HasActed 兩個旗標記錄它這回合還能不能做這些事。
    /// </summary>
    public class Unit : MonoBehaviour
    {
        [Header("身分")]
        public string unitName = "Unit";
        [Tooltip("所屬勢力。若留空會嘗試從父物件自動抓取。")]
        public Faction faction;

        [Header("數值")]
        public UnitStats stats = new UnitStats();

        [Header("開場位置")]
        [Tooltip("戰鬥開始時要放置的格座標。BattleManager 會在戰鬥開始前自動把單位放到這一格。")]
        public GridCoord startCoord;
        [Tooltip("是否於戰鬥開始時自動放置到 startCoord。若你想用其他方式擺放單位，可關閉。")]
        public bool placeAtStartCoord = true;

        [Header("移動表現")]
        [Tooltip("沿路徑移動時，每格花費的秒數 (純表演，不影響邏輯)。")]
        public float moveStepDuration = 0.12f;

        // ---- 執行時期狀態 ----
        public GridCoord Coord { get; private set; }
        public bool HasMoved { get; private set; }
        public bool HasActed { get; private set; }
        /// <summary>待機 = 自動視為防禦。防禦時受到的傷害減半 (可在 TakeDamage 調整)。</summary>
        public bool IsDefending { get; private set; }
        public bool IsMoving { get; private set; }

        public UnitStats Stats => stats;
        public bool IsAlive => stats.IsAlive;
        public Inventory Inventory { get; } = new Inventory();

        /// <summary>單位死亡時觸發 (BattleManager 監聽以更新戰鬥目標)。</summary>
        public event Action<Unit> OnDied;
        /// <summary>HP 變動時觸發 (給血條 UI 用)。</summary>
        public event Action<Unit> OnHealthChanged;

        private void Awake()
        {
            if (faction == null) faction = GetComponentInParent<Faction>();
            if (faction != null) faction.Register(this);
        }

        /// <summary>由 BattleGrid 呼叫，內部同步座標 (不直接動棋盤，避免雙向遞迴)。</summary>
        public void SetCoordInternal(GridCoord c) => Coord = c;

        // ---------- 敵我判定 ----------

        public bool IsHostileTo(Unit other)
        {
            if (other == null || faction == null || other.faction == null) return false;
            return faction.IsHostileTo(other.faction);
        }

        public bool IsFriendlyTo(Unit other)
        {
            if (other == null || faction == null || other.faction == null) return false;
            return faction.IsFriendlyTo(other.faction);
        }

        // ---------- 回合狀態 ----------

        /// <summary>輪到本勢力時，重置本單位的行動旗標。</summary>
        public void BeginTurn()
        {
            HasMoved = false;
            HasActed = false;
            IsDefending = false;
        }

        /// <summary>本單位這回合是否已完全結束 (移動與主行動都用掉了)。</summary>
        public bool HasFinishedTurn => HasMoved && HasActed;

        public void MarkMoved() => HasMoved = true;
        public void MarkActed() => HasActed = true;

        public void Defend()
        {
            IsDefending = true;
            HasMoved = true;
            HasActed = true;
        }

        // ---------- 傷害 / 治療 ----------

        public void TakeDamage(int rawDamage)
        {
            int dmg = Mathf.Max(0, rawDamage);
            if (IsDefending) dmg = Mathf.CeilToInt(dmg * 0.5f); // 防禦減半
            stats.currentHP -= dmg;
            stats.ClampVitals();
            OnHealthChanged?.Invoke(this);
            if (!IsAlive) Die();
        }

        public void Heal(int amount)
        {
            stats.currentHP = Mathf.Min(stats.maxHP, stats.currentHP + Mathf.Max(0, amount));
            OnHealthChanged?.Invoke(this);
        }

        private void Die()
        {
            OnDied?.Invoke(this);
            if (faction != null) faction.Unregister(this);
            gameObject.SetActive(false); // 實際專案可換成死亡動畫再銷毀
        }

        // ---------- 移動表演 ----------

        /// <summary>
        /// 沿著給定路徑逐格移動 (協程，供 BattleManager yield 等待動畫完成)。
        /// 邏輯上的「佔格」由 BattleGrid.PlaceUnit 更新，這裡只負責平滑移動視覺位置。
        /// </summary>
        public IEnumerator MoveAlongPath(BattleGrid grid, List<GridCoord> path)
        {
            if (path == null || path.Count < 2) yield break;
            IsMoving = true;

            // 逐格平滑補間，做出「一格一格移動」的效果 (共用 GridMover)。
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 from = grid.CoordToWorld(path[i - 1]);
                Vector3 to = grid.CoordToWorld(path[i]);
                yield return GridMover.MoveStep(from, to, moveStepDuration, transform);
            }

            // 移動結束，正式登記到最終格。
            grid.PlaceUnit(this, path[path.Count - 1]);
            IsMoving = false;
        }
    }
}
