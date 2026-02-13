using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using PlayKit_SDK;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayKit_SDK.Editor
{
    /// <summary>
    /// Device Authorization Flow for Unity Editor.
    /// Uses PKCE (Proof Key for Code Exchange) for secure authorization.
    /// </summary>
    public class DeviceAuthEditorFlow
    {
        // PKCE parameters
        private string _codeVerifier;
        private string _codeChallenge;
        private string _sessionId;
        private string _authUrl;

        // Polling configuration
        private int _pollIntervalMs = 8000; // Start with 8 seconds poll interval
        private const int MAX_POLL_INTERVAL_MS = 30000; // Max 30 seconds between polls
        private const int SDK_TIMEOUT_SECONDS = 300; // SDK-side timeout: 5 minutes
        private DateTime _expiresAt;
        private DateTime _sdkTimeoutAt;
        private bool _cancelled = false;
        private bool _isPolling = false;

        // Events
        public event Action<DeviceAuthResult> OnSuccess;
        public event Action<string> OnError;
        public event Action<string> OnStatusUpdate;
        public event Action OnCancelled;

        /// <summary>
        /// Start the Device Authorization flow for developer authentication.
        /// Uses global scope to get a token that can access all games the developer owns.
        /// </summary>
        /// <param name="scope">Authorization scope (default: developer:full)</param>
        public async Task<DeviceAuthResult> StartFlowAsync(string scope = "developer:full")
        {
            _cancelled = false;
            _isPolling = false;

            try
            {
                // Step 1: Generate PKCE parameters
                _codeVerifier = GenerateCodeVerifier();
                _codeChallenge = GenerateCodeChallenge(_codeVerifier);
                OnStatusUpdate?.Invoke("Preparing...");

                // Step 2: Initiate device auth session (no gameId needed for global token)
                var baseUrl = PlayKitSettings.Instance?.BaseUrl ?? "https://playkit.ai";
                var initResult = await InitiateDeviceAuthAsync(baseUrl, scope);

                if (!initResult.Success)
                {
                    OnError?.Invoke(initResult.Error);
                    return null;
                }

                _sessionId = initResult.SessionId;
                _authUrl = initResult.AuthUrl;
                _pollIntervalMs = Math.Max(initResult.PollInterval * 1000, 8000); // Min 8 seconds poll interval
                _expiresAt = DateTime.UtcNow.AddSeconds(initResult.ExpiresIn);
                _sdkTimeoutAt = DateTime.UtcNow.AddSeconds(SDK_TIMEOUT_SECONDS); // SDK-side timeout

                // Step 3: Open browser for user authorization
                OnStatusUpdate?.Invoke("Opening browser for authorization...");
                Application.OpenURL(_authUrl);

                // Step 4: Start polling for authorization
                OnStatusUpdate?.Invoke("Waiting for browser authorization...");
                return await PollForAuthorizationAsync(baseUrl);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Device auth failed: {ex.Message}";
                OnError?.Invoke(errorMsg);
                Debug.LogError($"[DeviceAuthEditorFlow] {errorMsg}");
                return null;
            }
        }

        /// <summary>
        /// Cancel the ongoing authorization flow
        /// </summary>
        public void Cancel()
        {
            _cancelled = true;
            _isPolling = false;
            OnCancelled?.Invoke();
        }

        /// <summary>
        /// Check if polling is in progress
        /// </summary>
        public bool IsPolling => _isPolling;

        #region PKCE Implementation

        private string GenerateCodeVerifier()
        {
            byte[] bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Base64UrlEncode(bytes);
        }

        private string GenerateCodeChallenge(string verifier)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(verifier);
                byte[] hash = sha256.ComputeHash(bytes);
                return Base64UrlEncode(hash);
            }
        }

        private string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        #endregion

        #region API Calls

        private async Task<InitiateResult> InitiateDeviceAuthAsync(string baseUrl, string scope)
        {
            var endpoint = $"{baseUrl}/api/device-auth/initiate";
            var requestData = new InitiateRequest
            {
                code_challenge = _codeChallenge,
                code_challenge_method = "S256",
                scope = scope
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
                    await Task.Delay(100);
                }

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    return new InitiateResult
                    {
                        Success = false,
                        Error = $"API Error: {webRequest.error} - {webRequest.downloadHandler.text}"
                    };
                }

                var response = JsonConvert.DeserializeObject<InitiateResponse>(webRequest.downloadHandler.text);
                return new InitiateResult
                {
                    Success = true,
                    SessionId = response.session_id,
                    AuthUrl = response.auth_url,
                    PollInterval = response.poll_interval ?? 5,
                    ExpiresIn = response.expires_in ?? 600
                };
            }
        }

        private async Task<DeviceAuthResult> PollForAuthorizationAsync(string baseUrl)
        {
            _isPolling = true;

            // Use the earlier of server expiration and SDK timeout
            DateTime effectiveExpiry = _sdkTimeoutAt < _expiresAt ? _sdkTimeoutAt : _expiresAt;

            while (!_cancelled && DateTime.UtcNow < effectiveExpiry)
            {
                try
                {
                    var endpoint = $"{baseUrl}/api/device-auth/poll?session_id={Uri.EscapeDataString(_sessionId)}&code_verifier={Uri.EscapeDataString(_codeVerifier)}";

                    using (var webRequest = UnityWebRequest.Get(endpoint))
                    {
                        PlayKitSDK.SetSDKHeaders(webRequest);
                        var operation = webRequest.SendWebRequest();
                        while (!operation.isDone)
                        {
                            await Task.Delay(100);
                        }

                        var responseText = webRequest.downloadHandler.text;
                        var response = JsonConvert.DeserializeObject<PollResponse>(responseText);

                        if (webRequest.result == UnityWebRequest.Result.Success)
                        {
                            if (response.status == "pending")
                            {
                                OnStatusUpdate?.Invoke("Waiting for user authorization...");
                                // Update poll interval if provided
                                if (response.poll_interval.HasValue)
                                {
                                    _pollIntervalMs = response.poll_interval.Value * 1000;
                                }
                            }
                            else if (response.status == "authorized")
                            {
                                _isPolling = false;
                                var result = new DeviceAuthResult
                                {
                                    AccessToken = response.access_token,
                                    RefreshToken = response.refresh_token,
                                    TokenType = response.token_type,
                                    ExpiresIn = response.expires_in ?? 0,
                                    Scope = response.scope
                                };
                                OnSuccess?.Invoke(result);
                                return result;
                            }
                        }
                        else
                        {
                            // Handle error responses
                            if (response?.error == "slow_down")
                            {
                                _pollIntervalMs = Math.Min(_pollIntervalMs * 2, MAX_POLL_INTERVAL_MS);
                                OnStatusUpdate?.Invoke("Slowing down polling rate...");
                            }
                            else if (response?.error == "access_denied")
                            {
                                _isPolling = false;
                                OnError?.Invoke("User denied authorization");
                                return null;
                            }
                            else if (response?.error == "expired_token")
                            {
                                _isPolling = false;
                                OnError?.Invoke("Session expired");
                                return null;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DeviceAuthEditorFlow] Poll error, retrying: {ex.Message}");
                }

                // Wait before next poll
                await Task.Delay(_pollIntervalMs);
            }

            _isPolling = false;

            if (_cancelled)
            {
                OnCancelled?.Invoke();
                return null;
            }

            OnError?.Invoke("Authorization session expired");
            return null;
        }

        #endregion

        #region Data Structures

        [Serializable]
        private class InitiateRequest
        {
            public string code_challenge;
            public string code_challenge_method;
            public string scope;
        }

        [Serializable]
        private class InitiateResponse
        {
            public string session_id;
            public string auth_url;
            public int? poll_interval;
            public int? expires_in;
        }

        [Serializable]
        private class PollResponse
        {
            public string status;
            public string access_token;
            public string refresh_token;
            public string token_type;
            public int? expires_in;
            public int? poll_interval;
            public string scope;
            public string error;
            public string error_description;
        }

        private class InitiateResult
        {
            public bool Success;
            public string Error;
            public string SessionId;
            public string AuthUrl;
            public int PollInterval;
            public int ExpiresIn;
        }

        #endregion
    }

    /// <summary>
    /// Result of a successful device authorization
    /// </summary>
    [Serializable]
    public class DeviceAuthResult
    {
        public string AccessToken;
        public string RefreshToken;
        public string TokenType;
        public int ExpiresIn;
        public string Scope;
    }
}
