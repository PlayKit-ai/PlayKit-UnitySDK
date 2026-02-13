using System;
using System.Text;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using PlayKit_SDK.Art;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayKit_SDK.Auth
{
    public class PlayKit_AuthManager : MonoBehaviour
    {
        // Storage keys
        private const string PlayerTokenKey = "PlayKit_SDK_PlayerToken";
        private const string RefreshTokenKey = "PlayKit_SDK_RefreshToken";
        private const string TokenExpiryKey = "PlayKit_SDK_TokenExpiry";

        // How early (in seconds) before expiry should we refresh the token
        private const int TOKEN_REFRESH_BUFFER_SECONDS = 300; // 5 minutes before expiry

        // Platform token detection settings (for same-domain WebGL scenarios)
        private const string DefaultPlatformTokenKey = "shared_token";

        private string _gameId;
        public string gameId { get => _gameId; }
        public string AuthToken { get; private set; }
        public string RefreshToken { get; private set; }
        public bool IsDeveloperToken { get; private set; }

        /// <summary>
        /// Platform token to use directly (optional).
        /// When set, SDK will use this token without triggering login flow.
        /// Useful for first-party apps that receive tokens from the platform.
        /// </summary>
        public string PlatformToken { get; set; }

        /// <summary>
        /// Whether to auto-detect platform token from browser localStorage (WebGL only).
        /// When enabled, SDK will check localStorage for a token stored by the platform
        /// (e.g., Agentland-Space) and use it directly without triggering Device Auth.
        /// Default: true
        /// </summary>
        public bool AutoDetectPlatformToken { get; set; } = true;

        /// <summary>
        /// localStorage key name for platform token detection.
        /// Default: 'shared_token'
        /// </summary>
        public string PlatformTokenKey { get; set; } = DefaultPlatformTokenKey;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string PlayKit_GetLocalStorage(string key);
#endif

        [SerializeField] private PlayKit_PlayerClient _playerClient;
        public PlayKit_PlayerClient PlayerClient { get => _playerClient; }
        private LoadingSpinner standaloneLoadingObject;

        private string ApiBaseUrl => PlayKitSettings.Instance?.BaseUrl ?? "https://playkit.ai";

        private void Awake()
        {
            // Create PlayerClient if it doesn't exist (for dynamically added components)
            if (_playerClient == null)
            {
                _playerClient = gameObject.AddComponent<PlayKit_PlayerClient>();
            }
        }

        public void Setup(string publishableKey, string developerToken = null)
        {
            _gameId = publishableKey;
            Debug.Log("[PlayKit SDK] Initializing authentication with the following game id: " + _gameId);
            if (!string.IsNullOrEmpty(developerToken))
            {
                AuthToken = developerToken;
                IsDeveloperToken = true;
            }
            if (standaloneLoadingObject == null)
            {
                var loadingPrefab = Resources.Load<GameObject>("Loading");
                if (loadingPrefab != null)
                {
                    var instance = Instantiate(loadingPrefab);
                    if (instance != null)
                    {
                        standaloneLoadingObject = instance.GetComponent<LoadingSpinner>();
                    }
                }

                if (standaloneLoadingObject == null)
                {
                    Debug.LogWarning("[PlayKit SDK] Loading prefab not found or missing LoadingSpinner component. Auth will proceed without loading UI.");
                }
            }
        }

        private void SetLoadingVisible(bool visible)
        {
            if (standaloneLoadingObject != null)
            {
                standaloneLoadingObject.gameObject.SetActive(visible);
            }
        }

        public async UniTask<bool> AuthenticateAsync()
        {
            SetLoadingVisible(true);

            // If using a developer token, authentication is always considered successful.
            if (IsDeveloperToken)
            {
                Debug.Log("[PlayKit SDK] Using developer token. Authentication successful.");
                SetLoadingVisible(false);
                return true;
            }

            // Check for explicitly set platform token (first-party apps)
            if (!string.IsNullOrEmpty(PlatformToken))
            {
                Debug.Log("[PlayKit SDK] Using platform token. Authentication successful.");
                AuthToken = PlatformToken;
                if (PlayerClient != null)
                {
                    PlayerClient.SetPlayerToken(AuthToken);
                }
                SetLoadingVisible(false);
                return true;
            }

            // WebGL: Auto-detect platform token from browser localStorage (same-domain scenario)
            if (AutoDetectPlatformToken)
            {
                string detectedToken = GetPlatformTokenFromBrowser();
                if (!string.IsNullOrEmpty(detectedToken))
                {
                    Debug.Log("[PlayKit SDK] Platform token detected from browser localStorage. Authentication successful.");
                    AuthToken = detectedToken;
                    if (PlayerClient != null)
                    {
                        PlayerClient.SetPlayerToken(AuthToken);
                    }
                    SetLoadingVisible(false);
                    return true;
                }
            }

            // Step 1: Try loading tokens from storage
            LoadTokens();

            // Step 2: Check if token is valid or can be refreshed
            if (!string.IsNullOrEmpty(AuthToken))
            {
                // Check if token is expired or about to expire
                if (IsTokenExpiredOrExpiringSoon())
                {
                    Debug.Log("[PlayKit SDK] Token expired or expiring soon, attempting refresh...");
                    if (await TryRefreshTokenAsync())
                    {
                        SetLoadingVisible(false);
                        Debug.Log("[PlayKit SDK] Token refreshed successfully.");
                        return true;
                    }
                    // Refresh failed, will need to re-login
                    Debug.Log("[PlayKit SDK] Token refresh failed, will require re-login.");
                }
                else
                {
                    // Token not expired, verify with API
                    if (await IsTokenValidWithAPICheck())
                    {
                        SetLoadingVisible(false);
                        Debug.Log("[PlayKit SDK] Existing valid player token found and verified.");
                        return true;
                    }
                    
                    // Token invalid, try refresh
                    Debug.Log("[PlayKit SDK] Token invalid, attempting refresh...");
                    if (await TryRefreshTokenAsync())
                    {
                        SetLoadingVisible(false);
                        Debug.Log("[PlayKit SDK] Token refreshed successfully after API check failure.");
                        return true;
                    }
                }
            }

            // Step 3: No valid tokens, initiate login process
            Debug.Log("[PlayKit SDK] No valid player token found. Initiating login process.");
            SetLoadingVisible(false);

            return await ShowLoginWebAsync();
        }

        /// <summary>
        /// Authenticate using an external auth provider (from platform addons).
        /// The provider handles the platform-specific auth flow and returns a standardized result.
        /// </summary>
        /// <param name="provider">The auth provider to use</param>
        /// <returns>True if authentication succeeded</returns>
        public async UniTask<bool> AuthenticateWithProviderAsync(IAuthProvider provider)
        {
            if (provider == null)
            {
                Debug.LogError("[PlayKit SDK] Cannot authenticate with null provider");
                return false;
            }

            SetLoadingVisible(true);

            try
            {
                Debug.Log($"[PlayKit SDK] Authenticating with provider: {provider.DisplayName}");

                // Subscribe to status changes for UI feedback
                provider.OnStatusChanged += OnProviderStatusChanged;

                // Call the provider's authentication method
                var result = await provider.AuthenticateAsync();

                // Unsubscribe from status changes
                provider.OnStatusChanged -= OnProviderStatusChanged;

                if (result == null || !result.Success)
                {
                    Debug.LogError($"[PlayKit SDK] Provider authentication failed: {result?.Error ?? "Unknown error"}");
                    SetLoadingVisible(false);
                    return false;
                }

                // Extract and store the player token
                if (string.IsNullOrEmpty(result.PlayerToken))
                {
                    Debug.LogError("[PlayKit SDK] Provider returned no player token");
                    SetLoadingVisible(false);
                    return false;
                }

                // Set the auth token
                AuthToken = result.PlayerToken;
                IsDeveloperToken = false;

                // Save tokens if refresh token is provided
                if (!string.IsNullOrEmpty(result.RefreshToken))
                {
                    RefreshToken = result.RefreshToken;
                    int expiresIn = result.ExpiresIn > 0 ? result.ExpiresIn :
                                    (result.ExpiresAt.HasValue ? (int)(result.ExpiresAt.Value - DateTime.UtcNow).TotalSeconds : 3600);

                    SavePlayerToken(result.PlayerToken, result.RefreshToken, expiresIn);
                }
                else if (!string.IsNullOrEmpty(result.PlayerToken))
                {
                    // Save token without refresh (legacy format)
                    string expiresAt = result.ExpiresAt?.ToString("o") ?? "";
                    SavePlayerToken(result.PlayerToken, expiresAt);
                }

                // Update PlayerClient
                if (PlayerClient != null)
                {
                    PlayerClient.SetPlayerToken(AuthToken);
                }

                Debug.Log($"[PlayKit SDK] Provider authentication successful. User: {result.UserId}");
                SetLoadingVisible(false);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit SDK] Provider authentication exception: {ex.Message}");
                provider.OnStatusChanged -= OnProviderStatusChanged;
                SetLoadingVisible(false);
                return false;
            }
        }

        /// <summary>
        /// Handle status updates from auth provider (for UI feedback)
        /// </summary>
        private void OnProviderStatusChanged(string status)
        {
            Debug.Log($"[PlayKit SDK] Auth provider status: {status}");
            // Could update loading UI here in the future
        }

        private async UniTask<bool> ShowLoginWebAsync()
        {
            var loginWebPrefab = Resources.Load<GameObject>("LoginWeb");
            if (loginWebPrefab == null)
            {
                Debug.LogError("[PlayKit SDK] LoginWeb prefab not found in Resources folder!");
                return false;
            }

            var loginWebInstance = GameObject.Instantiate(loginWebPrefab);
            var authFlowManager = loginWebInstance.GetComponent<PlayKit_AuthFlowManager>();
            if (authFlowManager == null)
            {
                Debug.LogError("[PlayKit SDK] AuthFlowManager component not found on the LoginWeb prefab!");
                GameObject.Destroy(loginWebInstance);
                return false;
            }

            // Pass the AuthManager reference so the flow can use our PlayerClient
            authFlowManager.AuthManager = this;

            // Wait until the AuthFlowManager reports success
            await UniTask.WaitUntil(() => authFlowManager.IsSuccess, cancellationToken: loginWebInstance.GetCancellationTokenOnDestroy());

            bool success = authFlowManager.IsSuccess;

            // Clean up the login UI.
            Destroy(loginWebInstance);

            if (success)
            {
                // The flow was successful, load the new tokens
                LoadTokens();
                return IsTokenValid();
            }

            Debug.LogError("[PlayKit SDK] Login flow did not complete successfully.");
            return false;
        }

        #region Token Storage

        private void LoadTokens()
        {
            // Do not overwrite a developer token.
            if (IsDeveloperToken) return;

            AuthToken = PlayerPrefs.GetString(PlayerTokenKey, null);
            RefreshToken = PlayerPrefs.GetString(RefreshTokenKey, null);
            
            if (!string.IsNullOrEmpty(AuthToken))
            {
                Debug.Log("[PlayKit SDK] Loaded tokens from storage.");
            }
        }

        /// <summary>
        /// Save player tokens (access token, refresh token, and expiry)
        /// </summary>
        public static void SavePlayerToken(string accessToken, string refreshToken, int expiresInSeconds)
        {
            PlayerPrefs.SetString(PlayerTokenKey, accessToken);
            
            if (!string.IsNullOrEmpty(refreshToken))
            {
                PlayerPrefs.SetString(RefreshTokenKey, refreshToken);
            }

            // Calculate and store expiry time
            DateTime expiryDate = expiresInSeconds > 0
                ? DateTime.UtcNow.AddSeconds(expiresInSeconds)
                : DateTime.MaxValue;

            PlayerPrefs.SetString(TokenExpiryKey, expiryDate.Ticks.ToString());
            PlayerPrefs.Save();

            Debug.Log($"[PlayKit SDK] Tokens saved. Expires in {expiresInSeconds} seconds.");
        }

        /// <summary>
        /// Legacy overload for backward compatibility
        /// </summary>
        public static void SavePlayerToken(string token, string expiresAtString)
        {
            PlayerPrefs.SetString(PlayerTokenKey, token);

            DateTime expiryDate = string.IsNullOrEmpty(expiresAtString)
                ? DateTime.MaxValue
                : DateTime.Parse(expiresAtString, null, System.Globalization.DateTimeStyles.RoundtripKind);

            PlayerPrefs.SetString(TokenExpiryKey, expiryDate.ToUniversalTime().Ticks.ToString());
            PlayerPrefs.Save();

            Debug.Log("[PlayKit SDK] New player token saved successfully.");
        }

        public static void ClearPlayerToken()
        {
            PlayerPrefs.DeleteKey(PlayerTokenKey);
            PlayerPrefs.DeleteKey(RefreshTokenKey);
            PlayerPrefs.DeleteKey(TokenExpiryKey);
            PlayerPrefs.Save();
            Debug.Log("[PlayKit SDK] Player tokens cleared.");
        }

        #endregion

        #region Platform Token Detection

        /// <summary>
        /// Attempt to get platform token from browser localStorage (WebGL only).
        /// Returns null on non-WebGL platforms or if token is not found.
        /// </summary>
        private string GetPlatformTokenFromBrowser()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                string key = string.IsNullOrEmpty(PlatformTokenKey) ? DefaultPlatformTokenKey : PlatformTokenKey;
                string token = PlayKit_GetLocalStorage(key);
                if (!string.IsNullOrEmpty(token))
                {
                    Debug.Log($"[PlayKit SDK] Platform token found in localStorage[{key}]");
                    return token;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayKit SDK] Error detecting platform token: {e.Message}");
            }
