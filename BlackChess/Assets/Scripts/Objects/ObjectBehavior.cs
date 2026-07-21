using BlackChess.SRPG.Units;
using UnityEngine;

namespace BlackChess.SRPG.Objects
{
    /// <summary>
    /// 物件「功能」的基底。所有具體功能 (Pushable / Breakable / Switch / TrackMovable)
    /// 都繼承它，並掛在同一個 InteractableObject 上以組合出複合功能。
    /// [RequireComponent] 確保任何功能元件都跟著一個 InteractableObject。
    /// </summary>
    [RequireComponent(typeof(InteractableObject))]
    public abstract class ObjectBehavior : MonoBehaviour
    {
        private InteractableObject _obj;
        public InteractableObject Obj => _obj != null ? _obj : (_obj = GetComponent<InteractableObject>());

        /// <summary>當單位選擇「互動」此物件時被呼叫。預設不做事，需要回應的功能自行覆寫。</summary>
        public virtual void OnInteract(Unit interactor) { }
    }
}
