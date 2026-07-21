using System.Collections.Generic;
using BlackChess.SRPG.AI;
using BlackChess.SRPG.Battle;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Items;
using BlackChess.SRPG.Objectives;
using BlackChess.SRPG.Objects;
using BlackChess.SRPG.Units;
using BlackChess.SRPG.View;
using UnityEngine;

namespace BlackChess.SRPG.Samples
{
    /// <summary>
    /// 【範例】用純程式碼把一整場戰鬥組起來，讓你看清楚每個系統怎麼被使用。
    /// 這相當於「把 BattleSystem.prefab 丟進場景」+ 手動擺盤面 的程式版，方便閱讀學習。
    ///
    /// 流程：
    ///   1. 建立棋盤 BattleGrid，鋪地形 (含黏液、牆)。
    ///   2. 建立三個勢力：玩家 / 盟軍 / 敵人 (盟軍與玩家同 teamId=0，敵人 teamId=1)。
    ///   3. 從 Prefab 生成單位，設定數值與開場座標。
    ///        Unit 是可重複使用的 Prefab —— 生成後再改數值就是不同角色。
    ///   4. 放一個場地道具 (藥水) 與一個可破壞瓶子 (打破掉藥水)。
    ///   5. 設定戰鬥目標 (殲滅所有敵人)。
    ///   6. 建立 BattleManager 並開戰。
    ///
    /// Prefab / 資產以 Inspector 欄位指定；若留空則自動從 Resources 載入 (Assets/SRPG/Resources/)。
    /// 因此本範例「放進場景直接播放」就能跑，不需手動拖曳。
    /// </summary>
    public class SampleBattleSetup : MonoBehaviour
    {
        [Header("盤面尺寸")]
        public int width = 10;
        public int height = 8;

        [Header("Unit / 物件 Prefab (留空則自動從 Resources 載入)")]
        public GameObject playerUnitPrefab;
        public GameObject enemyUnitPrefab;
        public GameObject allyUnitPrefab;
        public GameObject bottlePrefab;

        [Header("資料資產 (留空則自動從 Resources 載入)")]
        public TileType groundType;
        public TileType slimeType;
        public TileType wallType;
        public ItemData potionItem;

        // 供 UI 讀取的執行時期引用
        public BattleGrid Grid { get; private set; }
        public BattleManager Manager { get; private set; }

        private void Start()
        {
            ResolveResources();
            BuildBoard();
            var factions = BuildFactions();
            SpawnUnits(factions);
            SpawnItemsAndObjects();
            var objectives = BuildObjectives();
            BuildManagerAndStart(factions, objectives);
            FrameCamera();
        }

        /// <summary>Inspector 沒指定的 Prefab / 資產，統一從 Resources 補齊。</summary>
        private void ResolveResources()
        {
            if (playerUnitPrefab == null) playerUnitPrefab = Resources.Load<GameObject>("Unit_Player");
            if (enemyUnitPrefab == null) enemyUnitPrefab = Resources.Load<GameObject>("Unit_Enemy");
            if (allyUnitPrefab == null) allyUnitPrefab = Resources.Load<GameObject>("Unit_Ally");
            if (bottlePrefab == null) bottlePrefab = Resources.Load<GameObject>("Prop_Bottle");
            if (groundType == null) groundType = Resources.Load<TileType>("Tile_Ground");
            if (slimeType == null) slimeType = Resources.Load<TileType>("Tile_Slime");
            if (wallType == null) wallType = Resources.Load<TileType>("Tile_Wall");
            if (potionItem == null) potionItem = Resources.Load<ItemData>("Item_Potion");
        }

        // ---------- 1. 棋盤 ----------
        private void BuildBoard()
        {
            var gridGO = new GameObject("BattleGrid");
            Grid = gridGO.AddComponent<BattleGrid>();
            Grid.width = width;
            Grid.height = height;
            Grid.cellSize = 1f;
            Grid.defaultTileType = groundType;
            Grid.BuildGrid(); // 先鋪滿一般地板

            // 黏液區 (移動力/2)：走進去要花 2 點移動力。
            foreach (var c in new[] { new GridCoord(4, 3), new GridCoord(4, 4), new GridCoord(5, 3), new GridCoord(5, 4) })
                Grid.SetTileType(c, slimeType);

            // 一道有缺口的牆 (不可通行)，逼迫尋路繞路 —— 用來觀察 Dijkstra 的效果。
            foreach (var c in new[] { new GridCoord(6, 1), new GridCoord(6, 2), new GridCoord(6, 3), new GridCoord(6, 5), new GridCoord(6, 6) })
                Grid.SetTileType(c, wallType);
        }

        // ---------- 2. 勢力 ----------
        private List<Faction> BuildFactions()
        {
            var player = CreateFaction("Player", FactionControl.Player, teamId: 0, new Color(0.30f, 0.55f, 1f));
            var ally = CreateFaction("Ally", FactionControl.AI, teamId: 0, new Color(0.30f, 0.85f, 0.55f));
            var enemy = CreateFaction("Enemy", FactionControl.AI, teamId: 1, new Color(1f, 0.40f, 0.40f));
            return new List<Faction> { player, ally, enemy };
        }

        private Faction CreateFaction(string name, FactionControl control, int teamId, Color color)
        {
            var go = new GameObject($"Faction_{name}");
            var f = go.AddComponent<Faction>();
            f.factionName = name;
            f.control = control;
            f.teamId = teamId;
            f.factionColor = color;
            return f;
        }

