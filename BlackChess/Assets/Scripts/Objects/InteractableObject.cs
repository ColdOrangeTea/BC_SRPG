using System.Collections;
using System.Collections.Generic;
using BlackChess.SRPG.Core;
using BlackChess.SRPG.Units;
using UnityEngine;

namespace BlackChess.SRPG.Objects
{
    /// <summary>
    /// 戰場上的「互動物件」(Object) 的核心。需求書要求「一個物件可以同時具有多種功能」，
    /// 例如：可被推、也可被破壞的補給箱。
    ///
    /// 設計採「組合 (Composition)」而非繼承：
    ///   - InteractableObject 只負責『佔格 / 座標 / 收集身上的行為元件』。
    ///   - 真正的功能寫成一個個 ObjectBehavior 元件 (Pushable / Breakable / Switch / TrackMovable)。
    ///   - 想要「可推 + 可破壞的箱子」，就在同一個 GameObject 掛 Pushable + Breakable 兩個元件即可。
    /// 這比「BreakablePushableBox」這種爆炸性的多重繼承組合乾淨得多。
    /// </summary>
    public class InteractableObject : MonoBehaviour
    {
        [Header("功能 (勾選此物件擁有的功能種類，會自動同步對應的行為元件)")]
        [Tooltip("可被推動 (PushableBehavior)。")]
        public bool canBePushed;
        [Tooltip("可被破壞 (BreakableBehavior)：有 HP、可被選為攻擊目標，破壞後可掉落道具。")]
        public bool canBeBroken;
        [Tooltip("是開關 (SwitchBehavior)：單位互動時觸發。")]
        public bool isSwitch;
        [Tooltip("沿軌道移動 (TrackMovableBehavior)：可被移動到其他地方的機關。")]
        public bool movesOnTrack;

        // 首次在 Inspector 顯示時，先把「已掛在物件上的行為元件」讀回勾選狀態，
        // 避免舊 Prefab (直接掛元件的做法) 一開啟就被誤刪元件。
        [SerializeField, HideInInspector] private bool featuresInitialized;

        [Header("阻擋")]
        [Tooltip("是否阻擋單位移動 (箱子擋路=true；地上的開關=false)。")]
        public bool blocksMovement = true;

        [Tooltip("是否阻擋攻擊/視線。")]
        public bool blocksLineOfSight = false;

        [Header("移動表現")]
        [Tooltip("被推 / 沿軌道前進時，每格花費的秒數 (純表演，不影響邏輯)。設 0 則瞬間到位。")]
        public float moveStepDuration = 0.12f;

        public GridCoord Coord { get; private set; }
        public BattleGrid Grid { get; private set; }

        /// <summary>是否正在播放移動動畫 (供呼叫端避免重複下令)。</summary>
        public bool IsMoving { get; private set; }

        private readonly List<ObjectBehavior> _behaviors = new List<ObjectBehavior>();

        public bool BlocksMovement => blocksMovement;
        public float MoveStepDuration => moveStepDuration;

        private void Awake()
        {
            SyncBehaviorsFromFlags();
            GetComponents(_behaviors);

            // 用程式生成、直接掛元件的物件 (未經過 Inspector) → 反向把元件同步回勾選狀態，
            // 讓 canBeBroken 等旗標在執行期也能正確反映實際功能。
            canBePushed |= GetComponent<PushableBehavior>() != null;
            canBeBroken |= GetComponent<BreakableBehavior>() != null;
            isSwitch |= GetComponent<SwitchBehavior>() != null;
            movesOnTrack |= GetComponent<TrackMovableBehavior>() != null;
        }

        /// <summary>依勾選的功能旗標，補上缺少的行為元件 (執行期用；Editor 由 OnValidate 同步)。</summary>
        private void SyncBehaviorsFromFlags()
        {
            if (canBePushed && GetComponent<PushableBehavior>() == null) gameObject.AddComponent<PushableBehavior>();
            if (canBeBroken && GetComponent<BreakableBehavior>() == null) gameObject.AddComponent<BreakableBehavior>();
            if (isSwitch && GetComponent<SwitchBehavior>() == null) gameObject.AddComponent<SwitchBehavior>();
            if (movesOnTrack && GetComponent<TrackMovableBehavior>() == null) gameObject.AddComponent<TrackMovableBehavior>();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor 裡勾/取消勾選功能時，自動加上或移除對應的行為元件，
        /// 讓「一個物件同時具有多種功能」只要打勾就能組出來 (可推+可破壞的補給箱等)。
        /// </summary>
        private void OnValidate()
        {
            if (Application.isPlaying) return;

            if (!featuresInitialized)
            {
                // 第一次：以「實際掛著的元件」為準初始化勾選狀態 (相容既有 Prefab)。
                canBePushed |= GetComponent<PushableBehavior>() != null;
                canBeBroken |= GetComponent<BreakableBehavior>() != null;
                isSwitch |= GetComponent<SwitchBehavior>() != null;
                movesOnTrack |= GetComponent<TrackMovableBehavior>() != null;
                featuresInitialized = true;
                return;
            }

            // OnValidate 中不允許直接 AddComponent/DestroyImmediate，延後一幀執行。
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return; // 物件可能已被刪除
                SyncFeatureComponent<PushableBehavior>(canBePushed);
                SyncFeatureComponent<BreakableBehavior>(canBeBroken);
                SyncFeatureComponent<SwitchBehavior>(isSwitch);
                SyncFeatureComponent<TrackMovableBehavior>(movesOnTrack);
            };
        }

