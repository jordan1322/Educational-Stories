using System;
using System.Collections.Generic;

[System.Serializable]
public class Participant
{
    public string name;
    public string gender;
}

[System.Serializable]
public class Message
{
    public string id;
    public string sender;
    public string sender_gender;
    public string content;
    public string timestamp;
    public Dictionary<string, object> metadata;
}

[System.Serializable]
public class QuestionsMessage
{
    public string id;
    public string content_01;
    public string content_02;
    public string content_03;
    public string content_04;
    public string content_05;
    public string content_06;
    public string content_07;
    public string content_08;
    public string content_09;
    public string content_10;
    public string timestamp;
    public Dictionary<string, object> metadata;
}

[System.Serializable]
public class RQuestionMessage
{
    public string id;
    public string content;
    public string timestamp;
    public Dictionary<string, object> metadata;
}

[System.Serializable]
public class QuestionAnswer
{
    public string id;
    public string question;
    public string[] answers;
    public string right_answer;
    public string timestamp;
    public Dictionary<string, object> metadata;
}

[System.Serializable]
public class Story
{
    public string id;
    public string content;
    public string timestamp;
    public Dictionary<string, object> metadata;
}

[System.Serializable]
public class DialogSection
{
    public List<Message> messages;
    public List<QuestionAnswer> question_answers;
}

public class Dialog
{
    public string id;
    public string title;
    public List<Participant> participants;
    public List<Story> stories;
    public List<DialogSection> sections;
    public List<QuestionsMessage> questions_messages;
    public List<RQuestionMessage> r_question_messages;
    public string created_at;
    public string updated_at;
    public Dictionary<string, object> metadata;

    // Backward compatibility properties
    public List<Message> messages
    {
        get
        {
            if (sections != null && sections.Count > 0)
                return sections[0].messages;
            return new List<Message>();
        }
    }

    public List<QuestionAnswer> question_answers
    {
        get
        {
            if (sections != null && sections.Count > 0)
                return sections[0].question_answers;
            return new List<QuestionAnswer>();
        }
    }

    public List<Message> messages_2
    {
        get
        {
            if (sections != null && sections.Count > 1)
                return sections[1].messages;
            return new List<Message>();
        }
    }

    public List<QuestionAnswer> question_answers_2
    {
        get
        {
            if (sections != null && sections.Count > 1)
                return sections[1].question_answers;
            return new List<QuestionAnswer>();
        }
    }

    [System.Serializable]
    public class DialogCollection
    {
        public List<Dialog> dialogs;
    }
}