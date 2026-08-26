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
        [SerializeField] private PlayerActionType _actionType;
        
        [Header("Defend")]
        [SerializeField] private bool _defend;

        [SerializeField] private GameObject _vfx;
        [SerializeField] private bool _isLimited;
        [SerializeField] private int _limit;
        [SerializeField, Range(0, 11)] float _difficultyCritical;

        [Header("TapZone Settings")]
        [Tooltip("Speed multiplier for marker (1.0 = base speed / 100%, 2.0 = 2x speed, 0.5 = half speed). Overrides TapZoneData.speedMultiplier curve.")]
        [SerializeField, Range(0.1f, 3f)] private float _speedPercent = 1f;
        [Tooltip("Success zone width as fraction of track (0.05=5% ... 1.0=100%). Overrides TapZoneData.zoneSize curve.")]
        [SerializeField, Range(0.05f, 1f)] private float _zoneWidthPercent = 0.2f;
        [SerializeField, Range(0, 50)] private int _criticalBonusPercent = 10;

        private int _baseDefend;
        [SerializeField] private int _currentLimit;
        public int BaseDefend => _baseDefend;
        public int BaseDamage { get; private set; }
        public int CriticalHitDamage { get; private set; }
        public float SpeedPercent => _speedPercent;
        public float ZoneWidthPercent => _zoneWidthPercent;
        public int CriticalDamage => Mathf.RoundToInt(BaseDamage * (1 + _criticalBonusPercent / 100f));
        public int CriticalBonusPercent => _criticalBonusPercent;
        public bool IsDefend => _defend;
        public GameObject VFX => _vfx;
        public int CurrentLimit => _currentLimit;
        public bool IsLimited => _isLimited;
        public int Limit => _limit;

        public float GetDepletionRatio()
        {
            if (!IsLimited || _limit <= 0) return 0f;
            return Mathf.Clamp01(1f - (float)_currentLimit / _limit);
        }
        public float DifficultyCritical => _difficultyCritical;
        public string ActionName => _name;
        public PlayerActionType ActionType => _actionType;

        private void Awake()
        {
            ResetLimit();
        }

        public void ResetLimit()
        {
            _currentLimit = _limit;
        }

        public void InitializeDefendShield(PlayerStats playerStats)
        {
            if (IsDefend)
            {
                _limit = playerStats.Shield;
                _currentLimit = _limit;
                _baseDefend = playerStats.BaseDefend;
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
            _currentLimit = Mathf.Min(_currentLimit + value, _limit);
        }
        public void InitializeDamage(int baseDamagePlayer, int criticalPercentage)
        {
            BaseDamage = Mathf.RoundToInt(baseDamagePlayer * (_percentageDamage / 100f));
            CriticalHitDamage = BaseDamage + Mathf.RoundToInt(BaseDamage * (criticalPercentage / 100f));
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