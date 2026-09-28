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
    private Label placeholder;
    private Label connectionStatus;
    private bool chatVisible = true;

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
        BuildAffordances();

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
        // `visible` left the panel's white background occupying the screen and the
        // button still reading "Hide Chat", so the control looked like it had done
        // nothing. Collapsing the container removes it from layout as well.
        chatVisible = !chatVisible;
        chatArea.style.display = chatVisible ? DisplayStyle.Flex : DisplayStyle.None;
        if (background != null)
            background.style.backgroundColor = chatVisible
                ? new StyleColor(Color.white)
                : new StyleColor(Color.clear);
        displayButton.text = chatVisible ? "Hide Chat" : "Show Chat";
    }

    /// <summary>
    /// Adds the pieces the participant needs in order to know what to do: how to
    /// talk, what the input is for, whether the server is there, and where the
    /// conversation begins. None of it existed, which is the first thing every
    /// round of testing reported.
    /// </summary>
    private void BuildAffordances()
    {
        var inputRow = uiChat.rootVisualElement.Q("InputRow");

        if (messageInput != null && inputRow != null && placeholder == null)
        {
            // Covers both ways in: nothing on screen said either was possible.
            placeholder = new Label("Type a message, or hold Space / the circle to talk")
            {
                pickingMode = PickingMode.Ignore
            };
            placeholder.style.position = Position.Absolute;
            placeholder.style.left = 8;
            placeholder.style.fontSize = 12;
            placeholder.style.color = new StyleColor(new Color(0.45f, 0.45f, 0.45f));
            inputRow.Add(placeholder);

            messageInput.RegisterValueChangedCallback(_ => RefreshPlaceholder());
            RefreshPlaceholder();
        }

        if (connectionStatus == null && chatArea != null)
        {
            connectionStatus = new Label("Connecting to the server...");
            connectionStatus.style.fontSize = 11;
            connectionStatus.style.paddingLeft = 6;
            connectionStatus.style.paddingTop = 2;
            connectionStatus.style.paddingBottom = 2;
            connectionStatus.style.color = new StyleColor(new Color(0.55f, 0.4f, 0.1f));
            chatArea.Insert(0, connectionStatus);
        }

        var scroll = uiChat.rootVisualElement.Q<ScrollView>("Chat");
        if (scroll != null && scroll.childCount == 0)
        {
            var start = new Label("Start of the conversation");
            start.style.fontSize = 10;
            start.style.unityTextAlign = TextAnchor.MiddleCenter;
            start.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            start.style.paddingTop = 6;
            start.style.paddingBottom = 6;
            scroll.Add(start);
        }
    }

    private void RefreshPlaceholder()
    {
        if (placeholder == null || messageInput == null) return;
        bool empty = string.IsNullOrEmpty(messageInput.value);
        placeholder.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>Show whether the server is reachable, rather than leaving the
    /// participant to guess from an input box that does nothing.</summary>
    public void SetConnectionStatus(bool connected)
    {
        if (connectionStatus == null) return;
        connectionStatus.text = connected
            ? "Connected to the server"
            : "Not connected — the agent cannot answer yet";
        connectionStatus.style.color = connected
            ? new StyleColor(new Color(0.1f, 0.5f, 0.2f))
            : new StyleColor(new Color(0.7f, 0.2f, 0.2f));
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
        SetConnectionStatus(enabled);
        RefreshPlaceholder();
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
