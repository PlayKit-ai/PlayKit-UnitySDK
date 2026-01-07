# PlayKit SDK for Unity

AI-powered game development SDK for Unity. Build intelligent NPCs, dynamic conversations, AI-generated images, voice interactions, and seamless player monetization.

## Features

- **AI NPCs**: Create intelligent non-player characters with conversation, voice, and action capabilities
- **AI Chat**: Integrate AI-powered chat interfaces with streaming responses
- **AI Image Generation**: Generate in-game images and assets using AI models
- **Voice & Transcription**: Real-time speech-to-text and text-to-speech for voice interactions
- **Monetization**: Built-in recharge system with support for multiple platforms (Standalone, Steam, iOS, Android)
- **Cross-Platform Auth**: Device-based authentication and Steam integration via addons
- **Localization**: Multi-language support (English, Chinese, Japanese, Korean)

## Requirements

- **Unity**: 2020.3 or later
- **Scripting Backend**: IL2CPP or Mono (Both supported)
- **.NET**: .NET Standard 2.1
- **Dependencies**:
  - UniTask (com.cysharp.unitask) 2.5.0+
  - Newtonsoft.Json 3.2.1+ (included)
  - Unity UI package (com.unity.ugui)

## Installation

### Via Unity Package Manager (Recommended)

1. Open **Window > Package Manager**
2. Click **+** > **Add package from git URL**
3. Enter: `https://github.com/playkit-ai/playkit-unity-sdk.git`

### Via manifest.json

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.playkit.sdk": "https://github.com/playkit-ai/playkit-unity-sdk.git#v0.2.0",
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"
  }
}
```

### Manual Installation

1. Download the latest release from [Releases](https://github.com/playkit-ai/playkit-unity-sdk/releases)
2. Extract to `Assets/PlayKit/` in your project
3. Install UniTask dependency

## Quick Start

### 1. Get API Credentials

1. Sign up at [PlayKit Dashboard](https://dashboard.playkit.ai)
2. Create a new game
3. Copy your **Game ID** and **API Key**

### 2. Configure SDK

1. In Unity, go to **Tools > PlayKit SDK > Settings**
2. Enter your **Game ID**
3. Configure distribution channel (auto-detected from game settings)
4. Optionally enable features:
   - Auto Recharge Prompt
   - Custom Recharge Modal
   - Debug Logging

### 3. Initialize SDK

```csharp
using PlayKit_SDK;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    async void Start()
    {
        // Initialize SDK (automatically uses PlayKitSettings)
        bool success = await PlayKit_SDK.InitializeAsync();

        if (success)
        {
            Debug.Log("PlayKit SDK initialized successfully!");
        }
    }
}
```

### 4. Use AI Features

#### AI Chat

```csharp
using PlayKit_SDK;
using Cysharp.Threading.Tasks;

var chatClient = PlayKit_SDK.GetChatClient();

// Send message with streaming response
await chatClient.SendMessageAsync(
    messages: new[] {
        new { role = "user", content = "Hello, AI!" }
    },
    onChunk: (chunk) => {
        Debug.Log($"Received: {chunk}");
    },
    onComplete: (fullResponse) => {
        Debug.Log($"Complete: {fullResponse}");
    }
);
```

#### AI NPC

```csharp
using PlayKit_SDK;

var npcClient = PlayKit_SDK.GetNPCClient();

// Initialize NPC
await npcClient.InitializeAsync("npc_character_id");

// Send message to NPC
var response = await npcClient.SendMessageAsync("What's your name?");
Debug.Log($"NPC: {response.Content}");

// Use NPC voice module
var voiceModule = npcClient.GetVoiceModule();
voiceModule.StartListening(); // Start recording
var transcription = await voiceModule.StopListeningAsync(); // Get text
```

#### AI Image Generation

```csharp
using PlayKit_SDK;

var imageClient = PlayKit_SDK.GetImageClient();

// Generate image
var result = await imageClient.GenerateImageAsync(
    prompt: "A fantasy castle at sunset",
    model: "flux-schnell"
);

if (result.Success)
{
    Texture2D texture = result.Texture;
    // Apply to material or UI
}
```

### 5. Handle Player Balance & Recharge

```csharp
using PlayKit_SDK;

var playerClient = PlayKit_SDK.GetPlayerClient();

// Get player balance
float balance = playerClient.GetDisplayBalance();
Debug.Log($"Player balance: {balance} USD");

// Listen for low balance
playerClient.OnBalanceLow += (balance) => {
    Debug.Log($"Balance is low: {balance}");
    // SDK automatically shows recharge UI if enableAutoRecharge = true
};

