using Manager;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Player.Item
{
    public class ActionUIController:MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private BattleSystem _battleSystem;
        private UIManagerBattle _uiManagerBattle;
        [SerializeField] private BaseAction _action;

        void Start()
        {
            _battleSystem = BattleSystem.Instance;
            _action.InitializeDamage(PlayerStats.Instance.BaseDamage);
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            _battleSystem.OnActionButtonClicked(_action);
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            var limit  = _action.IsLimited ? "limited" : _action.CurrentLimit.ToString();
            _battleSystem.OnChangeActionDescription(_action.name 
                                                    + " - Uses left:" +limit+" Deal "
                                                    +_action.MaxDamage+"-"+_action.MinDamage
                                                    +" damage to 1 chosen enemy");
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            _battleSystem.OnChangeActionDescription("");
        }
        void OnDestroy()
        {
            _battleSystem.OnChangeActionDescription("");
        }
    }
}