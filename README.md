<p align="center">
  <img src="https://img.shields.io/badge/unity-6%20(URP)-black?logo=unity&logoColor=white" alt="Unity" />
  <img src="https://img.shields.io/badge/platform-Web%20(WebGL2)-blue" alt="Platform" />
  <img src="https://img.shields.io/badge/transport-NativeWebSocket-00A8E8" alt="Transport" />
  <img src="https://img.shields.io/badge/csharp-11-purple?logo=csharp&logoColor=white" alt="C#" />
  <img src="https://img.shields.io/badge/license-MIT-green" alt="License" />
</p>

<h1 align="center">🌐 Open Virtual Agent Research Platform — Unity Web Client</h1>

<p align="center">
  <strong>Browser client for driving embodied conversational agents: real-time voice interaction, lip-sync, emotion, gestures, and spatial movement, rendered in a Unity Web build with no plugins to install.</strong>
</p>

<p align="center">
  <em>Reference Unity implementation of the OVARP client protocol for the web. Ported from the XREAL glasses client; the agent runtime is identical, the platform layer is not.</em>
</p>

---

## ✨ What is this repo?

**This repository** is the **web** frontend for **OVARP** (Open Virtual Agent Research Platform). It connects to an OVARP-compatible backend over a WebSocket and renders a fully animated 3D avatar — synchronized speech, lip-sync, facial emotions, gestures, gaze, and spatial movement — inside a browser canvas.

- 🎙️ **Voice capture** — `getUserMedia` + AudioWorklet through a JS plugin, packed as WAV and streamed to the server as base64
- 👄 **Lip-sync** — Real-time amplitude analysis drives blendshape-based mouth animation
- 😊 **Emotion system** — Server-commanded facial expressions via blendshapes
- 🤖 **Gesture playback** — Named animation triggers (`clap`, `bow`, `thumbs_up`, `dance`, `thinking`, etc.)
- 👀 **Head look-at** — Agent gaze directed at the user, away, or another agent
- 📐 **Spatial movement** — Agent repositioned relative to the camera orientation (`move_closer`, `move_farther`, `move_left`, `move_right`, `reset_position`)
- 💬 **Chat UI** — UI Toolkit conversation log with user/agent bubbles, theming, and dark mode
- 🔌 **Server setup UI** — IP or full `ws://` / `wss://` URL entry before the session starts, persisted via `PlayerPrefs`

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  User (browser)                                                 │
│  ──────────────                                                 │
│  • Speaks into microphone (getUserMedia, secure context only)   │
│  • Input: Spacebar | Screen tap (mobile) | Push-to-talk button  │
└────────────────────┬────────────────────────────────────────────┘
                     │ WAV audio (base64)
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  Controller.cs — Agent State Machine                            │
│  ───────────────────────────────────                            │
│  ┌───────────┐  ┌──────────────┐  ┌──────────────┐             │
│  │   Idle    │─▶│   Waiting    │─▶│   Speaking   │             │
│  │           │  │              │  │              │             │
│  │ • listens │  │ • awaits TTS │  │ • plays clip │             │
│  │ • records │  │ • buffers    │  │ • lip-syncs  │             │
│  └───────────┘  └──────────────┘  └──────────────┘             │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  OvarpServerConnector.cs — WebSocket transport                  │
│  ─────────────────────────────────────────────                  │
│  NativeWebSocket → ws(s)://<host>:8000/ws/client/<id>           │
│    • Editor / standalone: System.Net.WebSockets                 │
│    • Web: the browser's WebSocket via .jslib                    │
│                                                                 │
│  Events fired back to Controller (main thread):                 │
│  OnTextReply · OnTtsComplete · OnUserTranscript                 │
│  OnMovementCommand · OnAnimationCommand · OnEmotionCommand      │
│  OnLooksCommand · OnAvatarCommand · OnConnectionFailed          │
└─────────────────────────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  Avatar Components                                              │
│  ─────────────────                                              │
│  • AnimController    — named animation triggers                 │
│  • EmotionController — blendshape facial expressions            │
│  • LipSync           — amplitude → mouth blendshape             │
│  • HeadLookAt        — gaze target switching                    │
│  • GesturePlayer     — gesture clip sequencing                  │
│  • BlinkEyes         — procedural eye blink                     │
└─────────────────────────────────────────────────────────────────┘
```

`Controller` is transport-agnostic — it reacts only to events from `OvarpServerConnector`.

---

## 🚀 Quick Start

### Prerequisites

- Unity **6000.3.13f1** with the **Web (WebGL) build support** module
- A running **OVARP-compatible WebSocket server** reachable from the browser
- Git (the WebSocket transport resolves from a git URL)

### Installation

```bash
git clone <this-repository-url>
cd OVARP-UnityWebClient
```

Open the project in Unity 6. Packages resolve automatically, including
[`com.endel.nativewebsocket`](https://github.com/endel/NativeWebSocket) pinned by commit in
`Packages/manifest.json`.

Open `Assets/Scenes/WebOVARP.unity` — it is the only scene in the build list.

### Configure `OvarpServerConnector`

Select the **VirtualAgent** GameObject in the scene and set:

| Field | Description |
|---|---|
| `Default Server Url` | Fallback WebSocket base URL (default `ws://localhost:8000`) |
| `Client Id` | Unique identifier for this connection in the WebSocket path |
| `Sender` | Origin device id in each message body |
| `Target Agent` | Agent ID to address on the server (must match `config.yaml`) |
| `Connect Timeout Seconds` | How long to wait before reporting a failed connection |

