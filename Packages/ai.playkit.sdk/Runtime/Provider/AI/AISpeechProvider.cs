using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayKit_SDK;
using PlayKit_SDK.Provider;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayKit_SDK.Provider.AI
{
    /// <summary>
    /// Provider for the platform text-to-speech endpoint (/ai/{gameId}/v2/audio/speech).
    /// On success the endpoint returns RAW AUDIO BYTES (not JSON); on failure it returns a JSON error body.
    /// </summary>
    internal class AISpeechProvider : ISpeechProvider
    {
        private const float RETRY_DELAY_SECONDS = 3f;
        private readonly Auth.PlayKit_AuthManager _authManager;

        public AISpeechProvider(Auth.PlayKit_AuthManager authManager, bool useOversea = false)
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

        private string GetSpeechUrl(string suffix = "")
        {
            var settings = PlayKitSettings.Instance;
            if (settings == null || string.IsNullOrEmpty(settings.GameId))
            {
                throw new InvalidOperationException("GameId is not configured in PlayKitSettings.");
            }
            return $"{settings.AIBaseUrl}/v2/audio/speech{suffix}";
        }

        private string GetVoicesUrl()
        {
            var settings = PlayKitSettings.Instance;
            if (settings == null || string.IsNullOrEmpty(settings.GameId))
            {
                throw new InvalidOperationException("GameId is not configured in PlayKitSettings.");
            }
            return $"{settings.AIBaseUrl}/v2/audio/voices";
        }

        private string GetAuthToken()
        {
            if (_authManager == null || string.IsNullOrEmpty(_authManager.AuthToken))
            {
                throw new InvalidOperationException("Authentication token is not available.");
            }
            return _authManager.AuthToken;
        }

        public async UniTask<SpeechAudioResponse> SynthesizeAsync(
            SpeechRequest request,
            int sampleRate,
            int channels,
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
                using (var webRequest = new UnityWebRequest(GetSpeechUrl(), "POST"))
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
                            Debug.LogWarning($"[AISpeechProvider] Request attempt {attempt + 1} failed: {ex.Message}, retrying...");
                            await UniTask.Delay(TimeSpan.FromSeconds(RETRY_DELAY_SECONDS), cancellationToken: cancellationToken);
                            continue;
                        }
                        Debug.LogError($"[AISpeechProvider] API request failed: {ex.Message}");
                        return null;
                    }

                    if (webRequest.result != UnityWebRequest.Result.Success)
                    {
                        if (attempt < maxRetries && IsRetryableError(webRequest))
                        {
                            Debug.LogWarning($"[AISpeechProvider] Request attempt {attempt + 1} failed: {webRequest.responseCode}, retrying...");
                            await UniTask.Delay(TimeSpan.FromSeconds(RETRY_DELAY_SECONDS), cancellationToken: cancellationToken);
                            continue;
                        }
                        // On failure the body is a JSON error payload.
                        Debug.LogError($"[AISpeechProvider] API Error: {webRequest.responseCode} - {webRequest.error}\n{webRequest.downloadHandler.text}");
                        return null;
                    }

                    // SUCCESS: body is raw audio bytes; metadata lives in response headers.
                    var audio = webRequest.downloadHandler.data;
                    if (audio == null || audio.Length == 0)
                    {
                        Debug.LogError("[AISpeechProvider] API returned success but empty audio body.");
                        return null;
                    }

                    int usageCharacters = 0;
                    int.TryParse(webRequest.GetResponseHeader("X-Usage-Characters"), out usageCharacters);

                    float? audioLengthMs = null;
                    if (float.TryParse(webRequest.GetResponseHeader("X-Audio-Length-Ms"), out var ms))
                    {
                        audioLengthMs = ms;
                    }

                    var contentType = webRequest.GetResponseHeader("Content-Type");

                    return new SpeechAudioResponse
                    {
                        Audio = audio,
                        // The route does not return the audio format/sample rate; we assume what we requested.
                        Format = string.IsNullOrEmpty(contentType) ? "pcm" : contentType,
                        UsageCharacters = usageCharacters,
                        AudioLengthMs = audioLengthMs,
                        SampleRate = sampleRate,
                        Channels = channels
                    };
                }
            }

            return null;
        }

        public async UniTask<SpeechAudioResponse> SynthesizeWithTimestampsAsync(
            SpeechRequest request,
            int sampleRate,
            int channels,
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
                using (var webRequest = new UnityWebRequest(GetSpeechUrl("-with-timestamps"), "POST"))
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
                            Debug.LogWarning($"[AISpeechProvider] Timestamps attempt {attempt + 1} failed: {ex.Message}, retrying...");
                            await UniTask.Delay(TimeSpan.FromSeconds(RETRY_DELAY_SECONDS), cancellationToken: cancellationToken);
                            continue;
                        }
                        Debug.LogError($"[AISpeechProvider] API request failed: {ex.Message}");
                        return null;
                    }

                    if (webRequest.result != UnityWebRequest.Result.Success)
                    {
                        if (attempt < maxRetries && IsRetryableError(webRequest))
                        {
                            Debug.LogWarning($"[AISpeechProvider] Timestamps attempt {attempt + 1} failed: {webRequest.responseCode}, retrying...");
                            await UniTask.Delay(TimeSpan.FromSeconds(RETRY_DELAY_SECONDS), cancellationToken: cancellationToken);
                            continue;
                        }
                        Debug.LogError($"[AISpeechProvider] API Error: {webRequest.responseCode} - {webRequest.error}\n{webRequest.downloadHandler.text}");
                        return null;
                    }

                    // SUCCESS: body is a JSON envelope (base64 audio + alignment).
                    SpeechTimestampsEnvelope envelope;
                    try
                    {
                        envelope = JsonConvert.DeserializeObject<SpeechTimestampsEnvelope>(webRequest.downloadHandler.text);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AISpeechProvider] Failed to parse timestamps envelope: {ex.Message}");
                        return null;
                    }

                    if (envelope == null || string.IsNullOrEmpty(envelope.AudioBase64))
                    {
                        Debug.LogError("[AISpeechProvider] Timestamps response missing audio.");
                        return null;
                    }

                    byte[] audioBytes;
                    try
                    {
                        audioBytes = Convert.FromBase64String(envelope.AudioBase64);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AISpeechProvider] Failed to decode base64 audio: {ex.Message}");
                        return null;
                    }

                    return new SpeechAudioResponse
                    {
                        Audio = audioBytes,
                        Format = string.IsNullOrEmpty(envelope.Format) ? "pcm" : envelope.Format,
                        UsageCharacters = envelope.UsageCharacters,
                        AudioLengthMs = envelope.AudioLengthMs,
                        SampleRate = sampleRate,
                        Channels = channels,
                        Alignment = envelope.Alignment
                    };
                }
            }

            return null;
        }

        public async UniTask<SpeechVoicesResponse> ListVoicesAsync(
            CancellationToken cancellationToken = default)
        {
            int maxRetries = GetMaxRetryCount();
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                using (var webRequest = UnityWebRequest.Get(GetVoicesUrl()))
                {
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
                            Debug.LogWarning($"[AISpeechProvider] Voices attempt {attempt + 1} failed: {ex.Message}, retrying...");
                            await UniTask.Delay(TimeSpan.FromSeconds(RETRY_DELAY_SECONDS), cancellationToken: cancellationToken);
                            continue;
                        }
                        Debug.LogError($"[AISpeechProvider] API request failed: {ex.Message}");
                        return null;
                    }

                    if (webRequest.result != UnityWebRequest.Result.Success)
                    {
                        if (attempt < maxRetries && IsRetryableError(webRequest))
                        {
                            Debug.LogWarning($"[AISpeechProvider] Voices attempt {attempt + 1} failed: {webRequest.responseCode}, retrying...");
                            await UniTask.Delay(TimeSpan.FromSeconds(RETRY_DELAY_SECONDS), cancellationToken: cancellationToken);
                            continue;
                        }
                        Debug.LogError($"[AISpeechProvider] API Error: {webRequest.responseCode} - {webRequest.error}\n{webRequest.downloadHandler.text}");
                        return null;
                    }

                    SpeechVoicesResponse response;
                    try
                    {
                        response = JsonConvert.DeserializeObject<SpeechVoicesResponse>(webRequest.downloadHandler.text);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AISpeechProvider] Failed to parse voices response: {ex.Message}");
                        return null;
                    }

                    if (response == null || response.Voices == null)
                    {
                        Debug.LogError("[AISpeechProvider] Voices response missing voice list.");
                        return null;
                    }

                    return response;
                }
            }

            return null;
        }
    }
}
