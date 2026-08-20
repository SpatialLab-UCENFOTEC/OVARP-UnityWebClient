using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

public class Chat : MonoBehaviour
{
    private UIDocument uiChat;
    private Button displayButton;
    private Button sendButton;
    private TextField messageInput;
    private VisualElement chatArea;
    private VisualElement background;

    /// <summary>Raised when the user submits typed text. Controller sends it to the server.</summary>
    public event Action<string> OnMessageSubmitted;

    /// <summary>
    /// True while the text field holds focus. The Controller reads this before acting on
    /// the spacebar, which would otherwise start recording on every space that gets typed.
    /// </summary>
    public bool IsTyping =>
        messageInput != null
        && uiChat != null
        && uiChat.rootVisualElement.focusController?.focusedElement == messageInput;
    public VisualTreeAsset userMessageTemplate;
    public VisualTreeAsset agentMessageTemplate;

    private TemplateContainer tempUser;
    private TemplateContainer tempAgent;

    
    void OnEnable()
    {
        uiChat = GetComponent<UIDocument>();
        displayButton = uiChat.rootVisualElement.Q<Button>("DisplayButton");
        chatArea = uiChat.rootVisualElement.Q("ChatArea");
        background = uiChat.rootVisualElement.Q("Background");
        displayButton.RegisterCallback<ClickEvent>(OnClick);

        messageInput = uiChat.rootVisualElement.Q<TextField>("MessageInput");
        sendButton = uiChat.rootVisualElement.Q<Button>("SendButton");
        if (sendButton != null)
            sendButton.RegisterCallback<ClickEvent>(OnSendClick);
        if (messageInput != null)
        {
            // TrickleDown: the field consumes Return during bubbling, so catch it on the way in
            messageInput.RegisterCallback<KeyDownEvent>(OnInputKeyDown, TrickleDown.TrickleDown);
        }
        // Stays off until the Controller reports a live connection
        SetInputEnabled(false);
        //string longText =
        //    "Hey there! It's great that we're working together on this project. I've noticed that there's been a lot on your plate lately. Is there anything I can do to help you complete your portion of the project?";
        //string longWord =
        //    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        //sendUserMessage("Hello\n");
        //sendUserMessage("It's me\n");
        //sendUserMessage(longWord);
        //sendAgentMessage("Hello from the other side\n");
        //sendAgentMessage(longText);
        //SendTempUser();

    }

    private void Awake()
    {
        GameManager.OnAgentNameChange += AgentNameChange;
        GameManager.OnUserNameChange += UserNameChange;
        GameManager.OnAgentColorChange += AgentColorChange;
        GameManager.OnUserColorChange += UserColorChange;
    }
    
    private void OnDestroy()
    {
        GameManager.OnAgentNameChange -= AgentNameChange;
        GameManager.OnUserNameChange -= UserNameChange;
        GameManager.OnAgentColorChange -= AgentColorChange;
        GameManager.OnUserColorChange -= UserColorChange;
    }

    public TemplateContainer sendUserMessage(string message)
    {
        TemplateContainer itemContainer = userMessageTemplate.Instantiate();
        itemContainer.Q<Label>("Text").text = message;
        itemContainer.Q<Label>("UserName").text = GameManager.Instance.GetUserName();
        itemContainer.Q<VisualElement>("UserBubble").style.backgroundColor = GameManager.Instance.getUserBubbleColor();
        if(GameManager.Instance.GetDarkMode())
        {
            itemContainer.Q<Label>("UserName").style.color = Color.white;
        }
        uiChat.rootVisualElement.Q("Chat").Add(itemContainer);
        DelayedScroll(uiChat.rootVisualElement.Q<ScrollView>("Chat"), itemContainer);
        return itemContainer;
    }

    public void SendTempUser()
    {
        if (tempUser != null)
        {
            killTempUser();
        }
        tempUser = sendUserMessage("test");
        //tempUser.Q<Label>("Text").text = "<b>...</b>";
        StartCoroutine(dotLoading(tempUser));
        tempUser.Q<VisualElement>("UserBubble").style.backgroundColor = Color.grey;
        tempUser.Q<VisualElement>("UserBubble").style.marginLeft = 155;
        tempUser.Q<VisualElement>("SenderBox").visible = false;
    }

    public void SendTempAgent()
    {
        if (tempAgent != null)
        {
            killTempAgent();
        }
        tempAgent = sendAgentMessage("test");
        tempAgent.Q<Label>("Text").text = "<b>...</b>";
        tempAgent.Q<VisualElement>("AgentBubble").style.backgroundColor = Color.grey;
        tempAgent.Q<VisualElement>("AgentBubble").style.marginRight = 155;
        tempAgent.Q<VisualElement>("SenderBox").visible = false;
    }