At runtime the URL is overridden through the **Server Setup UI** panel before the session starts.

### Build for Web

1. **File ▸ Build Profiles**, select **Web**, and switch platform.
2. The player settings are already configured: Brotli compression, 96 MB initial heap,
   WebAssembly 2023, and the custom `OVARP` template in `Assets/WebGLTemplates/`.
3. Build into **`Build/Web`** — that path is what `vercel.json` publishes and the only build
   output the repository tracks.

Headless equivalent, for CI or a quick check:

```bash
Unity -batchmode -quit -nographics -projectPath . -buildTarget WebGL \
      -executeMethod WebBuilder.Build -buildOutput Build/Web
```

The editor must be closed — Unity refuses the project lock and exits silently with status 0.

### Deploying to Vercel

The repository is set up so Vercel serves the committed build directly, with no build step:

- `vercel.json` sets `outputDirectory` to `Build/Web` and adds the `Content-Encoding: br`
  headers the Brotli files need, plus `application/wasm` for the `.wasm.br`.
- `.gitignore` ignores every build output **except** `Build/Web`.
- `.gitattributes` keeps `.br`, `.wasm` and `.data` in Git LFS so rebuilds do not bloat the
  main pack.

**Enable Git LFS in the Vercel project settings and redeploy.** It is off by default, and
without it Vercel deploys the LFS pointer files — 130-byte text stubs — instead of the build,
and the page fails to load.

Note that every rebuild rewrites ~19 MB of binaries. That history is permanent; if the churn
becomes a problem, move the build to an orphan branch or to CI.

### Connecting from a hosted page

Vercel serves over HTTPS, which constrains what the client can reach:

| Server URL | Works from an HTTPS page? |
|---|---|
| `wss://host` with a valid certificate | Yes |
| `ws://localhost:8000` | Yes in Chrome and Firefox 84+, **no in Safari** |
| `ws://192.168.x.x:8000` | No — blocked as mixed content |

Loopback is exempt from mixed-content blocking because the specification treats it as a
potentially trustworthy origin, so a hosted page can talk to an OVARP server running on the
**viewer's own machine**. That covers local development against the deployed client; anything
else needs a publicly reachable `wss://` endpoint (a tunnel such as Cloudflare Tunnel or ngrok
is the quickest way to get one).

### Serving the build elsewhere

