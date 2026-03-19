using System;
using System.Text;
using Cysharp.Threading.Tasks;
using PlayKit_SDK;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayKit_SDK.Provider.AI
{
    /// <summary>
    /// Provider for the platform AI endpoint (/ai/{gameId}/v2/chat)
    /// Uses platform-hosted AI models with game-based routing
    /// </summary>
    internal class AIChatProvider : IChatProvider
    {
        private readonly Auth.PlayKit_AuthManager _authManager;

        public AIChatProvider(Auth.PlayKit_AuthManager authManager, bool useOversea = false)
        {
            _authManager = authManager;
            // Note: useOversea parameter is deprecated, use PlayKitSettings.CustomBaseUrl instead
        }

        private string GetChatUrl()
        {
            var settings = PlayKitSettings.Instance;
            if (settings == null || string.IsNullOrEmpty(settings.GameId))
            {
                throw new InvalidOperationException("GameId is not configured in PlayKitSettings.");
            }
            return $"{settings.AIBaseUrl}/v2/chat";
        }

        private string GetAuthToken()
        {
            if (_authManager == null || string.IsNullOrEmpty(_authManager.AuthToken))
            {
                throw new InvalidOperationException("Authentication token is not available.");
            }
            return _authManager.AuthToken;
        }

        public async UniTask<ChatCompletionResponse> ChatCompletionAsync(
            ChatCompletionRequest request, 
            System.Threading.CancellationToken cancellationToken = default)
        {
            // Debug.Log("[AIChatProvider] ChatCompletionAsync");
            
            // Convert to AI endpoint format if needed (currently same as OpenAI format)
            var json = JsonConvert.SerializeObject(request, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            
            using (var webRequest = new UnityWebRequest(GetChatUrl(), "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(new UTF8Encoding().GetBytes(json));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", $"Bearer {GetAuthToken()}");
                PlayKitSDK.SetSDKHeaders(webRequest);

                try
                {
                    await webRequest.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    Debug.LogError($"[AIChatProvider] API request failed: {ex.Message}");
                    return null;
                }
                
                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[AIChatProvider] API Error: {webRequest.responseCode} - {webRequest.error}\n{webRequest.downloadHandler.text}");

                    // Try to parse error response for billing/limit errors
                    TryHandleBillingError(webRequest.downloadHandler.text, (int)webRequest.responseCode);

                    return null;
                }
                
                // Parse response - AI endpoint returns OpenAI compatible format for non-streaming
                return JsonConvert.DeserializeObject<ChatCompletionResponse>(webRequest.downloadHandler.text);
            }
        }

        public async UniTask ChatCompletionStreamAsync(
            ChatCompletionRequest request, 
            Action<string> onTextDelta,
            Action<StreamCompletionResponse> onLegacyResponse, 
            Action onFinally, 
            System.Threading.CancellationToken cancellationToken = default)
        {
            // Debug.Log("[AIChatProvider] ChatCompletionStreamAsync");
            
            var json = JsonConvert.SerializeObject(request, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            
            using (var webRequest = new UnityWebRequest(GetChatUrl(), "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(new UTF8Encoding().GetBytes(json));
                webRequest.downloadHandler = new StreamingDownloadHandler(onTextDelta, onLegacyResponse);
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", $"Bearer {GetAuthToken()}");
                PlayKitSDK.SetSDKHeaders(webRequest);

                try
                {
                    await webRequest.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    Debug.LogError($"[AIChatProvider] API stream request failed: {ex.Message}");

                    // Try to parse billing error from response (e.g., 402 TOKEN_SPENDING_LIMIT_REACHED)
                    if (webRequest.downloadHandler != null)
                    {
                        try
                        {
                            TryHandleBillingError(webRequest.downloadHandler.text, (int)webRequest.responseCode);
                        }
                        catch { /* ignore parse failures in streaming error path */ }
                    }
                }
                finally
                {
                    onFinally?.Invoke();
                }
            }
        }

        /// <summary>
        /// Try to parse an error response and fire appropriate events on the PlayerClient.
        /// Handles TOKEN_SPENDING_LIMIT_REACHED (distinct from generic insufficient balance).
        /// </summary>
        private void TryHandleBillingError(string responseText, int statusCode)
        {
            if (string.IsNullOrEmpty(responseText)) return;

            try
            {
                var errorResponse = JsonConvert.DeserializeObject<PlayKit_ApiErrorResponse>(responseText);
                var errorCode = errorResponse?.error?.code;
                if (string.IsNullOrEmpty(errorCode)) return;

                var playerClient = _authManager?.PlayerClient;
                if (playerClient == null) return;

                if (errorCode == PlayKit_ErrorCodes.TOKEN_SPENDING_LIMIT_REACHED)
                {
                    // Token spending limit exhausted — wallet may still have balance
                    playerClient.HandleTokenSpendingLimitReached(errorResponse.error.details);
                }
                else if (errorCode == PlayKit_ErrorCodes.PLAYER_INSUFFICIENT_CREDIT ||
                         errorCode == PlayKit_ErrorCodes.INSUFFICIENT_CREDITS ||
                         errorCode == PlayKit_ErrorCodes.INSUFFICIENT_DEVELOPER_BALANCE)
                {
                    // Generic insufficient balance
                    var exception = new PlayKitApiErrorException(
                        errorResponse.error.message ?? "Insufficient credits",
                        errorCode,
                        statusCode
                    );
                    playerClient.HandleInsufficientCredits(exception);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIChatProvider] Failed to parse billing error: {ex.Message}");
            }
        }

        private class StreamingDownloadHandler : DownloadHandlerScript
        {
            private readonly Action<string> _onTextDelta;
            private readonly Action<StreamCompletionResponse> _onLegacyResponse;
            
            public StreamingDownloadHandler(Action<string> onTextDelta, Action<StreamCompletionResponse> onLegacyResponse) 
            { 
                _onTextDelta = onTextDelta;
                _onLegacyResponse = onLegacyResponse;
            }
            
            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength == 0) return true;
                
                var lines = Encoding.UTF8.GetString(data, 0, dataLength).Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    if (line.StartsWith("data: "))
                    {
                        var jsonData = line.Substring(6);
                        if (jsonData.Trim() == "[DONE]") continue;
                        
                        try 
                        { 
                            // Try to parse as UI Message Stream format first
                            var uiMessage = JsonConvert.DeserializeObject<UIMessageStreamResponse>(jsonData);
                            if (uiMessage != null && !string.IsNullOrEmpty(uiMessage.Type))
                            {
                                // Handle UI Message Stream format
                                if (uiMessage.Type == "text-delta" && !string.IsNullOrEmpty(uiMessage.Delta))
                                {
                                    _onTextDelta?.Invoke(uiMessage.Delta);
                                }
                                else if (uiMessage.Type == "finish")
                                {
                                    // Normal stream completion — no action needed,
                                    // onFinally will fire when the HTTP request completes.
                                }
                                else if (uiMessage.Type == "abort")
                                {
                                    // Server-side timeout or cancellation
                                    Debug.LogWarning($"[AIChatProvider] Stream aborted by server: {uiMessage.Reason ?? "unknown reason"}");
                                }
                                else if (uiMessage.Type == "error")
                                {
                                    Debug.LogError($"[AIChatProvider] Stream error from server: {uiMessage.ErrorText ?? "unknown error"}");
                                }
                                continue;
                            }
                        }
                        catch (JsonException)
                        {
                            // Not UI Message Stream format, try legacy format
                        }
                        
                        try
                        {
                            // Fallback to legacy OpenAI compatible format
                            var legacyResponse = JsonConvert.DeserializeObject<StreamCompletionResponse>(jsonData);
                            _onLegacyResponse?.Invoke(legacyResponse);
                        }
                        catch (JsonException ex) 
                        { 
                            Debug.LogWarning($"[AIChatProvider] Failed to parse streaming response: {ex.Message}\nData: {jsonData}");
                        }
                    }
                }
                return true;
            }
        }
    }
}