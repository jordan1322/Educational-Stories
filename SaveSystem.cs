using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SaveSystem : MonoBehaviour
{

    int bronzeTrophies, silverTrophies, goldTrophies, participationTrophies, totalStories;
    List<string> storiesRead;
    string combined;
    bool storyReread;

    public TMP_Text bronzeTrophiesText, silverTrophiesText,goldTrophiesText,participationTrophiesText,totalStoriesText;
    public GameObject rereadText;
    // Start is called before the first frame update
    void Start()
    {
        bronzeTrophies = PlayerPrefs.GetInt("Bronze Trophies", 0);
        silverTrophies = PlayerPrefs.GetInt("Silver Trophies", 0);
        goldTrophies = PlayerPrefs.GetInt("Gold Trophies", 0);
        participationTrophies = PlayerPrefs.GetInt("Participation Trophies", 0);
        totalStories = PlayerPrefs.GetInt("Total Stories", 0);

        bronzeTrophiesText.text=bronzeTrophies.ToString();
        silverTrophiesText.text = silverTrophies.ToString();
        goldTrophiesText.text = goldTrophies.ToString();
        participationTrophiesText.text = participationTrophies.ToString();
        totalStoriesText.text = totalStories.ToString();

        combined = PlayerPrefs.GetString("Stories Read", "");
        storiesRead = new List<string>(combined.Split('|'));
    }

    public void UpdateTrophy(int typeOfTrophy)
    {
        if (storyReread)
        {
            return;
        }
        if (typeOfTrophy == 0)
        {
            participationTrophies++;
            PlayerPrefs.SetInt("Participation Trophies", participationTrophies);
            participationTrophiesText.text = participationTrophies.ToString();
        }
        else
        {
            if(typeOfTrophy==1)
            {
                bronzeTrophies++;
                PlayerPrefs.SetInt("Bronze Trophies", bronzeTrophies);
                bronzeTrophiesText.text = bronzeTrophies.ToString();
            }
            else
            {
                if(typeOfTrophy==2)
                {
                    silverTrophies++;
                    PlayerPrefs.SetInt("Silver Trophies", silverTrophies);
                    silverTrophiesText.text = silverTrophies.ToString();
                }
                else
                {
                    goldTrophies++;
                    PlayerPrefs.SetInt("Gold Trophies", goldTrophies);
                    goldTrophiesText.text = goldTrophies.ToString();
                }
            }
        }
    }
    public void UpdateNumOfStories()
    {
        if(storyReread)
        {
            return;
        }
        totalStories++;
        PlayerPrefs.SetInt("Total Stories", totalStories);
        totalStoriesText.text = totalStories.ToString();
    }

    public void IsStoryReread(string id)
    {
        storyReread = storiesRead.Contains(id);
        if(!storyReread)
        {
            rereadText.SetActive(false);
            AddStoryToRead(id);
        }
        else
        {
            rereadText.SetActive(true);
        }
    }

    public void AddStoryToRead(string id)
    {
        storiesRead.Add(id);
        combined = string.Join("|", storiesRead);
        PlayerPrefs.SetString("Stories Read", combined);
    }
}

