# PlayKit SDK - Steam Addon

Steam authentication addon for PlayKit SDK. Provides seamless Steam login integration using Facepunch.Steamworks.

## Requirements

- Unity 2020.3 or later
- PlayKit SDK (com.playkit.sdk) 0.2.0+
- UniTask (com.cysharp.unitask) 2.5.0+
- Steam client running on the target machine

## Dependencies

This addon includes the following libraries:

- **Facepunch.Steamworks v2.4.1** (MIT License) - Already included in `Plugins/Facepunch.Steamworks/`
  - Windows x86 and x64 support
  - macOS support
  - Linux support
  - Steam API native libraries (steam_api.dll, libsteam_api.so, libsteam_api.dylib)

No additional downloads required - all dependencies are bundled with the package.

## Installation

### Via Unity Package Manager (Git URL)

1. Open Window > Package Manager
2. Click '+' > Add package from git URL
3. Enter: `https://github.com/playkit-ai/playkit-unity-steam.git`

### Via manifest.json

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.playkit.sdk.steam": "https://github.com/playkit-ai/playkit-unity-steam.git#v0.1.0"
  }
}
```

## Setup

### 1. Configure PlayKit Settings

1. Go to Tools > PlayKit SDK > Settings
2. Enter your Game ID (from PlayKit dashboard)
3. Make sure your game is configured as a Steam channel (steam_release, steam_demo, or steam_playtest)

### 2. Configure Steam App ID

In your scene, add the `PlayKit_SteamAuthManager` component:

```csharp
// Or set it via code
var steamAuth = gameObject.AddComponent<PlayKit_SteamAuthManager>();
steamAuth.SetSteamAppId(YOUR_STEAM_APP_ID);
```

### 3. Authenticate

```csharp
using PlayKit_SDK.Steam;
using Cysharp.Threading.Tasks;

public class GameManager : MonoBehaviour
{
    private PlayKit_SteamAuthManager _steamAuth;

    async void Start()
    {
        _steamAuth = GetComponent<PlayKit_SteamAuthManager>();

        _steamAuth.OnAuthSuccess += (result) => {
            Debug.Log($"Logged in! User: {result.userId}, Steam: {result.steamId}");
        };

        _steamAuth.OnAuthError += (error) => {
            Debug.LogError($"Auth failed: {error}");
        };

        bool success = await _steamAuth.AuthenticateAsync();
    }
}
```

## API Reference

### PlayKit_SteamAuthManager

Main authentication manager.

**Properties:**
- `IsAuthenticated` - Whether the user is currently authenticated
- `SteamId` - The user's Steam ID (64-bit format)
- `SteamAppId` - The Steam App ID being used
- `LastAuthResult` - The last authentication result

**Methods:**
- `SetSteamAppId(uint appId)` - Set the Steam App ID at runtime
- `AuthenticateAsync()` - Initialize Steam and authenticate with PlayKit
- `Logout()` - Clear authentication state

**Events:**
- `OnAuthSuccess` - Fired when authentication succeeds
- `OnAuthError` - Fired when authentication fails

### PlayKit_SteamService

Low-level Steamworks wrapper.

**Properties:**
- `IsInitialized` - Whether Steam is initialized
- `SteamId` - Current user's Steam ID
- `SteamName` - Current user's Steam name

**Methods:**
- `InitializeAsync(uint appId)` - Initialize Steam client
- `GetSessionTicketAsync()` - Get a session ticket for auth
- `Shutdown()` - Shutdown Steam client

## Troubleshooting

### "Failed to initialize Steam"

- Make sure Steam is running
- Check that your Steam App ID is correct
- Verify you own the game on Steam (for testing)

### "Steam ticket verification failed"

- Verify your Steam Web API Key is configured in PlayKit dashboard
- Check that the App ID matches your channel configuration
- Ensure the API key has access to the App ID

## License

MIT License - See LICENSE file for details.
