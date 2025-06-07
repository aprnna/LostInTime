using System;
using Manager;
using UnityEngine;
using UnityEngine.Serialization;

namespace Player
{
    public class EnemyController:MonoBehaviour
    {
        [SerializeField] private GameObject _marker;
        private BattleSystem _battleSystem;
        public EnemyStats EnemyStats { get; private set; }
        public Animator AnimEnemy { get; private set; }

        private void Start()
        {
            _battleSystem = BattleSystem.Instance;
            EnemyStats = GetComponent<EnemyStats>();
            AnimEnemy = GetComponent<Animator>();
            _marker.SetActive(false);
        }

        public void PlayAnim(string stateName)
        {
            AnimEnemy.Play(stateName);
        }
        public void OnChangeMarker(bool value)
        {
            _marker.SetActive(value);
        }
        void OnMouseEnter()
        {
            OnChangeMarker(true);
            _battleSystem.OnHoverEnemy(EnemyStats, true);
        }

        private void OnMouseExit()
        {
            if(_battleSystem.SelectedTarget != null) return;
            OnChangeMarker(false);
            _battleSystem.OnHoverEnemy(EnemyStats, false);
        }

        void OnMouseDown()
        {
            _battleSystem.OnEnemyButtonClicked(this);
        }
    }
}