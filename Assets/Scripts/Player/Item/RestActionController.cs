using System;
using Manager;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Player.Item
{
    public class RestActionController:MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private RestItem _restAction;
        [SerializeField] private BaseAction _action;
        public BaseAction Action => _action;
        public RestItem RestItem => _restAction;
        private RestManager _restManager;
        void Start()
        {
            _restManager = RestManager.Instance;
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            _restManager.OnClickAction(this);
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            _restManager.OnHoverAction(this);
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            _restManager.SetDescription("");

        }
        void OnDestroy()
        {
            _restManager.SetDescription("");
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