using BlackChess.SRPG.Units;
using UnityEngine;
using UnityEngine.Events;

namespace BlackChess.SRPG.Objects
{
    /// <summary>
    /// 「開關」功能 (需求書：開關城門、水流等環境互動)。
    /// 單位互動時切換開/關狀態，並透過 UnityEvent 通知外部 —— 你可以在 Inspector 把
    /// 「打開城門 (移除擋路物件)」「改變某格地形 (放水)」等反應接到 onTurnedOn / onTurnedOff。
    /// 這樣開關本身不需要知道它控制的是什麼，達到解耦。
    /// </summary>
    public class SwitchBehavior : ObjectBehavior
    {
        public bool isOn;

        [Tooltip("是否只能觸發一次 (例如一次性的機關)。")]
        public bool oneShot = false;
        private bool _used;

        [Header("事件 (在 Inspector 接上要觸發的反應)")]
        public UnityEvent onTurnedOn;
        public UnityEvent onTurnedOff;

        public override void OnInteract(Unit interactor)
        {
            if (oneShot && _used) return;

            isOn = !isOn;
            _used = true;

            if (isOn) onTurnedOn?.Invoke();
            else onTurnedOff?.Invoke();
        }
    }
}
