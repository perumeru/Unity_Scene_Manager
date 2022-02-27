using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Singleton
{
    public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : SingletonMonoBehaviour<T>
    {
        private static T instance = default(T);

        public static T Instance
        {
            get
            {
                return instance ? instance : instance = FindObjectOfType<T>() ? FindObjectOfType<T>() : new GameObject(typeof(T).Name).AddComponent<T>();
            }
        }

        virtual protected void Awake()
        {
            if (Instance != this) DestroyImmediate(this);
            else if(instance == null) instance = (T)this;
        }
    }
}