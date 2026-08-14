using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T instance;

    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                instance = GameObject.FindFirstObjectByType<T>();

                if (instance == null)
                {
                    instance = new GameObject(nameof(T)).AddComponent<T>();
                }
            }
            return instance;
        }


    }
}