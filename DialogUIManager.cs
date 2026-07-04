using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class DialogUIManager : MonoBehaviour
{
    [Header("UI References")]
    public Button previousButton;
    public Button nextButton;
    public Text canvasCounterText;
    public Text dialogInfoText;

    [Header("Controller Reference")]
    public MCPDialogControllerComplete dialogController;

    void Start()
    {
        // Setup button events
        if (previousButton != null)
        {
            previousButton.onClick.AddListener(OnPreviousClicked);
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(OnNextClicked);
        }

        // Initialize UI
        UpdateUI();
    }

    public void OnPreviousClicked()
    {
        if (dialogController == null) return;

        dialogController.PreviousCanvas();
        UpdateUI();
    }

    public void OnNextClicked()
    {
        if (dialogController == null) return;

        dialogController.NextCanvas();
        UpdateUI();
    }

    public void OnDialogFetched()
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (dialogController == null) return;

        int canvasCount = dialogController.GetCanvasCount();
        int currentIndex = dialogController.GetCurrentCanvasIndex();

        // Update navigation buttons
        if (previousButton != null)
        {
            previousButton.interactable = currentIndex > 0;
        }

        if (nextButton != null)
        {
            if (!dialogController.GetAfterQuizMode())
            {
                nextButton.interactable = currentIndex < canvasCount - 1 - dialogController.GetTotalNumOfQuestions();
            }
            else
            {
                nextButton.interactable = currentIndex < canvasCount - 1 ;
            }
        }

        // Update counter text
        if (canvasCounterText != null)
        {
            canvasCounterText.text = $"Canvas {currentIndex + 1} of {canvasCount}";
        }

        // Update dialog info
        if (dialogInfoText != null)
        {
            Dialog dialog = dialogController.GetCurrentDialog();
            if (dialog != null)
            {
                // Count total stories, messages and Q&As from all sections
                int totalStories = dialog.stories?.Count ?? 0;
                int totalMessages = 0;
                int totalQAs = 0;
                if (dialog.sections != null)
                {
                    foreach (var section in dialog.sections)
                    {
                        if (section.messages != null)
                            totalMessages += section.messages.Count;
                        if (section.question_answers != null)
                            totalQAs += section.question_answers.Count;
                    }
                }

                dialogInfoText.text = $"Dialog: {dialog.title}\n" +
                                    $"Participants: {dialog.participants?.Count ?? 0}\n" +
                                    $"Stories: {totalStories}\n" +
                                    $"Messages: {totalMessages}\n" +
                                    $"Question-Answers: {totalQAs}\n" +
                                    $"Sections: {dialog.sections?.Count ?? 0}";
            }
            else
            {
                dialogInfoText.text = "No dialog loaded";
            }
        }
        Debug.Log($"CanvasCount: {canvasCount}, CurrentIndex: {currentIndex}");
    }

    // Method to be called when a new dialog is fetched
    public void RefreshUI()
    {
        OnDialogFetched();
    }
}
