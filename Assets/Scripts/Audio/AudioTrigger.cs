using System;
using UnityEngine;

namespace Audio
{
    public class AudioTrigger:MonoBehaviour
    {
        private AudioManager _audioManager;
        [SerializeField] private SoundType _soundType;

        private void Start()
        {
            _audioManager = AudioManager.Instance;
        }

        public void TriggerSound()
        {
            _audioManager.PlaySound(_soundType);
        }

    }
}