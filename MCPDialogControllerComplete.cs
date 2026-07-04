using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using TMPro;
using static Dialog;

/// <summary>
/// Fixed MCP Dialog Controller with improved error handling and timeout management
/// </summary>
public class MCPDialogControllerComplete : MonoBehaviour
{

    public bool serverMode;
    [Header("MCP Configuration")]
    public string pythonPath = "python";
    public string serverScriptPath = "mcp_server_fixed.py";
    public string workingDirectory = ""; // *** SOS *** add the path to the server

    // Alternative Python paths to try
    private readonly string[] pythonPaths = {
        "python",
        "python3",
        "C:\\Users\\User\\AppData\\Local\\Microsoft\\WindowsApps\\python.exe",
        "C:\\Program Files\\Python311\\python.exe",
        "C:\\Python311\\python.exe"
    };

    [Header("UI References")]
    public TMP_InputField dialogIdInput;
    public Button fetchButton;
    public TextMeshProUGUI getDialogText;

    [Header("Canvas Prefabs")]
    public GameObject firstDialogPrefab;
    public GameObject secondDialogPrefab;
    public GameObject canvasQAPrefab;
    public GameObject canvasStoryPrefab;
    public Transform canvasParent;
    public Transform roadParent;
    public Transform firstCanvas;

    [Header("Sprites")]
    public Sprite femaleTeacher;
    public Sprite maleTeacher;
    public Sprite femaleStudent;
    public Sprite maleStudent;
    public Sprite neutralPerson;

    [Header("Text References in CanvasGo")]
    public string msgSenderTextName = "msgSender";
    public string msgGenderTextName = "msgGender";
    public string msgContentTextName = "msgContent";

    [Header("Text References in CanvasQA")]
    public string questionTextName = "questionText";
    public string answersTextName = "answersText";
    public string rightAnswerTextName = "rightAnswerText";

    [Header("Text References in CanvasStory")]
    public string storyContentTextName = "storyContent";

    [Header("UI Manager Reference")]
    public DialogUIManager uiManager;

    [Header("Debug Settings")]
    public bool enableDebugLogging = true;
    public float connectionTimeout = 10f;
    public float responseTimeout = 5f;

    private Process mcpServerProcess;
    private StreamWriter serverInput;
    private StreamReader serverOutput;
    private StreamReader serverError;
    private List<GameObject> instantiatedCanvases = new List<GameObject>();
    private Dialog currentDialog;
    private int currentCanvasIndex = 0;
    private bool isServerConnected = false;
    private bool isConnecting = false;
    private Coroutine connectionCoroutine;

    [Header("Menus and Panels")]
    public GameObject mainMenu;
    public GameObject settingsMenu;
    public GameObject storyMenu;
    public GameObject loadingScreen;
    public GameObject quizSettingsPanel;
    public GameObject errorQuizPanel;
    public SaveSystem saveSystem;

    [Header("Buttons")]
    public GameObject previousButton;
    public GameObject nextButton;
    public GameObject storySettingsButton;
    public GameObject goToButtons;

    [Header("Loading and Questions Text")]
    public TMP_Text loadingText;
    public Slider loadingBar;
    public TMP_Text numOfQuestionsText;

    private bool speedMode = false, isTyping = false, skipText = false, quizMode = false, afterQuiz=false;
    private float loadingProgress = 0f;
    private int correctQuestions = 0, totalQuestions = 0,totalQuestionsCreated=0;

    [Header("Sounds")]
    public AudioSource audioSource;
    public AudioClip clickSound;
    public AudioClip typeSound;

    // MCP Protocol classes
    [System.Serializable]
    public class MCPRequest
    {
        public string jsonrpc = "2.0";
        public int id;
        public string method;
        public object @params;
    }

    [System.Serializable]
    public class MCPResponse
    {
        public string jsonrpc = "2.0";
        public int id;
        public object result;
        public MCPError error;
    }

    [System.Serializable]
    public class MCPError
    {
        public int code;
        public string message;
        public object data;
    }

    [System.Serializable]
    public class MCPToolResult
    {
        public List<MCPContent> content;
        public bool isError;
    }

    [System.Serializable]
    public class MCPContent
    {
        public string type;
        public string text;
    }

    void Start()
    {
        StartCoroutine(LoadingAnimation());
        StartCoroutine(AnimateDots());
        // Setup button events
        if (fetchButton != null)
            fetchButton.onClick.AddListener(OnFetchButtonClicked);

        dialogIdInput.onValueChanged.AddListener(PlayTypeSound);

        Button[] buttons = FindObjectsOfType<Button>(true);

        foreach (Button btn in buttons)
        {
            btn.onClick.AddListener(PlayClick);
        }

        // Start MCP server connection with timeout
        if (serverMode)
        {
            StartCoroutine(ConnectToMCPServerWithTimeout());
        }

    }

    void PlayClick()
    {
        audioSource.PlayOneShot(clickSound);
    }

    private string oldText = "";
    void PlayTypeSound(string newText)
    {
        if (newText.Length > oldText.Length)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(typeSound);
            audioSource.pitch = 1f;
        }

