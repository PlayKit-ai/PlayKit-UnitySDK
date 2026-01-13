# PlayKit SDK - Recharge Modal System

## Overview

The Recharge Modal System provides a confirmation UI before opening the recharge portal in the browser. This improves user experience by:

- Showing current balance before recharging
- Giving users a chance to cancel if they accidentally triggered recharge
- Supporting multiple languages (English, Chinese Simplified/Traditional, Japanese, Korean)
- Being fully customizable via prefab override

## Quick Start

### 1. Enable/Disable Recharge Modal

Open **Tools > PlayKit SDK > Settings** and check the **Recharge Configuration** section:

- ✅ **Show Recharge Modal**: Enable to show confirmation modal before opening browser
- ⬜ **Show Recharge Modal**: Disable to open browser directly (default behavior)

### 2. Using the Default Modal

The SDK includes a default modal implementation. To use it:

1. No action required - the default modal works out of the box
2. The modal will automatically show when:
   - User triggers recharge (e.g., via auto-prompt when balance is low)
   - `ShowRechargeModal` is enabled in PlayKitSettings

**Default modal path:** `Resources/PlayKit/UI/RechargeModal.prefab`

### 3. Creating a Custom Modal Prefab

If you want to customize the UI, create your own prefab:

#### Step 1: Create UI Prefab

1. Create a new Canvas in your scene
2. Add UI elements (see required elements below)
3. Attach `PlayKit_RechargeModalController` component to the root GameObject
4. Assign UI references in the Inspector

#### Step 2: Required UI Elements

Your prefab MUST have these UI elements (assign in Inspector):

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `modalRoot` | GameObject | ✅ | Root object to show/hide |
| `titleText` | Text | ✅ | Modal title |
| `messageText` | Text | ✅ | Main message text |
| `balanceLabelText` | Text | ✅ | "Current Balance:" label |
| `balanceValueText` | Text | ✅ | Balance value display |
| `rechargeButton` | Button | ✅ | Recharge button |
| `cancelButton` | Button | ✅ | Cancel button |
| `rechargeButtonText` | Text | ⬜ | Optional: Button text (auto-localized) |
| `cancelButtonText` | Text | ⬜ | Optional: Button text (auto-localized) |

#### Step 3: Save to Resources Folder

1. Create folder: `Assets/Resources/PlayKit/UI/`
2. Save your prefab as: `RechargeModal.prefab`
3. OR: Save anywhere in Resources and set custom path in PlayKitSettings

#### Step 4: Set Custom Path (Optional)

If you saved your prefab to a different location:

1. Open **Tools > PlayKit SDK > Settings**
2. Find **Custom Recharge Modal Prefab Path**
3. Enter path relative to Resources folder (e.g., `MyGame/UI/CustomRechargeModal`)

## Advanced Usage

### Programmatic Control

```csharp
using PlayKit_SDK;

// Get the RechargeManager
var rechargeManager = PlayKit_SDK.GetRechargeManager();

// Enable/disable modal at runtime
rechargeManager.SetShowModal(true);  // Show modal
rechargeManager.SetShowModal(false); // Skip modal, open browser directly

// Trigger recharge manually
await rechargeManager.RechargeAsync();
```

### Custom Recharge Logic

You can completely override the recharge behavior:

#### Option 1: Subscribe to Events

```csharp
using PlayKit_SDK;

var rechargeManager = PlayKit_SDK.GetRechargeManager();

// Listen to recharge events
rechargeManager.OnRechargeInitiated += () =>
{
    Debug.Log("Recharge started");
    // Your custom logic here
};

rechargeManager.OnRechargeCancelled += () =>
{
    Debug.Log("User cancelled recharge");
    // Your custom logic here
};

rechargeManager.OnRechargeCompleted += (result) =>
{
    Debug.Log($"Recharge completed: {result.Data}");
    // Your custom logic here
};
```

#### Option 2: Implement Custom IRechargeProvider

