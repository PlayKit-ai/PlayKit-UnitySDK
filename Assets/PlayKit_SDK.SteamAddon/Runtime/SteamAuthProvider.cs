using System;
using Cysharp.Threading.Tasks;
using PlayKit_SDK.Auth;
using UnityEngine;

namespace PlayKit_SDK.Steam
{
    /// <summary>
    /// Steam authentication provider implementation.
    /// Wraps PlayKit_SteamAuthManager and implements IAuthProvider interface.
    /// </summary>
    public class SteamAuthProvider : IAuthProvider
    {
        private PlayKit_SteamAuthManager _steamAuthManager;

        public string ProviderId => "steam";
        public string DisplayName => "Steam Authentication";

        public bool IsAvailable
        {
            get
            {
                // In Unity Editor, always return true - let initialization happen in AuthenticateAsync
                // SteamClient.IsValid only returns true AFTER initialization, so checking it here
                // would always fail before authentication begins
#if UNITY_EDITOR
                return true;
#else
                // In builds, check if Steam client is running
                try
                {
                    return Steamworks.SteamClient.IsValid;
                }
                catch
                {
                    return false;
                }
#endif
            }
        }

        public event Action<string> OnStatusChanged;

        private bool _isConfigured = false;

        /// <summary>
        /// Constructor - gets or creates SteamAuthManager
        /// </summary>
        public SteamAuthProvider()
        {
            // Try to find existing SteamAuthManager in scene
            _steamAuthManager = UnityEngine.Object.FindObjectOfType<PlayKit_SteamAuthManager>();

            if (_steamAuthManager == null)
            {
                // Create a new GameObject with SteamAuthManager
                var go = new GameObject("PlayKit_SteamAuthManager");
                _steamAuthManager = go.AddComponent<PlayKit_SteamAuthManager>();
                UnityEngine.Object.DontDestroyOnLoad(go);
                Debug.Log("[SteamAuthProvider] Created new SteamAuthManager");
            }

            // Don't configure Steam App ID here - do it in AuthenticateAsync
            // to avoid async initialization issues
        }

        /// <summary>
        /// Configure Steam App ID by fetching from server
        /// </summary>
        private async UniTask ConfigureSteamAppIdAsync()
        {
            try
            {
                var settings = PlayKitSettings.Instance;
                if (settings == null)
                {
                    Debug.LogWarning("[SteamAuthProvider] PlayKitSettings not found");
                    return;
                }

                string gameId = settings.GameId;
                if (string.IsNullOrEmpty(gameId))
                {
                    Debug.LogWarning("[SteamAuthProvider] Game ID not configured");
                    return;
                }

                // Fetch Steam App ID from server
                var baseUrl = settings.BaseUrl;
                string developerToken = settings.DeveloperToken;

                if (string.IsNullOrEmpty(developerToken))
                {
                    Debug.LogWarning("[SteamAuthProvider] No developer token available for fetching Steam App ID");
                    return;
                }

                string endpoint = $"{baseUrl}/api/external/steam-config/{gameId}";

                using (var request = UnityEngine.Networking.UnityWebRequest.Get(endpoint))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {developerToken}");
                    await request.SendWebRequest();

                    if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        var response = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(request.downloadHandler.text);
                        if (response?.ContainsKey("steamConfig") == true)
                        {
                            var config = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(response["steamConfig"].ToString());
                            string appIdStr = config?.ContainsKey("releaseAppId") == true
                                ? config["releaseAppId"]?.ToString()
                                : null;

                            if (!string.IsNullOrEmpty(appIdStr) && uint.TryParse(appIdStr, out uint appId))
                            {
                                _steamAuthManager.SetSteamAppId(appId);
                                Debug.Log($"[SteamAuthProvider] Configured Steam App ID: {appId}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SteamAuthProvider] Failed to configure Steam App ID: {ex.Message}");
            }
        }

        public async UniTask<AuthResult> AuthenticateAsync()
        {
            var result = new AuthResult
            {
                ProviderId = ProviderId,
                Success = false
            };

            try
            {
                OnStatusChanged?.Invoke("Configuring Steam...");

                // Configure Steam App ID first (if not already done)
                if (!_isConfigured)
                {
                    await ConfigureSteamAppIdAsync();
                    _isConfigured = true;
                }

                OnStatusChanged?.Invoke("Initializing Steam...");

                // Call the existing SteamAuthManager authentication
                bool success = await _steamAuthManager.AuthenticateAsync();

                if (!success)
                {
                    result.Error = "Steam authentication failed";
                    OnStatusChanged?.Invoke("Authentication failed");
                    return result;
                }

                // Get the auth result from SteamAuthManager
                var steamResult = _steamAuthManager.LastAuthResult;
                if (steamResult == null || !steamResult.success)
                {
                    result.Error = steamResult?.error ?? steamResult?.message ?? "Unknown error";
                    return result;
                }

                // Convert SteamAuthResult to standardized AuthResult
                result.Success = true;
                result.PlayerToken = steamResult.playerToken;
                result.UserId = steamResult.userId;
                result.PlatformUserId = steamResult.steamId;

                // Parse expiry if provided
                if (!string.IsNullOrEmpty(steamResult.expiresAt))
                {
                    if (DateTime.TryParse(steamResult.expiresAt, out DateTime expiresAt))
                    {
                        result.ExpiresAt = expiresAt;
                        result.ExpiresIn = (int)(expiresAt - DateTime.UtcNow).TotalSeconds;
                    }
                }

                // Add Steam-specific metadata
                result.Metadata["steamId"] = steamResult.steamId;
                result.Metadata["userCreated"] = steamResult.userCreated;
                result.Metadata["tokenName"] = steamResult.tokenName;

                OnStatusChanged?.Invoke("Authentication successful");
                Debug.Log($"[SteamAuthProvider] Authentication successful for Steam ID: {steamResult.steamId}");

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamAuthProvider] Authentication exception: {ex.Message}");
                result.Error = ex.Message;
                OnStatusChanged?.Invoke($"Error: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Initialize Steamworks without performing authentication.
        /// Used when a developer token is already configured.
        /// Allows IAP to function by providing access to Steam ID.
        /// </summary>
        public async UniTask<bool> InitializeWithoutAuthAsync()
        {
            Debug.Log("[SteamAuthProvider] Initializing for developer mode (no authentication)");

            try
            {
                OnStatusChanged?.Invoke("Configuring Steam...");

                // Configure Steam App ID first (same as AuthenticateAsync)
                if (!_isConfigured)
                {
                    await ConfigureSteamAppIdAsync();
                    _isConfigured = true;
                }

                OnStatusChanged?.Invoke("Initializing Steamworks...");

                // Call initialize-only method
                bool success = await _steamAuthManager.InitializeSteamOnlyAsync();

                if (success)
                {
                    OnStatusChanged?.Invoke("Steam initialized");
                    Debug.Log("[SteamAuthProvider] Steamworks initialized for developer mode");
                }
                else
                {
                    OnStatusChanged?.Invoke("Initialization failed");
                    Debug.LogError("[SteamAuthProvider] Failed to initialize Steamworks");
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamAuthProvider] InitializeWithoutAuth exception: {ex.Message}");
                OnStatusChanged?.Invoke($"Error: {ex.Message}");
                return false;
            }
        }

        public void Cleanup()
        {
            try
            {
                if (_steamAuthManager != null)
                {
                    _steamAuthManager.Logout();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SteamAuthProvider] Cleanup exception: {ex.Message}");
            }
        }
    }
}
