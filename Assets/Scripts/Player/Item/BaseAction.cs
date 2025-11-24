using System;
using Cysharp.Threading.Tasks;
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
        [SerializeField, Range(0, 11)] float _difficultyCritical;

        private int _minDefend;
        private int _maxDefend;
        private int _baseDefend;
        [SerializeField] private int _currentLimit;
        public int MinDefend => _minDefend;
        public int MaxDefend => _maxDefend;
        public int BaseDefend => _baseDefend;
        public int BaseDamage { get; private set; }
        public int MinDamage { get; private set; }
        public int MaxDamage { get; private set; }
        public int CriticalHitDamage { get; private set; }
        public bool IsDefend => _defend;
        public GameObject VFX => _vfx;
        public int CurrentLimit => _currentLimit;
        public bool IsLimited => _isLimited;
        public bool IsIntervalDamage => _interval > 0;
        public float DifficultyCritical => _difficultyCritical;
        public string ActionName => _name;

        private void Awake()
        {
            ResetLimit();
        }

        public void ResetLimit()
        {
            Debug.Log("Reset Action: "+_name);
            _currentLimit = _limit;
        }

        public void InitializeDefendShield(PlayerStats playerStats)
        {
            if (IsDefend)
            {
                _limit = playerStats.Shield;
                _currentLimit = _limit;
                if (_limit > 0)
                {
                    var baseDefendShield = playerStats.BaseDefend * _multipleDefend;
                    _baseDefend = baseDefendShield;
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
            InitializeDefendShield(playerStats);
            InitializeDamage(playerStats.BaseDamage, playerStats.CriticalHitPercentage);
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
        public void InitializeDamage(int baseDamagePlayer, int criticalPercentage )
        {
            BaseDamage = Mathf.RoundToInt(baseDamagePlayer * (_percentageDamage / 100f));
            CriticalHitDamage = BaseDamage + Mathf.RoundToInt(BaseDamage * (criticalPercentage / 100f));
            MinDamage = BaseDamage - _interval;
            MaxDamage = BaseDamage + _interval;
        }
        public async UniTask PlayVfx(Transform position)
        {
            var vfxObject = Instantiate(_vfx, position);
            vfxObject.transform.SetParent(position.parent);
            var animator = vfxObject.GetComponent<Animator>();
            await UniTask.Yield();
            while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f || animator.IsInTransition(0))
            {
                await UniTask.Yield();
            }
            await UniTask.Delay(TimeSpan.FromSeconds(0.5), ignoreTimeScale: false);
            Destroy(vfxObject);
        }
    }
}