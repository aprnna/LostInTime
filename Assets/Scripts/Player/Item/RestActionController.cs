using System;
using Manager;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Player.Item
{
    public class RestActionController:MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private RestItem _restAction;
        [SerializeField] private BaseAction[] _action;
        public BaseAction[] Action => _action;
        public RestItem RestItem => _restAction;
        private RestSystem _restSystem;
        void Start()
        {
            _restSystem = RestSystem.Instance;
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            _restSystem.OnClickAction(this);
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            _restSystem.OnHoverAction(this);
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            _restSystem.SetDescription("");

        }
        void OnDestroy()
        {
            _restSystem.SetDescription("");
        }
    }

    [Serializable]
    public class RestItem
    {
        [SerializeField] private int _min;
        [SerializeField] private int _max;
        [SerializeField] private RestType _type;
        public int Min => _min;
        public int Max => _max;
        public RestType Type => _type;
    }

    public enum RestType
    {
        Repair,
        Heal
    }
}