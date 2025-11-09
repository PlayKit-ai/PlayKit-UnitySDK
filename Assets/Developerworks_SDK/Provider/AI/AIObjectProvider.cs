using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayKit_SDK.Provider.AI
{
    /// <summary>
    /// Provider for the platform AI object generation endpoint (/ai/{gameId}/v1/generateObject)
    /// Uses platform-hosted models for structured object generation
    /// </summary>
    internal class AIObjectProvider : IObjectProvider
    {
        private readonly Auth.PlayKit_AuthManager _authManager;
        private readonly bool _useOversea = false;

        public AIObjectProvider(Auth.PlayKit_AuthManager authManager, bool useOversea = false)
        {
            _authManager = authManager;
            _useOversea = useOversea;
        }

        private string GetObjectUrl()
        {
            if (_authManager == null || string.IsNullOrEmpty(_authManager.gameId))
            {
                throw new InvalidOperationException("PublishableKey (GameId) is not available from AuthManager.");
            }
            if (_useOversea)
            {
                return $"https://dwoversea.agentlandlab.com/ai/{_authManager.gameId}/v1/generateObject";
            }
            return $"https://playkit.agentlandlab.com/ai/{_authManager.gameId}/v1/generateObject";
        }

        private string GetAuthToken()
        {
            if (_authManager == null || string.IsNullOrEmpty(_authManager.AuthToken))
            {
                throw new InvalidOperationException("Authentication token is not available.");
            }
            return _authManager.AuthToken;
        }

        public async UniTask<ObjectGenerationResponse<T>> GenerateObjectAsync<T>(
            ObjectGenerationRequest request,
            System.Threading.CancellationToken cancellationToken = default)
        {
            Debug.Log($"[AIObjectProvider] GenerateObjectAsync for schema: {request.SchemaName}");

            // Validate request
            if (string.IsNullOrEmpty(request.Model))
            {
                throw new ArgumentException("Model is required for object generation");
            }

            if (string.IsNullOrEmpty(request.Prompt))
            {
                throw new ArgumentException("Prompt is required for object generation");
            }

            var json = JsonConvert.SerializeObject(request, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            Debug.Log($"[AIObjectProvider] Request JSON: {json}");

            using (var webRequest = new UnityWebRequest(GetObjectUrl(), "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(new UTF8Encoding().GetBytes(json));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", $"Bearer {GetAuthToken()}");

                try
                {
                    await webRequest.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    Debug.LogError($"[AIObjectProvider] API request failed: {ex.Message}");
                    return null;
                }

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[AIObjectProvider] API Error: {webRequest.responseCode} - {webRequest.error}\n{webRequest.downloadHandler.text}");
                    return null;
                }

                // Parse response
                try
                {
                    // First parse as generic object response
                    var genericResponse = JsonConvert.DeserializeObject<ObjectGenerationResponse<object>>(webRequest.downloadHandler.text);
                    
                    if (genericResponse == null)
                    {
                        Debug.LogError("[AIObjectProvider] Failed to parse response as generic object");
                        return null;
                    }

                    // Then create typed response
                    var typedResponse = new ObjectGenerationResponse<T>
                    {
                        FinishReason = genericResponse.FinishReason,
                        Usage = genericResponse.Usage,
                        Model = genericResponse.Model,
                        Id = genericResponse.Id,
                        Timestamp = genericResponse.Timestamp
                    };

                    // Convert the object to the target type
                    if (genericResponse.Object != null)
                    {
                        try
                        {
                            string objectJson = JsonConvert.SerializeObject(genericResponse.Object);
                            typedResponse.Object = JsonConvert.DeserializeObject<T>(objectJson);
                        }
                        catch (JsonException ex)
                        {
                            Debug.LogError($"[AIObjectProvider] Failed to deserialize object to type {typeof(T).Name}: {ex.Message}");
                            return null;
                        }
                    }

                    Debug.Log($"[AIObjectProvider] Successfully generated structured object of type {typeof(T).Name}");
                    return typedResponse;
                }
                catch (JsonException ex)
                {
                    Debug.LogError($"[AIObjectProvider] Failed to parse response: {ex.Message}\nResponse: {webRequest.downloadHandler.text}");
                    return null;
                }
            }
        }

        public async UniTask<ObjectGenerationResponse<object>> GenerateObjectAsync(
            ObjectGenerationRequest request,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return await GenerateObjectAsync<object>(request, cancellationToken);
        }
    }

    /// <summary>
    /// Interface for object generation providers
    /// </summary>
    internal interface IObjectProvider
    {
        UniTask<ObjectGenerationResponse<T>> GenerateObjectAsync<T>(
            ObjectGenerationRequest request,
            System.Threading.CancellationToken cancellationToken = default);

        UniTask<ObjectGenerationResponse<object>> GenerateObjectAsync(
            ObjectGenerationRequest request,
            System.Threading.CancellationToken cancellationToken = default);
    }
}