        private void SyncFeatureComponent<T>(bool enabled) where T : ObjectBehavior
        {
            var c = GetComponent<T>();
            if (enabled && c == null)
                UnityEditor.Undo.AddComponent<T>(gameObject);
            else if (!enabled && c != null)
                UnityEditor.Undo.DestroyObjectImmediate(c);
        }
#endif

        /// <summary>把物件登記到棋盤某格 (瞬間就位，用於開場佈置)。</summary>
        public void PlaceOnGrid(BattleGrid grid, GridCoord coord)
        {
            RegisterAt(grid, coord);
            transform.position = grid.CoordToWorld(coord);
        }

        /// <summary>
        /// 只更新「邏輯佔格」(從舊格移除、登記到新格)，不動視覺位置。
        /// 給動畫移動用：先更新棋盤狀態，再由 GridMover 平滑補上視覺位置。
        /// </summary>
        public void RegisterAt(BattleGrid grid, GridCoord coord)
        {
            Grid = grid;
            // 從舊格移除
            var old = grid.GetTile(Coord);
            if (old != null && old.interactable == this) old.interactable = null;

            Coord = coord;
            var tile = grid.GetTile(coord);
            if (tile != null) tile.interactable = this;
        }

        /// <summary>把物件瞬間移到新格 (保留給不需要表演的情境 / 向後相容)。</summary>
        public void MoveTo(GridCoord coord)
        {
            if (Grid != null) PlaceOnGrid(Grid, coord);
        }

        /// <summary>
        /// 把物件平滑移到相鄰的目標格 (協程)。先更新邏輯佔格，再逐格補間視覺位置，
        /// 做出「一格一格移動」的效果。extraFollowers 會跟著一起移動 (例如礦車上的乘客)。
        /// </summary>
        public IEnumerator MoveToAnimated(GridCoord coord, params Transform[] extraFollowers)
        {
            if (Grid == null) yield break;

            Vector3 from = transform.position;
            RegisterAt(Grid, coord);              // 邏輯佔格立即更新，避免移動途中被別的物件搶格
            Vector3 to = Grid.CoordToWorld(coord);

            IsMoving = true;
            yield return GridMover.MoveStep(from, to, moveStepDuration, BuildMovers(extraFollowers));
            IsMoving = false;
        }

        /// <summary>
        /// 沿著給定路徑逐格平滑移動 (協程)。path[0] 應為目前所在格。
        /// 每經過一格就更新一次邏輯佔格，讓棋盤狀態隨移動即時同步。
        /// </summary>
        public IEnumerator MoveAlongPathAnimated(IList<GridCoord> path, params Transform[] extraFollowers)
        {
            if (Grid == null || path == null || path.Count < 2) yield break;

            IsMoving = true;
            var movers = BuildMovers(extraFollowers);
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 from = Grid.CoordToWorld(path[i - 1]);
                Vector3 to = Grid.CoordToWorld(path[i]);
                RegisterAt(Grid, path[i]);
                yield return GridMover.MoveStep(from, to, moveStepDuration, movers);
            }
            IsMoving = false;
        }

        /// <summary>把自己的 transform 與額外跟隨者組成一個陣列，交給 GridMover 一起補間。</summary>
        private Transform[] BuildMovers(Transform[] extraFollowers)
        {
            if (extraFollowers == null || extraFollowers.Length == 0)
                return new[] { transform };

            var movers = new Transform[extraFollowers.Length + 1];
            movers[0] = transform;
            for (int i = 0; i < extraFollowers.Length; i++)
                movers[i + 1] = extraFollowers[i];
            return movers;
        }

        public void RemoveFromGrid()
        {
            if (Grid == null) return;
            var tile = Grid.GetTile(Coord);
            if (tile != null && tile.interactable == this) tile.interactable = null;
        }

        /// <summary>
        /// 由某單位對此物件執行「互動」。會把互動事件分派給身上所有 ObjectBehavior，
        /// 讓每個功能各自決定要不要回應 (例如開關會切換、破壞則無反應)。
        /// </summary>
        public void Interact(Unit interactor)
        {
            foreach (var b in _behaviors)
                b.OnInteract(interactor);
        }

        public T GetBehavior<T>() where T : ObjectBehavior => GetComponent<T>();
    }
}