```csharp
using PlayKit_SDK.Recharge;
using Cysharp.Threading.Tasks;

public class MyCustomRechargeProvider : IRechargeProvider
{
    public string RechargeMethod => "custom";
    public bool IsAvailable => true;

    public event Action OnRechargeInitiated;
    public event Action<RechargeResult> OnRechargeCompleted;
    public event Action OnRechargeCancelled;

    public void Initialize(string baseUrl, string gameId, Func<string> getPlayerToken)
    {
        // Your initialization logic
    }

    public async UniTask<RechargeResult> RechargeAsync(string sku = null)
    {
        // Your custom recharge logic
        // For example: Open your own in-game store UI

        OnRechargeInitiated?.Invoke();

        // Show your custom UI
        bool success = await ShowMyCustomStoreUI();

        if (success)
        {
            return new RechargeResult { Initiated = true };
        }
        else
        {
            OnRechargeCancelled?.Invoke();
            return new RechargeResult { Initiated = false, Error = "Cancelled" };
        }
    }

    public async UniTask<ProductListResult> GetAvailableProductsAsync()
    {
        // Return your custom product list
        return new ProductListResult { Success = true, Products = myProducts };
    }
}

// Register your custom provider
var rechargeManager = PlayKit_SDK.GetRechargeManager();
rechargeManager.RegisterProvider(new MyCustomRechargeProvider());
```

#### Option 3: Disable Auto-Recharge and Handle Manually

```csharp
using PlayKit_SDK;

// In PlayKitSettings:
// - Uncheck "Enable Auto Recharge"

// Then handle low balance manually:
var playerClient = PlayKit_SDK.GetPlayerClient();
playerClient.OnBalanceLow += (balance) =>
{
    Debug.Log($"Balance low: {balance}");
    // Show your own UI or logic
    ShowMyCustomRechargeUI();
};
```

## Multi-Language Support

The modal automatically displays text in the user's system language:

| Language | Code | Title | Message |
|----------|------|-------|---------|
| English | en-US | "Recharge Confirmation" | "Your balance is low. Would you like to recharge?" |
| Chinese (Simplified) | zh-CN | "充值提示" | "您的余额不足，是否前往充值？" |
| Chinese (Traditional) | zh-TW | "儲值提示" | "您的餘額不足，是否前往儲值？" |
| Japanese | ja-JP | "チャージ確認" | "残高が不足しています。チャージしますか？" |
| Korean | ko-KR | "충전 확인" | "잔액이 부족합니다. 충전하시겠습니까？" |

Language is detected automatically from `Application.systemLanguage`.

## Steam Channel Safety

**CRITICAL:** The recharge modal system respects Steam channel restrictions.

For Steam channels (`channelType` starts with "steam_"):
- Browser recharge is **automatically blocked**
- Modal will **never show** for Steam channels
- Steam games **must use Steam overlay** for purchases (via `SteamRechargeProvider`)

This is a **security requirement** to prevent Steam games from opening third-party payment pages.

## Troubleshooting

### Modal Doesn't Show

**Check:**
1. ✅ Is `ShowRechargeModal` enabled in PlayKitSettings?
2. ✅ Is the prefab located at `Resources/PlayKit/UI/RechargeModal.prefab`?
3. ✅ Does the prefab have `PlayKit_RechargeModalController` component?
4. ✅ Are all required UI references assigned in the Inspector?
5. ✅ Is the channel type NOT "steam_*"? (Steam blocks browser recharge)

**Debug Logs:**
```
[PlayKit_RechargeModalManager] Loading modal from: PlayKit/UI/RechargeModal
[PlayKit_RechargeModalManager] Modal loaded successfully
```

### Modal Shows Wrong Language

The modal uses `Application.systemLanguage` to detect language. If you need to override:

```csharp
using PlayKit_SDK.UI;

// Show modal with specific language
var manager = PlayKit_RechargeModalManager.Instance;
await manager.ShowModalAsync(balance: 10.50f, language: "zh-CN");
```

### Prefab Not Found Error

```
[PlayKit_RechargeModalManager] Failed to load modal prefab at Resources/PlayKit/UI/RechargeModal.prefab
```

