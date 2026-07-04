using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class QuizButton : MonoBehaviour
{
    public bool correctAnswer=false;
    public TMP_Text buttonAnswer;
    public GameObject correctText, incorrectText;
    public GameObject gameManager;
    private QuizRoadManager quizRoadManager;

    private void Start()
    {
        quizRoadManager = FindObjectOfType<QuizRoadManager>();
    }
    public void SetAnswer(string answer)
    {
        buttonAnswer.text = answer;
    }

    public void CorrectAnswer()
    {
        quizRoadManager.SubmitAnswer(correctAnswer);
        GameObject.Find("Game Manager").GetComponent<MCPDialogControllerComplete>().QuestionAnswered(correctAnswer);
        if (correctAnswer)
        {
            gameObject.GetComponent<Button>().image.color = Color.green;
            correctText.SetActive(true);
            
        }
        else
        {
            gameObject.GetComponent<Button>().image.color = Color.red;
            incorrectText.SetActive(true);
        }
    }    

    public void HideButton()
    {
        if (!correctAnswer)
        {
            gameObject.SetActive(false);
        }
        else
        {
            gameObject.GetComponent<Button>().image.color = Color.green;
            gameObject.GetComponent<Button>().enabled = false;

        }
    }
}