        oldText = newText;
    }


    private IEnumerator ConnectToMCPServerWithTimeout()
    {
        if (isConnecting)
        {
            DebugLog("Already connecting to MCP server, skipping...");
            yield break;
        }

        isConnecting = true;
        UpdateStatus("Connecting to MCP server...");

        // Start connection coroutine
        connectionCoroutine = StartCoroutine(ConnectToMCPServer());

        // Wait for connection with timeout
        float elapsedTime = 0f;
        while (isConnecting && elapsedTime < connectionTimeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsedTime += 0.1f;
        }

        if (isConnecting)
        {
            DebugLog("Connection timeout reached, stopping connection attempt");
            if (connectionCoroutine != null)
            {
                StopCoroutine(connectionCoroutine);
                connectionCoroutine = null;
            }
            UpdateStatus("Connection timeout - MCP server may not be responding");
            isConnecting = false;
        }
    }

    private IEnumerator ConnectToMCPServer()
    {
        UpdateStatus("Starting MCP server...");

        bool serverStarted = false;
        Exception lastException = null;
        string workingPythonPath = null;

        // Try each Python path until one works
        foreach (string pythonPathToTry in pythonPaths)
        {
            UpdateStatus($"Trying Python path: {pythonPathToTry}");
            DebugLog($"Attempting to start MCP server with Python: {pythonPathToTry}");

            try
            {
                // Start the MCP server process
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = pythonPathToTry,
                    Arguments = serverScriptPath,
                    WorkingDirectory = "C:/Users/iorda/OneDrive/Desktop/pyMCPServerJson",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                mcpServerProcess = Process.Start(startInfo);
                serverInput = mcpServerProcess.StandardInput;
                serverOutput = mcpServerProcess.StandardOutput;
                serverError = mcpServerProcess.StandardError;
                serverStarted = true;
                workingPythonPath = pythonPathToTry;
                DebugLog($"Successfully started MCP server with Python: {pythonPathToTry}");
                break;
            }
            catch (Exception e)
            {
                lastException = e;
                DebugLog($"Failed to start with Python path '{pythonPathToTry}': {e.Message}");

                // Clean up any partial process
                if (mcpServerProcess != null && !mcpServerProcess.HasExited)
                {
                    mcpServerProcess.Kill();
                    mcpServerProcess = null;
                }
            }
        }

        if (!serverStarted)
        {
            string errorMessage = lastException != null ? lastException.Message : "Unknown error";
            UpdateStatus($"Failed to start MCP server with any Python path. Last error: {errorMessage}");
            DebugLogError($"MCP Server Error: {lastException}");
            isConnecting = false;
            yield break;
        }

        UpdateStatus($"MCP server started successfully with Python: {workingPythonPath}");

        // Wait a moment for server to start
        yield return new WaitForSeconds(1f);

        // Initialize MCP connection with timeout
        yield return StartCoroutine(InitializeMCPConnectionWithTimeout());
    }

    private IEnumerator InitializeMCPConnectionWithTimeout()
    {
        UpdateStatus("Initializing MCP connection...");

        // Check if the process is still running
        if (mcpServerProcess == null || mcpServerProcess.HasExited)
        {
            UpdateStatus("MCP server process has terminated unexpectedly");
            DebugLogError("MCP server process is not running");
            isConnecting = false;
            yield break;
        }

        bool initSuccess = false;
        Exception initException = null;
        string response = null;

        try
        {
            // Send initialization request
            var initRequest = new MCPRequest
            {
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new
                    {
                        tools = new { }
                    },
                    clientInfo = new
                    {
                        name = "Unity MCP Client",
                        version = "1.0.0"
                    }
                }
            };

            string initJson = JsonConvert.SerializeObject(initRequest);
            DebugLog($"Sending initialization request: {initJson}");
            serverInput.WriteLine(initJson);
            serverInput.Flush();
            initSuccess = true;
        }
        catch (Exception e)
        {
            initException = e;
        }

        if (initException != null)
        {
            UpdateStatus($"MCP initialization error: {initException.Message}");
            DebugLogError($"MCP Init Error: {initException}");
            isConnecting = false;
            yield break;
        }

        if (!initSuccess)
        {
            UpdateStatus("Failed to send initialization request");
            isConnecting = false;
            yield break;
        }

        // Read response with timeout
        DebugLog("Waiting for MCP server initialization response...");
        float elapsedTime = 0f;
        bool responseReceived = false;

        while (!responseReceived && elapsedTime < responseTimeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsedTime += 0.1f;

            // Check if process is still running
            if (mcpServerProcess == null || mcpServerProcess.HasExited)
            {
                UpdateStatus("MCP server process terminated during initialization");
                DebugLogError("MCP server process exited unexpectedly");
                isConnecting = false;
                yield break;
            }

            // Try to read response
            try
            {
                if (serverOutput.Peek() >= 0)
                {
                    response = serverOutput.ReadLine();
                    responseReceived = true;
                    DebugLog($"Received MCP response: {response}");
                }
            }
            catch (Exception e)
            {
                DebugLog($"Error reading response: {e.Message}");
            }
        }

        if (!responseReceived)
        {
            UpdateStatus("MCP server initialization timeout");
            DebugLogError("MCP server did not respond within timeout period");
            isConnecting = false;
            yield break;
        }

        if (!string.IsNullOrEmpty(response))
        {
            try
            {
                var initResponse = JsonConvert.DeserializeObject<MCPResponse>(response);
                if (initResponse.error == null)
                {
                    isServerConnected = true;
                    UpdateStatus("Connected to MCP server successfully");
                    DebugLog("MCP server initialization successful");

                    // Send notifications/initialized to complete the MCP handshake
                    try
                    {
                        var initializedNotification = new
                        {
                            jsonrpc = "2.0",
                            method = "notifications/initialized",
                            @params = new { }
                        };

                        string notificationJson = JsonConvert.SerializeObject(initializedNotification);
                        DebugLog($"Sending initialized notification: {notificationJson}");
                        serverInput.WriteLine(notificationJson);
                        serverInput.Flush();
                        DebugLog("Initialized notification sent successfully");
                    }
                    catch (Exception e)
                    {
                        DebugLogError($"Error sending initialized notification: {e.Message}");
                    }
                }
                else
                {
                    UpdateStatus($"MCP initialization failed: {initResponse.error.message}");
                }
            }
            catch (Exception e)
            {
                UpdateStatus($"Error parsing MCP response: {e.Message}");
                DebugLogError($"MCP Parse Error: {e}");
            }
        }
        else
        {
            UpdateStatus("No response from MCP server");
        }

        isConnecting = false;
    }

    public void OnFetchButtonClicked()
    {
        if (!serverMode)
        {
            LoadLocalDialog(dialogIdInput.text.Trim());
            return;
        }

        if (!isServerConnected)
        {
            UpdateStatus("Not connected to MCP server");
            return;
        }

        if (dialogIdInput == null || string.IsNullOrEmpty(dialogIdInput.text))
        {
            UpdateStatus("Please enter a dialog ID (try: dialog_001)");
            getDialogText.text = "Please enter a dialog ID";
            return;
        }


        string dialogId = dialogIdInput.text.Trim();
        DebugLog($"Attempting to fetch dialog with ID: {dialogId}");
        StartCoroutine(FetchDialogWithTimeout(dialogId));
    }

    private IEnumerator FetchDialogWithTimeout(string dialogId)
    {
        UpdateStatus("Fetching dialog...");
        getDialogText.text = "Fetching Story...";
        DebugLog($"Starting fetch dialog for ID: {dialogId}");

        bool requestSent = false;
        Exception requestException = null;


        try
        {
            // Call get_dialog tool
            var request = new MCPRequest
            {
                id = UnityEngine.Random.Range(1000, 9999),
                method = "tools/call",
                @params = new
                {
                    name = "get_dialog",
                    arguments = new
                    {
                        dialog_id = dialogId
                    }
                }
            };

            string requestJson = JsonConvert.SerializeObject(request);
            DebugLog($"Sending fetch request: {requestJson}");
            serverInput.WriteLine(requestJson);
            serverInput.Flush();
            DebugLog("Fetch request sent successfully");

            requestSent = true;
        }
        catch (Exception e)
        {
            requestException = e;
        }

        if (requestException != null)
        {
            UpdateStatus($"Error sending fetch request: {requestException.Message}");
            DebugLogError($"Fetch Request Error: {requestException}");
            yield break;
        }

        if (!requestSent)
        {
            UpdateStatus("Failed to send fetch request");
            yield break;
        }

        // Wait for response with timeout
        DebugLog("Waiting for MCP server to process fetch request...");
        float elapsedTime = 0f;
        bool responseReceived = false;
        string response = null;

        while (!responseReceived && elapsedTime < responseTimeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsedTime += 0.1f;

            // Check if server process is still running
            if (mcpServerProcess == null || mcpServerProcess.HasExited)
            {
                UpdateStatus("MCP server process terminated during fetch");
                DebugLogError("MCP server process exited during fetch operation");
                yield break;
            }

            // Try to read response
            try
            {
                // Check if there's data available to read
                if (serverOutput != null && !serverOutput.EndOfStream)
                {
                    // Use a more robust reading approach
                    if (serverOutput.Peek() >= 0)
                    {
                        response = serverOutput.ReadLine();
                        if (!string.IsNullOrEmpty(response))
                        {
                            responseReceived = true;
                            DebugLog($"Received response: {response}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugLog($"Error reading response: {e.Message}");
                // Don't break on read errors, continue trying
            }
        }

        if (!responseReceived)
        {
            UpdateStatus("No response received from MCP server (timeout)");
            DebugLogError("MCP server did not respond within timeout period");
            yield break;
        }

        if (!string.IsNullOrEmpty(response))
        {
            try
            {
                var mcpResponse = JsonConvert.DeserializeObject<MCPResponse>(response);
                if (mcpResponse.error == null && mcpResponse.result != null)
                {
                    // Parse the result
                    var result = JsonConvert.SerializeObject(mcpResponse.result);
                    var toolResult = JsonConvert.DeserializeObject<MCPToolResult>(result);

                    if (toolResult != null && toolResult.content != null && toolResult.content.Count > 0)
                    {
                        string dialogJson = toolResult.content[0].text;
                        currentDialog = JsonConvert.DeserializeObject<Dialog>(dialogJson);

                        if (currentDialog != null)
                        {
                            UpdateStatus($"Dialog '{currentDialog.title}' loaded successfully");
                            getDialogText.text = "Story loaded successfully";
                            saveSystem.IsStoryReread(dialogId);
                            mainMenu.gameObject.SetActive(false);
                            settingsMenu.gameObject.SetActive(false);
                            storyMenu.gameObject.SetActive(false);
                            previousButton.gameObject.SetActive(true);
                            nextButton.gameObject.SetActive(true);
                            goToButtons.gameObject.SetActive(true);
                            storySettingsButton.gameObject.SetActive(true);
                            DisplayDialog();

                            // Notify UI manager
                            if (uiManager != null)
                            {
                                uiManager.RefreshUI();
                            }
                        }
                        else
                        {
                            UpdateStatus("Failed to parse dialog data");
                        }
                    }
                    else
                    {
                        UpdateStatus("No dialog data received");
                    }
                }
                else
                {
                    UpdateStatus($"Error fetching dialog: {mcpResponse.error?.message ?? "Unknown error"}");
                }
            }
            catch (Exception e)
            {
                UpdateStatus($"Error parsing dialog response: {e.Message}");
                getDialogText.text = "Wrong Story ID";
                DebugLogError($"Fetch Parse Error: {e}");
            }
        }
        else
        {
            UpdateStatus("No response from server");
        }
    }

    // Display methods (simplified versions)
    private void DisplayDialog()
    {
        if (currentDialog == null) return;

        DebugLog("=== Starting DisplayDialog ===");
        DebugLog($"Dialog ID: {currentDialog.id}");
        DebugLog($"Dialog Title: {currentDialog.title}");
        DebugLog($"Stories count: {currentDialog.stories?.Count ?? 0}");
        DebugLog($"Sections count: {currentDialog.sections?.Count ?? 0}");

        // Clear existing canvases
        ClearCanvases();

        // Display stories first (if any)
        DisplayStories();

        // Display regular messages
        DisplayMessages();

        // Display question-answer pairs (main focus)
        DisplayQuestionAnswers();

        DebugLog($"=== DisplayDialog completed. Total canvases: {instantiatedCanvases.Count} ===");

        // Turn all canvases OFF
        for (int i = 0; i < instantiatedCanvases.Count; i++)
        {
            instantiatedCanvases[i].SetActive(false);
        }

        // Show first one
        currentCanvasIndex = 0;
        ActivateCanvas(0);
    }

    private void DisplayStories()
    {
        if (currentDialog == null || currentDialog.stories == null || currentDialog.stories.Count == 0)
        {
            DebugLog("No stories to display");
            return;
        }

        DebugLog($"DisplayStories: Found {currentDialog.stories.Count} stories to display");

        // Check if prefab is assigned
        if (canvasStoryPrefab == null)
        {
            DebugLogError("CanvasStoryPrefab is not assigned! Please assign the story canvas prefab in the inspector.");
            UpdateStatus("Error: Story canvas prefab not assigned");
            return;
        }

        for (int i = 0; i < currentDialog.stories.Count; i++)
        {
            Story story = currentDialog.stories[i];

            // Skip empty stories
            if (string.IsNullOrEmpty(story.content) || story.content.Trim() == "*" || story.content.Trim() == "")
            {
                DebugLog($"Skipping empty story at index {i}");
                continue;
            }

            DebugLog($"Processing story {i}: {story.content}");

            // Instantiate canvas for this story
            GameObject canvas = Instantiate(canvasStoryPrefab);
            canvas.transform.SetParent(canvasParent, false);

            instantiatedCanvases.Add(canvas);

            // Ensure canvas is properly set up for visibility
            SetupCanvasForVisibility(canvas, i);

            // Set canvas name
            canvas.name = $"StoryCanvas_{i}";
            DebugLog($"Created story canvas: {canvas.name}");

            // Find text component
            TMP_Text storyContentText = FindTextInCanvas(canvas, storyContentTextName);

            DebugLog($"Story text component found - Content: {(storyContentText != null ? "YES" : "NO")}");

            // Set story content text
            if (storyContentText != null)
            {
                string originalText = storyContentText.text;
                storyContentText.text = story.content ?? "";

                // Force text to be visible
                storyContentText.gameObject.SetActive(true);
                storyContentText.enabled = true;

                // Force UI refresh
                storyContentText.SetAllDirty();

                DebugLog($"Set story content text: '{originalText}' -> '{storyContentText.text}'");
                DebugLog($"Text component active: {storyContentText.gameObject.activeInHierarchy}");
                DebugLog($"Text component enabled: {storyContentText.enabled}");
                DebugLog($"Text color: {storyContentText.color}");
                DebugLog($"Text font size: {storyContentText.fontSize}");

                // Immediate verification
                if (string.IsNullOrEmpty(storyContentText.text))
                {
                    DebugLogError($"Story text is empty after setting! Story content: '{story.content}'");
                }
            }
            else
            {
                DebugLogError($"Story content text component not found! Looking for: {storyContentTextName}");
            }

            // Canvas will be activated later by ActivateCanvas method
            DebugLog($"Story canvas {i} created: {canvas.name}");

            DebugLog($"Story {i} completed: {story.content}");
        }

        DebugLog($"DisplayStories completed. Total canvases created: {instantiatedCanvases.Count}");
    }

    private void DisplayMessages()
    {
        // Count total messages from all sections
        int totalMessageCount = 0;
        if (currentDialog.sections != null)
        {
            foreach (var section in currentDialog.sections)
            {
                if (section.messages != null)
                    totalMessageCount += section.messages.Count;
            }
        }

        if (totalMessageCount == 0)
        {
            DebugLog("No regular messages to display");
            return;
        }

        DebugLog($"DisplayMessages: Found {totalMessageCount} messages to display");
        DebugLog($"CanvasParent: {(canvasParent != null ? "Assigned" : "NULL")}");

        int messageIndex = 0;

        // Display messages from all sections
        if (currentDialog.sections != null)
        {
            for (int sectionIndex = 0; sectionIndex < currentDialog.sections.Count; sectionIndex++)
            {
                var section = currentDialog.sections[sectionIndex];
                if (section.messages != null)
                {
                    for (int i = 0; i < section.messages.Count; i++)
                    {
                        Message message = section.messages[i];
                        DebugLog($"Processing section {sectionIndex + 1} message {i}: {message.sender} ({message.sender_gender}): {message.content}");

                        // Check if prefab is assigned

                        // Instantiate canvas for this message
                        GameObject canvas;
                        // Create canvas independently
                        if (i % 2 == 0)
                        {
                            canvas = Instantiate(firstDialogPrefab);
                        }
                        else
                        {
                            canvas = Instantiate(secondDialogPrefab);
                        }
                        canvas.transform.SetParent(canvasParent, false);
                        instantiatedCanvases.Add(canvas);



                        // Set canvas name
                        canvas.name = $"MessageCanvas_{messageIndex}_Section{sectionIndex + 1}";
                        DebugLog($"Created canvas: {canvas.name}");
                        DebugLog($"Canvas position: {canvas.transform.position}");
                        DebugLog($"Canvas parent: {(canvas.transform.parent != null ? canvas.transform.parent.name : "None")}");
                        DebugLog($"Canvas active in hierarchy: {canvas.activeInHierarchy}");

                        // Find text components
                        TMP_Text senderText = FindTextInCanvas(canvas, msgSenderTextName);
                        TMP_Text contentText = FindTextInCanvas(canvas, msgContentTextName);
                        PanelUI panel = canvas.GetComponent<PanelUI>();

                        // Set text content
                        if (senderText != null)
                        {
                            string originalSenderText = senderText.text;
                            senderText.text = message.sender;

                            DebugLog($"Set sender text: '{originalSenderText}' -> '{senderText.text}'");
                            DebugLog($"Sender text component active: {senderText.gameObject.activeInHierarchy}, enabled: {senderText.enabled}");
                        }
                        else
                        {
                            DebugLogError($"Sender text component not found! Looking for: {msgSenderTextName}");
                        }
                        // Set character sprite

                        if (senderText != null)
                        {
                            if (message.sender.ToLower() == "teacher" && message.sender_gender.ToLower() == "man")
                            {
                                panel.SetSprite(maleTeacher);
                            }
                            else
                            {
                                if (message.sender.ToLower() == "teacher" && message.sender_gender.ToLower() == "woman")
                                {
                                    panel.SetSprite(femaleTeacher);
                                }
                                else
                                {
                                    if (message.sender.ToLower() == "student" && message.sender_gender.ToLower() == "woman")
                                    {
                                        panel.SetSprite(femaleStudent);
                                    }
                                    else
                                    {
                                        if (message.sender.ToLower() == "student" && message.sender_gender.ToLower() == "man")
                                        {
                                            panel.SetSprite(maleStudent);
                                        }
                                        else
                                        {
                                            panel.SetSprite(neutralPerson);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            DebugLogError($"Gender text component not found! Looking for: {msgGenderTextName}");
                            panel.SetSprite(neutralPerson);
                        }

                        if (contentText != null)
                        {
                            string originalContentText = contentText.text;
                            contentText.text = message.content ?? "";

                            // Force text to be visible

                            DebugLog($"Set content text: '{originalContentText}' -> '{contentText.text}'");
                            DebugLog($"Content text component active: {contentText.gameObject.activeInHierarchy}, enabled: {contentText.enabled}");
                        }
                        else
                        {
                            DebugLogError($"Content text component not found! Looking for: {msgContentTextName}");
                        }

                        // Canvas will be activated later by ActivateCanvas method
                        DebugLog($"Canvas {messageIndex} created: {canvas.name}");

                        DebugLog($"Message {messageIndex} completed: {message.sender} ({message.sender_gender}): {message.content}");
                        messageIndex++;
                    }
                }
            }
        }

        DebugLog($"DisplayMessages completed. Total canvases created: {instantiatedCanvases.Count}");
    }

    bool hasMoreThan2Questions,hasFourAnswers,hasCorrectAnswer;
    private void DisplayQuestionAnswers()
    {
        //booleans that make sure the story was generated properly, if not it shows an error message
        hasMoreThan2Questions = true;
        hasFourAnswers = true;
        hasCorrectAnswer = true;
        // Add quiz settings panel between the dialog and the questions
        quizSettingsPanel.transform.SetParent(canvasParent, false);
        instantiatedCanvases.Add(quizSettingsPanel);
        quizSettingsPanel.GetComponent<RectTransform>().anchoredPosition = new Vector3(0f, 0f, 0f);

        int totalQACount = 0;
        if (currentDialog.sections != null)
        {
            foreach (var section in currentDialog.sections)
            {
                if (section.question_answers != null)
                    totalQACount += section.question_answers.Count;
            }
        }

        if (totalQACount == 0)
        {
            DebugLog("No question-answer pairs to display");
            UpdateStatus("No question-answer pairs found in this dialog");
            return;
        }

        DebugLog($"DisplayQuestionAnswers: Found {totalQACount} question-answer pairs to display");

        // Check if prefab is assigned
        if (canvasQAPrefab == null)
        {
            DebugLogError("CanvasQAPrefab is not assigned! Please assign the question-answer canvas prefab in the inspector.");
            UpdateStatus("Error: Question-answer canvas prefab not assigned");
            return;
        }

        int canvasIndex = 0;

        // Display question-answers from all sections
        if (currentDialog.sections != null)
        {
            for (int sectionIndex = 0; sectionIndex < currentDialog.sections.Count; sectionIndex++)
            {
                var section = currentDialog.sections[sectionIndex];
                if (section.question_answers != null)
                {
                    //second check makes sure that max of 10 questions are created, not more
                    for (int i = 0; i < section.question_answers.Count && i < 10; i++)
                    {
                        QuestionAnswer qa = section.question_answers[i];
                        DebugLog($"Processing section {sectionIndex + 1} question-answer {i}: {qa.question} -> {qa.right_answer}");

                        // Create canvas for this question-answer pair
                        CreateQuestionAnswerCanvas(qa, canvasIndex, sectionIndex + 1);
                        canvasIndex++;
                    }
                }
            }
        }

        DebugLog($"DisplayQuestionAnswers completed. Total canvases created: {instantiatedCanvases.Count}");
        numOfQuestionsText.text = "Total number of questions: " + totalQuestionsCreated.ToString();


        if(totalQuestionsCreated<3)
        {
            hasMoreThan2Questions = false;
        }

        if(!hasCorrectAnswer || !hasFourAnswers || !hasMoreThan2Questions) 
        {
            quizSettingsPanel.transform.SetParent(firstCanvas, true);
            quizSettingsPanel.SetActive(false);
            instantiatedCanvases.Remove(quizSettingsPanel);

            errorQuizPanel.transform.SetParent(canvasParent, false);
            int insertIndex = instantiatedCanvases.Count - totalQuestionsCreated;
            instantiatedCanvases.Insert(insertIndex, errorQuizPanel);
            errorQuizPanel.GetComponent<RectTransform>().anchoredPosition = new Vector3(0f, 0f, 0f);

            errorQuizPanel.transform.SetSiblingIndex(errorQuizPanel.transform.GetSiblingIndex() - totalQuestionsCreated);
        }
    }

    private void CreateQuestionAnswerCanvas(QuestionAnswer qa, int canvasIndex, int sectionNumber)
    {
        // Instantiate canvas for this question-answer pair
        GameObject canvas = Instantiate(canvasQAPrefab);
        canvas.transform.SetParent(canvasParent, false);
        instantiatedCanvases.Add(canvas);

        // Ensure canvas is properly set up for visibility
        SetupCanvasForVisibility(canvas, canvasIndex);

        // Set canvas name
        canvas.name = $"QACanvas_{canvasIndex}_Section{sectionNumber}";
        totalQuestionsCreated++;
        DebugLog($"Created question-answer canvas: {canvas.name}");

        // Find text components
        TMP_Text questionText = FindTextInCanvas(canvas, questionTextName);

        // Set question text
        if (questionText != null)
        {
            string originalQuestionText = questionText.text;
            questionText.text = qa.question ?? "";

            DebugLog($"Set question text: '{originalQuestionText}' -> '{questionText.text}'");
            DebugLog($"Question text component active: {questionText.gameObject.activeInHierarchy}, enabled: {questionText.enabled}");
        }
        else
        {
            DebugLogError($"Question text component not found! Looking for: {questionTextName}");
        }

        Button[] buttons = FindObjectsOfType<Button>();
        if (qa.answers != null)
        {
            if(qa.answers.Length != 4) 
            {
                hasFourAnswers = false;
                return;
            }
            bool correctAnswerplaced = false;
            for (int j = 0; j < qa.answers.Length; j++)
            {
                buttons[3 - j].GetComponent<QuizButton>().SetAnswer(qa.answers[j]);
                if (qa.answers[j].Remove(0, 3) == qa.right_answer)
                {
                    buttons[3 - j].GetComponent<QuizButton>().correctAnswer = true;
                    correctAnswerplaced = true;
                }
            }
            if (!correctAnswerplaced)
            {
                hasCorrectAnswer = false;
            }
        }


        // Canvas will be activated later by ActivateCanvas method
        DebugLog($"Question-answer canvas {canvasIndex} created: {canvas.name}");

        DebugLog($"Question-answer pair {canvasIndex} completed: {qa.question} -> {qa.right_answer}");
    }


    // Utility methods
    private void UpdateStatus(string message)
    {
        DebugLog($"Status: {message}");
    }

    private void ClearCanvases()
    {
        foreach (GameObject canvas in instantiatedCanvases)
        {
            if (canvas != null)
            {
                Destroy(canvas);
            }
        }
        instantiatedCanvases.Clear();
    }

    private TMP_Text FindTextInCanvas(GameObject canvas, string textName)
    {
        if (canvas == null)
        {
            DebugLogError($"Canvas is null when looking for text '{textName}'");
            return null;
        }

        // Search for TMP text component with the specified name
        TMP_Text[] texts = canvas.GetComponentsInChildren<TMP_Text>(true);
        DebugLog($"Looking for TMP text '{textName}' in canvas '{canvas.name}'. Found {texts.Length} TMP text components:");

        foreach (TMP_Text text in texts)
        {
            DebugLog($"  - TMP_Text component: '{text.name}' (active: {text.gameObject.activeInHierarchy})");
            if (text.name == textName)
            {
                DebugLog($"  ✓ FOUND MATCH: '{text.name}' == '{textName}'");
                return text;
            }
        }

        // Case-insensitive search
        foreach (TMP_Text text in texts)
        {
            if (string.Equals(text.name, textName, StringComparison.OrdinalIgnoreCase))
            {
                DebugLog($"  ✓ FOUND CASE-INSENSITIVE MATCH: '{text.name}' == '{textName}'");
                return text;
            }
        }

        // Common variations
        string[] commonVariations = {
        textName.ToLower(),
        textName.ToUpper(),
        textName.Replace("Text", ""),
        textName.Replace("text", ""),
        textName + "Text",
        textName + "text"
    };

        foreach (TMP_Text text in texts)
        {
            foreach (string variation in commonVariations)
            {
                if (string.Equals(text.name, variation, StringComparison.OrdinalIgnoreCase))
                {
                    DebugLog($"  ✓ FOUND VARIATION MATCH: '{text.name}' matches variation '{variation}' of '{textName}'");
                    return text;
                }
            }
        }

        DebugLogError($"TMP_Text component '{textName}' not found in canvas '{canvas.name}'.");
        return null;
    }

    private void SetupCanvasForVisibility(GameObject canvas, int index)
    {
        if (canvas == null) return;

        // Ensure canvas is active
        canvas.SetActive(true);

        // Set canvas to cover entire screen
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect != null)
        {
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
        }

        // Make sure text is visible but preserve original positioning
        Text[] texts = canvas.GetComponentsInChildren<Text>(true);
        foreach (Text text in texts)
        {
            if (text != null)
            {
                text.gameObject.SetActive(true);
                text.enabled = true;

                // DON'T override positioning - keep original prefab positioning
                // The prefab should have the correct positioning already
            }
        }

        DebugLog($"Set up canvas {index} for Screen Space Overlay: {canvas.name}");
        DebugLog($"Canvas active after setup: {canvas.activeInHierarchy}");
    }

    // Debug logging methods
    private void DebugLog(string message)
    {
        if (enableDebugLogging)
        {
            UnityEngine.Debug.Log($"[MCP Fixed] {message}");
        }
    }

    private void DebugLogError(string message)
    {
        if (enableDebugLogging)
        {
            UnityEngine.Debug.LogError($"[MCP Fixed] {message}");
        }
    }

    // Navigation methods
    public void NextCanvas()
    {

        if (instantiatedCanvases.Count == 0) return;

        int nextIndex = (currentCanvasIndex + 1) % instantiatedCanvases.Count;
        ActivateCanvas(nextIndex);
        DebugLog($"Next canvas: {currentCanvasIndex + 1} of {instantiatedCanvases.Count}");
    }

    public void PreviousCanvas()
    {
        if (instantiatedCanvases.Count == 0) return;

        int prevIndex = (currentCanvasIndex - 1 + instantiatedCanvases.Count) % instantiatedCanvases.Count;
        ActivateCanvas(prevIndex);
        DebugLog($"Previous canvas: {currentCanvasIndex + 1} of {instantiatedCanvases.Count}");
    }

    private void ActivateCanvas(int index)
    {
        if (index < 0 || index >= instantiatedCanvases.Count) return;

        for (int i = 0; i < instantiatedCanvases.Count; i++)
        {
            instantiatedCanvases[i].SetActive(i == index);
        }

        currentCanvasIndex = index;
        TMP_Text dialogText = FindTextInCanvas(instantiatedCanvases[index], "msgContent");

        if (dialogText != null && !speedMode)
        {
            StartCoroutine(TypeText(dialogText, index));
        }
    }

    //function to show text as if someone is typing it
    IEnumerator TypeText(TMP_Text textToType, int canvasNum)
    {
        isTyping = true;
        AutoScrollHandler autoScrollHandler = instantiatedCanvases[canvasNum].GetComponentInChildren<AutoScrollHandler>();

        previousButton.SetActive(false);
        nextButton.SetActive(false);
        goToButtons.SetActive(false);
        storySettingsButton.SetActive(false);
        String tempText = textToType.text;
        textToType.text = "";
        for (int i = 0; i < tempText.Length; i++)
        {
            if (skipText)
            {
                textToType.text = tempText;
                break;
            }
            if (autoScrollHandler != null)
            {
                autoScrollHandler.OnTextUpdated();
            }
            textToType.text += tempText[i];
            yield return new WaitForSeconds(0.015f);
        }
        yield return null;
        previousButton.SetActive(true);
        nextButton.SetActive(true);
        goToButtons.SetActive(true);
        storySettingsButton.SetActive(true);
        skipText = false;
        isTyping = false;
    }


    // Public methods for UI manager
    public Dialog GetCurrentDialog()
    {
        return currentDialog;
    }

    public int GetCanvasCount()
    {
        return instantiatedCanvases.Count;
    }

    public int GetCurrentCanvasIndex()
    {
        return currentCanvasIndex;
    }

    // Test methods
    public void TestConnection()
    {
        if (isServerConnected)
        {
            UpdateStatus("MCP server is connected");
        }
        else if (isConnecting)
        {
            UpdateStatus("MCP server is connecting...");
        }
        else
        {
            UpdateStatus("MCP server is not connected");
        }
    }

    public void TestListDialogs()
    {
        if (!isServerConnected)
        {
            UpdateStatus("Not connected to MCP server");
            return;
        }

        StartCoroutine(TestListDialogsCoroutine());
    }

    private IEnumerator TestListDialogsCoroutine()
    {
        UpdateStatus("Testing list_dialogs...");
        DebugLog("Testing list_dialogs tool call");

        bool requestSent = false;
        Exception requestException = null;

        try
        {
            // Call list_dialogs tool
            var request = new MCPRequest
            {
                id = UnityEngine.Random.Range(1000, 9999),
                method = "tools/call",
                @params = new
                {
                    name = "list_dialogs",
                    arguments = new { }
                }
            };

            string requestJson = JsonConvert.SerializeObject(request);
            DebugLog($"Sending list_dialogs request: {requestJson}");
            serverInput.WriteLine(requestJson);
            serverInput.Flush();
            requestSent = true;
        }
        catch (Exception e)
        {
            requestException = e;
        }

        if (requestException != null)
        {
            UpdateStatus($"Error sending list_dialogs request: {requestException.Message}");
            DebugLogError($"List Dialogs Request Error: {requestException}");
            yield break;
        }

        if (!requestSent)
        {
            UpdateStatus("Failed to send list_dialogs request");
            yield break;
        }

        // Wait for response with timeout
        DebugLog("Waiting for list_dialogs response...");
        float elapsedTime = 0f;
        bool responseReceived = false;
        string response = null;

        while (!responseReceived && elapsedTime < responseTimeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsedTime += 0.1f;

            // Check if server process is still running
            if (mcpServerProcess == null || mcpServerProcess.HasExited)
            {
                UpdateStatus("MCP server process terminated during list_dialogs");
                DebugLogError("MCP server process exited during list_dialogs operation");
                yield break;
            }

            // Try to read response
            try
            {
                if (serverOutput != null && !serverOutput.EndOfStream)
                {
                    if (serverOutput.Peek() >= 0)
                    {
                        response = serverOutput.ReadLine();
                        if (!string.IsNullOrEmpty(response))
                        {
                            responseReceived = true;
                            DebugLog($"Received list_dialogs response: {response}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugLog($"Error reading list_dialogs response: {e.Message}");
            }
        }

        if (!responseReceived)
        {
            UpdateStatus("No response from list_dialogs (timeout)");
            DebugLogError("MCP server did not respond to list_dialogs within timeout period");
        }
        else
        {
            UpdateStatus("list_dialogs test completed - check console for response");
        }
    }

    public void ForceReconnect()
    {
        if (isConnecting)
        {
            UpdateStatus("Already connecting, please wait...");
            return;
        }

        // Clean up existing connection
        if (mcpServerProcess != null && !mcpServerProcess.HasExited)
        {
            mcpServerProcess.Kill();
            mcpServerProcess = null;
        }

        isServerConnected = false;
        isConnecting = false;

        // Start new connection
        StartCoroutine(ConnectToMCPServerWithTimeout());
    }

    void OnDestroy()
    {
        // Clean up MCP server process
        if (mcpServerProcess != null && !mcpServerProcess.HasExited)
        {
            mcpServerProcess.Kill();
            mcpServerProcess.Dispose();
        }

        if (serverInput != null)
            serverInput.Close();

        if (serverOutput != null)
            serverOutput.Close();

        if (serverError != null)
            serverError.Close();
    }

    private IEnumerator LoadingAnimation()
    {
            loadingProgress = 0f;

        if (serverMode)
        {
            while (!isServerConnected)
            {
                // Move up to 90%
                loadingProgress = Mathf.MoveTowards(loadingProgress, 0.9f, Time.deltaTime * 0.5f);
                loadingBar.value = loadingProgress;

                yield return null;
            }
        }

            // Finish to 100%
            while (loadingProgress < 1f)
            {
                loadingProgress = Mathf.MoveTowards(loadingProgress, 1f, Time.deltaTime * 2f);
                loadingBar.value = loadingProgress;

                yield return null;
            }
        

        yield return new WaitForSeconds(0.5f);
        loadingScreen.SetActive(false);
        mainMenu.SetActive(true);
        gameObject.GetComponent<AudioSource>().Play();
    }

    //little function for the loading animation
    IEnumerator AnimateDots()
    {
        int dots = 0;

        while (loadingScreen.activeSelf)
        {
            dots++;
            if (dots > 3) dots = 1;

            loadingText.text = "Loading" + new string('.', dots);

            yield return new WaitForSeconds(0.25f);
        }
    }

    public void QuestionAnswered(bool correct)
    {
        totalQuestions++;
        if (correct)
        {
            correctQuestions++;
        }
        if (totalQuestions == totalQuestionsCreated)
        {
            
        }
    }

   //resetting everything when back to the menu
    public void BackToMenu()
    {
            quizSettingsPanel.transform.SetParent(firstCanvas, true);
            quizSettingsPanel.SetActive(false);
            instantiatedCanvases.Remove(quizSettingsPanel);

        errorQuizPanel.transform.SetParent(firstCanvas, true);
        errorQuizPanel.SetActive(false);
        instantiatedCanvases.Remove(errorQuizPanel);

        foreach (Transform child in canvasParent)
        {
            GameObject.Destroy(child.gameObject);
        }
        previousButton.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(false);
        goToButtons.SetActive(false);
        storySettingsButton.gameObject.SetActive(false);
        mainMenu.SetActive(true);
        totalQuestions = 0;
        correctQuestions = 0;
        totalQuestionsCreated = 0;
        quizMode = false;
        afterQuiz = false;
        getDialogText.text = "";
        dialogIdInput.text = "";

        foreach (Transform child in roadParent)
        {
            GameObject.Destroy(child.gameObject);
        }


    }

    public void SetSpeedMode()
    {
        speedMode = !speedMode;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (isTyping)
            {
                skipText = true;
            }
        }
    }

    public void ShowStory()
    {
        ActivateCanvas(0);
        gameObject.GetComponent<DialogUIManager>().RefreshUI();
    }

    public void ShowDialog()
    {
        ActivateCanvas(currentDialog.stories.Count);
        gameObject.GetComponent<DialogUIManager>().RefreshUI();
    }

    public void ShowQuiz()
    {
        if (afterQuiz)
        {
            ActivateCanvas(canvasParent.childCount - totalQuestionsCreated);
        }
        else
        {
            ActivateCanvas(canvasParent.childCount - totalQuestionsCreated - 1);
        }
        gameObject.GetComponent<DialogUIManager>().RefreshUI();
    }

    public int GetTotalNumOfQuestions()
    {
        return totalQuestionsCreated;
    }

    public void SetQuiz()
    {
        ActivateCanvas(canvasParent.childCount - totalQuestionsCreated);
        previousButton.SetActive(false);
        nextButton.SetActive(false);
        goToButtons.SetActive(false);
        quizMode = true;
    }

    public void ShowButtons()
    {
        if(!quizMode)
        {
            previousButton.SetActive(true);
            nextButton.SetActive(true);
            goToButtons.SetActive(true);
        }
    }

    public void NextQuestion()
    {
        StartCoroutine(ShowNextQuestion());
    }
   public IEnumerator ShowNextQuestion()
    {
        yield return new WaitForSeconds(2);
        ActivateCanvas(canvasParent.childCount - totalQuestionsCreated+totalQuestions);
    }

    public void SetQuizMode(bool mode)
    {
        quizMode = mode;
    }

    //showing everything as intented on the story after user finishes the quiz and decides to read it again
    public void SetAfterQuizMode(bool mode)
    {
        afterQuiz = mode;
        if(afterQuiz)
        {
            quizSettingsPanel.transform.SetParent(firstCanvas, true);
            quizSettingsPanel.SetActive(false);
            instantiatedCanvases.Remove(quizSettingsPanel);
            currentCanvasIndex -=1;
        }
    }

    public bool GetQuizMode()
    {
        return quizMode;
    }

    public bool GetAfterQuizMode()
    {
        return afterQuiz;
    }

    private void LoadLocalDialog(string dialogId)
    {
        UpdateStatus("Loading local dialog...");
        DebugLog($"Loading local dialog: {dialogId}");

        // Load JSON file
        UnityEngine.TextAsset jsonFile = Resources.Load<UnityEngine.TextAsset>("Dialogs/" + dialogId);

        if (jsonFile == null)
        {
            UpdateStatus("Local dialog file not found");
            DebugLogError($"Could not find local dialog file: {dialogId}");
            return;
        }

        try
        {
            // Deserialize the FULL file
            DialogCollection collection =
                JsonConvert.DeserializeObject<DialogCollection>(jsonFile.text);

            if (collection == null || collection.dialogs == null)
            {
                UpdateStatus("Invalid dialog file");
                return;
            }

            // Find correct dialog
            currentDialog = collection.dialogs.Find(d => d.id == dialogId);

            if (currentDialog == null)
            {
                UpdateStatus("Dialog not found");
                return;
            }

            // SAME CODE AS SERVER VERSION
            UpdateStatus($"Dialog '{currentDialog.title}' loaded locally");

            getDialogText.text = "Story loaded successfully";

            saveSystem.IsStoryReread(dialogId);

            mainMenu.gameObject.SetActive(false);
            settingsMenu.gameObject.SetActive(false);
            storyMenu.gameObject.SetActive(false);

            previousButton.gameObject.SetActive(true);
            nextButton.gameObject.SetActive(true);
            goToButtons.gameObject.SetActive(true);
            storySettingsButton.gameObject.SetActive(true);

            DisplayDialog();

            if (uiManager != null)
            {
                uiManager.RefreshUI();
            }
        }
        catch (Exception e)
        {
            UpdateStatus($"Error reading local dialog: {e.Message}");
            DebugLogError($"Local dialog parse error: {e}");
        }
    }

}
