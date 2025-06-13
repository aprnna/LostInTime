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
            if (!_action.IsDefend && _action.IsLimited && _action.CurrentLimit <= 0)
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
                    _battleSystem.StateMachine.ChangeState(_battleSystem.DamageRouletteState);
                }
                else _battleSystem.StateMachine.ChangeState(_battleSystem.SelectEnemyState);
            }
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            _action.InitializeDamage(_playerStats.BaseDamage);
            var limit  = _action.IsLimited ? _action.CurrentLimit.ToString() :"unlimited";
            SetActionDescription(_action.name 
                                 + " - Uses left: "+limit+" Deal "
                                 +_action.MinDamage+"-"+_action.MaxDamage
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