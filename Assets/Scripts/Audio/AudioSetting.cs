using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Audio
{
    public class AudioSetting:MonoBehaviour
    {
        [SerializeField] private AudioMixer _myMixer;
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Slider _sfxSlider;
        
        private void Start()
        {
            Debug.Log(PlayerPrefs.HasKey("musicVolume"));
            if (PlayerPrefs.HasKey("musicVolume"))
            {
                LoadVolume();
            }
            else
            {
                SetMusicVolume();
                SetSfxVolume();
            }
        }

        public void SetMusicVolume()
        {
            float volume = _musicSlider.value;
            _myMixer.SetFloat("music", Mathf.Log10(volume)*20);
            PlayerPrefs.SetFloat("musicVolume",volume);
        }
        public void SetSfxVolume()
        {
            float volume = _sfxSlider.value;
            _myMixer.SetFloat("sfx", Mathf.Log10(volume)*20);
            PlayerPrefs.SetFloat("sfxVolume",volume);
        }


        private void LoadVolume()
        {
            _musicSlider.value = PlayerPrefs.GetFloat("musicVolume");
            _sfxSlider.value = PlayerPrefs.GetFloat("sfxVolume");
            SetMusicVolume();
            SetSfxVolume();
        }
    }
}