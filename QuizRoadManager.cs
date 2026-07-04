using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

public class QuizRoadManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject tilePrefab;
    public GameObject winTilePrefab;
    public GameObject loseTilePrefab;
    public GameObject playerPrefab;

    [Header("Parent")]
    public Transform roadParent;

    [Header("Road Settings")]
    public float roadWidth = 700f;
    public float minTileSize = 50f;
    public float maxTileSize = 120f;
    public float spacing = -10f;
    public float aPlayerHeightOffset = 0f;
    private float playerHeightOffset;

    private List<GameObject> tiles = new List<GameObject>();
    private GameObject player;

    private int playerTileIndex = 0;
    private float currentTileSize;

    [Header("Quiz Settings")]
    public int quizCharacter = 0;
    public int quizDifficulty = 0;
    public GameObject readyText;
    public GameObject livesText;
    public MCPDialogControllerComplete gameManager;

    [Header("Sprites")]
    public Sprite[] sprites;
    public Sprite[] trophySprites;

    [Header("Animators and Images")]
    public Animator winAnimator;
    public Animator loseAnimator;
    public Image trophyImage;
    public GameObject storySettingsButton;
    private Animator characterAnimator;
    private Animator droppingTileAnimator;

    [Header("Save System")]
    public SaveSystem saveSystem;

    [Header("Win and Lose Sounds")]
    public AudioClip winSound;
    public AudioClip loseSound;

    public void StartQuiz()
    {
        ClearRoad();
        saveSystem.UpdateNumOfStories();
        int questionCount = gameManager.GetTotalNumOfQuestions();
        GenerateRoad(questionCount+1);
        SpawnPlayer();
        gameManager.SetQuiz();
    }

    void GenerateRoad(int tileCount)
    {
        currentTileSize = Mathf.Clamp(
              (roadWidth - (spacing * (tileCount - 1))) / tileCount,
              minTileSize,
              maxTileSize
              );

       

        float totalWidth =
            (tileCount * currentTileSize) +
            ((tileCount - 1) * spacing);

        float startX = -totalWidth / 2f + currentTileSize / 2f;

        for (int i = 0; i < tileCount; i++)
        {
            GameObject tile;
            if (i + 1 != tileCount)
            {
                 tile = Instantiate(tilePrefab, roadParent);
            }
            else
            {
                 tile = Instantiate(winTilePrefab, roadParent);
            }

            float xPos = startX + i * (currentTileSize + spacing);

            RectTransform rt = tile.GetComponent<RectTransform>();

            if (rt != null)
            {
                rt.sizeDelta = new Vector2(currentTileSize, currentTileSize);
                rt.anchoredPosition = new Vector2(xPos, 0);
            }
            else
            {
                tile.transform.localPosition = new Vector3(xPos, 0, 0);
                tile.transform.localScale = Vector3.one;
            }

            tiles.Add(tile);
        }
    }

    void SpawnPlayer()
    {
        player = Instantiate(playerPrefab, roadParent);

        float scale = currentTileSize / 100f;
        player.transform.localScale = Vector3.one * scale;

        playerHeightOffset = aPlayerHeightOffset*scale;
        playerTileIndex = 3 - quizDifficulty;
        MovePlayerToTile(playerTileIndex);

        characterAnimator = player.GetComponent<Animator>();
        characterAnimator.SetInteger("character", quizCharacter - 1);
    }

    void MovePlayerToTile(int tileIndex)
    {
        if (tileIndex < 0 || tileIndex >= tiles.Count) return;

        player.transform.position =
            tiles[tileIndex].transform.position +
            new Vector3(0, playerHeightOffset, 0);
    }

    IEnumerator MovePlayerSmooth(int tileIndex)
    {
        Vector3 start = player.transform.position;
        Vector3 target = tiles[tileIndex].transform.position + new Vector3(0, playerHeightOffset, 0);

        float duration = 0.75f;
        float time = 0f;

        characterAnimator.SetBool("isWalking", true);

        while (time < duration)
        {
            player.transform.position = Vector3.Lerp(start, target, time / duration);
            time += Time.deltaTime;
            yield return null;
        }

        characterAnimator.SetBool("isWalking", false);
        player.transform.position = target;

        yield return new WaitForSeconds(0.55f);
        RemoveFirstTile();
    }

    public void SubmitAnswer(bool correct)
    {
        if (!gameManager.GetAfterQuizMode())
        { 
            if (tiles.Count <= 1)
            {
                WinGame();
                gameManager.SetQuizMode(false);
                gameManager.SetAfterQuizMode(true);
                return;
            }

            if (correct)
            {
                playerTileIndex++;
                StartCoroutine(MovePlayerSmooth(playerTileIndex));

                if (playerTileIndex >= tiles.Count)
                    playerTileIndex = tiles.Count - 1;

                GetComponent<AudioSource>().clip = winSound;
                GetComponent<AudioSource>().Play();
            }
            else
            {
                GetComponent<AudioSource>().clip = loseSound;
                GetComponent<AudioSource>().Play();
            }
            StartCoroutine(RemoveFirstTile());
        }
    }

    IEnumerator RemoveFirstTile()
    {
        playerTileIndex--;
        if(playerTileIndex<0 || playerTileIndex>= tiles.Count-1 || playerTileIndex== tiles.Count-2)
        {
            storySettingsButton.SetActive(false);
        }
        GameObject tileToRemove = tiles[0];
        droppingTileAnimator = tileToRemove.GetComponent<Animator>();

        droppingTileAnimator.SetBool("Shaking", true);

        float duration = 1f;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            yield return null;
        }

        droppingTileAnimator.SetBool("Shaking", false);
        if (playerTileIndex < 0 || playerTileIndex >= tiles.Count)
        {
            characterAnimator.SetInteger("character", 4);
        }

        duration = 0.5f;
        time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            yield return null;
        }

        tiles.RemoveAt(0);
        Destroy(tileToRemove);

        if (playerTileIndex < 0 || playerTileIndex >= tiles.Count)
        {
            Destroy(player);
        }

        if (playerTileIndex < 0 || playerTileIndex >= tiles.Count)
        {
            gameManager.SetQuizMode(false);
            gameManager.SetAfterQuizMode(true);
            LoseGame();
        }
        else
        {

            if (playerTileIndex == tiles.Count - 1)
            {
                gameManager.SetQuizMode(false);
                gameManager.SetAfterQuizMode(true);
                WinGame();
            }
            else
            {
                gameManager.NextQuestion();
            }
        }
    }

    void WinGame()
    {
        saveSystem.UpdateTrophy(quizDifficulty);
        if (quizDifficulty==1)
        {
            trophyImage.sprite = trophySprites[0];
        }
        else
        {
            if(quizDifficulty==2)
            {
                trophyImage.sprite = trophySprites[1];
            }
            else
            {
                trophyImage.sprite= trophySprites[2];
            }
        }
        StartCoroutine(ShowGradePanel(true));
    }

    void LoseGame()
    {
        saveSystem.UpdateTrophy(0);
        StartCoroutine(ShowGradePanel(false));
    }

    void ClearRoad()
    {
        foreach (GameObject tile in tiles)
        {
            Destroy(tile);
        }

        tiles.Clear();

        if (player != null)
            Destroy(player);
    }

    public void ChangeCharacter(int character)
    {
        quizCharacter = character;
        if (quizCharacter != 0 && quizDifficulty != 0)
        {
            readyText.SetActive(true);
        }
        playerPrefab.GetComponent<Image>().sprite = sprites[character-1];
    }

    public void ChangeDifficulty(int difficulty)
    {
        quizDifficulty = difficulty;
        livesText.SetActive(true);
        if (difficulty == 1)
        {
            livesText.GetComponent<TMP_Text>().text = "You will have 2 lives";
        }
        else
        {
            if (difficulty == 2)
            {
                livesText.GetComponent<TMP_Text>().text = "You will have 1 life";
            }
            else
            {
                livesText.GetComponent<TMP_Text>().text = "You will have 0 lives";
            }
        }
        if (quizCharacter != 0 && quizDifficulty != 0)
        {
            readyText.SetActive(true);
        }
    }

    IEnumerator ShowGradePanel(bool win)
    {
        storySettingsButton.SetActive(false);
        yield return new WaitForSeconds(1);
        foreach(GameObject tile in tiles)
        {
            Destroy(tile);
        }
        Destroy(player);

        if (win)
        {
            winAnimator.SetTrigger("ShowPanel");
        }
        else
        {
            loseAnimator.SetTrigger("ShowPanel");
        }
    }
}
