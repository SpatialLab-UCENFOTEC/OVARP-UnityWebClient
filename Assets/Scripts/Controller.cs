using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

[RequireComponent(typeof(AudioSource))]
public class Controller : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioSource audioSource2;
    public AgentState CurrentState { get; private set; }
    public string[] CurrentIntent { get; private set; }
    public string CurrentUtterance { get; private set; }
    private Queue<string> utteranceQueue = new Queue<string>();
    private Queue<string[]> intentQueue = new Queue<string[]>();
    public bool paused;
    public float audioFadeIn = .2f;
    public float audioFadeOut = .3f;
    public enum AgentState
    {
        Idle,
        Waiting,
        Speaking
    }

    public GameObject agent;
    public GameObject LipSync;
    public LipSync lipSync;

    public Chat chat;
    public GameObject ChatObject;

    public Queue<string> responses;

    public AnimController anim;
    public RecIndicator recIndicator;
    public EmotionController emotionController;
    public HeadLookAt headLookAt;

    // OVARP WebSocket transport (assigned in Inspector)
    public OvarpServerConnector serverConnector;

    private Vector3 _agentInitialPosition;
    private const float MoveStep = 0.1f;

    private IMicrophoneCapture _microphone;
    private bool isAudioReady = false;
    private bool isListening = false;
    private bool _playbackStarted = false; // guards against exiting Speaking before Play() is called

    void Start()
    {
        _microphone = MicrophoneCaptureFactory.Create();

        LipSync = GameObject.Find("Frank");
        lipSync = LipSync.GetComponent<LipSync>();

        CurrentState = AgentState.Idle;
        audioSource2 = GetComponent<AudioSource>();

        ChatObject = GameObject.Find("Chat UIDocument");
        chat = ChatObject.GetComponent<Chat>();
        responses = new Queue<string>();

        anim             = GameObject.Find("Frank").GetComponent<AnimController>();
        emotionController = GameObject.Find("Frank").GetComponent<EmotionController>();
        headLookAt       = GameObject.Find("Frank").GetComponent<HeadLookAt>();
        recIndicator     = GameObject.Find("Sphere").GetComponent<RecIndicator>();

        _agentInitialPosition = agent.transform.position;

        if (serverConnector == null)
            Debug.LogError("[Controller] serverConnector is not assigned in the Inspector.");
        else
        {
            serverConnector.OnConnected       += OnServerConnected;
            serverConnector.OnUserTranscript  += OnUserTranscript;
            serverConnector.OnTextReply       += OnAgentTextReply;
            serverConnector.OnTtsComplete     += OnAgentTtsReady;
            serverConnector.OnMovementCommand  += OnAgentMovement;
            serverConnector.OnAnimationCommand += OnAgentAnimation;
            serverConnector.OnAvatarCommand    += OnAgentAvatarChange;
            serverConnector.OnEmotionCommand   += OnAgentEmotion;
            serverConnector.OnLooksCommand     += OnAgentLooks;
        }

        if (chat != null)
            chat.OnMessageSubmitted += OnUserTypedMessage;
    }

    private void OnDestroy()
    {
        if (serverConnector != null)
        {
            serverConnector.OnConnected        -= OnServerConnected;
            serverConnector.OnUserTranscript   -= OnUserTranscript;
            serverConnector.OnTextReply        -= OnAgentTextReply;
            serverConnector.OnTtsComplete      -= OnAgentTtsReady;
            serverConnector.OnMovementCommand  -= OnAgentMovement;
            serverConnector.OnAnimationCommand -= OnAgentAnimation;
            serverConnector.OnAvatarCommand    -= OnAgentAvatarChange;
            serverConnector.OnEmotionCommand   -= OnAgentEmotion;
            serverConnector.OnLooksCommand     -= OnAgentLooks;
        }

        if (chat != null)
            chat.OnMessageSubmitted -= OnUserTypedMessage;
    }

    // ── Microphone permission ─────────────────────────────────────────────────

    /// <summary>
    /// Call from a user gesture handler (ServerSetupUI's Connect button). Browsers reject
    /// getUserMedia outside one, which would latch the permission to denied for the session.
    /// </summary>
    public void RequestMicrophoneAccess() => _microphone?.RequestPermission();

    // ── OvarpServerConnector event handlers ───────────────────────────────────

    private void OnServerConnected()
    {
        _inputEnabled = true;
        chat?.SetInputEnabled(true);
    }

    private void OnUserTranscript(string text)
    {
        chat.killTempUser();
        chat.sendUserMessage(text);
    }

    /// <summary>
    /// Text typed into the chat box. Chat has already shown the user's bubble, so this
    /// only puts the agent into its waiting state and forwards the message.
    /// </summary>
    private void OnUserTypedMessage(string text)
    {
        // The socket can drop after connecting; without this the message would be
        // dropped with only a browser-console warning to show for it.
        if (serverConnector == null || !serverConnector.IsConnected)
        {
            chat.sendAgentMessage("Not connected to the server — your message was not sent.");
            chat.SetInputEnabled(false);
            return;
        }

        chat.SendTempAgent();
        anim.StartThinking();
        serverConnector.SendText(text);
    }

    private void OnAgentTextReply(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        anim.StopThinking();
        ReceiveMessage(text);
    }

    private void OnAgentTtsReady(AudioClip clip)
    {
        audioSource.clip = clip;
        lipSync.audioSource.clip = clip;
        isAudioReady = true;
    }

    private void OnAgentMovement(string direction)
    {
        if (direction == "reset_position")
        {
            agent.transform.position = _agentInitialPosition;
            return;
        }

        // Movement relative to the user (camera) horizontal orientation
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        Vector3 forward = cam != null
            ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized
            : Vector3.forward;
        Vector3 right = cam != null
            ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized
            : Vector3.right;

        Vector3 delta = direction switch
        {
            "move_closer"  => -forward,
            "move_farther" => forward,
            "move_left"    => -right,
            "move_right"   => right,
            _              => Vector3.zero
        };

        agent.transform.position += delta * MoveStep;
    }

    private void OnAgentAnimation(string animName)
    {
        anim.PlayAnimation(animName);
    }

    private void OnAgentEmotion(string emotion)
    {
        emotionController?.SetEmotion(emotion);
    }

    private void OnAgentLooks(string lookTarget)
    {
        headLookAt?.SetLookTarget(lookTarget);
    }

    private void OnAgentAvatarChange(string avatarName)
    {
        // TODO: implement avatar prefab swap
        Debug.Log($"[Controller] Avatar change requested: '{avatarName}' — not yet implemented.");
    }

    // ── Update / state machine ────────────────────────────────────────────────

    void Update()
    {
        FixAudioClicks();

        if (CurrentState == AgentState.Idle)
        {
            if (!paused && utteranceQueue.Count > 0)
            {
                CurrentState = AgentState.Waiting;
                CurrentUtterance = utteranceQueue.Dequeue();
                CurrentIntent = intentQueue.Dequeue();
            }
        }
        else if (CurrentState == AgentState.Waiting)
        {
            if (isAudioReady)
            {
                CurrentState = AgentState.Speaking;
                _playbackStarted = false;
                StartCoroutine(lipSync.AnalyzeAudioClip(audioSource.clip));
            }
        }
        else if (CurrentState == AgentState.Speaking)
        {
            if (audioSource.isPlaying) _playbackStarted = true;
            if (_playbackStarted && !audioSource.isPlaying)
            {
                CurrentState = AgentState.Idle;
                isAudioReady = false;
                _playbackStarted = false;
                CurrentIntent = null;
            }
        }

        if (responses.Count != 0)
        {
            chat.killTempAgent();
            chat.sendAgentMessage(responses.Dequeue());
        }

        CheckInputTriggers();
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private bool _inputEnabled = false;
    private bool _touchWasActive = false;

    private void CheckInputTriggers()
    {
        if (!_inputEnabled) return;

        // Typing a space in the chat box would otherwise start recording
        if (chat != null && chat.IsTyping) return;

        // Desktop: spacebar toggle
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            ProcessInput();
            return;
        }

        // Mobile browsers: screen tap toggle — gate on rising edge to avoid multi-frame re-trigger
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            var phase = touch.phase.ReadValue();
            bool touchActive = phase != UnityEngine.InputSystem.TouchPhase.None
                            && phase != UnityEngine.InputSystem.TouchPhase.Ended
                            && phase != UnityEngine.InputSystem.TouchPhase.Canceled;
            if (touchActive && !_touchWasActive)
            {
                // Skip mic trigger when the tap lands on a UI element
                int fingerId = (int)touch.touchId.ReadValue();
                bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId);
                if (!overUI)
                    ProcessInput();
                _touchWasActive = true;
                return;
            }
            if (!touchActive)
                _touchWasActive = false;
        }
    }

    // ── Recording ─────────────────────────────────────────────────────────────

    /// <summary>Toggles recording. Also wired to the push-to-talk button in the chat UI.</summary>
    public void ProcessInput()
    {
        if (!isListening)
        {
            if (_microphone.Permission != MicrophonePermission.Granted)
            {
                Debug.LogWarning($"[Controller] Microphone unavailable (permission: {_microphone.Permission}).");
                return;
            }

            recIndicator.StartBlinking();
            _microphone.StartRecording();
            chat.SendTempUser();
            isListening = true;
        }
        else
        {
            recIndicator.StopBlinking();
            bool sent = StopRecording();
            if (sent) anim.StartThinking();
            isListening = false;
        }
    }

    // Returns true if audio was captured and sent, false if nothing was recorded.
    private bool StopRecording()
    {
        AudioClip clip = _microphone.StopRecording();
        if (clip == null)
        {
            Debug.LogWarning("StopRecording: no audio captured.");
            return false;
        }

        serverConnector.SendAudio(SavWav.ToWavBytes(clip));
        return true;
    }

    // ── Speech queue ──────────────────────────────────────────────────────────

    public void ReceiveMessage(string text, bool immediate = false)
    {
        utteranceQueue.Enqueue(text);
        intentQueue.Enqueue(null);
        responses.Enqueue(text);
    }

    public void SpeakIntent(string message, bool immediate = false)
    {
        Speak(message, immediate);
    }

    public void Speak(string utterance, bool immediate = false, string[] intent = null)
    {
        if (immediate)
        {
            utteranceQueue.Clear();
            intentQueue.Clear();
            CurrentState = AgentState.Idle;
        }
        utteranceQueue.Enqueue(utterance);
        intentQueue.Enqueue(intent);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void FixAudioClicks()
    {
        // Fade in/out to hide TTS audio clicks at start/end
        if (audioSource.clip == null) return;

        if (audioSource.time < audioFadeIn)
        {
            audioSource.volume = audioSource.time / audioFadeIn;
        }
        else if (audioSource.time > audioSource.clip.length - audioFadeOut)
        {
            audioSource.volume = (audioSource.clip.length - audioSource.time) / audioFadeOut;
        }
        else
        {
            audioSource.volume = 1;
        }
    }
}
