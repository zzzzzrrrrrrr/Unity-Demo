// Path: Assets/_Scripts/Tools/SingletonMono.cs
using UnityEngine;

namespace ARPGDemo.Tools
{
    /// <summary>
    public abstract class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static readonly object InstanceLock = new object();
        private static T s_instance;
        private static bool s_isQuitting;

        [Header("Singleton")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        public static T Instance
        {
            get
            {
                if (s_isQuitting)
                {
                    return null;
                }

                lock (InstanceLock)
                {
                    if (s_instance == null)
                    {
                        s_instance = FindObjectOfType<T>();
                    }

                    return s_instance;
                }
            }
        }

        protected virtual bool DontDestroyEnabled => dontDestroyOnLoad;

        protected virtual void Awake()
        {
            lock (InstanceLock)
            {
                if (s_instance == null)
                {
                    s_instance = this as T;
                    if (DontDestroyEnabled)
                    {
                        DontDestroyOnLoad(gameObject);
                    }
                    return;
                }

                if (s_instance != this)
                {
                    Destroy(gameObject);
                }
            }
        }

        protected virtual void OnApplicationQuit()
        {
            s_isQuitting = true;
        }

        protected virtual void OnDestroy()
        {
            lock (InstanceLock)
            {
                if (s_instance == this)
                {
                    s_instance = null;
                }
            }
        }
    }
}

