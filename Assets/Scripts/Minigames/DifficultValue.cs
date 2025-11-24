using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Minigames
{
    [Serializable]

    public class DifficultValue
    {
    
        [HorizontalGroup, SerializeField]
        private float Min;
        [HorizontalGroup, SerializeField]
        private float Max;
        [SerializeField] public float Impossible;
        public static float ImpossibleThreshold = 10;

        public float Get(float value)
        {
            if (value > ImpossibleThreshold)
            {
                return Impossible;
            }
            return Mathf.Lerp(Min, Max, value / ImpossibleThreshold);
        }
        public int GetRounded(float value)
        {
            var res = Get(value);
            return Mathf.RoundToInt(res);
        }
    }
}