// Manually trigger recharge
var rechargeManager = PlayKit_SDK.GetRechargeManager();
await rechargeManager.RechargeAsync();
```

## Key Components

### Core Services

| Service | Purpose | Access Method |
|---------|---------|---------------|
| `PlayKit_SDK` | Main SDK entry point | Static class |
| `PlayKit_PlayerClient` | Player info & balance | `PlayKit_SDK.GetPlayerClient()` |
| `PlayKit_ChatClient` | AI chat conversations | `PlayKit_SDK.GetChatClient()` |
| `PlayKit_NPCClient` | AI NPC interactions | `PlayKit_SDK.GetNPCClient()` |
| `PlayKit_ImageClient` | AI image generation | `PlayKit_SDK.GetImageClient()` |
| `PlayKit_TranscribeClient` | Speech-to-text | `PlayKit_SDK.GetTranscribeClient()` |
| `PlayKit_RechargeManager` | Monetization & IAP | `PlayKit_SDK.GetRechargeManager()` |

### Settings (PlayKitSettings)

Located at `Resources/PlayKitSettings.asset` or configured via **Tools > PlayKit SDK > Settings**:

| Setting | Description |
|---------|-------------|
| `gameId` | Your game's unique identifier |
| `environment` | Production/Staging/Development |
| `channelType` | Distribution channel (standalone, steam_*, ios, android) |
| `enableAutoRecharge` | Auto-prompt when balance is low |
| `showRechargeModal` | Show confirmation modal before recharge |
| `enableDebugLogs` | Enable detailed logging |

## Monetization & Recharge System

PlayKit provides a complete monetization solution with support for multiple platforms:

### Platform Support

| Platform | Channel Type | Purchase Flow |
|----------|--------------|---------------|
| Standalone (PC/Mac) | `standalone` | Opens browser recharge page |
| Steam | `steam_*` | In-game purchase via Steam Overlay |
| iOS | `ios` | In-app purchase via App Store |
| Android | `android` | In-app purchase via Google Play |

### Auto Recharge

The SDK automatically detects low balance and prompts users to recharge:

```csharp
// Enable in PlayKitSettings:
enableAutoRecharge = true;
showRechargeModal = true; // Optional: Show confirmation modal first
```

### Custom Recharge UI

You can implement your own recharge UI using events:

```csharp
using PlayKit_SDK;
using PlayKit_SDK.Recharge;

var playerClient = PlayKit_SDK.GetPlayerClient();
var rechargeManager = PlayKit_SDK.GetRechargeManager();

// Disable auto UI
playerClient.AutoPromptRecharge = false;

// Listen for low balance
playerClient.OnBalanceLow += async (balance) => {
    // Show your custom UI
    bool userWantsRecharge = await ShowCustomRechargeUI();

    if (userWantsRecharge)
    {
        // Get product list
        var products = await rechargeManager.GetAvailableProductsAsync();

        // Show products to user
        string selectedSku = await ShowProductSelection(products.Products);

        // Purchase (Standalone: no SKU, others: pass SKU)
        bool isStandalone = PlayKitSettings.Instance.ChannelType == "standalone";
        await rechargeManager.RechargeAsync(isStandalone ? null : selectedSku);
    }
};
```

See `Example/Scripts/Example_CustomRechargeFlow.cs` for a complete implementation.

### Recharge Events

| Event | When Fired | Parameters |
|-------|------------|------------|
| `OnBalanceLow` | Balance below threshold | `float balance` |
| `OnInsufficientCredits` | API call failed (no credits) | `PlayKitException exception` |
| `OnRechargeInitiated` | Recharge started | None |
| `OnRechargeCompleted` | Purchase successful | `RechargeResult result` |
| `OnRechargeCancelled` | User cancelled | None |

For detailed recharge documentation, see [Payment Guide](https://docs.playkit.ai/unity/payment).

## Authentication

### Device Auth (Default)

Automatic device-based authentication. No additional setup required.

```csharp
// Device auth happens automatically on SDK initialization
bool success = await PlayKit_SDK.InitializeAsync();
```

### Steam Auth (Addon)

For Steam games, install the Steam addon:

```bash
# Via Unity Package Manager
https://github.com/playkit-ai/playkit-unity-steam.git
```

See `SDKs/Unity.SteamAddon/README.md` for setup instructions.

## Localization

The SDK supports multiple languages with automatic detection:

| Language | Code | Status |
|----------|------|--------|
| English | en-US | Full support |
| Chinese (Simplified) | zh-CN | Full support |
| Chinese (Traditional) | zh-TW | Full support |
| Japanese | ja-JP | Full support |
| Korean | ko-KR | Full support |

Language is auto-detected from `Application.systemLanguage`. Override via:

```csharp
// Set language for specific components
PlayKit_RechargeModalManager.Instance.ShowModalAsync(balance, language: "zh-CN");
```

## Examples

The SDK includes comprehensive examples:

| Example | Location | Description |
|---------|----------|-------------|
| Chat Demo | `Example/Scenes/ChatScene.unity` | AI chat with streaming responses |
| NPC Demo | `Example/Scenes/NPCScene.unity` | Interactive AI NPC with voice |
| Image Gen Demo | `Example/Scenes/ImageScene.unity` | AI image generation |
| Custom Recharge | `Example/Scripts/Example_CustomRechargeFlow.cs` | Custom recharge UI implementation |

## Advanced Topics

### Addon System

PlayKit supports addons for extending functionality:

```csharp
using PlayKit_SDK;

