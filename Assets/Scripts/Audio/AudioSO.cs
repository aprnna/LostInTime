using UnityEngine;

namespace Audio
{ 
    [CreateAssetMenu(menuName = "Audio Manager", fileName = "Sounds SO")]
    public class AudioSO:ScriptableObject
    {
        public SoundList[] sounds;
    }
}