    private IEnumerator dotLoading(TemplateContainer tc)
    {
        int dotsCounter = 1;
        string dots = "...";
        while (true)
        {
            if (tc.Q<Label>("Text") != null)
            {
                tc.Q<Label>("Text").text = "<b>"+dots.Substring(0,dotsCounter%3+1)+"</b>";
                dotsCounter++;
                yield return new WaitForSeconds(1f);
            }
            else {
                break;
            }
        }
        yield return null;
    }

    public TemplateContainer sendAgentMessage(string message)
    {
        TemplateContainer itemContainer = agentMessageTemplate.Instantiate();
        itemContainer.Q<Label>("Text").text = message;
        itemContainer.Q<Label>("AgentName").text = GameManager.Instance.GetAgentName();
        itemContainer.Q<VisualElement>("AgentBubble").style.backgroundColor = GameManager.Instance.getAgentBubbleColor();
        if(GameManager.Instance.GetDarkMode())
        {
            itemContainer.Q<Label>("AgentName").style.color = Color.white;
        }
        uiChat.rootVisualElement.Q("Chat").Add(itemContainer);
        DelayedScroll(uiChat.rootVisualElement.Q<ScrollView>("Chat"), itemContainer);
        return itemContainer;
    }

    public void killTempUser()
    {
        StopCoroutine(dotLoading(tempUser));
        tempUser.Clear();
        tempUser = null;
    }

    public void killTempAgent()
    {
        if (tempAgent != null)
        {
            StopCoroutine(dotLoading(tempAgent));
            tempAgent.Clear();
            tempAgent = null;
        }
    }

    private IEnumerator scrollLater(ScrollView sv, VisualElement ve)
    {
        yield return new WaitForSeconds(0.1f);
        sv.ScrollTo(ve);
    }

    public virtual void DelayedScroll(ScrollView sv, VisualElement ve)
    {
        StartCoroutine(scrollLater(sv, ve));
    }

    void OnClick(ClickEvent evt)
    {
        chatArea.visible = !chatArea.visible;
    }

    /// <summary>
    /// Enable or disable the typing controls. Left off until the socket is open, so a
    /// message cannot be composed and echoed into the log with nowhere to go.
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        if (messageInput != null)
        {
            messageInput.SetEnabled(enabled);
            messageInput.tooltip = enabled ? "Type a message and press Enter" : "Waiting for the server";
        }
        if (sendButton != null)
            sendButton.SetEnabled(enabled);
    }

    void OnSendClick(ClickEvent evt)
    {
        SubmitTypedMessage();
    }

    void OnInputKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;

        SubmitTypedMessage();
        evt.StopPropagation();
    }

    /// <summary>Echo the typed text into the log and hand it to whoever is listening.</summary>
    private void SubmitTypedMessage()
    {
        if (messageInput == null) return;

        string text = messageInput.value?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        messageInput.value = string.Empty;
        sendUserMessage(text);
        OnMessageSubmitted?.Invoke(text);
        messageInput.Focus();
    }

    void AgentNameChange(string name)
    {
        uiChat.rootVisualElement.Query().Where(elem => elem.name == "AgentName")
            .ForEach(elem => elem.Q<Label>("AgentName").text = name);
    }

    void UserNameChange(string name)
    {
        uiChat.rootVisualElement.Query().Where(elem => elem.name == "UserName")
            .ForEach(elem => elem.Q<Label>("UserName").text = name);
    }

    void AgentColorChange(Color color)
    {
        uiChat.rootVisualElement.Query().Where(elem => elem.name == "AgentBubble")
            .ForEach(elem => elem.Q<VisualElement>("AgentBubble").style.backgroundColor = color);
    }
    
    void UserColorChange(Color color)
    {
        uiChat.rootVisualElement.Query().Where(elem => elem.name == "UserBubble")
            .ForEach(elem => elem.Q<VisualElement>("UserBubble").style.backgroundColor = color);
    }

    public void DarkMode(bool state, Color color)
    {
        background.style.backgroundColor = color;
        Color textColor;
        if (state)
        {
            textColor = Color.white;
        }
        else
        {
            textColor = Color.black;
        }
        uiChat.rootVisualElement.Query().Where(elem => elem.name is "AgentName" or "UserName")
            .ForEach(elem => elem.style.color = textColor);
    }
}
