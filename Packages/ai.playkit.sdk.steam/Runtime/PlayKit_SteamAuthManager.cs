using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace PlayKit_SDK.Steam
{
    /// <summary>
    /// Result of Steam authentication with PlayKit backend.
    /// </summary>
    [Serializable]
    public class SteamAuthResult
    {
        public bool success;
        public string userId;
        public string steamId;
        public bool userCreated;
        public string playerToken;
        public string tokenName;
        public string expiresAt;
        public string error;
        public string message;
    }

    /// <summary>
    /// Steam authentication manager for PlayKit SDK.
    /// Handles the complete Steam login flow and integrates with PlayKit.
    /// </summary>
    public class PlayKit_SteamAuthManager : MonoBehaviour
    {
        [Header("Steam Configuration")]
        [Tooltip("Your Steam App ID. Must match the channel configuration in PlayKit dashboard.")]
        [SerializeField] private uint _steamAppId;

        private PlayKit_SteamService _steamService;
        private bool _isAuthenticated;
        private string _steamId;
        private SteamAuthResult _lastAuthResult;

        /// <summary>
        /// Event fired when authentication succeeds.
        /// </summary>
        public event Action<SteamAuthResult> OnAuthSuccess;

        /// <summary>
        /// Event fired when authentication fails.
        /// </summary>
        public event Action<string> OnAuthError;

        /// <summary>
        /// Whether the user is currently authenticated.
        /// </summary>
        public bool IsAuthenticated => _isAuthenticated;

        /// <summary>
        /// The user's Steam ID (64-bit format).
        /// </summary>
        public string SteamId => _steamId;

        /// <summary>
        /// The Steam App ID being used.
        /// </summary>
        public uint SteamAppId => _steamAppId;

        /// <summary>
        /// The last authentication result.
        /// </summary>
        public SteamAuthResult LastAuthResult => _lastAuthResult;

        private void Awake()
        {
            _steamService = new PlayKit_SteamService();
        }

        private void Update()
        {
            // Run Steam callbacks
            _steamService?.RunCallbacks();
        }

        private void OnDestroy()
        {
            _steamService?.Shutdown();
        }

        private void OnApplicationQuit()
        {
            _steamService?.Shutdown();
        }

        /// <summary>
        /// Set the Steam App ID at runtime.
        /// Must be called before AuthenticateAsync if not set in inspector.
        /// </summary>
        public void SetSteamAppId(uint appId)
        {
            _steamAppId = appId;
        }

        /// <summary>
        /// Initialize Steam and authenticate with PlayKit backend.
        /// </summary>
        /// <returns>True if authentication succeeded</returns>
        public async UniTask<bool> AuthenticateAsync()
        {
            if (_steamAppId == 0)
            {
                var error = "Steam App ID not configured";
                Debug.LogError($"[PlayKit Steam] {error}");
                OnAuthError?.Invoke(error);
                return false;
            }

            try
            {
                // Step 1: Initialize Steam
                Debug.Log("[PlayKit Steam] Initializing Steam...");
                if (!await _steamService.InitializeAsync(_steamAppId))
                {
                    var error = "Failed to initialize Steam. Is Steam running?";
                    OnAuthError?.Invoke(error);
                    return false;
                }

                _steamId = _steamService.SteamId;
                Debug.Log($"[PlayKit Steam] Steam initialized. SteamID: {_steamId}");

                // Step 2: Get session ticket
                Debug.Log("[PlayKit Steam] Getting session ticket...");
                var ticket = await _steamService.GetSessionTicketAsync();
                if (string.IsNullOrEmpty(ticket))
                {
                    var error = "Failed to get Steam session ticket";
                    OnAuthError?.Invoke(error);
                    return false;
                }

                // Step 3: Verify with PlayKit backend
                Debug.Log("[PlayKit Steam] Verifying with PlayKit...");
                var result = await VerifyWithPlayKitAsync(ticket);
                if (result == null || !result.success)
                {
                    var error = result?.error ?? result?.message ?? "Authentication failed";
                    OnAuthError?.Invoke(error);
                    return false;
                }

                _lastAuthResult = result;
                _isAuthenticated = true;
                OnAuthSuccess?.Invoke(result);

                Debug.Log($"[PlayKit Steam] Authentication successful! User: {result.userId}");

                // Note: SteamRechargeProvider is now automatically registered via SteamAddonDescriptor.GetRechargeProvider()

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit Steam] Authentication failed: {ex.Message}");
                OnAuthError?.Invoke(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Initialize Steam without authentication (for developer token scenarios).
        /// Only initializes Steamworks to enable IAP functionality.
        /// Does not create session tickets or perform backend authentication.
        /// </summary>
        public async UniTask<bool> InitializeSteamOnlyAsync()
        {
            if (_steamAppId == 0)
            {
                var error = "Steam App ID not configured";
                Debug.LogError($"[PlayKit Steam] {error}");
                OnAuthError?.Invoke(error);
                return false;
            }

            try
            {
                // Initialize Steam
                Debug.Log("[PlayKit Steam] Initializing Steamworks for developer mode...");
                if (!await _steamService.InitializeAsync(_steamAppId, skipRestartCheck: true))
                {
                    var error = "Failed to initialize Steam. Is Steam running?";
                    Debug.LogError($"[PlayKit Steam] {error}");
                    OnAuthError?.Invoke(error);
                    return false;
                }

                _steamId = _steamService.SteamId;
                Debug.Log($"[PlayKit Steam] Steamworks initialized. SteamID: {_steamId}");
                Debug.Log("[PlayKit Steam] Skipping authentication - developer token mode");

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit Steam] Initialization failed: {ex.Message}");
                OnAuthError?.Invoke(ex.Message);
                return false;
            }
        }

        private async UniTask<SteamAuthResult> VerifyWithPlayKitAsync(string ticket)
        {
            // Get PlayKit settings
            var settings = Resources.Load<ScriptableObject>("PlayKitSettings");
            if (settings == null)
            {
                Debug.LogError("[PlayKit Steam] PlayKitSettings not found in Resources");
                return null;
            }

            // Use reflection to get BaseUrl and GameId from settings
            var settingsType = settings.GetType();

            string baseUrl = "https://api.playkit.ai";
            string gameId = null;

            // Try to get BaseUrl
            var baseUrlProp = settingsType.GetProperty("BaseUrl");
            if (baseUrlProp != null)
            {
                baseUrl = baseUrlProp.GetValue(settings) as string ?? baseUrl;
            }
            else
            {
                var baseUrlField = settingsType.GetField("baseUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (baseUrlField != null)
                {
                    baseUrl = baseUrlField.GetValue(settings) as string ?? baseUrl;
                }
            }

            // Try to get GameId
            var gameIdProp = settingsType.GetProperty("GameId");
            if (gameIdProp != null)
            {
                gameId = gameIdProp.GetValue(settings) as string;
            }
            else
            {
                var gameIdField = settingsType.GetField("gameId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (gameIdField != null)
                {
                    gameId = gameIdField.GetValue(settings) as string;
                }
            }

            if (string.IsNullOrEmpty(gameId))
            {
                Debug.LogError("[PlayKit Steam] Game ID not configured in PlayKitSettings");
                return new SteamAuthResult { success = false, error = "Game ID not configured" };
            }

            // Build request
            var endpoint = $"{baseUrl}/api/auth/steam/verify";
            var payload = JsonConvert.SerializeObject(new { ticket, gameId });

            using var request = new UnityWebRequest(endpoint, "POST");
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(payload));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[PlayKit Steam] Verify request failed: {request.error}");
                return new SteamAuthResult { success = false, error = request.error };
            }

            var response = JsonConvert.DeserializeObject<SteamAuthResult>(request.downloadHandler.text);
            return response;
        }

        /// <summary>
        /// Logout and clear authentication state.
        /// </summary>
        public void Logout()
        {
            _isAuthenticated = false;
            _lastAuthResult = null;
            _steamService?.CancelTicket();
            Debug.Log("[PlayKit Steam] Logged out");
        }
    }
}
