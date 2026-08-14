using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class UICanvas : Singleton<UICanvas>
{
    [SerializeField] bool isDestroyOnClose = false;


    //xu ly canvas dien thoai tai tho
    private void Awake()
    {
        RectTransform rect = GetComponent<RectTransform>();
        float ratio = (float)Screen.width / Screen.height;
        if (ratio > 2.1f)
        {
            Vector2 leftBottom = rect.offsetMin;
            Vector2 rightTop = rect.offsetMax;

            leftBottom.y = 0f;
            rightTop.y = 0f;

            rect.offsetMin = leftBottom;
            rect.offsetMax = rightTop;
        }
    }
    //goi truoc khi canvas active
    public virtual void SetUp()
    {

    }

    //goi sau khi duoc active
    public virtual void Open()
    {
        gameObject.SetActive(true);
    }

    //tat canvas sau n time(s)
    public virtual void Close(float time)
    {
        Invoke(nameof(CloseDirectly), time);
    }

    //tat luon canvas
    public virtual void CloseDirectly()
    {
        if (isDestroyOnClose)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);

        }
    }
}