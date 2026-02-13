using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayKit_SDK;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayKit_SDK.Auth
{
    /// <summary>
    /// Device Authorization Flow for Runtime.
    /// Replaces the OTP (send-code/verify-code) authentication method.
    /// Uses PKCE (Proof Key for Code Exchange) for secure authorization.
    /// </summary>
    public class PlayKit_DeviceAuthFlow : MonoBehaviour
    {
        // Events
        public event Action<DeviceAuthResult> OnAuthSuccess;
        public event Action<string> OnAuthError;
        public event Action<DeviceAuthStatus> OnStatusChanged;
        public event Action OnCancelled;

        // State
        private DeviceAuthStatus _status = DeviceAuthStatus.Idle;
        private CancellationTokenSource _pollCts;
        private bool _isRunning = false;

        // PKCE parameters
        private string _codeVerifier;
        private string _codeChallenge;
        private string _sessionId;
        private string _authUrl;

        // Polling configuration
        private int _pollIntervalMs = 5000;
        private DateTime _expiresAt;

        /// <summary>
        /// Current status of the auth flow
        /// </summary>
        public DeviceAuthStatus Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    _status = value;
                    OnStatusChanged?.Invoke(_status);
                }
            }
        }

        /// <summary>
        /// Whether the auth flow is currently running
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// The authorization URL (available after flow starts)
        /// </summary>
        public string AuthUrl => _authUrl;

        /// <summary>
        /// Start the Device Authorization flow for player authentication.
        /// </summary>
        /// <param name="gameId">The game ID to authenticate for</param>
        /// <param name="scope">Authorization scope (default: player:play)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>DeviceAuthResult on success, null on failure</returns>
        public async UniTask<DeviceAuthResult> StartAuthFlowAsync(
            string gameId,
            string scope = "player:play",
            CancellationToken cancellationToken = default)
        {
            if (_isRunning)
            {
                Debug.LogWarning("[PlayKit_DeviceAuthFlow] Auth flow already running");
                return null;
            }

            _isRunning = true;
            _pollCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Status = DeviceAuthStatus.Initiating;

            try
            {
                // Step 1: Generate PKCE parameters
                _codeVerifier = GenerateCodeVerifier();
                _codeChallenge = GenerateCodeChallenge(_codeVerifier);
                Debug.Log("[PlayKit_DeviceAuthFlow] PKCE parameters generated");

                // Step 2: Initiate device auth session
                var baseUrl = PlayKitSettings.Instance?.BaseUrl ?? "https://playkit.ai";
                var initResult = await InitiateDeviceAuthAsync(baseUrl, gameId, scope, _pollCts.Token);

                if (!initResult.Success)
                {
                    Status = DeviceAuthStatus.Error;
                    OnAuthError?.Invoke(initResult.Error);
                    return null;
                }

                _sessionId = initResult.SessionId;
                _authUrl = initResult.AuthUrl;
                _pollIntervalMs = initResult.PollInterval * 1000;
                _expiresAt = DateTime.UtcNow.AddSeconds(initResult.ExpiresIn);

                // Step 3: Open browser for user authorization
                Status = DeviceAuthStatus.WaitingForBrowser;
                Debug.Log($"[PlayKit_DeviceAuthFlow] Opening browser: {_authUrl}");
                Application.OpenURL(_authUrl);

                // Step 4: Start polling for authorization
                Status = DeviceAuthStatus.Polling;
                var result = await PollForAuthorizationAsync(baseUrl, _pollCts.Token);

                if (result != null)
                {
                    Status = DeviceAuthStatus.Authorized;
                    OnAuthSuccess?.Invoke(result);
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                Status = DeviceAuthStatus.Idle;
                OnCancelled?.Invoke();
                return null;
            }
            catch (Exception ex)
            {
                Status = DeviceAuthStatus.Error;
                var errorMsg = $"Device auth failed: {ex.Message}";
                OnAuthError?.Invoke(errorMsg);
                Debug.LogError($"[PlayKit_DeviceAuthFlow] {errorMsg}");
                return null;
            }
            finally
            {
                _isRunning = false;
                _pollCts?.Dispose();
                _pollCts = null;
            }
        }

        /// <summary>
        /// Cancel the ongoing authorization flow
        /// </summary>
        public void CancelFlow()
        {
            if (_pollCts != null && !_pollCts.IsCancellationRequested)
            {
                _pollCts.Cancel();
                Status = DeviceAuthStatus.Idle;
                OnCancelled?.Invoke();
            }
        }

        private void OnDestroy()
        {
            CancelFlow();
        }

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

        private async UniTask<InitiateResult> InitiateDeviceAuthAsync(
            string baseUrl, string gameId, string scope, CancellationToken cancellationToken)
        {
            var endpoint = $"{baseUrl}/api/device-auth/initiate";
            var requestData = new InitiateRequest
            {
                game_id = gameId,
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

                try
                {
                    await webRequest.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    return new InitiateResult
                    {
                        Success = false,
                        Error = $"Network error: {ex.Message}"
                    };
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

        private async UniTask<DeviceAuthResult> PollForAuthorizationAsync(
            string baseUrl, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && DateTime.UtcNow < _expiresAt)
            {
                try
                {
                    var endpoint = $"{baseUrl}/api/device-auth/poll?session_id={Uri.EscapeDataString(_sessionId)}&code_verifier={Uri.EscapeDataString(_codeVerifier)}";

                    using (var webRequest = UnityWebRequest.Get(endpoint))
                    {
                        PlayKitSDK.SetSDKHeaders(webRequest);
                        // Don't use ToUniTask() as it throws on non-2xx status codes
                        // Instead, manually wait for the request to complete
                        var operation = webRequest.SendWebRequest();
                        while (!operation.isDone)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            await UniTask.Yield();
                        }

                        var responseText = webRequest.downloadHandler.text;
                        PollResponse response = null;
                        
                        try
                        {
                            response = JsonConvert.DeserializeObject<PollResponse>(responseText);
                        }
                        catch
                        {
                            // Failed to parse response, continue polling
                            Debug.LogWarning($"[PlayKit_DeviceAuthFlow] Failed to parse poll response: {responseText}");
                        }

                        if (webRequest.result == UnityWebRequest.Result.Success)
                        {
                            if (response?.status == "pending")
                            {
                                // Update poll interval if provided
                                if (response.poll_interval.HasValue)
                                {
                                    _pollIntervalMs = response.poll_interval.Value * 1000;
                                }
                            }
                            else if (response?.status == "authorized")
                            {
                                return new DeviceAuthResult
                                {
                                    AccessToken = response.access_token,
                                    RefreshToken = response.refresh_token,
                                    TokenType = response.token_type,
                                    ExpiresIn = response.expires_in ?? 0,
                                    Scope = response.scope
                                };
                            }
                        }
                        else
                        {
                            // Handle error responses (including HTTP 400)
                            if (response?.error == "slow_down")
                            {
                                _pollIntervalMs = Math.Min(_pollIntervalMs * 2, 30000);
                                Debug.Log("[PlayKit_DeviceAuthFlow] Slowing down poll rate");
                            }
                            else if (response?.error == "access_denied")
                            {
                                Status = DeviceAuthStatus.Denied;
                                OnAuthError?.Invoke("用户拒绝了授权\nUser denied authorization");
                                return null;
                            }
                            else if (response?.error == "expired_token")
                            {
                                Status = DeviceAuthStatus.Expired;
                                OnAuthError?.Invoke("会话已过期\nSession expired");
                                return null;
                            }
                            else
                            {
                                // Unknown error, log but continue polling
                                Debug.LogWarning($"[PlayKit_DeviceAuthFlow] Poll returned error: {response?.error ?? webRequest.error}");
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Network error or other exception, continue polling
                    Debug.LogWarning($"[PlayKit_DeviceAuthFlow] Poll exception, retrying: {ex.Message}");
                }

                // Wait before next poll
                await UniTask.Delay(_pollIntervalMs, cancellationToken: cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }

            Status = DeviceAuthStatus.Expired;
            OnAuthError?.Invoke("授权会话已过期\nAuthorization session expired");
            return null;
        }

        #endregion

        #region Data Structures

        [Serializable]
        private class InitiateRequest
        {
            public string game_id;
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
    /// Status of the Device Authorization flow
    /// </summary>
    public enum DeviceAuthStatus
    {
        Idle,
        Initiating,
        WaitingForBrowser,
        Polling,
        Authorized,
        Denied,
        Expired,
        Error
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
