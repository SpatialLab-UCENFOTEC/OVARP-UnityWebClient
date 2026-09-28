// WebSocket client for the OVARP server (/ws/client/{id}).
// Transport is NativeWebSocket: ClientWebSocket in the editor, the browser's
// WebSocket via .jslib on the Web target, behind one API.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NativeWebSocket;
using UnityEngine;

public class OvarpServerConnector : MonoBehaviour
{
    [Header("OVARP server")]
    [SerializeField] private string defaultServerUrl = "ws://localhost:8000";
    // Set at runtime by ServerSetupUI; overrides the serialized URL when non-empty
    private string _runtimeServerUrl;
    // client_id: identifies this connection in the WebSocket URL path
    [SerializeField] private string clientId    = "web_01";
    // sender: identifies the origin device in every message body
    [SerializeField] private string sender      = "web_01";
    [SerializeField] private string targetAgent = "agent_alpha";
    [SerializeField] private float  connectTimeoutSeconds = 5f;

    public event Action            OnConnected;
    public event Action<string>    OnConnectionFailed;
    public event Action<string>    OnTextReply;
    public event Action<string>    OnUserTranscript;
    public event Action<AudioClip> OnTtsComplete;
    public event Action<string>    OnMovementCommand;   // move_closer, move_farther, move_left, move_right, reset_position
    public event Action<string>    OnAnimationCommand;  // clap, bow, thumbs_up, thinking, shrug, dance, etc.
    public event Action<string>    OnEmotionCommand;    // neutral, happy, sad, angry, surprised
    public event Action<string>    OnLooksCommand;      // user, away, agent_beta
    public event Action<string>    OnReplyChunk;        // incremental text while the reply streams in
    public event Action<PipelineError> OnPipelineError; // a stage failed server-side
    public event Action<TurnLatency>   OnLatency;       // stt/llm/tts/total for the finished turn

    /// <summary>A stage of the server pipeline that failed, so the UI can stop waiting.</summary>
    public readonly struct PipelineError
    {
        public readonly string Stage;     // stt, llm or tts
        public readonly string Provider;
        public readonly string Message;

        public PipelineError(string stage, string provider, string message)
        {
            Stage = stage; Provider = provider; Message = message;
        }
    }

    /// <summary>Per-turn timings, published once the audio for the turn is done.</summary>
    public readonly struct TurnLatency
    {
        public readonly int SttMs, LlmMs, TtsMs, TotalMs;

        public TurnLatency(int stt, int llm, int tts, int total)
        {
            SttMs = stt; LlmMs = llm; TtsMs = tts; TotalMs = total;
        }
    }

    /// <summary>True while the socket is open and messages can actually be sent.</summary>
    public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open;

    // ── private state ──────────────────────────────────────────────────────────
    private WebSocket _ws;
    private readonly List<byte> _ttsBuffer = new();   // accumulates decoded tts_chunk bytes
    private bool  _isConnecting;
    private bool  _hasConnected;
    private bool  _shuttingDown;
    private bool  _failureReported;   // one failure message per connect attempt
    private float _reconnectDelay = 1f;

    // ══════════════════════════════════════════════════════════════════════════
    // Unity lifecycle
    // ══════════════════════════════════════════════════════════════════════════

    private void Update()
    {
        // In WebGL the browser invokes the callbacks directly; elsewhere the
        // receive loop queues them for the main thread.
#if !UNITY_WEBGL || UNITY_EDITOR
        _ws?.DispatchMessageQueue();
#endif
    }