public interface IPlayKitAddon
{
    string AddonName { get; }
    void Initialize();
    void Shutdown();
}

// Register addon
PlayKit_SDK.RegisterAddon(new MySteamAddon());
```

### Custom AI Providers

Implement custom AI model providers:

```csharp
using PlayKit_SDK.AI;

public class MyCustomAIProvider : IAIProvider
{
    // Implement interface methods
}

// Register provider
PlayKit_AIProviderRegistry.RegisterProvider(new MyCustomAIProvider());
```

### Error Handling

All async methods return results with error handling:

```csharp
var result = await imageClient.GenerateImageAsync(prompt);

if (result.Success)
{
    // Use result.Texture
}
else
{
    Debug.LogError($"Error: {result.Error}");
    // Handle error codes
    if (result.ErrorCode == ErrorCode.InsufficientCredits)
    {
        // Prompt recharge
    }
}
```

## Platform-Specific Notes

### Windows/Mac/Linux (Standalone)

- Uses browser-based recharge flow
- Device authentication is default
- Steam addon available for Steam builds

### Steam

- **IMPORTANT**: Must use Steam overlay for purchases (not browser)
- Requires Steam addon: `com.playkit.sdk.steam`
- Configure Steam App ID in `PlayKit_SteamAuthManager`

### iOS

- Requires iOS IAP setup in App Store Connect
- Configure products in PlayKit Dashboard
- Set channel type to `ios`

### Android

- Requires Google Play Billing setup
- Configure products in Google Play Console and PlayKit Dashboard
- Set channel type to `android`

### WebGL

- Limited support (some async features unavailable)
- Device auth may have limitations
- Consider custom authentication

## Troubleshooting

### SDK Initialization Failed

**Check:**
- Valid Game ID in PlayKitSettings
- Internet connection available
- API server reachable (check firewall)

**Debug:**
```csharp
PlayKitSettings.Instance.EnableDebugLogs = true;
```

### Balance Always Shows 0.00

**Check:**
- Player authenticated successfully
- `GetPlayerInfoAsync()` called at least once
- Valid player token

### Recharge Not Working

**For Standalone:**
- Browser must be able to open
- Check channel type is `standalone`

**For Steam:**
- Steam must be running
- Steam overlay must be enabled
- Valid Steam App ID configured
- Steam addon installed

**For iOS/Android:**
- IAP products configured in platform console
- Products synced to PlayKit Dashboard
- Platform billing library integrated

### Compilation Errors

**Missing UniTask:**
```bash
# Install via Package Manager
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
```

**Missing Newtonsoft.Json:**
- Already included in SDK at `ThirdParty/com.unity.nuget.newtonsoft-json@3.2.1`

## API Documentation

Full API documentation available at: [https://docs.playkit.ai/unity](https://docs.playkit.ai/unity)

### Core Documentation

- [Getting Started](https://docs.playkit.ai/unity/getting-started)
- [Authentication](https://docs.playkit.ai/unity/authentication)
- [AI Chat](https://docs.playkit.ai/unity/chat)
- [AI NPCs](https://docs.playkit.ai/unity/npc)
- [Image Generation](https://docs.playkit.ai/unity/image)
- [Voice & Transcription](https://docs.playkit.ai/unity/voice)
- [Payment & Monetization](https://docs.playkit.ai/unity/payment) **← New comprehensive guide**

### Advanced Documentation

- [Addon Development](https://docs.playkit.ai/unity/addons)
- [Steam Integration](https://docs.playkit.ai/unity/steam)
- [Error Handling](https://docs.playkit.ai/unity/error-handling)
- [Best Practices](https://docs.playkit.ai/unity/best-practices)

## Support

- **Documentation**: [https://docs.playkit.ai](https://docs.playkit.ai)
- **Discord**: [https://discord.gg/playkit](https://discord.gg/playkit)
- **Email**: support@playkit.ai
- **GitHub Issues**: [https://github.com/playkit-ai/playkit-unity-sdk/issues](https://github.com/playkit-ai/playkit-unity-sdk/issues)

## License

MIT License - See [LICENSE](LICENSE) file for details.

## Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history and updates.

---

Made with ❤️ by the PlayKit team
