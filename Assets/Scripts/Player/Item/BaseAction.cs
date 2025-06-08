using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Player.Item
{
    [CreateAssetMenu(menuName = "ItemActions", fileName = "Item")]
    public class BaseAction : ScriptableObject
    {
        [SerializeField] private string _name;
        [SerializeField] private int _percentageDamage;
        [SerializeField] private int _interval;
        
        [Header("Defend")]
        [SerializeField] private bool _defend;
        [SerializeField] private int _multipleDefend;
        [SerializeField] private int _intervalDefend;
            
        [SerializeField] private GameObject _vfx;
        [SerializeField] private bool _isLimited;
        [SerializeField] private int _limit;
        private int _minDefend;
        private int _maxDefend;
        
        private int _currentLimit;
        public int MinDefend => _minDefend;
        public int MaxDefend => _maxDefend;
        public int BaseDamage { get; private set; }
        public int MinDamage { get; private set; }
        public int MaxDamage { get; private set; }
        public bool IsDefend => _defend;
        public GameObject VFX => _vfx;
        public int CurrentLimit => _currentLimit;
        public bool IsLimited => _isLimited;

        public void InitializeDefendShield(PlayerStats playerStats)
        {
            if (IsDefend)
            {
                Debug.Log(playerStats.Shield);
                _limit = playerStats.Shield;
                _currentLimit = _limit;
                if (_limit > 0)
                {
                    var baseDefendShield = playerStats.BaseDefend * _multipleDefend;
                    _minDefend = baseDefendShield - _intervalDefend;
                    _maxDefend = baseDefendShield + _intervalDefend;
                }
                else
                {
                    _currentLimit = 0;
                    _minDefend = playerStats.MinBaseDefend();
                    _maxDefend = playerStats.MaxBaseDefend();                
                }
            }
        }

        public void Initialize(PlayerStats playerStats)
        {
            _currentLimit = _limit;
            InitializeDefendShield(playerStats);
            InitializeDamage(playerStats.BaseDamage);
        }
        public void UseAction()
        {
            if(IsDefend && _currentLimit <= 0) return; 
            _currentLimit -= 1;
        }

        public void AddLimit(int value)
        {
            _currentLimit += value;
        }
        private void InitializeDamage(int baseDamagePlayer)
        {
            BaseDamage = baseDamagePlayer * (_percentageDamage / 100);
            MinDamage = BaseDamage - _interval;
            MaxDamage = BaseDamage + _interval;
        }
    }
}