# BlackChess 2D 戰棋系統 (SRPG Framework)

適用 Unity 2023.2 (URP)。所有腳本位於 `Assets/Scripts`，命名空間根為 `BlackChess.SRPG`。
四面向、回合制、多勢力、含目標判定的戰棋核心框架。純邏輯與資料驅動，未綁定任何美術，方便你套上自己的 StickFigure 角色。

> **想直接看它跑？** 打開 `Assets/Scenes/SampleBattle.unity` 按 Play 即可（可操作、有 AI）。
> 範例的說明與操作方式見 `Assets/Scripts/Samples/README.md`，可重複使用的 Prefab 在 `Assets/SRPG/Resources/`。

---

## 一、資料夾與職責

| 資料夾 | 命名空間 | 職責 |
|--------|----------|------|
| `Core/` | `BlackChess.SRPG.Core` | 棋盤基礎：座標、地形、格子、棋盤管理與座標轉換 |
| `Pathfinding/` | `BlackChess.SRPG.Pathfinding` | **Dijkstra 尋路**、移動範圍計算、最小堆積優先佇列 |
| `Units/` | `BlackChess.SRPG.Units` | 單位、數值、勢力、背包 |
| `Actions/` | `BlackChess.SRPG.Actions` | 四種行動：移動 / 攻擊 / 待機 / 道具 |
| `AI/` | `BlackChess.SRPG.AI` | 自主行為：進攻 / 防禦 / 守衛 (盟軍與敵人共用) |
| `Items/` | `BlackChess.SRPG.Items` | 道具定義與場地道具撿取 |
| `Objects/` | `BlackChess.SRPG.Objects` | 互動物件：可推 / 可破壞 / 開關 / 軌道礦車 (組合式) |
| `Objectives/` | `BlackChess.SRPG.Objectives` | 戰鬥目標：殲滅 / 抵達地點 |
| `Battle/` | `BlackChess.SRPG.Battle` | 回合順序、戰鬥總管、戰況查詢 |

> 目前刻意不使用 Assembly Definition，全部編進 `Assembly-CSharp`，跨資料夾互相參照即可正常編譯。若日後要加快編譯，可再各自加 `.asmdef`。

---

## 二、移動系統與 Dijkstra 演算法 (需求重點說明)

### 為什麼是 Dijkstra 而不是 BFS？

需求：`MOV=4` 時人物最多移動 4 格，但要考慮**障礙物 (不可走)** 與**黏液地板 (移動力/2)**。

- **BFS** 只在「每一步成本都一樣」時才正確 —— 它找的是「格數最少」。
- 本系統每格**進入成本不同**：一般地板 `moveCost=1`、黏液 `moveCost=2`、障礙 `isWalkable=false`。
  當成本不一致時，「格數最少」不等於「總成本最少」，必須用 **Dijkstra**。

「黏液 = 移動力/2」是這樣實現的：進入黏液格扣 2 點移動力，所以同樣 `MOV=4`
只能穿過 2 格黏液（`2×2=4`），等於在黏液上的有效移動力砍半。障礙物則因 `isWalkable=false`
被尋路直接略過，代表「不能走」。

### 演算法流程（見 `Pathfinding/Pathfinder.cs → ComputeMovementRange`）

```
1. 起點成本 = 0，放入最小堆積 (優先佇列)。
2. 取出「目前累積成本最小」的格子 current。
3. 對 current 的四個鄰居 next：
       newCost = cost[current] + next.EnterCost      // 地形成本
       若 next 不可通行 (障礙/擋路物件/敵人) → 略過
       若 newCost > MOV                              → 超出移動力，略過
       若 newCost 比先前記錄更低                       → 更新 cost[next]、cameFrom[next]=current，放回堆積
4. 重複 2~3 直到堆積清空。
   → cost 字典內所有格子 = 「走得到的範圍」；cameFrom 可反推任一格的完整路徑。
```

- **正確性**：因為每次都先處理成本最小的格子，第一次確定某格成本時，那必是最小成本（Dijkstra 的核心保證）。
- **複雜度**：`O(E log V)`。四面向下每格最多 4 條邊，數百格瞬間完成。
- **優先佇列**：Unity 的 C# 版本沒有內建 `PriorityQueue`，因此在 `Pathfinding/MinHeap.cs`
  自製了二元最小堆積，讓「取出最小成本」是 `O(log n)`。

