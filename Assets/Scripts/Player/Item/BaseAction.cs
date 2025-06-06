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
        [SerializeField] private bool _defend;
        public int BaseDamage { get; private set; }
        public int MinDamage { get; private set; }
        public int MaxDamage { get; private set; }
        public bool IsDefend => _defend;
        
        public void InitializeDamage(int baseDamagePlayer)
        {
            BaseDamage = baseDamagePlayer * (_percentageDamage / 100);
            MinDamage = BaseDamage - _interval;
            MaxDamage = BaseDamage + _interval;
        }
    }
}