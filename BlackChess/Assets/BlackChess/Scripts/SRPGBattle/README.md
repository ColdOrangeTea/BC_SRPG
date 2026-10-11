# BlackChess 戰棋核心：尋路模組

適用 Unity 2023.2 (URP)。腳本位於 `Assets/Scripts/SRPGBattle`，命名空間根為 `BlackChess.SRPG`。
目前只保留**核心尋路演算法**，不含單位、回合、AI、戰鬥 UI 等系統 (原本的 SampleBattle 雛型已移除，需要時可從 git 歷史找回)。

## 資料夾

| 資料夾 | 命名空間 | 內容 |
|--------|----------|------|
| `Core/` | `BlackChess.SRPG.Core` | `GridCoord` (格子座標、四方向、曼哈頓距離)、`TileType` (地形資料：可否通行、移動成本) |
| `Pathfinding/` | `BlackChess.SRPG.Pathfinding` | `Pathfinder` (Dijkstra)、`MovementRange`、`MinHeap`、`IPathGrid`、`TileTypePathGrid` |

地形資料資產在 `Assets/SRPG/Resources/`：`Tile_Ground` (成本 1)、`Tile_Slime` (成本 2，等於移動力砍半)、`Tile_Wall` (不可走)。

## 為什麼是 Dijkstra 而不是 BFS？

每格**進入成本不同** (地板 1、黏液 2、牆不可走) 時，「格數最少」不等於「總成本最少」，BFS 會算錯，必須用 Dijkstra。
`MOV=4` 時只能穿過 2 格黏液 (`2×2=4`)，等於在黏液上移動力砍半。詳細流程見 `Pathfinder.cs` 開頭註解。

## 用法

`Pathfinder` 只透過 `IPathGrid` 詢問「在不在棋盤內」與「進入成本 (負數 = 不可通過)」，不認識任何單位或物件類別。
誰擋路 (敵人、箱子、大型敵人佔的格子) 由實作端決定。現成的 `TileTypePathGrid` 可直接使用：

```csharp
var grid = new TileTypePathGrid(10, 7, groundType);
grid.SetTileType(new GridCoord(3, 2), slimeType);   // 黏液
grid.SetBlocked(new GridCoord(5, 5), true);           // 被敵人佔據

MovementRange range = Pathfinder.ComputeMovementRange(grid, unitCoord, mov);
List<GridCoord> path = range.GetPathTo(targetCoord);  // 範圍內某格的路徑
List<GridCoord> stops = Pathfinder.GetStoppableCells(range, c => !friendOccupied.Contains(c));
List<GridCoord> far = Pathfinder.FindPath(grid, unitCoord, enemyCoord, allowBlockedTarget: true);
```

3D 棋盤 (`Assets/Scripts/View3D/Board3D`) 使用同一個 `GridCoord`，之後可讓它實作 `IPathGrid` 或搭配 `TileTypePathGrid` 接上尋路。
