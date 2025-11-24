using System;
using Manager;
using TMPro;
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
        [SerializeField] private TMP_Text _description;

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
            if (_action.IsLimited && _action.CurrentLimit <= 0)
            {
                Debug.Log("Cannot Use");
                return;
            }
            OnActionButtonClicked(_action);
        }
        private void OnActionButtonClicked(BaseAction action)
        {
            Debug.Log(_battleSystem.StateMachine.CurrentState);
            if (_battleSystem.StateMachine.CurrentState == _battleSystem.SelectActionState)
            {
                if (action.IsLimited)
                {
                    if (!(action.CurrentLimit > 0) && !action.IsDefend)
                    {
                        Debug.Log("Out of limit"); 
                        return;
                    }
                    action.UseAction();
                }
                _battleSystem.SelectAction(action); 
                if (action.IsDefend)
                {
                    _playerStats.UseShield();
                    _battleSystem.StateMachine.ChangeState(_battleSystem.CriticalAttackState);
                }
                else _battleSystem.StateMachine.ChangeState(_battleSystem.SelectEnemyState);
            }
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            _action.InitializeDamage(_playerStats.BaseDamage, _playerStats.CriticalHitPercentage);
            var limit  = _action.IsLimited ? _action.CurrentLimit.ToString() :"unlimited";
            var damage = _action.IsIntervalDamage ? _action.MinDamage+"-"+_action.MaxDamage : _action.BaseDamage.ToString();
            if(_action.IsDefend)
            {
                SetActionDescription(_action.name 
                                     + " - Uses left: "+limit+" Gain "
                                     + _action.BaseDefend
                                     +" defend shield");
                return;
            }
            SetActionDescription(_action.name 
                                 + " - Uses left: "+limit+" Deal "
                                 + damage
                                 +" damage to 1 chosen enemy");
        }
 
        public void OnPointerExit(PointerEventData eventData)
        {
            SetActionDescription("");
        }

        private void SetActionDescription(string text)
        {
            _description.text = text;
        }
        void OnDestroy()
        {
            SetActionDescription("");
        }
    }
}