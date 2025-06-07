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
            _battleSystem.OnChangeActionDescription(_action.name + " Deal " + _action.MinDamage +" - "+_action.MaxDamage+" Damage to 1 chosen enemy");
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