#endif
            return null;
        }

        #endregion

        #region Token Validation

        private bool IsTokenValid()
        {
            if (string.IsNullOrEmpty(AuthToken))
            {
                return false;
            }

            if (IsDeveloperToken)
            {
                return true;
            }

            return !IsTokenExpired();
        }

        private bool IsTokenExpired()
        {
            string expiryString = PlayerPrefs.GetString(TokenExpiryKey, "0");
            if (long.TryParse(expiryString, out long expiryTicks))
            {
                return DateTime.UtcNow.Ticks > expiryTicks;
            }
            return true; // Invalid format = expired
        }

        private bool IsTokenExpiredOrExpiringSoon()
        {
            string expiryString = PlayerPrefs.GetString(TokenExpiryKey, "0");
            if (long.TryParse(expiryString, out long expiryTicks))
            {
                var bufferTicks = TimeSpan.FromSeconds(TOKEN_REFRESH_BUFFER_SECONDS).Ticks;
                return DateTime.UtcNow.Ticks > (expiryTicks - bufferTicks);
            }
            return true; // Invalid format = expired
        }

        private async UniTask<bool> IsTokenValidWithAPICheck()
        {
            if (string.IsNullOrEmpty(AuthToken))
            {
                return false;
            }

            if (IsDeveloperToken)
            {
                return true;
            }

            // Token hasn't expired according to stored data, verify with API
            if (PlayerClient != null)
            {
                if (!PlayerClient.HasValidPlayerToken())
                {
                    PlayerClient.SetPlayerToken(AuthToken);
                }

                try
                {
                    Debug.Log("[PlayKit SDK] Verifying token with player-info API...");
                    var result = await PlayerClient.GetPlayerInfoAsync();

                    if (!result.Success)
                    {
                        Debug.LogWarning($"[PlayKit SDK] Token verification failed: {result.Error}");
                        return false;
                    }

                    Debug.Log($"[PlayKit SDK] Token verified successfully. User ID: {result.Data.UserId}");
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PlayKit SDK] Error verifying token: {e.Message}");
                    return false;
                }
            }

            // If we can't verify with API, trust the expiry check
            return !IsTokenExpired();
        }

        #endregion

        #region Token Refresh

        /// <summary>
        /// Try to refresh the access token using the stored refresh token
        /// </summary>
        private async UniTask<bool> TryRefreshTokenAsync()
        {
            if (string.IsNullOrEmpty(RefreshToken))
            {
                Debug.Log("[PlayKit SDK] No refresh token available.");
                return false;
            }

            try
            {
                var endpoint = $"{ApiBaseUrl}/api/device-auth/refresh";
                var requestData = new RefreshTokenRequest
                {
                    refresh_token = RefreshToken,
                    game_id = _gameId
                };

                string jsonPayload = JsonConvert.SerializeObject(requestData);

                using (var webRequest = new UnityWebRequest(endpoint, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                    webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                    PlayKitSDK.SetSDKHeaders(webRequest);

                    var operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await UniTask.Yield();
                    }

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        var response = JsonConvert.DeserializeObject<RefreshTokenResponse>(webRequest.downloadHandler.text);

                        if (!string.IsNullOrEmpty(response.access_token))
                        {
                            // Save the new tokens
                            AuthToken = response.access_token;
                            if (!string.IsNullOrEmpty(response.refresh_token))
                            {
                                RefreshToken = response.refresh_token;
                            }

                            SavePlayerToken(
                                response.access_token,
                                response.refresh_token ?? RefreshToken,
                                response.expires_in ?? 3600
                            );

                            // Update PlayerClient with new token
                            if (PlayerClient != null)
                            {
                                PlayerClient.SetPlayerToken(AuthToken);
                            }

                            Debug.Log("[PlayKit SDK] Token refreshed successfully.");
                            return true;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[PlayKit SDK] Token refresh failed: {webRequest.error} - {webRequest.downloadHandler.text}");

                        // Check if refresh token is invalid/expired
                        try
                        {
                            var errorResponse = JsonConvert.DeserializeObject<RefreshErrorResponse>(webRequest.downloadHandler.text);
                            if (errorResponse?.error == "invalid_grant" || errorResponse?.error == "expired_token")
                            {
                                Debug.Log("[PlayKit SDK] Refresh token is invalid or expired, clearing tokens.");
                                ClearPlayerToken();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[PlayKit SDK] Failed to parse token refresh error response: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit SDK] Token refresh error: {ex.Message}");
            }

            return false;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get access to the PlayerClient for querying user information.
        /// This should be called after successful authentication.
        /// </summary>
        public PlayKit_PlayerClient GetPlayerClient()
        {
            if (IsTokenValid())
            {
                // Set token for both player tokens and developer tokens
                if (PlayerClient != null && !PlayerClient.HasValidPlayerToken())
                {
                    PlayerClient.SetPlayerToken(AuthToken);
                }
                return PlayerClient;
            }
            return null;
        }

        /// <summary>
        /// Force refresh the token. Useful when you detect the token might be invalid.
        /// </summary>
        public async UniTask<bool> ForceRefreshTokenAsync()
        {
            if (IsDeveloperToken)
            {
                return true;
            }

            return await TryRefreshTokenAsync();
        }

        /// <summary>
        /// Log out the current user and clear all stored tokens.
        /// </summary>
        public void Logout()
        {
            ClearPlayerToken();
            AuthToken = null;
            RefreshToken = null;
            
            if (PlayerClient != null)
            {
                PlayerClient.ClearPlayerToken();
            }
            
            Debug.Log("[PlayKit SDK] User logged out.");
        }

        #endregion

        #region Data Structures

        [Serializable]
        private class RefreshTokenRequest
        {
            public string refresh_token;
            public string game_id;
        }

        [Serializable]
        private class RefreshTokenResponse
        {
            public string access_token;
            public string refresh_token;
            public string token_type;
            public int? expires_in;
        }

        [Serializable]
        private class RefreshErrorResponse
        {
            public string error;
            public string error_description;
        }

        #endregion
    }
}