**Solution:** Create the prefab or set custom path in PlayKitSettings.

If you don't want to use the modal at all, simply disable `ShowRechargeModal` in PlayKitSettings.

### Balance Shows 0.00

The modal gets balance from `PlayKit_PlayerClient.GetDisplayBalance()`.

**Check:**
1. ✅ Has player authenticated successfully?
2. ✅ Has `GetPlayerInfoAsync()` been called at least once?
3. ✅ Is the player token valid?

**Debug:**
```csharp
var playerClient = PlayKit_SDK.GetPlayerClient();
float balance = playerClient.GetDisplayBalance();
Debug.Log($"Current balance: {balance}");
```

## Configuration Reference

### PlayKitSettings Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `channelType` | string | "standalone" | Distribution channel (e.g., "steam_release", "ios", "android") |
| `enableAutoRecharge` | bool | true | Enable auto-prompt when balance is low |
| `showRechargeModal` | bool | true | Show confirmation modal before opening browser |
| `customRechargeModalPrefabPath` | string | "" | Custom prefab path (relative to Resources folder) |

### BrowserRechargeProvider Properties

| Property | Type | Description |
|----------|------|-------------|
| `ShowModal` | bool | Whether to show modal (controlled by PlayKitSettings) |
| `RechargePortalUrl` | string | Custom recharge portal URL (optional) |

## Examples

### Example 1: Basic Usage

```csharp
using PlayKit_SDK;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        // Modal is automatically configured via PlayKitSettings
        // No additional code needed!
    }
}
```

### Example 2: Manual Recharge Trigger

```csharp
using PlayKit_SDK;
using UnityEngine;
using UnityEngine.UI;

public class RechargeButton : MonoBehaviour
{
    public Button rechargeButton;

    void Start()
    {
        rechargeButton.onClick.AddListener(OnRechargeClicked);
    }

    async void OnRechargeClicked()
    {
        var rechargeManager = PlayKit_SDK.GetRechargeManager();
        var result = await rechargeManager.RechargeAsync();

        if (result.Initiated)
        {
            Debug.Log("Recharge initiated successfully");
        }
        else
        {
            Debug.Log($"Recharge failed: {result.Error}");
        }
    }
}
```

### Example 3: Disable Modal for Specific Scenario

```csharp
using PlayKit_SDK;

// Disable modal for this specific recharge
var rechargeManager = PlayKit_SDK.GetRechargeManager();
rechargeManager.SetShowModal(false);
await rechargeManager.RechargeAsync();

// Re-enable modal for future recharges
rechargeManager.SetShowModal(true);
```

## Technical Details

### Architecture

```
PlayKit_SDK
    ↓ initializes
PlayKit_RechargeManager
    ↓ creates
BrowserRechargeProvider
    ↓ uses (if ShowModal = true)
PlayKit_RechargeModalManager (Singleton)
    ↓ loads prefab from Resources
PlayKit_RechargeModalController (on prefab)
    ↓ displays UI
User clicks Recharge/Cancel
```

### Files

| File | Purpose |
|------|---------|
| `PlayKit_RechargeModalController.cs` | UI controller for the modal prefab |
| `PlayKit_RechargeModalManager.cs` | Singleton manager for loading/showing modal |
| `BrowserRechargeProvider.cs` | Browser recharge provider with modal integration |
| `PlayKit_RechargeManager.cs` | Main recharge manager |
| `PlayKitSettings.cs` | Settings ScriptableObject |

### Dependencies

- **UniTask**: For async/await support
- **Unity UI**: For buttons and text components (built-in with Unity)

## Related Documentation

- [PlayKit SDK Documentation](https://docs.playkit.ai)
- [Recharge System Guide](https://docs.playkit.ai/unity/recharge)
- [Steam Integration Guide](https://docs.playkit.ai/unity/steam)
- [Addon Management](https://docs.playkit.ai/unity/addons)

## Support

If you encounter issues:

1. Check the troubleshooting section above
2. Review Unity Console logs for error messages
3. Join our Discord: https://discord.gg/playkit
4. Email support: support@playkit.ai
