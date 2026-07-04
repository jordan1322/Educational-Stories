using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PanelUI : MonoBehaviour
{
    public Image characterImage;
    
    public void SetSprite(Sprite sprite)
    {
        characterImage.sprite= sprite;
    }
}
