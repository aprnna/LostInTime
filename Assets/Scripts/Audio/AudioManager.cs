using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;

namespace Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager:PersistentSingleton<AudioManager>
    {
        [SerializeField] private AudioSO _audioSo;
        private AudioSource _audioSource;

        private void Start()
        {
            _audioSource = gameObject.GetComponent<AudioSource>();
        }

        public void PlaySound(SoundType sound, AudioSource source = null, float volume = 1)
        {
            SoundList soundList = _audioSo.sounds[(int)sound];
            AudioClip[] clips = soundList.sounds;
            AudioClip randomClip = clips[UnityEngine.Random.Range(0, clips.Length)];
            
            if(source)
            {
                source.outputAudioMixerGroup = soundList.mixer;
                source.clip = randomClip;
                source.volume = volume * soundList.volume;
                source.Play();
            }
            else
            {
                _audioSource.outputAudioMixerGroup = soundList.mixer;
                _audioSource.PlayOneShot(randomClip, volume * soundList.volume);
            }
        }

        public void StopSound(SoundType sound, AudioSource source = null)
        {
            SoundList soundList = _audioSo.sounds[(int)sound];
            AudioClip[] clips = soundList.sounds;
            if(source)
            {
                source.Stop();
            }
            else
            {
                _audioSource.outputAudioMixerGroup = soundList.mixer;
                _audioSource.Stop();
            }
        }
    }
    [Serializable]
    public struct SoundList
    {
        [HideInInspector] public string name;
        [Range(0, 1)] public float volume;
        public AudioMixerGroup mixer;
        public AudioClip[] sounds;
    }
}