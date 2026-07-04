using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonFunctions : MonoBehaviour
{
    public Sprite sprite1, sprite2;
    private bool boolsprite1 = true;
    public GameObject text;
    public AudioSource sounds;
    public QuizRoadManager quizManager;
    public void ExitApp()
    {
        Application.Quit();
    }

    public void ChangeSprite()
    {
        if(boolsprite1)
        {
            gameObject.GetComponent<Image>().sprite = sprite2;
            boolsprite1 = false;
        }
        else
        {
            gameObject.GetComponent<Image>().sprite = sprite1;
            boolsprite1 = true;
        }
    }

    public void PressDown()
    {
        text.transform.localPosition += new Vector3(0f, -5f, 0f);
    }

    public void PressUp()
    {
        text.transform.localPosition += new Vector3(0f, 5f, 0f);
    }

    public void ToggleSounds()
    {
        sounds.mute = !sounds.mute;
        quizManager.GetComponent<AudioSource>().mute = !quizManager.GetComponent<AudioSource>().mute;
    }
}