    private void OnDestroy()
    {
        _shuttingDown = true;
        DisposeSocket();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Connection
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Called by ServerSetupUI with the user-entered server base URL (e.g. ws://192.168.1.5:8000).</summary>
    public void ConnectToServer(string serverBaseUrl)
    {
        _runtimeServerUrl = serverBaseUrl;
        _shuttingDown = false;
        _hasConnected = false;
        _reconnectDelay = 1f;
        Connect();
    }

    private string ResolveUrl()
    {
        string baseUrl = string.IsNullOrEmpty(_runtimeServerUrl) ? defaultServerUrl : _runtimeServerUrl;
        return $"{baseUrl.TrimEnd('/')}/ws/client/{clientId}";
    }

    /// <summary>
    /// A page served over HTTPS cannot open an insecure ws:// socket. Browsers block it
    /// before any handshake, so surface it as a clear message instead of a silent failure.
    /// Loopback is exempt: the mixed content spec treats it as a potentially trustworthy
    /// origin, which is what makes a hosted page able to reach a server on the user's machine.
    /// </summary>
    private string DetectMixedContent(string url)
    {
        string pageUrl = Application.absoluteURL;
        bool pageIsSecure = !string.IsNullOrEmpty(pageUrl) &&
                            pageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        bool socketIsInsecure = url.StartsWith("ws://", StringComparison.OrdinalIgnoreCase);

        if (!pageIsSecure || !socketIsInsecure || IsLoopback(url)) return null;

        return "This page is served over HTTPS, so the browser blocks insecure ws:// connections. " +
               "Use wss://, or run the server on localhost.";
    }

    private static bool IsLoopback(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) return false;

        string host = uri.Host;
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host == "::1"
            || host == "[::1]"
            || host.StartsWith("127.", StringComparison.Ordinal);
    }

    private async void Connect()
    {
        if (_isConnecting) return;

        string url = ResolveUrl();

        string mixedContent = DetectMixedContent(url);
        if (mixedContent != null)
        {
            ReportFailure(mixedContent);
            return;
        }

        _isConnecting = true;
        _failureReported = false;

        DisposeSocket();
        _ws = new WebSocket(url);
        _ws.OnOpen    += HandleOpen;
        _ws.OnMessage += HandleMessage;
        _ws.OnError   += HandleError;
        _ws.OnClose   += HandleClose;

        StartCoroutine(FailIfNotOpenWithin(connectTimeoutSeconds, _ws));

        try
        {
            await _ws.Connect();
        }
        catch (Exception e)
        {
            _isConnecting = false;
            Debug.LogWarning($"[OvarpServerConnector] Connect failed: {e.Message}");
            ReportFailure($"Cannot reach the OVARP server at {url}.");
        }
    }

    // Detaching the handlers matters on reconnect: the previous socket can still emit a
    // close callback and would otherwise drive a second reconnect chain.
    private void DisposeSocket()
    {
        if (_ws == null) return;

        WebSocket socket = _ws;
        _ws = null;

        socket.OnOpen    -= HandleOpen;
        socket.OnMessage -= HandleMessage;
        socket.OnError   -= HandleError;
        socket.OnClose   -= HandleClose;

        // Retrying from the setup panel can leave a socket mid-handshake.
        if (socket.State == WebSocketState.Open || socket.State == WebSocketState.Connecting)
            _ = socket.Close();
    }

    private void ReportFailure(string message)
    {
        if (_failureReported) return;
        _failureReported = true;
        Debug.LogWarning($"[OvarpServerConnector] {message}");
        OnConnectionFailed?.Invoke(message);
    }

    // NativeWebSocket has no connect timeout of its own, and on the Web target the
    // browser can sit in Connecting for a long time before giving up.
    private IEnumerator FailIfNotOpenWithin(float seconds, WebSocket socket)
    {
        yield return new WaitForSeconds(seconds);

        if (socket != _ws || _hasConnected || socket.State != WebSocketState.Connecting)
            yield break;

        _isConnecting = false;
        ReportFailure($"Timed out connecting to {ResolveUrl()}.");
        _ = socket.Close();
    }

    private void HandleOpen()
    {
        _isConnecting = false;
        _hasConnected = true;
        _reconnectDelay = 1f;
        Debug.Log("[OvarpServerConnector] Connected to OVARP server.");
        OnConnected?.Invoke();
    }

    private void HandleError(string error)
    {
        Debug.LogWarning($"[OvarpServerConnector] WebSocket error: {error}");
    }

    private void HandleClose(WebSocketCloseCode closeCode)
    {
        _isConnecting = false;

        if (_shuttingDown) return;

        if (!_hasConnected)
        {
            ReportFailure($"Cannot reach the OVARP server at {ResolveUrl()}.");
            return;
        }

        Debug.LogWarning($"[OvarpServerConnector] Connection closed ({closeCode}). Reconnecting in {_reconnectDelay}s…");
        StartCoroutine(ReconnectAfterDelay(_reconnectDelay));
        _reconnectDelay = Mathf.Min(_reconnectDelay * 2f, 30f);
    }

    private IEnumerator ReconnectAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!_shuttingDown) Connect();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Send
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Send recorded WAV bytes to the OVARP server as a base64 stt_request.</summary>
    public async void SendAudio(byte[] wavBytes)
    {
        if (_ws == null || _ws.State != WebSocketState.Open)
        {
            Debug.LogWarning("[OvarpServerConnector] WebSocket not open — dropping audio.");
            return;
        }

        string base64 = Convert.ToBase64String(wavBytes);
        string json = $"{{\"sender\":\"{sender}\",\"target_device\":\"all\","
                    + $"\"target_agent\":\"{targetAgent}\",\"command_type\":\"audio\","
                    + $"\"command\":\"stt_request\",\"subcommand\":{{\"audio_base64\":\"{base64}\"}}}}";

        try
        {
            await _ws.SendText(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[OvarpServerConnector] Send failed: {e.Message}");
        }
    }

    /// <summary>Send typed text to the OVARP server as an llm_request.</summary>
    public async void SendText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        if (_ws == null || _ws.State != WebSocketState.Open)
        {
            Debug.LogWarning("[OvarpServerConnector] WebSocket not open — dropping message.");
            return;
        }

        string json = $"{{\"sender\":\"{sender}\",\"target_device\":\"all\","
                    + $"\"target_agent\":\"{targetAgent}\",\"command_type\":\"message\","
                    + $"\"command\":\"llm_request\",\"subcommand\":{{\"text\":\"{EscapeJson(text)}\"}}}}";

        try
        {
            await _ws.SendText(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[OvarpServerConnector] Send failed: {e.Message}");
        }
    }

    // The outgoing payload is assembled as a string rather than serialized, so anything
    // the participant types has to be escaped here or a quote breaks the whole message.
    private static string EscapeJson(string value)
    {
        var sb = new StringBuilder(value.Length + 16);
        foreach (char c in value)
        {
            switch (c)
            {
                case '"':  sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Receive
    // ══════════════════════════════════════════════════════════════════════════

    private void HandleMessage(byte[] data)
    {
        string json = Encoding.UTF8.GetString(data);

        try
        {
            string topic   = ExtractField(json, "topic");
            string command = ExtractField(json, "command");

            if (topic == "message" && command == "user_transcript")
            {
                string text = ExtractField(json, "text");
                if (!string.IsNullOrEmpty(text))
                    OnUserTranscript?.Invoke(text);
            }
            else if (topic == "message" && command == "llm_reply")
            {
                string text = ExtractField(json, "text");
                if (!string.IsNullOrEmpty(text))
                    OnTextReply?.Invoke(text);
                else
                    Debug.LogWarning("[OvarpServerConnector] llm_reply received but text was empty.");
            }
            else if (topic == "audio" && command == "tts_chunk")
            {
                string chunk = ExtractField(json, "audio_base64");
                if (!string.IsNullOrEmpty(chunk))
                    _ttsBuffer.AddRange(Convert.FromBase64String(chunk));
                else
                    Debug.LogWarning("[OvarpServerConnector] tts_chunk received but audio_base64 was empty.");
            }
            else if (topic == "audio" && command == "tts_complete")
            {
                CompleteTts();
            }
            else if (topic == "action" && command == "execute_state")
            {
                DispatchExecuteState(json);
            }
            else if (topic == "message" && command == "llm_reply_chunk")
            {
                string chunk = ExtractField(json, "text");
                if (!string.IsNullOrEmpty(chunk))
                    OnReplyChunk?.Invoke(chunk);
            }
            else if (topic == "system" && command == "pipeline_error")
            {
                // Without this the chat sits on "…" forever when a provider fails
                OnPipelineError?.Invoke(new PipelineError(
                    ExtractField(json, "stage")    ?? "pipeline",
                    ExtractField(json, "provider") ?? "",
                    ExtractField(json, "message")  ?? "The server could not complete this turn."));
            }
            else if (topic == "system" && command == "latency")
            {
                OnLatency?.Invoke(new TurnLatency(
                    ExtractInt(json, "stt_ms"), ExtractInt(json, "llm_ms"),
                    ExtractInt(json, "tts_ms"), ExtractInt(json, "total_ms")));
            }
            else if (topic == "system" && command == "marker_logged")
            {
                // Confirmation only; nothing for the participant to see.
            }
            else
            {
                Debug.Log($"[OvarpServerConnector] Unhandled message — topic='{topic}' command='{command}'");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[OvarpServerConnector] HandleMessage error: {e.Message}");
        }
    }

    private void CompleteTts()
    {
        if (_ttsBuffer.Count == 0)
        {
            Debug.LogWarning("[OvarpServerConnector] tts_complete but buffer is empty.");
            return;
        }

        byte[] wavBytes = _ttsBuffer.ToArray();
        _ttsBuffer.Clear();

        try
        {
            var (samples, channels, sampleRate) = DecodeWavPcm(wavBytes);
            var clip = AudioClip.Create("tts_ovarp", samples.Length / channels, channels, sampleRate, false);
            clip.SetData(samples, 0);
            OnTtsComplete?.Invoke(clip);
        }
        catch (Exception e)
        {
            Debug.LogError($"[OvarpServerConnector] WAV decode failed: {e.Message}");
        }
    }

    private void DispatchExecuteState(string json)
    {
        string movement  = ExtractField(json, "movement");
        string animation = ExtractField(json, "actions");
        string emotion   = ExtractField(json, "emotions");
        string looks     = ExtractField(json, "looks");

        // These were an else-if chain. The server marks every category required in
        // the LLM tool schema, so a generated execute_state carries all of them at
        // once — which meant only `movement` ever fired and the agent never changed
        // expression. Each category is independent and all of them apply.
        bool handled = false;
        if (!string.IsNullOrEmpty(movement))  { OnMovementCommand?.Invoke(movement);   handled = true; }
        if (!string.IsNullOrEmpty(animation)) { OnAnimationCommand?.Invoke(animation); handled = true; }
        if (!string.IsNullOrEmpty(emotion))   { OnEmotionCommand?.Invoke(emotion);     handled = true; }
        if (!string.IsNullOrEmpty(looks))     { OnLooksCommand?.Invoke(looks);         handled = true; }

        if (!handled)
            Debug.LogWarning("[OvarpServerConnector] execute_state received but no recognized subcommand key.");
    }

    private static (float[] samples, int channels, int sampleRate) DecodeWavPcm(byte[] wav)
    {
        // Standard PCM WAV header layout (matches SavWav.cs output)
        int channels   = BitConverter.ToUInt16(wav, 22);
        int sampleRate = BitConverter.ToInt32(wav, 24);
        const int dataOffset = 44;

        int sampleCount = (wav.Length - dataOffset) / 2;
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
            samples[i] = BitConverter.ToInt16(wav, dataOffset + i * 2) / 32768f;

        return (samples, channels, sampleRate);
    }

    /// <summary>
    /// Reads one string field out of the envelope.
    ///
    /// This used to stop at the next quote character and return the raw slice,
    /// which had two consequences the participant could see: a reply containing
    /// its own quotation mark was cut short there, and escape sequences arrived
    /// as literal text, so the agent appeared to say "\n\n" out loud in the chat.
    /// The scan now honours backslash escapes and decodes them.
    /// </summary>
    private static string ExtractField(string json, string key)
    {
        int start = FindValueStart(json, key);
        if (start < 0 || json[start] != '"') return null;
        start++;

        var sb = new StringBuilder();
        for (int i = start; i < json.Length; i++)
        {
            char c = json[i];

            if (c == '"') return sb.ToString();   // unescaped quote ends the value

            if (c != '\\') { sb.Append(c); continue; }

            if (++i >= json.Length) break;
            switch (json[i])
            {
                case 'n':  sb.Append('\n'); break;
                case 'r':  sb.Append('\r'); break;
                case 't':  sb.Append('\t'); break;
                case 'b':  sb.Append('\b'); break;
                case 'f':  sb.Append('\f'); break;
                case '"':  sb.Append('"');  break;
                case '/':  sb.Append('/');  break;
                case '\\': sb.Append('\\'); break;
                case 'u':
                    if (i + 4 < json.Length &&
                        int.TryParse(json.Substring(i + 1, 4),
                                     System.Globalization.NumberStyles.HexNumber,
                                     System.Globalization.CultureInfo.InvariantCulture,
                                     out int code))
                    {
                        sb.Append((char)code);
                        i += 4;
                    }
                    break;
                default:
                    sb.Append(json[i]);   // unknown escape: keep the character
                    break;
            }
        }

        Debug.LogWarning($"[OvarpServerConnector] Unterminated string for '{key}'.");
        return sb.ToString();
    }

    /// <summary>Reads one integer field, or 0 when it is absent.</summary>
    private static int ExtractInt(string json, string key)
    {
        int start = FindValueStart(json, key);
        if (start < 0) return 0;

        int end = start;
        while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
        return int.TryParse(json.Substring(start, end - start), out int value) ? value : 0;
    }

    /// <summary>Index of the first character of the value for <paramref name="key"/>.</summary>
    private static int FindValueStart(string json, string key)
    {
        string search = $"\"{key}\":";
        int at = json.IndexOf(search, StringComparison.Ordinal);
        if (at < 0) return -1;
        at += search.Length;
        while (at < json.Length && json[at] == ' ') at++;
        return at < json.Length ? at : -1;
    }
}
