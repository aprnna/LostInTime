using System;
using Cysharp.Threading.Tasks;
using Input;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Minigames
{
    public class TapZone : Minigame
    {
        [SerializeField] private TapZoneData _tapZoneData;
        
        [Header("References")]
        [SerializeField] private RectTransform trackArea;
        [SerializeField] private RectTransform marker;
        [SerializeField] private RectTransform successZone;
        [SerializeField] private TMP_Text resultText;

        [Header("Events")]
        [SerializeField] private UnityEvent onCatch;
        [SerializeField] private UnityEvent onMiss;
        
        [Header("Debug Info")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private TMP_Text debugText;

        private enum MinigameState { Idle, Waiting, Running, Result }
        
        private MinigameState state = MinigameState.Idle;
        private float t;
        private int direction = 1;
        private float biteTimer;
        private float currentSpeed;
        private float currentZoneSizeFraction;

        protected override MinigameComponent[] MinigameComponents()
        {
            return null;
        }

        protected override async UniTask OnInit()
        {
            if (_tapZoneData == null)
            {
                Debug.LogError("TapZone: TapZoneData is not assigned!");
                return;
            }
            
            if (resultText != null)
                resultText.text = "";
            
            if (debugText != null)
                debugText.gameObject.SetActive(showDebugInfo);
            
            await UniTask.CompletedTask;
        }

        protected override void OnPrepare(float difficulty)
        {
            base.OnPrepare(difficulty);
            
            if (_tapZoneData == null) return;
            
            // Calculate speed using DifficultValue
            float speedMultiplier = _tapZoneData.GetSpeedMultiplier(difficulty);
            currentSpeed = _tapZoneData.BaseSpeed * speedMultiplier;
            
            // Setup zone with difficulty scaling
            if (_tapZoneData.RandomizeZone)
            {
                SetupZone(difficulty);
            }
            
            // Random starting position (horizontal)
            t = UnityEngine.Random.Range(0.05f, 0.95f);
            direction = UnityEngine.Random.value < 0.5f ? 1 : -1;
            ApplyMarkerPosition();
            
            // Set bite timer
            biteTimer = UnityEngine.Random.Range(_tapZoneData.WaitRange.x, _tapZoneData.WaitRange.y);
            state = MinigameState.Waiting;
            
            if (resultText != null)
                resultText.text = "Wait for bite...";
            
            UpdateDebugInfo(difficulty);
        }

        public override void OnPlay(float difficulty)
        {
            switch (state)
            {
                case MinigameState.Waiting:
                    biteTimer -= Time.deltaTime;
                    if (biteTimer <= 0)
                    {
                        state = MinigameState.Running;
                        if (resultText != null)
                            resultText.text = "TAP NOW!";
                        Debug.Log("TapZone: Bite detected!");
                    }
                    break;
                    
                case MinigameState.Running:
                    UpdateMarker();
                    break;
            }
        }

        private void OnEnable()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.Minigames.Tap.OnDown += PerformTap;
            }
        }

        private void OnDisable()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.Minigames.Tap.OnDown -= PerformTap;
            }
        }

        private void PerformTap()
        {
            if (!isPlaying) return;
            
            Debug.Log("TapZone: PerformTap");
            if (state == MinigameState.Running)
            {
                 Evaluate().Forget();
            }
        }

        private void UpdateMarker()
        {
            // Horizontal movement
            t += direction * currentSpeed * Time.deltaTime;
            
            if (t >= 1f)
            {
                t = 1f;
                direction = -1;
            }
            else if (t <= 0f)
            {
                t = 0f;
                direction = 1;
            }
            
            ApplyMarkerPosition();
        }

        private void ApplyMarkerPosition()
        {
            if (!trackArea || !marker) return;
            
            // Horizontal positioning (X-axis)
            float x = Mathf.Lerp(GetTrackLeft(), GetTrackRight(), t);
            var pos = marker.anchoredPosition;
            pos.x = x;
            marker.anchoredPosition = pos;
        }

        private async UniTask Evaluate()
        {
            state = MinigameState.Result;
            bool success = IsMarkerInsideZone();
            await UniTask.Delay(500);
            
            if (success)
            {
                if (resultText != null)
                    resultText.text = "Catch!";
                
                onCatch?.Invoke();
                SetResult(Result.Success);
            }
            else
            {
                if (resultText != null)
                    resultText.text = "Miss!";
                
                onMiss?.Invoke();
                SetResult(Result.Failure);
            }
        }

        private bool IsMarkerInsideZone()
        {
            if (!marker || !successZone) return false;
            
            // Check horizontal overlap
            float markerX = marker.anchoredPosition.x;
            float zoneHalf = successZone.rect.width * 0.5f;
            float zoneCenter = successZone.anchoredPosition.x;
            float zoneMin = zoneCenter - zoneHalf;
            float zoneMax = zoneCenter + zoneHalf;
            
            return markerX >= zoneMin && markerX <= zoneMax;
        }

        private void SetupZone(float difficulty)
        {
            if (!trackArea || !successZone || _tapZoneData == null) return;

            float trackW = trackArea.rect.width;
            
            // Get zone size from DifficultValue (0-1 range)
            // As difficulty increases, zone size DECREASES
            currentZoneSizeFraction = _tapZoneData.GetZoneSize(difficulty);
            
            // Calculate actual zone width in pixels
            float zoneW = Mathf.Clamp(currentZoneSizeFraction, 0.05f, 0.95f) * trackW;

            // Random center position within clamp range
            float minCenter = Mathf.Lerp(GetTrackLeft(), GetTrackRight(), _tapZoneData.ZoneCenterClamp.x);
            float maxCenter = Mathf.Lerp(GetTrackLeft(), GetTrackRight(), _tapZoneData.ZoneCenterClamp.y);
            float centerX = UnityEngine.Random.Range(minCenter, maxCenter);

            // Apply size (horizontal)
            var size = successZone.sizeDelta;
            size.x = zoneW;
            successZone.sizeDelta = size;

            // Apply position (horizontal) - clamp to keep zone fully inside track
            var pos = successZone.anchoredPosition;
            pos.x = Mathf.Clamp(centerX, GetTrackLeft() + zoneW * 0.5f, GetTrackRight() - zoneW * 0.5f);
            successZone.anchoredPosition = pos;
            
            Debug.Log($"TapZone: Difficulty {difficulty:F1} | Zone Size: {currentZoneSizeFraction:P1} | Width: {zoneW:F0}px | Speed: {currentSpeed:F2}");
        }

        private void UpdateDebugInfo(float difficulty)
        {
            if (!showDebugInfo || debugText == null || _tapZoneData == null) return;
            
            float speedMult = _tapZoneData.GetSpeedMultiplier(difficulty);
            float zoneSize = _tapZoneData.GetZoneSize(difficulty);
            
            string difficultyLevel = difficulty > DifficultValue.ImpossibleThreshold ? "<color=red>IMPOSSIBLE</color>" : $"{difficulty:F1}";
            
            debugText.text = $"<b>Difficulty: {difficultyLevel}</b>\n" +
                           $"Speed: {currentSpeed:F2} (base: {_tapZoneData.BaseSpeed:F1}, mult: {speedMult:F2}x)\n" +
                           $"Zone Size: {currentZoneSizeFraction:P1} (width: {successZone.rect.width:F0}px)";
        }

        private float GetTrackLeft() => -trackArea.rect.width * 0.5f;
        private float GetTrackRight() => trackArea.rect.width * 0.5f;

        protected override void OnCancel()
        {
            state = MinigameState.Idle;
            if (resultText != null)
                resultText.text = "Cancelled";
        }

        protected override void OnCleanUp()
        {
            state = MinigameState.Idle;
            t = 0f;
            direction = 1;
            
            if (debugText != null && showDebugInfo)
                debugText.text = "";
        }

        public void PressStop()
        {
            PerformTap();
        }

        private void OnValidate()
        {
            if (_tapZoneData == null)
            {
                Debug.LogWarning($"TapZone on {gameObject.name}: TapZoneData is not assigned!");
            }
        }
    }
}