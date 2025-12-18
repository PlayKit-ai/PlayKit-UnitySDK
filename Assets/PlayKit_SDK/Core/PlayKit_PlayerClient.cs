using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayKit_SDK;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayKit_SDK
{
    public class PlayKit_PlayerClient : MonoBehaviour
    {
        // BaseUrl is now retrieved from PlayKitSettings
        private string BaseUrl => PlayKitSettings.Instance?.BaseUrl ?? "https://playkit.ai";

        private int timeoutSeconds = 30;
        private int maxRetryCount = 3;
        private float retryDelaySeconds = 3.0f;
        private bool enableDebugLogs = true;

        private string currentJWT;
        private string playerToken;
        private PlayerInfo cachedPlayerInfo;
        
        // Events
        public event Action<PlayerInfo> OnPlayerInfoUpdated;
        public event Action<string> OnPlayerTokenReceived;
        public event Action<string> OnNicknameChanged;
        public event Action<string> OnError;

        // ADDED: Public property to access the result of the last JWT exchange.
        public JWTExchangeResponse LastExchangeResponse { get; private set; }

        #region Data Structures

        [Serializable]
        public class PlayerInfo
        {
            [JsonProperty("userId")] public string UserId { get; set; }
            [JsonProperty("credits")] public float Credits { get; set; }
            /// <summary>
            /// Player nickname (per-game nickname > first_name > null)
            /// </summary>
            [JsonProperty("nickname")] public string Nickname { get; set; }
            // [JsonProperty("tokenType")] public string TokenType { get; set; }
            // [JsonProperty("tokenId")] public string TokenId { get; set; }
        }

        [Serializable]
        public class SetNicknameRequest
        {
            [JsonProperty("nickname")] public string Nickname { get; set; }
        }

        [Serializable]
        public class SetNicknameResponse
        {
            [JsonProperty("success")] public bool Success { get; set; }
            [JsonProperty("nickname")] public string Nickname { get; set; }
            [JsonProperty("gameId")] public string GameId { get; set; }
            [JsonProperty("error")] public SetNicknameError Error { get; set; }
        }

        [Serializable]
        public class SetNicknameError
        {
            [JsonProperty("code")] public string Code { get; set; }
            [JsonProperty("message")] public string Message { get; set; }
        }

        [Serializable]
        public class JWTExchangeRequest
        {
            [JsonProperty("token")] public string Token { get; set; }
        }

        [Serializable]
        public class JWTExchangeResponse
        {
            [JsonProperty("success")] public bool Success { get; set; }
            [JsonProperty("userId")] public string UserId { get; set; }
            [JsonProperty("playerToken")] public string PlayerToken { get; set; }
            [JsonProperty("tokenName")] public string TokenName { get; set; }
            [JsonProperty("createdAt")] public string CreatedAt { get; set; }
            [JsonProperty("expiresAt")] public string ExpiresAt { get; set; } // null means never expires
        }
        
        [Serializable]
        public class ErrorResponse
        {
            [JsonProperty("error")] public string Error { get; set; }
        }
        
        public class ApiResult<T>
        {
            public bool Success { get; set; }
            public T Data { get; set; }
            public string Error { get; set; }
            public int StatusCode { get; set; }
        }
        
        #endregion



        #region Public API

        /// <summary>
        /// Initializes the client with a short-lived JWT and automatically exchanges it for a Player Token.
        /// </summary>
        /// <param name="jwt">The short-lived JWT from your game's authentication system.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the JWT was successfully exchanged for a Player Token.</returns>
        public async UniTask<(bool,string)> InitializeAsync(string jwt, CancellationToken cancellationToken = default)
        {
            Debug.Log("exchanging with" + jwt);
            if (string.IsNullOrEmpty(jwt))
            {
                string error = "JWT token cannot be null or empty";
                Debug.LogError(error);
                OnError?.Invoke(error);
                return (false,error);
            }
            
            currentJWT = jwt;
            Debug.Log("SDK initialized with JWT. Exchanging for Player Token...");
            
            var result = await ExchangeJWTForPlayerTokenAsync(cancellationToken);
            return (result.Item1.Success,result.Item2);
        }
        
        /// <summary>
        /// Exchanges the stored JWT for a long-lived Player Token by sending it in the Authorization header.
        /// </summary>
        public async UniTask<(ApiResult<JWTExchangeResponse>, string)> ExchangeJWTForPlayerTokenAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(currentJWT))
            {
                string error = "No JWT token available for exchange";
                Debug.LogError(error);
                OnError?.Invoke(error);
                return (new ApiResult<JWTExchangeResponse> { Success = false, Error = error }, error);
            }
    
            string url = $"{BaseUrl}/api/external/exchange-jwt";
    
            // The JWT is now passed as an auth token for the header.
            // We send an empty object as the body, as our endpoint no longer reads it.
            var result = await PostRequestAsync<object, JWTExchangeResponse>(url, new object(), cancellationToken, currentJWT);
    
            // The rest of the logic remains the same...
            if (result.Success && result.Data.Success)
            {
                playerToken = result.Data.PlayerToken;
                LastExchangeResponse = result.Data;

                Debug.Log($"Player token received: {playerToken}");
                Debug.Log($"Token name: {result.Data.TokenName}");
                Debug.Log($"Expires at: {result.Data.ExpiresAt ?? "Never"}");
        
                OnPlayerTokenReceived?.Invoke(playerToken);
        
                GetPlayerInfoAsync(cancellationToken).Forget(); // Fire and forget
            }
            else
            {
                // Consolidate error handling
                string error = result.Error ?? "JWT exchange failed on the server.";
                if (enableDebugLogs) Debug.LogError(error);
                OnError?.Invoke(error);
                result.Success = false; // Ensure success is false
                result.Error = error;
                return (result, error);
            }
            return (result, result.Error);
        }
        public async UniTask<ApiResult<PlayerInfo>> GetPlayerInfoAsync(CancellationToken cancellationToken = default)
        {
            string authToken = GetAuthToken();
            if (string.IsNullOrEmpty(authToken))
            {
                string error = "No valid auth token available";
                Debug.LogError(error);
                OnError?.Invoke(error);
                return new ApiResult<PlayerInfo> { Success = false, Error = error };
            }
            
            string url = $"{BaseUrl}/api/external/player-info";
            var result = await GetRequestAsync<PlayerInfo>(url, authToken, cancellationToken);
            
            if (result.Success)
            {
                cachedPlayerInfo = result.Data;
                Debug.Log($"Player info updated: {cachedPlayerInfo.UserId} has {cachedPlayerInfo.Credits} credits.");
                OnPlayerInfoUpdated?.Invoke(cachedPlayerInfo);
            }
            
            return result;
        }

        public bool HasValidPlayerToken() => !string.IsNullOrEmpty(playerToken);
        public PlayerInfo GetCachedPlayerInfo() => cachedPlayerInfo;
        public string GetPlayerToken() => playerToken;

        /// <summary>
        /// Gets the player's nickname from cached player info
        /// </summary>
        /// <returns>The nickname, or null if not set</returns>
        public string GetNickname() => cachedPlayerInfo?.Nickname;

        /// <summary>
        /// Sets the player's nickname for the current game.
        /// Requires a game-specific player token (not a global token).
        /// </summary>
        /// <param name="nickname">The nickname to set (1-16 characters, letters/numbers/Chinese/underscores/spaces only)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>ApiResult with SetNicknameResponse on success</returns>
        public async UniTask<ApiResult<SetNicknameResponse>> SetNicknameAsync(string nickname, CancellationToken cancellationToken = default)
        {
            string authToken = GetAuthToken();
            if (string.IsNullOrEmpty(authToken))
            {
                string error = "No valid auth token available";
                Debug.LogError(error);
                OnError?.Invoke(error);
                return new ApiResult<SetNicknameResponse> { Success = false, Error = error };
            }

            // Validate nickname locally first
            if (string.IsNullOrEmpty(nickname))
            {
                string error = "Nickname cannot be null or empty";
                Debug.LogError(error);
                OnError?.Invoke(error);
                return new ApiResult<SetNicknameResponse> { Success = false, Error = error };
            }

            string trimmedNickname = nickname.Trim();
            if (trimmedNickname.Length == 0)
            {
                string error = "Nickname cannot be empty";
                Debug.LogError(error);
                OnError?.Invoke(error);
                return new ApiResult<SetNicknameResponse> { Success = false, Error = error };
            }

            if (trimmedNickname.Length > 16)
            {
                string error = "Nickname must be 16 characters or less";
                Debug.LogError(error);
                OnError?.Invoke(error);
                return new ApiResult<SetNicknameResponse> { Success = false, Error = error };
            }

            string url = $"{BaseUrl}/api/external/set-game-player-nickname";
            var requestData = new SetNicknameRequest { Nickname = trimmedNickname };

            var result = await PostRequestAsync<SetNicknameRequest, SetNicknameResponse>(url, requestData, cancellationToken, authToken);

            if (result.Success && result.Data != null)
            {
                if (result.Data.Success)
                {
                    // Update cached player info
                    if (cachedPlayerInfo != null)
                    {
                        cachedPlayerInfo.Nickname = result.Data.Nickname;
                        OnPlayerInfoUpdated?.Invoke(cachedPlayerInfo);
                    }

                    Debug.Log($"Nickname set successfully: {result.Data.Nickname}");
                    OnNicknameChanged?.Invoke(result.Data.Nickname);
                }
                else if (result.Data.Error != null)
                {
                    string error = result.Data.Error.Message ?? "Failed to set nickname";
                    Debug.LogError($"Set nickname failed: {result.Data.Error.Code} - {error}");
                    OnError?.Invoke(error);
                    result.Success = false;
                    result.Error = error;
                }
            }

            return result;
        }

        
        /// <summary>
        /// Sets the player token directly (for use when loading from storage)
        /// </summary>
        /// <param name="token">The player token</param>
        public void SetPlayerToken(string token)
        {
            playerToken = token;
            Debug.Log($"Player token set: {token.Substring(0, Math.Min(20, token.Length))}...");
                
            // When a token is set, we can immediately try to fetch player info
            GetPlayerInfoAsync().Forget();
        }
        // ... Other public and private methods remain the same ...
        // For brevity, the unchanged helper methods (PostRequestAsync, GetRequestAsync, etc.) are omitted,
        // but they should be included in your final file. They are identical to your provided code.
        
        #endregion

        #region Network Request Methods
        private async UniTask<ApiResult<TResponse>> PostRequestAsync<TRequest, TResponse>(
            string url,
            TRequest requestData,
            CancellationToken cancellationToken,
            string authToken = null) // ADDED: Optional parameter for auth token
        {
            string jsonData = JsonConvert.SerializeObject(requestData);
            byte[] postData = Encoding.UTF8.GetBytes(jsonData);

            int attempt = 0;
            while (attempt < maxRetryCount)
            {
                if (enableDebugLogs) Debug.Log($"POST to {url}");
                using (var request = new UnityWebRequest(url, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(postData);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");

                    // ADDED: Set the Authorization header if a token is provided
                    if (!string.IsNullOrEmpty(authToken))
                    {
                        request.SetRequestHeader("Authorization", $"Bearer {authToken}");
                    }

                    var operation = request.SendWebRequest();
                    float timer = 0f;

                    while (!operation.isDone && timer < timeoutSeconds)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                        timer += Time.deltaTime;
                    }

                    // Simplified success check
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        return ProcessResponse<TResponse>(request);
                    }

                    Debug.LogWarning($"POST attempt {attempt + 1} failed: {request.error} (Status: {request.responseCode})");

                    attempt++;
                    if (attempt < maxRetryCount)
                        await UniTask.Delay(TimeSpan.FromSeconds(retryDelaySeconds), cancellationToken: cancellationToken);
                }
            }

            return new ApiResult<TResponse>
            {
                Success = false,
                Error = $"POST request failed after {maxRetryCount} attempts",
                StatusCode = 0
            };
        }
        private async UniTask<ApiResult<TResponse>> GetRequestAsync<TResponse>(
            string url,
            string authToken,
            CancellationToken cancellationToken)
        {
            int attempt = 0;
            while (attempt < maxRetryCount)
            {
                using (var request = UnityWebRequest.Get(url))
                {
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Authorization", $"Bearer {authToken}");

                    var operation = request.SendWebRequest();
                    float timer = 0f;

                    while (!operation.isDone && timer < timeoutSeconds)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                        timer += Time.deltaTime;
                    }

                    if (operation.isDone && !(request.result == UnityWebRequest.Result.ConnectionError) && !(request.result == UnityWebRequest.Result.ProtocolError))
                    {
                        return ProcessResponse<TResponse>(request);
                    }

                    Debug.LogWarning($"GET attempt {attempt + 1} failed: {request.error}");

                    attempt++;
                    if (attempt < maxRetryCount)
                        await UniTask.Delay(TimeSpan.FromSeconds(retryDelaySeconds), cancellationToken: cancellationToken);
                }
            }

            return new ApiResult<TResponse>
            {
                Success = false,
                Error = $"GET request failed after {maxRetryCount} attempts",
                StatusCode = 0
            };
        }

        private ApiResult<T> ProcessResponse<T>(UnityWebRequest request)
        {
            var statusCode = (int)request.responseCode;
            string responseText = request.downloadHandler.text;

            if (enableDebugLogs)
            {
                Debug.Log($"Response ({statusCode}): {responseText}");
            }

            try
            {
                var data = JsonConvert.DeserializeObject<T>(responseText);
                return new ApiResult<T>
                {
                    Success = true,
                    Data = data,
                    StatusCode = statusCode
                };
            }
            catch (Exception ex)
            {
                if (enableDebugLogs)
                {
                    Debug.LogError($"Failed to deserialize response: {ex.Message}");
                }

                ErrorResponse error = null;
                try
                {
                    error = JsonConvert.DeserializeObject<ErrorResponse>(responseText);
                }
                catch
                {
                    Debug.LogError($"Failed to deserialize response: {responseText}");
                }

                return new ApiResult<T>
                {
                    Success = false,
                    Error = error?.Error ?? $"Failed to parse server response. Status Code: {statusCode}",
                    StatusCode = statusCode
                };
            }
        }
        #endregion

        private string GetAuthToken() { return !string.IsNullOrEmpty(playerToken) ? playerToken : currentJWT; }
    }
}