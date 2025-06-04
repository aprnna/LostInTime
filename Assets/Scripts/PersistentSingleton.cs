 using System;
 using UnityEngine;

 public class PersistentSingleton<T> : MonoBehaviour where T : Component  
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
#if UNITY_EDITOR
                if(!Application.isPlaying)
                {
                    throw new UnauthorizedAccessException("Accessing PersistentSingleton while In Editor is Illegal");
                }
#endif
                if (_instance == null)
                {
                    if (_isDestroyed) return null;
                    _instance = FindExistingInstance() ?? CreateNewInstance();
                }

                return _instance;
            }
        }

        private static bool _isDestroyed;



        private static T FindExistingInstance()
        {
            T[] existingInstances = FindObjectsByType<T>(FindObjectsSortMode.None);
            
            // No instance found
            if (existingInstances == null || existingInstances.Length == 0) return null;

            return existingInstances[0];
        }

        private static T CreateNewInstance()
        {
            var resourcePath = $"{typeof(T).Name}/{typeof(T).Name}";
            var prefab = Resources.Load<T>(resourcePath);
            if (prefab == null)
            {
                var containerGO = new GameObject("__" + typeof(T).Name + " (Persistent Singleton)");
                return containerGO.AddComponent<T>();
            }

            return Instantiate<T>(prefab);
        }

        protected virtual void NotifyInstanceRepeated()
        {
            Component.Destroy(this.GetComponent<T>());
        }

        protected virtual void OnDestroy()
        {
            if (this != _instance) return;
            _isDestroyed = true;
        }

        protected virtual void Awake()
        {
            T thisInstance = this.GetComponent<T>();

            if (_instance == null)
            {
                _instance = thisInstance;
                DontDestroyOnLoad(_instance.gameObject);

            }

            else if (thisInstance != _instance)
            {
                NotifyInstanceRepeated();
                return;
            }
        }
    }