相關 API：
- `Pathfinder.ComputeMovementRange(grid, origin, mov, unit)` → 回傳 `MovementRange`（可走範圍 + 路徑反推）。
- `MovementRange.GetPathTo(target)` → 反推起點到目標的完整路徑。
- `Pathfinder.GetStoppableCells(...)` → 過濾出「可實際停留」的格子（排除友軍暫佔的格），供 UI 高亮。

---

## 三、回合與勢力順序 (需求重點說明)

見 `Battle/TurnOrderSystem.cs` 與 `Battle/BattleManager.cs`。

- 勢力（Faction）可有多個：玩家 / 盟軍 / 敵人 / 其他。以 `teamId` 判定敵我（相同=友方，不同=敵對），
  所以「玩家+盟軍」可設同一 `teamId` 對抗敵人，也能做多方混戰。
- **行動先後 = 各勢力所有存活單位的 SPD 總和，由大到小**。
  例：玩家合=8 > 盟軍=6 > 敵人=4 → 玩家 → 盟軍 → 敵人。
- 每個「大回合」開始時重算一次順序（單位陣亡會改變 SPD 總和）。
- 輪到某勢力時：
  - **玩家勢力**：可自由選擇任一可行動單位下令，直到全部行動完或手動結束回合。
  - **AI 勢力**：每個單位依序跑自己的 `UnitAI`。
- 每個單位一回合可做：**一次移動** + **一個主行動（攻擊 / 待機 / 道具）**。待機自動視為防禦（受傷減半）。

---

## 四、戰鬥目標

見 `Objectives/`。`BattleManager` 每次單位行動後評估：**任一目標失敗→戰敗；全部達成→勝利**。

- `AnnihilateObjective`：殲滅指定敵方 `teamId`；我方全滅則失敗。
- `ReachLocationObjective`：指定陣營「任一 / 全部」單位抵達撤退點即達成。

要新增目標（護送、限回合數、保護 NPC…）只要繼承 `BattleObjective` 覆寫 `Evaluate` 即可。

---

## 五、Unit（棋子）

`Units/Unit.cs`。玩家角色、盟軍、敵人**共用同一個腳本**，差異只在：
- `faction.control` = `Player` 或 `AI`
- AI 單位額外掛一個 `UnitAI` 子類（決定行為類型）

數值 `UnitStats`：`HP / MP / ATK / RNG / MOV / SPD`
- `MOV` 決定最大移動成本、`RNG` 決定攻擊範圍（曼哈頓距離）、`SPD` 決定勢力順序。

---

## 六、AI 行為（策略模式）

`AI/`。盟軍與敵人共用，掛不同子類即得不同行為：

| 腳本 | 行為 |
|------|------|
| `AggressiveAI` | 進攻：追最近敵人，能打就打 |
| `DefensiveAI` | 防禦：待原地，敵人進警戒圈才反擊 |
| `GuardAI` | 守衛：守住指定點（如基地），不追出守備範圍 |

AI 只產出 `UnitPlan`（要不要移動 / 攻擊 / 待機），實際執行交給 `BattleManager`，決策與演出分離。
要新增行為（例如「優先攻擊殘血」「保護友軍」）就再寫一個 `UnitAI` 子類。

---

## 七、道具

`Items/`。場地道具 `FieldItem` **不佔格**：單位可站上去或穿過。撿取範圍為曼哈頓距離 ≤ `pickupRange`
（預設 1 = 站在格上或上下左右四格皆可撿），符合需求書描述。玩家移動後會自動嘗試撿腳邊道具
（`BattleManager.PlayerMove` 內呼叫 `ItemAction.TryPickUpNearby`）。

---

## 八、互動物件（組合式，一物可多功能）

`Objects/`。核心 `InteractableObject` 只負責佔格與座標，功能寫成一個個 `ObjectBehavior` 元件：

| 元件 | 功能 | 對應需求 |
|------|------|----------|
| `PushableBehavior` | 可被推一格 | 特殊指令移動的箱子 |
| `BreakableBehavior` | 有 HP，破壞後掉落道具 | 打掉掉補血物的瓶子 |
| `SwitchBehavior` | 開關，UnityEvent 觸發環境反應 | 城門 / 水流開關 |
| `TrackMovableBehavior` | 沿軌道移動、可載人 | 礦車 |

