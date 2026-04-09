using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayKit_SDK;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayKit_SDK.Provider.AI
{
    /// <summary>
    /// Provider for the platform audio transcription endpoint (/ai/{gameId}/v2/audio/transcriptions)
    /// </summary>
    internal class AITranscriptionProvider : ITranscriptionProvider
    {
        private const float RETRY_DELAY_SECONDS = 3f;
        private readonly Auth.PlayKit_AuthManager _authManager;

        public AITranscriptionProvider(Auth.PlayKit_AuthManager authManager, bool useOversea = false)
        {
            _authManager = authManager;
            // Note: useOversea parameter is deprecated, use PlayKitSettings.CustomBaseUrl instead
        }

        private static int GetMaxRetryCount()
        {
            var settings = PlayKitSettings.Instance;
            return settings != null ? settings.AIRequestMaxRetryCount : 3;
        }

        private static bool IsRetryableError(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError) return true;
            if (request.result == UnityWebRequest.Result.DataProcessingError) return true;
            var code = (int)request.responseCode;
            return code >= 500 || code == 429 || code == 0;
        }

        private string GetTranscriptionUrl()
        {
            var settings = PlayKitSettings.Instance;
            if (settings == null || string.IsNullOrEmpty(settings.GameId))
            {
                throw new InvalidOperationException("GameId is not configured in PlayKitSettings.");
            }
            return $"{settings.AIBaseUrl}/v2/audio/transcriptions";
        }

        private string GetAuthToken()
        {
            if (_authManager == null || string.IsNullOrEmpty(_authManager.AuthToken))
            {
                throw new InvalidOperationException("Authentication token is not available.");
            }
            return _authManager.AuthToken;
        }

        public async UniTask<TranscriptionResponse> TranscribeAsync(
            TranscriptionRequest request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonConvert.SerializeObject(request, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            var postData = new UTF8Encoding().GetBytes(json);

            int maxRetries = GetMaxRetryCount();
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                using (var webRequest = new UnityWebRequest(GetTranscriptionUrl(), "POST"))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(postData);
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
                        if (attempt < maxRetries && IsRetryableError(webRequest))
                        {
                            Debug.LogWarning($"[AITranscriptionProvider] Request attempt {attempt + 1} failed: {ex.Message}, retrying...");
                            await UniTask.Delay(TimeSpan.FromSeconds(RETRY_DELAY_SECONDS), cancellationToken: cancellationToken);
                            continue;
                        }
                        Debug.LogError($"[AITranscriptionProvider] API request failed: {ex.Message}");
                        return null;
                    }

                    if (webRequest.result != UnityWebRequest.Result.Success)
                    {
                        if (attempt < maxRetries && IsRetryableError(webRequest))
                        {
                            Debug.LogWarning($"[AITranscriptionProvider] Request attempt {attempt + 1} failed: {webRequest.responseCode}, retrying...");
                            await UniTask.Delay(TimeSpan.FromSeconds(RETRY_DELAY_SECONDS), cancellationToken: cancellationToken);
                            continue;
                        }
                        Debug.LogError($"[AITranscriptionProvider] API Error: {webRequest.responseCode} - {webRequest.error}\n{webRequest.downloadHandler.text}");
                        return null;
                    }

                    return JsonConvert.DeserializeObject<TranscriptionResponse>(webRequest.downloadHandler.text);
                }
            }

            return null;
        }
    }
}
