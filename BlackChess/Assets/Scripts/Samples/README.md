# 範例戰鬥 (Sample Battle)

一個「打開就能跑」的完整示範，用來理解前面所有腳本怎麼串起來使用。

## 怎麼執行

1. 開啟場景 `Assets/Scenes/SampleBattle.unity`。
2. 按 Play。畫面會用色塊畫出棋盤、單位、可移動範圍（純 OnGUI，不需任何美術資源）。

> 若 Console 出現 `The referenced script ... is missing`，通常是 Unity 尚未重新編譯完成；
> 等編譯結束、或重新匯入一次即可。所有腳本 / 資產的 GUID 已固定寫死並互相對應。

## 操作方式

| 操作 | 行為 |
|------|------|
| 左鍵點自己的單位 | 選取，並顯示藍色可移動範圍 |
| 左鍵點藍色格 | 移動（沿 Dijkstra 最短路徑，會自動撿腳邊道具） |
| 左鍵點射程內的敵人 | 攻擊 |
| 空白鍵 | 選取中的單位待機（自動防禦，受傷減半） |
| Enter / E | 結束我方回合，換 AI（盟軍→敵人）行動 |

達成目標（殲滅所有敵人）或我方全滅時，左上角 HUD 會顯示勝／敗。

## 這個範例示範了什麼

`SampleBattleSetup.cs`（純程式碼組裝一場戰鬥，等同「把 BattleSystem.prefab 丟進場景＋擺盤」的可讀版）：

- **棋盤 / 地形**：10×8 棋盤，中央一塊**黏液**（移動力/2）、一道有缺口的**牆**（不可走）。
  選取單位觀察藍色範圍，就能看到 Dijkstra 如何因地形成本繞路、少走黏液。
- **三個勢力**：玩家(操作) / 盟軍(GuardAI) / 敵人(AggressiveAI)。
  SPD 總和 = 玩家 8 > 盟軍 6 > 敵人 4，對應需求書的行動順序範例。
- **從 Prefab 生成單位**：見下方「可重複使用的 Prefab」。
- **道具**：場上一瓶藥水（可撿）＋一個可破壞瓶子（打破掉藥水）。
- **目標**：`AnnihilateObjective`（殲滅敵人；我方全滅則敗）。

`SampleBattleUI.cs`：示範**玩家端**如何呼叫 `BattleManager.PlayerMove / PlayerAttack / PlayerWait /
EndPlayerFactionTurn`，並用 `Pathfinder.ComputeMovementRange` 畫可移動範圍。正式專案把 OnGUI 換成
Sprite / Tilemap / UGUI 即可，**邏輯呼叫方式完全一樣**。

## 可重複使用的 Prefab（`Assets/SRPG/Resources/`）

| Prefab | 組成 | 用途 |
|--------|------|------|
| `Unit_Player.prefab` | `Unit` | 玩家角色。複製後改數值 = 不同角色 |
| `Unit_Enemy.prefab` | `Unit` + `AggressiveAI` | 進攻型敵人 |
| `Unit_Ally.prefab` | `Unit` + `GuardAI` | 守衛型盟軍 |
| `Prop_Bottle.prefab` | `InteractableObject` + `BreakableBehavior` | 可破壞、掉落道具的瓶子 |

以及資料資產：`Tile_Ground / Tile_Slime / Tile_Wall`（地形）、`Item_Potion`（道具）。

> **Unit 就是最典型的可重複使用 Prefab**：邏輯（移動、戰鬥、回合狀態）都在 `Unit` 元件裡，
> 換一個角色只要複製 Prefab、改 `stats`、（敵人/盟軍）換一顆 AI 元件即可。
> 目前 Prefab 只含邏輯元件、沒有美術；要顯示你的 StickFigure，替 Prefab 加一個 `SpriteRenderer`
> 並指定 Sprite 即可，戰鬥邏輯完全不受影響。

## 放進 Resources 的原因

範例把 Prefab / 資產放在 `Resources/`，讓 `SampleBattleSetup` 用 `Resources.Load` 自動載入，
於是「場景只有一個 DemoBattle 物件」也能跑，不必手動拖曳引用。
正式專案更常見的作法是：在 `SampleBattleSetup` 的 Inspector 欄位直接把 Prefab 拖進去
（欄位已保留），此時就不需要 Resources。