Voice capture requires a **secure context**: HTTPS, or `http://localhost`. Over plain HTTP on a
LAN address the browser will not expose `getUserMedia` and the microphone is unavailable — the
loading page warns about this explicitly.

The build is Brotli-compressed, so the host must send `Content-Encoding: br` for the files in
`Build/`. Unity's **Build and Run** dev server handles this automatically; for any other host,
configure it or switch **Compression Format** to `Gzip`/`Disabled` in Player Settings.

### Editor / desktop

Press **Spacebar** to toggle recording. In the editor the microphone falls back to Unity's
built-in `Microphone` class, so voice input works in Play mode without a browser.

---

## 🌐 Web platform notes

Porting from the XREAL client meant replacing every platform-level dependency. What changed and why:

| Concern | Why it could not be reused | What the web client does |
|---|---|---|
| **Microphone** | The `Microphone` class is absent from `UnityEngine.AudioModule.dll` in the Web player | `Assets/Plugins/WebGL/OvarpMic.jslib` — `getUserMedia` + `AudioWorklet`, pulled into managed memory by `WebMicrophoneCapture` |
| **WebSocket** | Browsers expose no raw sockets, so `System.Net.WebSockets.ClientWebSocket` cannot work | NativeWebSocket, which routes to the browser's own `WebSocket` on Web |
| **Threading** | Web builds are single-threaded; `Task`, background receive loops and timeout tokens are unreliable | Coroutines and main-thread callbacks throughout |
| **File I/O** | Recording round-tripped through `Application.persistentDataPath` | `SavWav.ToWavBytes()` encodes to a `byte[]` in memory |
| **Mic permission** | Browsers reject `getUserMedia` outside a user gesture, latching a denial for the session | Requested from the **Connect** button click |
| **XR stack** | XREAL, AR Foundation, XR Hands and XRI have no Web providers | Removed; the scene uses a plain camera rig |

Two things worth knowing when deploying:

- **Mixed content.** An HTTPS page cannot open a `ws://` socket. `OvarpServerConnector` detects
  this before connecting and reports it in the setup panel instead of failing silently. Use `wss://`.
- **No OpenAI fallback.** The XREAL client shipped a Whisper → GPT-4 → TTS fallback. It is compiled
  out of Web builds: the API key would be extractable from the public `.data` bundle, and
  `api.openai.com` sends no CORS headers, so the calls would fail from a browser anyway.

---

## 🎮 Avatar System

The avatar is driven entirely by server-sent events. Each event type maps to a dedicated component:

### 🎙️ Lip-Sync
Real-time per-frame amplitude analysis of the TTS `AudioClip` drives mouth blendshapes in `LipSync.cs`. No phoneme data required — works with any TTS provider output.

### 😊 Emotion Control
`EmotionController.cs` receives string commands from the server (`neutral`, `happy`, `sad`, `angry`, `surprised`) and blends the corresponding facial blendshapes.

### 🤖 Gestures & Animations
`AnimController.cs` maps named string commands to Animator triggers. `GesturePlayer.cs` handles clip sequencing for multi-step gestures. Supported values are defined in the OVARP server's `config.yaml`.

### 👀 Gaze
`HeadLookAt.cs` accepts look targets from the server — `user` (main camera), `away`, or another agent ID — and smoothly transitions the avatar's head orientation.

### 📐 Spatial Movement
`Controller.cs` handles movement commands relative to the current camera orientation, so `move_closer` / `move_farther` always move toward/away from the user.

---

## ⚙️ Configuration

### WebSocket Protocol

The client sends audio to the server as:

```json
{
  "sender": "web_01",
  "target_device": "all",
  "target_agent": "agent_alpha",
  "command_type": "audio",
  "command": "stt_request",
  "subcommand": { "audio_base64": "<base64-encoded WAV>" }
}
```

The server responds across the following topics:

| Topic | Command | Payload |
|---|---|---|
| `message` | `llm_reply` | `text` |
| `message` | `user_transcript` | `text` |
| `audio` | `tts_chunk` | `audio_base64` — streamed PCM WAV chunks |
| `audio` | `tts_complete` | signals end of TTS stream |
| `action` | `execute_state` | `movement` / `actions` / `avatar` / `emotions` / `looks` |

### GameManager

`GameManager.cs` is a singleton that holds session-scoped state. Configure via Inspector or at runtime:

| Setting | Default | Description |
|---|---|---|
| Agent name | `Nova` | Displayed in the chat UI |
| User name | `User` | Displayed in the chat UI |
| User bubble color | Blue | Chat bubble tint |
| Agent bubble color | Blue | Chat bubble tint |
| Dark mode | Off | UI theme toggle |

---

## 📁 Project Structure

```
<repo>/
│
├── Assets/
│   ├── Scenes/
│   │   └── WebOVARP.unity           # Main OVARP session scene (only scene in the build)
│   │
│   ├── Scripts/
│   │   ├── Controller.cs            # Agent state machine, input, audio playback
│   │   ├── OvarpServerConnector.cs  # OVARP WebSocket client
│   │   ├── GameManager.cs           # Singleton session config (names, colors, dark mode)
│   │   ├── AnimController.cs        # Named animation triggers and thinking state
│   │   ├── EmotionController.cs     # Blendshape-based facial expression control
│   │   ├── HeadLookAt.cs            # Gaze target switching
│   │   ├── GesturePlayer.cs         # Gesture clip sequencing
│   │   ├── LipSync.cs               # Amplitude → mouth blendshape mapping
│   │   ├── BlinkEyes.cs             # Procedural eye blink animation
│   │   ├── RecIndicator.cs          # Recording state blink indicator
│   │   ├── SavWav.cs                # AudioClip → in-memory PCM WAV bytes
│   │   ├── Audio/
│   │   │   ├── IMicrophoneCapture.cs      # Capture abstraction + permission states
│   │   │   ├── MicrophoneCaptureFactory.cs
│   │   │   ├── WebMicrophoneCapture.cs    # Browser capture via OvarpMic.jslib
│   │   │   └── UnityMicrophoneCapture.cs  # Editor / standalone capture
│   │   └── UI/
│   │       ├── Chat.cs              # UI Toolkit chat log
│   │       ├── ScrollSpeed.cs       # Chat scroll behavior
│   │       └── ServerSetupUI.cs     # Pre-session panel for server URL entry
│   │
│   ├── Plugins/WebGL/
│   │   └── OvarpMic.jslib           # getUserMedia + AudioWorklet capture bridge
│   │
│   ├── WebGLTemplates/OVARP/
│   │   └── index.html               # Loading gate, responsive canvas, secure-context warning
│   │
│   ├── Settings/
│   │   ├── OVARP-URP.asset          # Project render pipeline asset
│   │   └── OVARP-Renderer.asset
│   │
│   ├── Avatars/                     # Avatar prefabs and assets
│   └── Prefabs/VirtualAgent.prefab  # Agent rig: Controller, connector, chat, avatar
│
├── Packages/
│   └── manifest.json                # Unity package dependencies
│
└── ProjectSettings/
    └── ProjectSettings.asset        # Web player settings
```

---

## 🎯 Roadmap

- [x] ~~WebSocket connection to OVARP server~~
- [x] ~~Lip-sync from TTS audio~~
- [x] ~~Emotion and gesture command handling~~
- [x] ~~Spatial movement relative to camera~~
- [x] ~~Browser microphone capture~~
- [ ] Push-to-talk button in the chat UI
- [ ] Downsample captured audio to 16 kHz to cut upload size
- [ ] Avatar prefab hot-swap at runtime
- [ ] Voice activity detection (VAD) for natural turn-taking
- [ ] Multi-agent scene support
- [ ] Optional WebXR immersive session

---

## Contributing

Issues and pull requests are welcome.

---

## License

MIT License © 2026 [Anonymous]

See [LICENSE](LICENSE) for the full license text.