        // ---------- 3. 單位 (從 Prefab 生成) ----------
        private void SpawnUnits(List<Faction> factions)
        {
            Faction player = factions[0], ally = factions[1], enemy = factions[2];

            // 玩家兩名：SPD 合 = 4+4 = 8
            var swordsman = Spawn(playerUnitPrefab, player, "劍士", new GridCoord(1, 1), hp: 20, atk: 6, rng: 1, mov: 4, spd: 4);
            var archer = Spawn(playerUnitPrefab, player, "弓手", new GridCoord(1, 2), hp: 16, atk: 5, rng: 2, mov: 4, spd: 4);

            // 給玩家單位一些起始道具，方便立即測試「道具」指令列表 (正式專案可改成靠撿取取得)。
            if (potionItem != null)
            {
                swordsman.Inventory.Add(potionItem);
                swordsman.Inventory.Add(potionItem);
                archer.Inventory.Add(potionItem);
            }

            // 盟軍一名 (守衛型 AI)：SPD 合 = 6
            var guardian = Spawn(allyUnitPrefab, ally, "守衛", new GridCoord(1, 4), hp: 18, atk: 5, rng: 1, mov: 4, spd: 6);
            var guardAI = guardian.GetComponent<GuardAI>();
            if (guardAI != null) { guardAI.useStartPositionAsGuardPoint = true; guardAI.guardRadius = 5; }

            // 敵人兩名 (進攻型 AI)：SPD 合 = 2+2 = 4
            Spawn(enemyUnitPrefab, enemy, "哥布林A", new GridCoord(8, 1), hp: 14, atk: 5, rng: 1, mov: 4, spd: 2);
            Spawn(enemyUnitPrefab, enemy, "哥布林B", new GridCoord(8, 6), hp: 14, atk: 5, rng: 1, mov: 4, spd: 2);

            // → 勢力行動順序 (SPD 總和)：玩家 8 > 盟軍 6 > 敵人 4，正好對應需求書範例。
        }

        /// <summary>從 Unit Prefab 生成一個單位，掛到勢力底下並設定數值與開場座標。</summary>
        private Unit Spawn(GameObject prefab, Faction faction, string name, GridCoord coord,
                           int hp, int atk, int rng, int mov, int spd)
        {
            // 以勢力為父物件生成 —— Unit.Awake 會自動 GetComponentInParent<Faction>() 完成登記。
            var go = Instantiate(prefab, faction.transform);
            go.name = name;

            var unit = go.GetComponent<Unit>();
            unit.unitName = name;
            unit.stats.maxHP = hp; unit.stats.currentHP = hp;
            unit.stats.atk = atk;
            unit.stats.rng = rng;
            unit.stats.mov = mov;
            unit.stats.spd = spd;

            // 開場座標交給 BattleManager.autoPlaceUnits 統一擺放。
            unit.startCoord = coord;
            unit.placeAtStartCoord = true;
            return unit;
        }

        // ---------- 4. 道具與互動物件 ----------
        private void SpawnItemsAndObjects()
        {
            // (a) 場地上的藥水：不佔格，玩家走近可撿。
            var itemGO = new GameObject("Field_Potion");
            var field = itemGO.AddComponent<FieldItem>();
            field.data = potionItem;
            field.PlaceOnGrid(Grid, new GridCoord(3, 2));

            // (b) 可破壞的瓶子：打破後掉落藥水。dropPrefab 用一個停用的 FieldItem 當範本。
            var bottleGO = Instantiate(bottlePrefab);
            var bottle = bottleGO.GetComponent<InteractableObject>();
            bottle.PlaceOnGrid(Grid, new GridCoord(7, 4));

            var breakable = bottleGO.GetComponent<BreakableBehavior>();
            if (breakable != null)
            {
                var dropTemplate = new GameObject("Potion_DropTemplate");
                var dropField = dropTemplate.AddComponent<FieldItem>();
                dropField.data = potionItem;
                dropTemplate.SetActive(false); // 當作 Prefab 範本，破壞時被 Instantiate 複製
                breakable.dropPrefab = dropField;
            }
        }

        // ---------- 5. 目標 ----------
        private List<BattleObjective> BuildObjectives()
        {
            var go = new GameObject("Objective_Annihilate");
            var obj = go.AddComponent<AnnihilateObjective>();
            obj.enemyTeamId = 1;   // 消滅敵人 (teamId 1)
            obj.protectTeamId = 0; // 我方 (玩家+盟軍) 全滅則失敗
            obj.description = "殲滅所有敵人";
            return new List<BattleObjective> { obj };
        }

        // ---------- 6. 總管並開戰 ----------
        private void BuildManagerAndStart(List<Faction> factions, List<BattleObjective> objectives)
        {
            var go = new GameObject("BattleManager");
            Manager = go.AddComponent<BattleManager>();
            Manager.grid = Grid;
            Manager.factions = factions;
            Manager.objectives = objectives;
            Manager.autoCollectFromScene = false; // 已直接指定，不需自動收集
            Manager.autoPlaceUnits = true;        // 依各單位 startCoord 擺放
            Manager.autoStart = false;            // 由我們手動開戰，確保上面都建好了
            Manager.aiActionDelay = 0.4f;

            Manager.StartBattle();
        }

        private void FrameCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographic = true;
            cam.orthographicSize = height * 0.5f + 1f;
            cam.transform.position = new Vector3(width * 0.5f, height * 0.5f, -10f);

            // 掛上視野控制 (滾輪縮放 + Slider + 左鍵拖曳平移)，以上面設定的取景當成固定的預設視野。
            if (cam.GetComponent<BattleCameraController>() == null)
                cam.gameObject.AddComponent<BattleCameraController>();
        }
    }
}
