using UnityEngine;

namespace Minigames
{
    [CreateAssetMenu(fileName = "TapZoneData", menuName = "Minigames/TapZone Data")]
    public class TapZoneData : ScriptableObject
    {
        [Header("Gameplay Settings")]
        [SerializeField] private float baseSpeed = 1.5f;
        [SerializeField] private Vector2 waitRange = new Vector2(0.75f, 1.75f);
        [SerializeField] private bool randomizeZone = true;
        
        [Header("Zone Position Settings")]
        [Tooltip("Zone position clamp (0-1)")]
        [SerializeField] private Vector2 zoneCenterClamp = new Vector2(0.15f, 0.85f);
        
        [Header("Difficulty Values")]
        [Tooltip("Speed multiplier based on difficulty (1.0 = base speed)")]
        [SerializeField] private DifficultValue speedMultiplier = new DifficultValue();
        
        [Tooltip("Zone size as fraction of track width (0-1)")]
        [SerializeField] private DifficultValue zoneSize = new DifficultValue();
        
        public float BaseSpeed => baseSpeed;
        public Vector2 WaitRange => waitRange;
        public bool RandomizeZone => randomizeZone;
        public Vector2 ZoneCenterClamp => zoneCenterClamp;
        
        public float GetSpeedMultiplier(float difficulty)
        {
            return speedMultiplier.Get(difficulty);
        }
        
        public float GetZoneSize(float difficulty)
        {
            return zoneSize.Get(difficulty);
        }
        
        private void OnValidate()
        {
            // Ensure base speed is positive
            if (baseSpeed <= 0) baseSpeed = 1f;
        }
    }
}