using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Developerworks.SDK;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayKit_SDK.Provider.AI
{
    /// <summary>
    /// Provider for structured object generation using the v2 /chat endpoint with native schema support
    /// </summary>
    internal class AIObjectProvider : IObjectProvider
    {
        private readonly Auth.PlayKit_AuthManager _authManager;

        public AIObjectProvider(Auth.PlayKit_AuthManager authManager, bool useOversea = false)
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

        public async UniTask<ObjectGenerationResponse<T>> GenerateObjectAsync<T>(
            ObjectGenerationRequest request,
            System.Threading.CancellationToken cancellationToken = default)
        {
            Debug.Log($"[AIObjectProvider] GenerateObjectAsync for schema: {request.SchemaName} (using /chat endpoint)");

            // Validate request
            if (string.IsNullOrEmpty(request.Model))
            {
                throw new ArgumentException("Model is required for object generation");
            }

            // Build messages array
            var messages = new List<ChatMessage>();

            // Add system message if provided
            if (!string.IsNullOrEmpty(request.System))
            {
                messages.Add(new ChatMessage { Role = "system", Content = request.System });
            }

            // Add messages from request if provided, otherwise use prompt
            if (request.Messages != null && request.Messages.Count > 0)
            {
                messages.AddRange(request.Messages.Select(m => new ChatMessage
                {
                    Role = m.Role,
                    Content = m.Content
                }));
            }
            else if (!string.IsNullOrEmpty(request.Prompt))
            {
                messages.Add(new ChatMessage { Role = "user", Content = request.Prompt });
            }
            else
            {
                throw new ArgumentException("Either Prompt or Messages is required for object generation");
            }

            // Build the v2 chat completion request with native schema support
            var chatRequest = new Dictionary<string, object>
            {
                ["model"] = request.Model,
                ["messages"] = messages,
                ["stream"] = false,
                // v2 native schema parameters
                ["schema"] = request.Schema,
                ["schemaName"] = request.SchemaName ?? "response",
                ["schemaDescription"] = request.SchemaDescription ?? "",
                ["output"] = request.Output ?? "object"  // object, array, enum, no-schema
            };

            if (request.Temperature.HasValue)
            {
                chatRequest["temperature"] = request.Temperature.Value;
            }

            if (request.MaxTokens.HasValue)
            {
                chatRequest["max_tokens"] = request.MaxTokens.Value;
            }

            var json = JsonConvert.SerializeObject(chatRequest, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            Debug.Log($"[AIObjectProvider] Request JSON: {json}");

            using (var webRequest = new UnityWebRequest(GetChatUrl(), "POST"))
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

                // Parse chat completion response
                try
                {
                    var chatResponse = JsonConvert.DeserializeObject<ChatCompletionResponse>(webRequest.downloadHandler.text);

                    if (chatResponse == null || chatResponse.Choices == null || chatResponse.Choices.Count == 0)
                    {
                        Debug.LogError("[AIObjectProvider] No choices in response");
                        return null;
                    }

                    var choice = chatResponse.Choices[0];
                    var content = choice.Message?.Content;

                    if (string.IsNullOrEmpty(content))
                    {
                        Debug.LogError("[AIObjectProvider] Empty content in response");
                        return null;
                    }

                    // Parse the JSON content from the response
                    var parsedObject = JsonConvert.DeserializeObject<T>(content);

                    var response = new ObjectGenerationResponse<T>
                    {
                        Object = parsedObject,
                        FinishReason = choice.FinishReason,
                        Model = chatResponse.Model,
                        Id = chatResponse.Id,
                        Timestamp = chatResponse.Created.ToString(),
                        Usage = chatResponse.Usage != null ? new ObjectUsage
                        {
                            InputTokens = chatResponse.Usage.PromptTokens,
                            OutputTokens = chatResponse.Usage.CompletionTokens,
                            TotalTokens = chatResponse.Usage.TotalTokens
                        } : null
                    };

                    Debug.Log($"[AIObjectProvider] Successfully generated structured object of type {typeof(T).Name}");
                    return response;
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