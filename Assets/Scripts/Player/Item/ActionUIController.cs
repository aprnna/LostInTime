using System;
using Manager;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Player.Item
{
    public class ActionUIController:MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private BattleSystem _battleSystem;
        private PlayerStats _playerStats;
        private UIManagerBattle _uiManagerBattle;
        [SerializeField] private BaseAction _action;

        void Start()
        {
            _playerStats = PlayerStats.Instance;
            _battleSystem = BattleSystem.Instance;
            _action.Initialize(_playerStats);

            _playerStats.OnShieldStatsChange += OnChangeShield;
        }
        
        private void OnDisable()
        {
            _playerStats.OnShieldStatsChange -= OnChangeShield;

        }

        private void OnChangeShield()
        {
            if (_action.IsDefend) _action.InitializeDefendShield(_playerStats);
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            _action.InitializeDefendShield(_playerStats);
            if (!_action.IsDefend && _action.IsLimited && _action.CurrentLimit <= 0)
            {
                Debug.Log("Cannot Use");
                return;
            }
            _battleSystem.OnActionButtonClicked(_action);
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            _action.InitializeDamage(_playerStats.BaseDamage);
            Debug.Log(_action.MaxDamage+" "+_action.MinDamage);
            var limit  = _action.IsLimited ? _action.CurrentLimit.ToString() :"unlimited";
            _battleSystem.OnChangeActionDescription(_action.name 
                                                    + " - Uses left: "+limit+" Deal "
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