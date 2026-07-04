using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AutoScrollHandler : MonoBehaviour
{
    private ScrollRect scrollRect;

    void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();
    }

    public void OnTextUpdated()
    {
        if (scrollRect == null) return;

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }
}
