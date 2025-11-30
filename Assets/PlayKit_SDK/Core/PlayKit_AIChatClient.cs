using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using PlayKit_SDK.Provider.AI;

namespace PlayKit_SDK
{
    public class PlayKit_AIChatClient
    {
        private readonly string _model;
        private readonly Services.ChatService _chatService;
        private readonly IObjectProvider _objectProvider;
        private PlayKit_SchemaLibrary _schemaLibrary;

        internal PlayKit_AIChatClient(string model, Services.ChatService chatService, IObjectProvider objectProvider)
        {
            _model = model;
            _chatService = chatService;
            _objectProvider = objectProvider;
            LoadDefaultSchemaLibrary();
        }

        /// <summary>
        /// Get the model name this client is using
        /// </summary>
        public string ModelName => _model;

        /// <summary>
        /// Set a custom schema library for structured output
        /// </summary>
        /// <param name="schemaLibrary">The schema library to use</param>
        public void SetSchemaLibrary(PlayKit_SchemaLibrary schemaLibrary)
        {
            _schemaLibrary = schemaLibrary;
        }

        /// <summary>
        /// Load the default schema library from Resources
        /// </summary>
        private void LoadDefaultSchemaLibrary()
        {
            _schemaLibrary = Resources.Load<PlayKit_SchemaLibrary>("SchemaLibrary");
            if (_schemaLibrary == null)
            {
                Debug.LogWarning("[DW_AIChatClient] No default SchemaLibrary found at Resources/SchemaLibrary. You can create one or set a custom library with SetSchemaLibrary()");
            }
        }

        /// <summary>
        /// Generate text using chat completion
        /// </summary>
        public async UniTask<Public.DW_AIResult<string>> TextGenerationAsync(Public.DW_ChatConfig config, CancellationToken cancellationToken = default)
        {
            return await _chatService.RequestAsync(_model, config, cancellationToken);
        }

        /// <summary>
        /// Generate text using streaming chat completion
        /// </summary>
        public async UniTask TextChatStreamAsync(Public.DW_ChatStreamConfig config, Action<string> onNewChunk, Action<string> onConcluded, CancellationToken cancellationToken = default)
        {
            await _chatService.RequestStreamAsync(_model, config, onNewChunk, onConcluded, cancellationToken);
        }

