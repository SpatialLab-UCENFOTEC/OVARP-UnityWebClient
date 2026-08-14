// Uses UI Toolkit (same pipeline as Chat) — requires a UIDocument component on this GameObject.
// Assign the same PanelSettings used by the Chat UIDocument in the Inspector.
using System;
using UnityEngine;
using UnityEngine.UIElements;

public class ServerSetupUI : MonoBehaviour
{
    [SerializeField] private OvarpServerConnector serverConnector;
    [SerializeField] private UIDocument chatDocument;
    [SerializeField] private string defaultIp = "localhost";

    private const string PlayerPrefsKey = "ovarp_server_url";
    private const string LegacyIpPrefsKey = "ovarp_server_ip";
    private const int DefaultPort = 8000;

    private VisualElement _root;
    private TextField _urlField;
    private Button _connectButton;
    private Label _statusLabel;
    private Controller _controller;

    private void Start()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
        _controller = FindFirstObjectByType<Controller>();
        BuildUI();

        // Hide chat visually and block its input without disabling the UIDocument —
        // disabling it in Awake breaks Chat.OnEnable() which initializes from rootVisualElement.
        if (chatDocument != null)
        {
            chatDocument.rootVisualElement.style.display = DisplayStyle.None;
            chatDocument.rootVisualElement.pickingMode = PickingMode.Ignore;
        }

        if (serverConnector != null)
        {
            serverConnector.OnConnected += OnConnected;
            serverConnector.OnConnectionFailed += OnConnectionFailed;
        }
        else
        {
            Debug.LogError("[ServerSetupUI] serverConnector is not assigned in the Inspector.");
        }
    }

    private void OnDestroy()
    {
        if (serverConnector != null)
        {
            serverConnector.OnConnected -= OnConnected;
            serverConnector.OnConnectionFailed -= OnConnectionFailed;
        }
    }

    private void OnConnected()
    {
        if (chatDocument != null)
        {
            chatDocument.rootVisualElement.style.display = DisplayStyle.Flex;
            chatDocument.rootVisualElement.pickingMode = PickingMode.Position;
        }

        _root.Clear();
        _root.style.display = DisplayStyle.None;
        enabled = false;
    }

    private void OnConnectionFailed(string reason)
    {
        _statusLabel.text = reason;
        _statusLabel.style.display = DisplayStyle.Flex;
        _connectButton.SetEnabled(true);
    }

    private void BuildUI()
    {
        _root.Clear();
        _root.pickingMode = PickingMode.Ignore;

        // Compact card anchored to top-left
        var card = new VisualElement();
        card.style.position = Position.Absolute;
        card.style.top = 16;
        card.style.left = 16;
        card.style.maxWidth = 420;
        card.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        card.style.paddingTop = card.style.paddingBottom = 16;
        card.style.paddingLeft = card.style.paddingRight = 16;
        card.pickingMode = PickingMode.Position;
        _root.Add(card);

        var title = new Label("OVARP server");
        title.style.color = Color.white;
        title.style.fontSize = 20;
        title.style.unityTextAlign = TextAnchor.MiddleLeft;
        title.style.marginBottom = 4;
        card.Add(title);

        var hint = new Label($"IP address or full ws:// / wss:// URL (port {DefaultPort} assumed)");
        hint.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        hint.style.fontSize = 13;
        hint.style.whiteSpace = WhiteSpace.Normal;
        hint.style.marginBottom = 10;
        card.Add(hint);

        _urlField = new TextField { value = LoadSavedValue() };
        _urlField.style.fontSize = 18;
        _urlField.style.marginBottom = 10;
        _urlField.style.minWidth = 240;
        card.Add(_urlField);

        _connectButton = new Button { text = "Connect" };
        _connectButton.style.fontSize = 18;
        _connectButton.style.paddingLeft = _connectButton.style.paddingRight = 16;
        _connectButton.style.paddingTop = _connectButton.style.paddingBottom = 8;
        _connectButton.style.backgroundColor = new Color(0f, 0.47f, 1f, 1f);
        _connectButton.style.color = Color.white;
        // ClickEvent only: PointerDownEvent fires before the browser treats the input as a
        // user gesture, and getUserMedia would be rejected.
        _connectButton.RegisterCallback<ClickEvent>(_ => OnConnectClicked());
        card.Add(_connectButton);

        _statusLabel = new Label();
        _statusLabel.style.color = new Color(1f, 0.45f, 0.4f, 1f);
        _statusLabel.style.fontSize = 14;
        _statusLabel.style.whiteSpace = WhiteSpace.Normal;
        _statusLabel.style.marginTop = 10;
        _statusLabel.style.display = DisplayStyle.None;
        card.Add(_statusLabel);
    }

    private string LoadSavedValue()
    {
        string saved = PlayerPrefs.GetString(PlayerPrefsKey, "");
        if (string.IsNullOrEmpty(saved))
            saved = PlayerPrefs.GetString(LegacyIpPrefsKey, "");

        return string.IsNullOrEmpty(saved) ? defaultIp : saved;
    }

    private void OnConnectClicked()
    {
        string input = _urlField.value.Trim();
        if (string.IsNullOrEmpty(input)) input = defaultIp;

        PlayerPrefs.SetString(PlayerPrefsKey, input);
        PlayerPrefs.Save();

        if (serverConnector == null) { Debug.LogError("[ServerSetupUI] serverConnector not assigned."); return; }

        // This click is the user gesture the browser requires before granting the microphone.
        _controller?.RequestMicrophoneAccess();

        _statusLabel.style.display = DisplayStyle.None;
        _connectButton.SetEnabled(false);
        serverConnector.ConnectToServer(BuildServerUrl(input));
    }

    private static string BuildServerUrl(string input)
    {
        if (input.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            return input;

        return input.Contains(":") ? $"ws://{input}" : $"ws://{input}:{DefaultPort}";
    }
}