**「一物多功能」**只要在同一 GameObject 掛多個元件即可，例如
「可推 + 可破壞的補給箱」= `InteractableObject` + `PushableBehavior` + `BreakableBehavior`。
避免了「BreakablePushableBox」這類爆炸性繼承。

---

## 九、最小組裝步驟（在 Unity 內）

1. **地形資產**：`Create → BlackChess/SRPG/Tile Type` 建立「一般地板(cost1)」「黏液(cost2)」「牆(不可走)」。
2. **棋盤**：空物件掛 `BattleGrid`，設定寬高與 `defaultTileType`，執行時自動生成；特殊地形再用 `SetTileType` 覆寫。
3. **勢力**：每個陣營一個空物件掛 `Faction`（設 `control`、`teamId`）。把該陣營的單位放為其子物件。
4. **單位**：角色物件掛 `Unit`，填 `UnitStats`。AI 單位再加一個 `AggressiveAI` / `DefensiveAI` / `GuardAI`。
   開場時用 `BattleGrid.PlaceUnit(unit, coord)` 把單位放上棋盤。
5. **目標**：空物件掛 `AnnihilateObjective` 或 `ReachLocationObjective`。
6. **總管**：空物件掛 `BattleManager`，把 `grid`、`factions`、`objectives` 拖進去。播放即開始跑主迴圈。
7. **玩家操作**：自製點擊 UI，呼叫 `BattleManager.PlayerMove / PlayerAttack / PlayerWait / PlayerUseItem`，
   並用 `Pathfinder.ComputeMovementRange` 高亮可走範圍；回合結束呼叫 `EndPlayerFactionTurn()`。

---

## 十、通用回合管理 Prefab：`Assets/Prefabs/BattleSystem.prefab`

一顆「放到任何戰鬥地圖都能用」的回合制管理 Prefab。內含：
- 根物件 `BattleSystem`：掛 `BattleManager`（回合順序 + 目標判定 + 主迴圈）。
- 子物件 `Grid`：掛 `BattleGrid`（棋盤，預設 12×12，可依地圖覆寫尺寸）。

**為什麼要「自動收集」？** Prefab 無法在資產中引用某個場景裡的物件（Faction/Unit/目標都是各地圖獨有的場景實例）。
因此 `BattleManager` 在開戰時會自動把該地圖的東西抓進來，達到「放進場景就能跑」：

| 欄位 | 行為 |
|------|------|
| `autoCollectFromScene` | 若 `grid`/`factions`/`objectives` 沒手動指定，開戰時用 `FindObjectsByType` 從場景自動收集 |
| `autoPlaceUnits` | 開戰時把每個 `Unit` 放到它自己的 `Unit.startCoord`，關卡設計者只要在單位上填座標即可佈陣 |
| `autoStart` | `Start()` 時自動開戰；關閉則由你手動呼叫 `StartBattle()`（過場後再開打） |

**每張地圖的最小佈置**：
1. 把 `BattleSystem.prefab` 拖進場景（一顆就好）。
2. 依地圖調整 `Grid` 的 `width`/`height` 與 `defaultTileType`（prefab 覆寫，不影響其他地圖）。
3. 場景放各勢力的 `Faction`（設 `control`/`teamId`），把單位設為其子物件並填 `startCoord`。
4. 場景放這關的目標（`AnnihilateObjective` / `ReachLocationObjective`）。
5. 播放 → `BattleManager` 自動收集本關的勢力/單位/目標並開始跑回合。

> 需要更換棋盤來源時，仍可手動把某個場景 `BattleGrid` 拖到 `BattleManager.grid`；有指定就不會自動覆蓋。

---

## 十一、尚未包含（可依需要擴充）

- 玩家點選 UI 與範圍高亮的實際繪製（框架已提供所有查詢 API，接上 Tilemap/Sprite 即可）。
- 存讀檔、技能/魔法系統、命中率與傷害公式細節（目前傷害 = ATK，防禦減半，留有擴充點）。
- 動畫與音效（`Unit.MoveAlongPath` 為協程，可在其中插入動畫觸發）。
