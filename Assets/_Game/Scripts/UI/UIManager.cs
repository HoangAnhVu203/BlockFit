using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : Singleton<UIManager>
{
    private const string UI_RESOURCE_PATH = "";

    Dictionary<System.Type, UICanvas> canvasActives = new Dictionary<System.Type, UICanvas>();
    Dictionary<System.Type, UICanvas> canvasPrefabs = new Dictionary<System.Type, UICanvas>();
    [SerializeField] Transform parent;

    private void Awake()
    {
        instance = this;

        if (parent == null)
        {
            parent = transform;
        }

        GameObject[] prefabs = Resources.LoadAll<GameObject>(UI_RESOURCE_PATH);
        for (int i = 0; i < prefabs.Length; i++)
        {
            UICanvas canvas = prefabs[i].GetComponent<UICanvas>();
            if (canvas == null)
            {
                continue;
            }

            canvasPrefabs[canvas.GetType()] = canvas;
        }
    }

    //mo canvas
    public T OpenUI<T>() where T : UICanvas
    {
        T canvas = GetUI<T>();

        canvas.SetUp();
        canvas.Open();

        return canvas;
    }

    //Dong canvas sau n time(s)
    public void CloseUI<T>(float time) where T : UICanvas
    {
        if (IsUILoaded<T>())
        {
            canvasActives[typeof(T)].Close(time);
        }
    }


    //Dong canvas truc tiep
    public UICanvas CloseUIDirectly<T>() where T : UICanvas
    {
        if (IsUILoaded<T>())
        {
            canvasActives[typeof(T)].CloseDirectly();
        }
        return null;
    }

    //Kiem tra canvas da dc tao chua
    public bool IsUILoaded<T>() where T : UICanvas
    {
        return canvasActives.ContainsKey(typeof(T)) && canvasActives[typeof(T)] != null;
    }

    //Kiem tra canvas da duoc active chua
    public bool IsUIOpened<T>() where T : UICanvas
    {
        return IsUILoaded<T>() && canvasActives[typeof(T)].gameObject.activeSelf;
    }

    //Lay canvas
    public T GetUI<T>() where T : UICanvas
    {
        if (!IsUILoaded<T>())
        {
            T prefab = GetUIPrefabs<T>();
            T canvas = Instantiate(prefab, parent);

            canvasActives[typeof(T)] = canvas;
        }

        return canvasActives[typeof(T)] as T;
    }


    public T GetUIPrefabs<T>() where T : UICanvas
    {
        if (canvasPrefabs.TryGetValue(typeof(T), out UICanvas prefab) && prefab != null)
        {
            return prefab as T;
        }

        throw new KeyNotFoundException(
            $"UI prefab '{typeof(T).Name}' was not loaded. Put a prefab with '{typeof(T).Name}' under a Resources folder."
        );
    }

    //Dong tat ca
    public void CloseAll()
    {
        foreach (var canvas in canvasActives.Values)
        {
            if (canvas != null && canvas.gameObject.activeSelf)
            {
                canvas.Close(0);
            }
        }
    }
}