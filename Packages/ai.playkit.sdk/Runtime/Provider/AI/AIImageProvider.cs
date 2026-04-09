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
    /// Provider for the platform AI image endpoint (/ai/{gameId}/v2/image)
    /// Uses platform-hosted image models with game-based routing
    /// </summary>
    internal class AIImageProvider : IImageProvider
    {
        private const float RETRY_DELAY_SECONDS = 3f;
        private readonly Auth.PlayKit_AuthManager _authManager;

        public AIImageProvider(Auth.PlayKit_AuthManager authManager, bool useOversea = false)
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

        private string GetImageUrl()
        {
            var settings = PlayKitSettings.Instance;
            if (settings == null || string.IsNullOrEmpty(settings.GameId))
            {
                throw new InvalidOperationException("GameId is not configured in PlayKitSettings.");
            }
            return $"{settings.AIBaseUrl}/v2/image";
        }

        private string GetAuthToken()
        {
            if (_authManager == null || string.IsNullOrEmpty(_authManager.AuthToken))
            {
                throw new InvalidOperationException("Authentication token is not available.");
            }
            return _authManager.AuthToken;
        }

        public async UniTask<ImageGenerationResponse> GenerateImageAsync(
            ImageGenerationRequest request, 
            System.Threading.CancellationToken cancellationToken = default)
        {
            // Debug.Log("[AIImageProvider] GenerateImageAsync");
            
            // Validate request
            if (string.IsNullOrEmpty(request.Model))
            {
                throw new ArgumentException("Model is required for image generation");
            }
            
            if (string.IsNullOrEmpty(request.Prompt))
            {
                throw new ArgumentException("Prompt is required for image generation");
            }
            
            var json = JsonConvert.SerializeObject(request, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var postData = new UTF8Encoding().GetBytes(json);

            int maxRetries = GetMaxRetryCount();
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                using (var webRequest = new UnityWebRequest(GetImageUrl(), "POST"))
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
                    catch (UnityWebRequestException ex) when (!(ex is OperationCanceledException))
                    {
                        // Check if we have response data to parse for non-retryable errors
                        if (webRequest.downloadHandler != null && !string.IsNullOrEmpty(webRequest.downloadHandler.text))
                        {
                            try
                            {
                                var errorResponse = JsonConvert.DeserializeObject<PlayKit_ApiErrorResponse>(webRequest.downloadHandler.text);
                                if (errorResponse?.error != null)
                                {
                                    if (errorResponse.error.code == PlayKit_ErrorCodes.INVALID_SIZE_FORMAT ||
                                        errorResponse.error.code == PlayKit_ErrorCodes.INVALID_SIZE_VALUE ||
                                        errorResponse.error.code == PlayKit_ErrorCodes.SIZE_EXCEEDS_LIMIT ||
                                        errorResponse.error.code == PlayKit_ErrorCodes.SIZE_NOT_MULTIPLE ||
                                        errorResponse.error.code == PlayKit_ErrorCodes.SIZE_NOT_ALLOWED)
                                    {
                                        throw new PlayKitImageSizeValidationException(
                                            errorResponse.error.message,
                                            errorResponse.error.code,
                                            request.Size
                                        );
                                    }

                                    throw new PlayKitApiErrorException(
                                        errorResponse.error.message,
                                        errorResponse.error.code,
                                        (int)webRequest.responseCode
                                    );
                                }
                            }
                            catch (JsonException)
                            {
                                Debug.LogError($"[AIImageProvider] Failed to parse error response: {webRequest.downloadHandler.text}");
                            }
                        }

                        // Retry on transient errors
                        if (attempt < maxRetries && IsRetryableError(webRequest))
                        {
                            Debug.LogWarning($"[AIImageProvider] Request attempt {attempt + 1} failed: {ex.Message}, retrying...");
                            await UniTask.Delay(TimeSpan.FromSeconds(RETRY_DELAY_SECONDS), cancellationToken: cancellationToken);
                            continue;
                        }

                        Debug.LogError($"[AIImageProvider] API request failed: {ex.Message}");
                        throw new PlayKitException($"Network request failed: {ex.Message}", ex);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException) && !(ex is PlayKitImageSizeValidationException) && !(ex is PlayKitApiErrorException) && !(ex is PlayKitException))
                    {
                        Debug.LogError($"[AIImageProvider] Unexpected error: {ex.Message}");
                        throw new PlayKitException($"Unexpected error: {ex.Message}", ex);
                    }

                    if (webRequest.result != UnityWebRequest.Result.Success)
                    {
                        // Check for non-retryable API errors first
                        try
                        {
                            var errorResponse = JsonConvert.DeserializeObject<PlayKit_ApiErrorResponse>(webRequest.downloadHandler.text);
                            if (errorResponse?.error != null)
                            {
                                if (errorResponse.error.code == PlayKit_ErrorCodes.INVALID_SIZE_FORMAT ||
                                    errorResponse.error.code == PlayKit_ErrorCodes.INVALID_SIZE_VALUE ||
                                    errorResponse.error.code == PlayKit_ErrorCodes.SIZE_EXCEEDS_LIMIT ||
                                    errorResponse.error.code == PlayKit_ErrorCodes.SIZE_NOT_MULTIPLE ||
                                    errorResponse.error.code == PlayKit_ErrorCodes.SIZE_NOT_ALLOWED)
                                {
                                    throw new PlayKitImageSizeValidationException(
                                        errorResponse.error.message,
                                        errorResponse.error.code,
                                        request.Size
                                    );
                                }

                                // Non-retryable API error
                                if (!IsRetryableError(webRequest))
                                {
                                    throw new PlayKitApiErrorException(
                                        errorResponse.error.message,
                                        errorResponse.error.code,
                                        (int)webRequest.responseCode
                                    );
                                }
                            }
                        }
                        catch (JsonException) { }
                        catch (PlayKitImageSizeValidationException) { throw; }
                        catch (PlayKitApiErrorException) { throw; }

                        // Retry on transient errors
                        if (attempt < maxRetries && IsRetryableError(webRequest))
                        {
                            Debug.LogWarning($"[AIImageProvider] Request attempt {attempt + 1} failed: {webRequest.responseCode}, retrying...");
                            await UniTask.Delay(TimeSpan.FromSeconds(RETRY_DELAY_SECONDS), cancellationToken: cancellationToken);
                            continue;
                        }

                        Debug.LogError($"[AIImageProvider] API Error: {webRequest.responseCode} - {webRequest.error}\n{webRequest.downloadHandler.text}");
                        throw new PlayKitException(
                            $"API request failed with status {webRequest.responseCode}: {webRequest.error}",
                            null,
                            (int)webRequest.responseCode
                        );
                    }

                    // Parse response
                    try
                    {
                        var response = JsonConvert.DeserializeObject<ImageGenerationResponse>(webRequest.downloadHandler.text);

                        if (response == null)
                        {
                            throw new PlayKitException("Image generation failed: server returned empty or invalid response");
                        }

                        if (response.Data == null || response.Data.Count == 0)
                        {
                            throw new PlayKitException("Image generation failed: server returned no image data");
                        }

                        Debug.Log($"[AIImageProvider] Successfully generated {response.Data.Count} images");
                        return response;
                    }
                    catch (JsonException ex)
                    {
                        Debug.LogError($"[AIImageProvider] Failed to parse response: {ex.Message}\nResponse: {webRequest.downloadHandler.text}");
                        throw new PlayKitException($"Failed to parse image generation response: {ex.Message}", ex);
                    }
                }
            }

            throw new PlayKitException("Image generation failed after all retry attempts");
        }
    }
}