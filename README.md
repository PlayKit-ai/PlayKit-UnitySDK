# Developerworks Unity SDK

[![Version](https://img.shields.io/badge/version-v0.1.7.2--beta-blue.svg)](https://github.com/cnqdztp/PlayKit-UnitySDK)
[![Unity](https://img.shields.io/badge/Unity-2020.3+-brightgreen.svg)](https://unity.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20WebGL-lightgrey.svg)](https://unity.com/)

Official Unity SDK for Developerworks AI platform. Integrate powerful AI capabilities including chat, image generation, audio transcription, and NPC conversations into your Unity games.

**官方 Unity SDK，用于 Developerworks AI 平台。将强大的 AI 功能（包括聊天、图像生成、语音转录和 NPC 对话）集成到您的 Unity 游戏中。**

## 📋 Table of Contents

- [Features](#-features)
- [Requirements](#-requirements)
- [Installation](#-installation)
- [Quick Start](#-quick-start)
- [Core Concepts](#-core-concepts)
- [API Reference](#-api-reference)
  - [SDK Initialization](#sdk-initialization)
  - [Chat Client](#chat-client)
  - [Image Generation Client](#image-generation-client)
  - [Audio Transcription Client](#audio-transcription-client)
  - [NPC Client](#npc-client)
  - [Player Client](#player-client)
  - [Microphone Recorder](#microphone-recorder)
- [Examples](#-examples)
- [Platform Support](#-platform-support)
- [Error Handling](#-error-handling)
- [Best Practices](#-best-practices)
- [FAQ](#-faq)
- [License](#-license)

## ✨ Features

- **🤖 AI Chat**: Text generation and conversation using various LLM models (GPT-4, GPT-4o-mini, etc.)
- **🎨 Image Generation**: AI-powered image creation with customizable parameters
- **🎤 Audio Transcription**: Speech-to-text conversion with Whisper model
- **💬 NPC Conversations**: Simplified interface for game character dialogues with automatic history management
- **👤 Player Management**: User authentication, credit tracking, and player information
- **🎙️ Voice Activity Detection (VAD)**: Smart microphone recording with automatic silence detection
- **🌐 Cross-platform**: Support for Windows, macOS, WebGL, and standalone builds
- **🔒 Secure Authentication**: Token-based authentication with encrypted local storage
- **⚡ Async/Await**: UniTask-based asynchronous operations for smooth gameplay

## 📦 Requirements

- **Unity**: 2020.3 or higher
- **Platform**: Windows, macOS, WebGL, or standalone builds
- **.NET**: .NET Standard 2.0+
- **Dependencies** (included):
  - UniTask (Cysharp.Threading.Tasks)
  - Newtonsoft.Json (JSON.NET)

## 🚀 Installation

### Method 1: Unity Package Manager (Recommended)

1. Open Unity Package Manager: `Window > Package Manager`
2. Click `+` button and select `Add package from git URL...`
3. Enter: `https://github.com/cnqdztp/Developerworks-UnitySDK.git`

### Method 2: Download and Import

1. Download the latest release from [GitHub Releases](https://github.com/cnqdztp/Developerworks-UnitySDK/releases)
2. Extract the contents to `Assets/Developerworks_SDK/`
3. Unity will automatically import the SDK

### Method 3: Git Submodule

```bash
cd your-unity-project
git submodule add https://github.com/cnqdztp/Developerworks-UnitySDK.git Assets/Developerworks_SDK
```

## 🎯 Quick Start

### 1. Get Your Game ID

Register your game at [Developerworks Platform](https://developerworks.agentlandlab.com) and obtain your `Game ID`.

### 2. Add SDK to Scene

1. Locate the `DW_SDK` prefab in `Assets/Developerworks_SDK/Prefab/DW_SDK.prefab`
2. Drag it into your **first scene** (the scene that loads when your game starts)
3. Select the `DW_SDK` GameObject in the hierarchy
4. In the Inspector, fill in your `Game ID`
5. (Optional) Set default model names for chat and image generation

### 3. Initialize the SDK

```csharp
using Developerworks_SDK;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    async void Start()
    {
        // Development mode: Use developer token (COSTS MONEY!)
        string devToken = "dev-your-token-here";
        bool success = await DW_SDK.InitializeAsync(devToken);

        // Production mode: Use player authentication (FREE for players)
        // bool success = await DW_SDK.InitializeAsync();

        if (success)
        {
            Debug.Log("SDK initialized successfully!");
            // Start using SDK features
        }
        else
        {
            Debug.LogError("SDK initialization failed!");
        }
    }
}
```

### 4. Use AI Features

```csharp
// Example: Simple chat conversation
var chatClient = DW_SDK.Factory.CreateChatClient("gpt-4o-mini");

var config = new DW_ChatConfig
{
    Messages = new List<DW_ChatMessage>
    {
        new DW_ChatMessage { Role = "user", Content = "Hello, how are you?" }
    }
};

string response = await chatClient.TextGenerationAsync(config);
Debug.Log($"AI Response: {response}");
```

## 🧠 Core Concepts

### Authentication Modes

The SDK supports two authentication modes:

#### Developer Token (Development)

- **Use case**: Testing and development
- **Cost**: Charges your developer account
- **Warning**: A visual warning is displayed in non-editor builds when using developer tokens
- **Usage**: `DW_SDK.InitializeAsync("dev-your-token-here")`

```csharp
// In development builds, a warning UI will appear for 5 seconds
// warning users that developer tokens have monetary costs
await DW_SDK.InitializeAsync(developerToken);
```

#### Player Token (Production)

- **Use case**: Production builds and released games
- **Cost**: Free (uses player's own credits/account)
- **Authentication**: Automatic login flow with shared token caching
- **Usage**: `DW_SDK.InitializeAsync()` (no parameters)

```csharp
// Players will see a login UI if not authenticated
// Token is cached across games for seamless experience
await DW_SDK.InitializeAsync();
```

### Token Storage

Tokens are securely stored using platform-specific methods:

- **Windows/macOS (Editor)**: Encrypted AES-128 file in `AppData` or `~/Library/Application Support`
- **WebGL**: Browser `localStorage` via JavaScript interop
- **Other Platforms**: Unity `PlayerPrefs` with encryption

Shared tokens allow players to authenticate once and use the same token across multiple Developerworks games.

## 📚 API Reference

### SDK Initialization

#### `DW_SDK.InitializeAsync()`

Initializes the SDK and handles authentication.

```csharp
public static async UniTask<bool> InitializeAsync(string developerToken = null)
```

**Parameters:**
- `developerToken` (optional): Developer token for testing. If null, uses player authentication.

**Returns:**
- `UniTask<bool>`: `true` if initialization succeeded, `false` otherwise

**Example:**
```csharp
// Development mode
bool success = await DW_SDK.InitializeAsync("dev-token-here");

// Production mode
bool success = await DW_SDK.InitializeAsync();
```

**Important Notes:**
- Must be called before using any SDK features
- DW_SDK prefab must exist in the first scene
- Game ID must be configured in the Inspector
- In non-editor builds with developer tokens, a warning UI is displayed

---

### Chat Client

Create an AI chat client for text generation and conversations.

#### `DW_SDK.Factory.CreateChatClient()`

```csharp
public static DW_AIChatClient CreateChatClient(string modelName = null)
```

**Parameters:**
- `modelName` (optional): Model to use (e.g., `"gpt-4o-mini"`, `"gpt-4"`). Uses default if null.

**Returns:**
- `DW_AIChatClient`: Chat client instance

**Example:**
```csharp
var chatClient = DW_SDK.Factory.CreateChatClient("gpt-4o-mini");
```

#### `TextGenerationAsync()`

Generates text response from chat messages.

```csharp
public async UniTask<string> TextGenerationAsync(DW_ChatConfig config)
```

**Parameters:**
- `config`: Chat configuration with messages

**Example:**
```csharp
var config = new DW_ChatConfig
{
    Messages = new List<DW_ChatMessage>
    {
        new DW_ChatMessage
        {
            Role = "system",
            Content = "You are a helpful assistant in a fantasy RPG game."
        },
        new DW_ChatMessage
        {
            Role = "user",
            Content = "What quests are available in this town?"
        }
    }
};

string response = await chatClient.TextGenerationAsync(config);
Debug.Log(response);
```

#### Message Roles

- `"system"`: System instructions (sets AI behavior and context)
- `"user"`: User messages (player input)
- `"assistant"`: AI responses (previous conversation history)

---

### Image Generation Client

Create images using AI image generation models.

#### `DW_SDK.Factory.CreateImageClient()`

```csharp
public static DW_AIImageClient CreateImageClient(string modelName = null)
```

**Parameters:**
- `modelName` (optional): Image model name. Uses default from Inspector if null.

**Returns:**
- `DW_AIImageClient`: Image generation client

**Example:**
```csharp
var imageClient = DW_SDK.Factory.CreateImageClient("dall-e-3");

var config = new DW_ImageConfig
{
    Prompt = "A majestic dragon flying over a medieval castle at sunset",
    Size = "1024x1024",
    Quality = "standard"
};

Texture2D image = await imageClient.GenerateImageAsync(config);

// Display the image
yourRawImage.texture = image;
```

**Supported Sizes:**
- `"256x256"`
- `"512x512"`
- `"1024x1024"`
- `"1792x1024"` (wide)
- `"1024x1792"` (tall)

---

### Audio Transcription Client

Convert speech to text using Whisper model.

#### `DW_SDK.Factory.CreateTranscriptionClient()`

```csharp
public static DW_AudioTranscriptionClient CreateTranscriptionClient(string modelName)
```

**Parameters:**
- `modelName`: Transcription model (typically `"whisper-1"`)

**Returns:**
- `DW_AudioTranscriptionClient`: Transcription client

**Example:**
```csharp
var transcriptionClient = DW_SDK.Factory.CreateTranscriptionClient("whisper-1");

// Transcribe audio file
byte[] audioData = File.ReadAllBytes("path/to/audio.wav");
string transcription = await transcriptionClient.TranscribeAsync(audioData, "audio.wav");

Debug.Log($"Transcription: {transcription}");
```

**Supported Audio Formats:**
- WAV (recommended for best quality)
- MP3
- FLAC
- OGG

**Best Practices:**
- Use 16kHz sample rate for optimal quality
- Keep audio clips under 60 seconds
- Use PCM16 encoding for WAV files

---

### NPC Client

Simplified client for game character conversations with automatic history management.

#### `DW_SDK.Populate.CreateNpc()`

```csharp
public static void CreateNpc(DW_NPCClient recipient, string modelName = null)
```

**Parameters:**
- `recipient`: NPC client instance to populate
- `modelName` (optional): Model name to use

**Example:**
```csharp
public class NPCController : MonoBehaviour
{
    private DW_NPCClient npcClient;

    async void Start()
    {
        // Initialize SDK first
        await DW_SDK.InitializeAsync();

        // Create NPC client
        npcClient = new DW_NPCClient();
        DW_SDK.Populate.CreateNpc(npcClient, "gpt-4o-mini");

        // Set character personality
        npcClient.SetSystemPrompt("You are a wise old wizard who speaks in riddles.");

        // Start conversation
        string response = await npcClient.Talk("Who are you?");
        Debug.Log($"Wizard: {response}");

        // Continue conversation (history is automatic!)
        string nextResponse = await npcClient.Talk("Can you teach me magic?");
        Debug.Log($"Wizard: {nextResponse}");
    }

    void ClearHistory()
    {
        // Clear conversation history when needed
        npcClient.ClearHistory();
    }
}
```

**Key Features:**
- Automatic conversation history management
- Simple `Talk()` interface
- System prompt for character personality
- History clearing for new conversations

---

### Player Client

Manage player information, credits, and account data.

#### `DW_SDK.GetPlayerClient()`

```csharp
public static DW_PlayerClient GetPlayerClient()
```

**Returns:**
- `DW_PlayerClient`: Player client instance

**Example:**
```csharp
var playerClient = DW_SDK.GetPlayerClient();

// Get player information
var playerInfo = await playerClient.GetPlayerInfoAsync();
Debug.Log($"Player: {playerInfo.Username}");
Debug.Log($"Credits: {playerInfo.Credits}");
Debug.Log($"Email: {playerInfo.Email}");

// Check if player has enough credits
if (playerInfo.Credits >= 10)
{
    // Perform AI operation
}
else
{
    Debug.LogWarning("Not enough credits!");
}
```

---

### Microphone Recorder

Record audio with Voice Activity Detection (VAD) for automatic silence detection.

#### `DW_MicrophoneRecorder` Component

Attach this component to a GameObject to enable microphone recording.

**Inspector Fields:**
- `Max Recording Duration`: Maximum recording time in seconds (default: 60)
- `Sample Rate`: Audio sample rate (default: 16000 Hz for Whisper)
- `VAD Threshold`: Voice activity detection sensitivity (default: 0.01)
- `Silence Duration`: Seconds of silence before auto-stop (default: 2.0)

**Example:**
```csharp
using Developerworks_SDK;
using UnityEngine;

public class VoiceRecorder : MonoBehaviour
{
    private DW_MicrophoneRecorder recorder;
    private DW_AudioTranscriptionClient transcriptionClient;

    async void Start()
    {
        await DW_SDK.InitializeAsync();

        // Get or add recorder component
        recorder = GetComponent<DW_MicrophoneRecorder>();
        if (recorder == null)
        {
            recorder = gameObject.AddComponent<DW_MicrophoneRecorder>();
        }

        // Create transcription client
        transcriptionClient = DW_SDK.Factory.CreateTranscriptionClient("whisper-1");
    }

    public void StartRecording()
    {
        recorder.StartRecording();
        Debug.Log("Recording started...");
    }

    public async void StopAndTranscribe()
    {
        byte[] audioData = recorder.StopRecording();

        if (audioData != null && audioData.Length > 0)
        {
            Debug.Log("Transcribing audio...");
            string transcription = await transcriptionClient.TranscribeAsync(
                audioData,
                "recording.wav"
            );
            Debug.Log($"Transcription: {transcription}");
        }
    }

    // Or use VAD to auto-stop on silence
    async void RecordWithVAD()
    {
        recorder.StartRecording();

        // Wait for VAD to detect silence
        while (recorder.IsRecording)
        {
            await UniTask.Yield();
        }

        // Automatically stopped by VAD
        byte[] audioData = recorder.GetRecordedData();
        string transcription = await transcriptionClient.TranscribeAsync(
            audioData,
            "vad_recording.wav"
        );
        Debug.Log($"VAD Transcription: {transcription}");
    }
}
```

**Properties:**
- `IsRecording`: Check if currently recording
- `CurrentDevice`: Get current microphone device name

**Methods:**
- `StartRecording()`: Start recording audio
- `StopRecording()`: Stop and return audio data as byte array
- `GetRecordedData()`: Get audio data without stopping

---

## 💡 Examples

The SDK includes a complete example project in `/Assets/Developerworks_SDK/Example/`.

### Example Scenes

1. **Menu Scene**: SDK initialization and navigation
2. **Chat Scene**: AI chat conversations with UI
3. **Image Scene**: Image generation with prompts
4. **Structured Output Scene**: Complex object generation

### Running Examples

1. Open the Example scene: `Assets/Developerworks_SDK/Example/Scenes/Demo_Menu.unity`
2. Set your Game ID in the DW_SDK prefab Inspector
3. Get a developer token from [Developerworks Platform](https://developerworks.agentlandlab.com)
4. Update `Demo_ExampleGameManager.cs` with your developer token (for testing only!)
5. Press Play

**⚠️ Warning**: Remove developer tokens before releasing your game!

### Example Code Snippets

#### Multi-turn Conversation

```csharp
var chatClient = DW_SDK.Factory.CreateChatClient("gpt-4o-mini");
var messages = new List<DW_ChatMessage>();

// Initial system message
messages.Add(new DW_ChatMessage
{
    Role = "system",
    Content = "You are a game merchant selling potions and weapons."
});

// First user message
messages.Add(new DW_ChatMessage
{
    Role = "user",
    Content = "What do you have for sale?"
});

var config = new DW_ChatConfig { Messages = messages };
string response1 = await chatClient.TextGenerationAsync(config);
Debug.Log($"Merchant: {response1}");

// Add AI response to history
messages.Add(new DW_ChatMessage
{
    Role = "assistant",
    Content = response1
});

// Continue conversation
messages.Add(new DW_ChatMessage
{
    Role = "user",
    Content = "How much for a health potion?"
});

config.Messages = messages;
string response2 = await chatClient.TextGenerationAsync(config);
Debug.Log($"Merchant: {response2}");
```

#### Streaming Image Generation

```csharp
public class ImageGenerator : MonoBehaviour
{
    [SerializeField] private RawImage displayImage;
    [SerializeField] private TMP_InputField promptInput;

    private DW_AIImageClient imageClient;

    async void Start()
    {
        await DW_SDK.InitializeAsync();
        imageClient = DW_SDK.Factory.CreateImageClient("dall-e-3");
    }

    public async void GenerateImage()
    {
        string prompt = promptInput.text;

        var config = new DW_ImageConfig
        {
            Prompt = prompt,
            Size = "1024x1024",
            Quality = "hd"
        };

        Texture2D texture = await imageClient.GenerateImageAsync(config);
        displayImage.texture = texture;
    }
}
```

#### Voice-to-Chat Pipeline

```csharp
public class VoiceChat : MonoBehaviour
{
    private DW_MicrophoneRecorder recorder;
    private DW_AudioTranscriptionClient transcriptionClient;
    private DW_NPCClient npcClient;

    async void Start()
    {
        await DW_SDK.InitializeAsync();

        recorder = gameObject.AddComponent<DW_MicrophoneRecorder>();
        transcriptionClient = DW_SDK.Factory.CreateTranscriptionClient("whisper-1");

        npcClient = new DW_NPCClient();
        DW_SDK.Populate.CreateNpc(npcClient, "gpt-4o-mini");
        npcClient.SetSystemPrompt("You are a friendly guide in a sci-fi adventure.");
    }

    public async void RecordAndChat()
    {
        // Record audio
        recorder.StartRecording();
        Debug.Log("Speak now...");
        await UniTask.Delay(5000); // Record for 5 seconds
        byte[] audioData = recorder.StopRecording();

        // Transcribe
        string userMessage = await transcriptionClient.TranscribeAsync(
            audioData,
            "voice.wav"
        );
        Debug.Log($"You said: {userMessage}");

        // Get AI response
        string npcResponse = await npcClient.Talk(userMessage);
        Debug.Log($"Guide: {npcResponse}");

        // Optional: Convert response to speech with TTS (not included in SDK)
    }
}
```

---

## 🌐 Platform Support

### Windows / macOS (Standalone)

Full support for all features:
- ✅ Chat
- ✅ Image Generation
- ✅ Audio Transcription
- ✅ Microphone Recording
- ✅ Player Authentication
- ✅ Developer Tokens
- ✅ Encrypted Token Storage

### WebGL

Supported features:
- ✅ Chat
- ✅ Image Generation
- ✅ Player Authentication (localStorage)
- ⚠️ Limited microphone support (browser permissions required)
- ❌ Developer tokens (use player authentication only)

**WebGL Notes:**
- Token storage uses browser `localStorage`
- Microphone requires HTTPS and user permission
- Some audio formats may have limited support

### iOS / Android

Partial support (experimental):
- ✅ Chat
- ✅ Image Generation
- ⚠️ Audio transcription (platform-dependent)
- ⚠️ Microphone (requires native permissions)

**Mobile Notes:**
- Test thoroughly on target devices
- Handle platform-specific permission requests
- Consider network performance for image generation

---

## ⚠️ Error Handling

### Common Errors

#### SDK Not Initialized

```csharp
// ❌ Wrong
var client = DW_SDK.Factory.CreateChatClient();

// ✅ Correct
await DW_SDK.InitializeAsync();
var client = DW_SDK.Factory.CreateChatClient();
```

#### Missing Game ID

```
[ERROR] Please fill in gameId from your game.
Get one from https://developerworks.agentlandlab.com
```

**Solution**: Set Game ID in DW_SDK prefab Inspector

#### Authentication Failed

```csharp
bool success = await DW_SDK.InitializeAsync();
if (!success)
{
    Debug.LogError("Authentication failed!");
    // Show login error UI
    // Or retry with different credentials
}
```

#### Network Errors

```csharp
try
{
    string response = await chatClient.TextGenerationAsync(config);
}
catch (Exception ex)
{
    Debug.LogError($"API call failed: {ex.Message}");
    // Show error message to user
    // Retry logic or fallback behavior
}
```

### Checking SDK Status

```csharp
if (DW_SDK.IsReady())
{
    // SDK is initialized and ready
    var client = DW_SDK.Factory.CreateChatClient();
}
else
{
    Debug.LogWarning("SDK not ready. Please initialize first.");
}
```

---

## 🎯 Best Practices

### 1. Initialize Once

Always initialize the SDK in your **first scene** and only **once**:

```csharp
public class GameInitializer : MonoBehaviour
{
    private static bool _isInitialized = false;

    async void Start()
    {
        if (!_isInitialized)
        {
            _isInitialized = await DW_SDK.InitializeAsync();
        }
    }
}
```

### 2. Use Appropriate Authentication

- **Development**: Use developer tokens for quick testing
- **Production**: Always use player authentication
- **Never**: Commit developer tokens to version control

```csharp
#if UNITY_EDITOR
    // Development mode
    await DW_SDK.InitializeAsync("dev-token-here");
#else
    // Production mode
    await DW_SDK.InitializeAsync();
#endif
```

### 3. Manage Conversation History

For chat applications, maintain conversation context:

```csharp
public class ChatManager : MonoBehaviour
{
    private List<DW_ChatMessage> conversationHistory = new List<DW_ChatMessage>();
    private const int MAX_HISTORY = 20; // Prevent context overflow

    void AddMessage(string role, string content)
    {
        conversationHistory.Add(new DW_ChatMessage
        {
            Role = role,
            Content = content
        });

        // Trim history if too long (keep system message)
        if (conversationHistory.Count > MAX_HISTORY)
        {
            var systemMsg = conversationHistory[0];
            conversationHistory = conversationHistory.Skip(1).Take(MAX_HISTORY - 1).ToList();
            conversationHistory.Insert(0, systemMsg);
        }
    }
}
```

### 4. Handle Loading States

Show loading indicators for async operations:

```csharp
public async void GenerateResponse()
{
    loadingIndicator.SetActive(true);

    try
    {
        string response = await chatClient.TextGenerationAsync(config);
        DisplayResponse(response);
    }
    finally
    {
        loadingIndicator.SetActive(false);
    }
}
```

### 5. Optimize Image Generation

```csharp
// Cache generated images
private Dictionary<string, Texture2D> imageCache = new Dictionary<string, Texture2D>();

public async UniTask<Texture2D> GetOrGenerateImage(string prompt)
{
    if (imageCache.TryGetValue(prompt, out Texture2D cached))
    {
        return cached;
    }

    var config = new DW_ImageConfig { Prompt = prompt, Size = "512x512" };
    Texture2D texture = await imageClient.GenerateImageAsync(config);
    imageCache[prompt] = texture;

    return texture;
}
```

### 6. Secure Developer Tokens

Never hardcode tokens in production:

```csharp
// ❌ Bad
await DW_SDK.InitializeAsync("dev-b41a6b70-1234-5678-abcd-374f4b48caed");

// ✅ Good - Use environment variables or ScriptableObjects
[SerializeField] private DeveloperSettings settings;
await DW_SDK.InitializeAsync(settings.DeveloperToken);
```

### 7. Test on Target Platforms

Always test on your deployment platform:

```csharp
void Awake()
{
    #if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("Running on WebGL - some features may be limited");
    #endif
}
```

---

## ❓ FAQ

### Q: How much does it cost to use the SDK?

**A:**
- **Development** (Developer Tokens): You pay for API usage based on your Developerworks account credits
- **Production** (Player Tokens): FREE for you - players use their own accounts/credits

### Q: Can I use the SDK offline?

**A:** No, the SDK requires an internet connection to communicate with Developerworks API servers.

### Q: What models are available?

**A:** Available models depend on your Developerworks account. Common models include:
- Chat: `gpt-4o-mini`, `gpt-4`, `gpt-3.5-turbo`
- Image: `dall-e-3`, `dall-e-2`, `stable-diffusion`
- Transcription: `whisper-1`

Check the [Developerworks Platform](https://developerworks.agentlandlab.com) for the latest model availability.

### Q: How do I clear player authentication?

**A:** Use the Unity menu:
```
Unity Menu > Developerworks SDK > Clear Local Player Token
```

Or programmatically:
```csharp
DW_AuthManager.ClearPlayerToken();
```

### Q: Can I use multiple SDK instances?

**A:** No, DW_SDK uses a singleton pattern. Only one instance should exist in your game, placed in the first scene.

### Q: How do I handle rate limiting?

**A:** Implement exponential backoff retry logic:

```csharp
public async UniTask<string> ChatWithRetry(DW_ChatConfig config, int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await chatClient.TextGenerationAsync(config);
        }
        catch (Exception ex)
        {
            if (i == maxRetries - 1) throw;

            int delay = (int)Math.Pow(2, i) * 1000; // 1s, 2s, 4s
            await UniTask.Delay(delay);
        }
    }
    return null;
}
```

### Q: Is the developer key warning customizable?

**A:** Yes, you can modify the `DeveloperKeyWarning.prefab` in `/Assets/Developerworks_SDK/Resources/` to customize the warning UI. The warning appears automatically in non-editor builds when using developer tokens.

### Q: How secure is token storage?

**A:**
- **Windows/macOS**: AES-128 encrypted files in secure application data directories
- **WebGL**: Browser localStorage (less secure, don't use developer tokens)
- **Other platforms**: PlayerPrefs with encryption

Never store sensitive developer tokens in production builds.

### Q: Can I use my own audio recording solution?

**A:** Yes! The transcription client accepts any `byte[]` audio data:

```csharp
byte[] myAudioData = GetAudioFromCustomRecorder();
string transcription = await transcriptionClient.TranscribeAsync(
    myAudioData,
    "custom_audio.wav"
);
```

---

## 📄 License

This SDK is provided under the [MIT License](LICENSE).

Copyright (c) 2025 Developerworks / AgentLand Lab

---

## 🔗 Links

- **Platform**: [https://developerworks.agentlandlab.com](https://developerworks.agentlandlab.com)
- **GitHub**: [https://github.com/cnqdztp/Developerworks-UnitySDK](https://github.com/cnqdztp/Developerworks-UnitySDK)
- **Support**: [Create an Issue](https://github.com/cnqdztp/Developerworks-UnitySDK/issues)
- **Examples**: See `/Assets/Developerworks_SDK/Example/`

---

## 🙏 Acknowledgments

- **UniTask**: Async/await support for Unity by Cysharp
- **Newtonsoft.Json**: JSON serialization library
- **Unity Technologies**: Game engine and development tools

---

**Made with ❤️ by the Developerworks Team**

*Empower your games with AI*
