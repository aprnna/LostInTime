using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Player.UI
{
    public class StatsItem:MonoBehaviour
    {
        [SerializeField] private string _name;
        [SerializeField] private TMP_Text _textMin;
        [SerializeField] private TMP_Text _textMax;

        public void SetStat(int max, int min, bool maxOnly = false)
        {
            if (maxOnly) _textMax.text = max.ToString();
            else
            {
                _textMax.text = max.ToString();
                _textMin.text = min.ToString();
            }
        }
    }
}