        /// <summary>
        /// Generate a structured object using a schema name, returns JObject for maximum flexibility
        /// </summary>
        /// <param name="schemaName">Name of the schema to use</param>
        /// <param name="prompt">Text prompt for generation</param>
        /// <param name="systemMessage">Optional system message</param>
        /// <param name="temperature">Optional temperature (0.0 to 2.0)</param>
        /// <param name="maxTokens">Optional maximum tokens</param>
        /// <returns>Generated object as JObject, or null if failed</returns>
        public async UniTask<JObject> GenerateStructuredAsync(
            string schemaName,
            string prompt,
            string systemMessage = null,
            float? temperature = null,
            int? maxTokens = null,
            CancellationToken cancellationToken = default)
        {
            if (_schemaLibrary == null)
            {
                Debug.LogError("[DW_AIChatClient] No schema library available. Please set one with SetSchemaLibrary() or create Resources/SchemaLibrary");
                return null;
            }

            if (string.IsNullOrEmpty(schemaName))
            {
                Debug.LogError("[DW_AIChatClient] Schema name cannot be empty");
                return null;
            }

            if (string.IsNullOrEmpty(prompt))
            {
                Debug.LogError("[DW_AIChatClient] Prompt cannot be empty");
                return null;
            }

            var schemaEntry = _schemaLibrary.FindSchema(schemaName);
            if (schemaEntry == null)
            {
                Debug.LogError($"[DW_AIChatClient] Schema '{schemaName}' not found in library");
                return null;
            }

            if (!schemaEntry.IsValid())
            {
                Debug.LogError($"[DW_AIChatClient] Schema '{schemaName}' is invalid");
                return null;
            }

            var parsedSchema = schemaEntry.GetParsedSchema();
            if (parsedSchema == null)
            {
                Debug.LogError($"[DW_AIChatClient] Failed to parse schema '{schemaName}'");
                return null;
            }

            var request = new ObjectGenerationRequest
            {
                Model = _model,
                Prompt = prompt,
                Schema = parsedSchema.ToObject<object>(),
                Output = "object",
                SchemaName = schemaName,
                SchemaDescription = schemaEntry.description,
                System = systemMessage,
                Temperature = temperature,
                MaxTokens = maxTokens
            };

            try
            {
                var response = await _objectProvider.GenerateObjectAsync<object>(request, cancellationToken);

                if (response?.Object != null)
                {
                    // Convert the response object to JObject for maximum flexibility
                    if (response.Object is JObject jobject)
                    {
                        return jobject;
                    }
                    else
                    {
                        // Convert any object to JObject
                        var json = JsonConvert.SerializeObject(response.Object);
                        return JObject.Parse(json);
                    }
                }
                else
                {
                    Debug.LogWarning($"[DW_AIChatClient] No object returned for schema '{schemaName}'");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DW_AIChatClient] Structured generation failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Generate a structured object and deserialize to a specific type
        /// </summary>
        /// <typeparam name="T">The type to deserialize to</typeparam>
        /// <param name="schemaName">Name of the schema to use</param>
        /// <param name="prompt">Text prompt for generation</param>
        /// <param name="systemMessage">Optional system message</param>
        /// <param name="temperature">Optional temperature (0.0 to 2.0)</param>
        /// <param name="maxTokens">Optional maximum tokens</param>
        /// <returns>Generated object deserialized to type T, or default(T) if failed</returns>
        public async UniTask<T> GenerateStructuredAsync<T>(
            string schemaName,
            string prompt,
            string systemMessage = null,
            float? temperature = null,
            int? maxTokens = null,
            CancellationToken cancellationToken = default)
        {
            var jobject = await GenerateStructuredAsync(schemaName, prompt, systemMessage, temperature, maxTokens, cancellationToken);
            
            if (jobject == null)
            {
                return default;
            }

            try
            {
                return jobject.ToObject<T>();
            }
            catch (JsonException ex)
            {
                Debug.LogError($"[DW_AIChatClient] Failed to deserialize to type {typeof(T).Name}: {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// Generate a structured object using messages (conversation format)
        /// </summary>
        /// <param name="schemaName">Name of the schema to use</param>
        /// <param name="messages">List of conversation messages</param>
        /// <param name="temperature">Optional temperature (0.0 to 2.0)</param>
        /// <param name="maxTokens">Optional maximum tokens</param>
        /// <returns>Generated object as JObject, or null if failed</returns>
        public async UniTask<JObject> GenerateStructuredAsync(
            string schemaName,
            List<Public.DW_ChatMessage> messages,
            float? temperature = null,
            int? maxTokens = null,
            CancellationToken cancellationToken = default)
        {
            if (_schemaLibrary == null)
            {
                Debug.LogError("[DW_AIChatClient] No schema library available. Please set one with SetSchemaLibrary() or create Resources/SchemaLibrary");
                return null;
            }

            if (string.IsNullOrEmpty(schemaName))
            {
                Debug.LogError("[DW_AIChatClient] Schema name cannot be empty");
                return null;
            }

            if (messages == null || messages.Count == 0)
            {
                Debug.LogError("[DW_AIChatClient] Messages cannot be null or empty");
                return null;
            }

            var schemaEntry = _schemaLibrary.FindSchema(schemaName);
            if (schemaEntry == null)
            {
                Debug.LogError($"[DW_AIChatClient] Schema '{schemaName}' not found in library");
                return null;
            }

            if (!schemaEntry.IsValid())
            {
                Debug.LogError($"[DW_AIChatClient] Schema '{schemaName}' is invalid");
                return null;
            }

            var parsedSchema = schemaEntry.GetParsedSchema();
            if (parsedSchema == null)
            {
                Debug.LogError($"[DW_AIChatClient] Failed to parse schema '{schemaName}'");
                return null;
            }

            var request = new ObjectGenerationRequest
            {
                Model = _model,
                Messages = messages,
                Schema = parsedSchema.ToObject<object>(),
                Output = "object",
                SchemaName = schemaName,
                SchemaDescription = schemaEntry.description,
                Temperature = temperature,
                MaxTokens = maxTokens
            };

            try
            {
                var response = await _objectProvider.GenerateObjectAsync<object>(request, cancellationToken);

                if (response?.Object != null)
                {
                    // Convert the response object to JObject for maximum flexibility
                    if (response.Object is JObject jobject)
                    {
                        return jobject;
                    }
                    else
                    {
                        // Convert any object to JObject
                        var json = JsonConvert.SerializeObject(response.Object);
                        return JObject.Parse(json);
                    }
                }
                else
                {
                    Debug.LogWarning($"[DW_AIChatClient] No object returned for schema '{schemaName}' with messages");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DW_AIChatClient] Structured generation with messages failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Generate a structured object using messages and deserialize to a specific type
        /// </summary>
        /// <typeparam name="T">The type to deserialize to</typeparam>
        /// <param name="schemaName">Name of the schema to use</param>
        /// <param name="messages">List of conversation messages</param>
        /// <param name="temperature">Optional temperature (0.0 to 2.0)</param>
        /// <param name="maxTokens">Optional maximum tokens</param>
        /// <returns>Generated object deserialized to type T, or default(T) if failed</returns>
        public async UniTask<T> GenerateStructuredAsync<T>(
            string schemaName,
            List<Public.DW_ChatMessage> messages,
            float? temperature = null,
            int? maxTokens = null,
            CancellationToken cancellationToken = default)
        {
            var jobject = await GenerateStructuredAsync(schemaName, messages, temperature, maxTokens, cancellationToken);
            
            if (jobject == null)
            {
                return default;
            }

            try
            {
                return jobject.ToObject<T>();
            }
            catch (JsonException ex)
            {
                Debug.LogError($"[DW_AIChatClient] Failed to deserialize to type {typeof(T).Name}: {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// Generate a structured object using direct schema JSON (without schema library)
        /// </summary>
        /// <param name="schemaJson">JSON schema string</param>
        /// <param name="prompt">Text prompt for generation</param>
        /// <param name="schemaName">Optional schema name for logging</param>
        /// <param name="systemMessage">Optional system message</param>
        /// <param name="temperature">Optional temperature (0.0 to 2.0)</param>
        /// <param name="maxTokens">Optional maximum tokens</param>
        /// <returns>Generated object as JObject, or null if failed</returns>
        public async UniTask<JObject> GenerateStructuredWithSchemaAsync(
            string schemaJson,
            string prompt,
            string schemaName = "DirectSchema",
            string systemMessage = null,
            float? temperature = null,
            int? maxTokens = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(schemaJson))
            {
                Debug.LogError("[DW_AIChatClient] Schema JSON cannot be empty");
                return null;
            }

            if (string.IsNullOrEmpty(prompt))
            {
                Debug.LogError("[DW_AIChatClient] Prompt cannot be empty");
                return null;
            }

            JObject parsedSchema;
            try
            {
                parsedSchema = JObject.Parse(schemaJson);
            }
            catch (JsonException ex)
            {
                Debug.LogError($"[DW_AIChatClient] Invalid schema JSON: {ex.Message}");
                return null;
            }

            var request = new ObjectGenerationRequest
            {
                Model = _model,
                Prompt = prompt,
                Schema = parsedSchema.ToObject<object>(),
                Output = "object",
                SchemaName = schemaName,
                System = systemMessage,
                Temperature = temperature,
                MaxTokens = maxTokens
            };

            try
            {
                var response = await _objectProvider.GenerateObjectAsync<object>(request, cancellationToken);
                
                if (response?.Object != null)
                {
                    if (response.Object is JObject jobject)
                    {
                        return jobject;
                    }
                    else
                    {
                        var json = JsonConvert.SerializeObject(response.Object);
                        return JObject.Parse(json);
                    }
                }
                else
                {
                    Debug.LogWarning($"[DW_AIChatClient] No object returned for direct schema");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DW_AIChatClient] Structured generation failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get all available schema names from the current library
        /// </summary>
        /// <returns>Array of schema names</returns>
        public string[] GetAvailableSchemas()
        {
            if (_schemaLibrary == null)
            {
                return new string[0];
            }
            
            return _schemaLibrary.GetValidSchemaNames();
        }

        /// <summary>
        /// Check if a schema exists in the current library
        /// </summary>
        /// <param name="schemaName">Name of the schema to check</param>
        /// <returns>True if schema exists and is valid</returns>
        public bool HasSchema(string schemaName)
        {
            return _schemaLibrary?.HasValidSchema(schemaName) == true;
        }

        /// <summary>
        /// Get the current schema library
        /// </summary>
        public PlayKit_SchemaLibrary CurrentSchemaLibrary => _schemaLibrary;

        /// <summary>
        /// Print chat messages in a pretty format for debugging
        /// </summary>
        /// <param name="messages">List of chat messages to print</param>
        /// <param name="title">Optional title for the chat log</param>
        public static void PrintPrettyChatMessages(List<Public.DW_ChatMessage> messages, string title = "Chat Messages")
        {
            if (messages == null || messages.Count == 0)
            {
                Debug.Log($"[ChatClient] {title}: No messages to display");
                return;
            }

            var logBuilder = new System.Text.StringBuilder();
            logBuilder.AppendLine($"=== {title} ===");
            logBuilder.AppendLine($"Total messages: {messages.Count}");
            logBuilder.AppendLine();

            for (int i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                string roleIcon = GetRoleIcon(message.Role);
                string roleDisplay = message.Role.ToUpper().PadRight(9);
                
                logBuilder.AppendLine($"[{i + 1:D2}] {roleIcon} {roleDisplay} │ {message.Content}");
                
                // Add separator between messages for readability
                if (i < messages.Count - 1)
                {
                    logBuilder.AppendLine("     ├─────────────┼─────────────────────────────────");
                }
            }
            
            logBuilder.AppendLine("=== End Chat Messages ===");
            Debug.Log(logBuilder.ToString());
        }

        /// <summary>
        /// Print chat messages array in a pretty format for debugging
        /// </summary>
        /// <param name="messages">Array of chat messages to print</param>
        /// <param name="title">Optional title for the chat log</param>
        public static void PrintPrettyChatMessages(Public.DW_ChatMessage[] messages, string title = "Chat Messages")
        {
            PrintPrettyChatMessages(messages?.ToList() ?? new List<Public.DW_ChatMessage>(), title);
        }

        /// <summary>
        /// Get an icon for different message roles
        /// </summary>
        /// <param name="role">The message role</param>
        /// <returns>Icon representing the role</returns>
        private static string GetRoleIcon(string role)
        {
            return role?.ToLower() switch
            {
                "system" => "🔧",
                "user" => "👤",
                "assistant" => "🤖",
                "tool" => "⚙️",
                _ => "❓"
            };
        }

        #region Tool Calling Support

        /// <summary>
        /// Generate text with tool calling support
        /// </summary>
        /// <param name="config">Chat configuration</param>
        /// <param name="tools">List of tools available for the model to use</param>
        /// <param name="toolChoice">Tool choice: "auto", "required", "none", or a specific tool</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Chat result including potential tool calls</returns>
        public async UniTask<Public.DW_AIResult<ChatCompletionResponse>> TextGenerationWithToolsAsync(
            Public.DW_ChatConfig config,
            List<ChatTool> tools,
            object toolChoice = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _chatService.RequestWithToolsAsync(_model, config, tools, toolChoice, cancellationToken);
                if (response != null)
                {
                    return new Public.DW_AIResult<ChatCompletionResponse>(response);
                }
                return new Public.DW_AIResult<ChatCompletionResponse>("Request failed");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatClient] TextGenerationWithToolsAsync error: {ex.Message}");
                return new Public.DW_AIResult<ChatCompletionResponse>(ex.Message);
            }
        }

        /// <summary>
        /// Generate text with tool calling support (streaming mode)
        /// Text chunks are streamed first, tool calls are returned in the onComplete callback
        /// </summary>
        /// <param name="config">Chat configuration</param>
        /// <param name="tools">List of tools available for the model to use</param>
        /// <param name="onTextChunk">Callback for each text chunk</param>
        /// <param name="onComplete">Callback when complete, includes tool calls if any</param>
        /// <param name="toolChoice">Tool choice: "auto", "required", "none", or a specific tool</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async UniTask TextGenerationWithToolsStreamAsync(
            Public.DW_ChatStreamConfig config,
            List<ChatTool> tools,
            Action<string> onTextChunk,
            Action<ChatCompletionResponse> onComplete,
            object toolChoice = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _chatService.RequestWithToolsStreamAsync(
                    _model,
                    config,
                    tools,
                    toolChoice,
                    onTextChunk,
                    onComplete,
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatClient] TextGenerationWithToolsStreamAsync error: {ex.Message}");
                onComplete?.Invoke(null);
            }
        }

        #endregion
    }
}