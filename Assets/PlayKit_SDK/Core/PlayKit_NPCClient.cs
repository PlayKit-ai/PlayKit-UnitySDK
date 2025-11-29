using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayKit_SDK.Public;
using UnityEngine;

namespace PlayKit_SDK
{
    /// <summary>
    /// A simplified NPC chat client that automatically manages conversation history.
    /// This is a "sugar" wrapper around DW_AIChatClient for easier usage.
    /// </summary>
    public class PlayKit_NPCClient : MonoBehaviour
    {
        [Tooltip("Character design/system prompt for this NPC 该NPC的角色设定/系统提示词")]
        
        [SerializeField] private string characterDesign;
        [Tooltip("Chat model name to use (leave empty to use SDK default) 使用的对话模型名称（留空则使用SDK默认值）")]
        [SerializeField] private string chatModel;
        public string CharacterDesign=>characterDesign;
        private PlayKit_AIChatClient _chatClient;
        private List<DW_ChatMessage> _conversationHistory;
        private string _currentPrompt;
        private bool _isTalking;
        public bool IsTalking { get { return _isTalking; } }
        private bool _isReady;
        public bool IsReady { get { return _isReady; } }

        public void Setup (PlayKit_AIChatClient chatClient)
        {
            _chatClient = chatClient;
            _isReady = true;
            
            Debug.Log($"[NPCClient] Using model '{chatClient.ModelName}' for both chat and structured responses");
        }
        
        private void Start()
        {
            _conversationHistory = new List<DW_ChatMessage>();
            Initialize().Forget();
        }

        private async UniTask Initialize()
        {
            await UniTask.WaitUntil(() => PlayKit_SDK.IsReady());
            if(!string.IsNullOrEmpty(characterDesign))
                SetSystemPrompt(characterDesign);
            if (!string.IsNullOrEmpty(chatModel))
            {
                PlayKit_SDK.Populate.CreateNpc(this,chatModel);
            }
            else
            {
                PlayKit_SDK.Populate.CreateNpc(this);

            }
        }

        /// <summary>
        /// Send a message to the NPC and get a response.
        /// The conversation history is automatically managed.
        /// </summary>
        /// <param name="message">The message to send to the NPC</param>
        /// <param name="cancellationToken">Cancellation token (defaults to OnDestroyCancellationToken)</param>
        /// <returns>The NPC's response</returns>
        public async UniTask<string> Talk(string message, CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            _isTalking = true;

            if (_chatClient == null)
            {
                Debug.LogError("[NPCClient] Chat client not initialized. This indicates that you haven't call DW_SDK.InitializeAsync() first and wait for it to complete.");
                _isTalking = false;
                return null;
            }

            await UniTask.WaitUntil(() => IsReady);
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError("NPC client is not active");
                return null;
            }
            if (string.IsNullOrEmpty(message))
            {
                return null;
            }

            // Add user message to history
            _conversationHistory.Add(new DW_ChatMessage
            {
                Role = "user",
                Content = message
            });

            try
            {
                // Create chat config with full conversation history
                var config = new DW_ChatConfig(_conversationHistory.ToList());
                var result = await _chatClient.TextGenerationAsync(config, token);

                if (result.Success && !string.IsNullOrEmpty(result.Response))
                {
                    // Add assistant response to history
                    _conversationHistory.Add(new DW_ChatMessage
                    {
                        Role = "assistant",
                        Content = result.Response
                    });
                    _isTalking = false;
                    return result.Response;
                }
                else
                {
                    _isTalking = false;
                    return null;
                }
            }
            catch (Exception ex)
            {
                _isTalking = false;

                UnityEngine.Debug.LogError($"[NPCClient] Error in Talk: {ex.Message}");
                return null;
            }

        }

        /// <summary>
        /// Send a message to the NPC and get a structured response using a schema name.
        /// Returns a JObject for maximum flexibility - you can access fields dynamically or deserialize to a specific type.
        /// The conversation history is automatically managed.
        /// </summary>
        /// <param name="message">The message to send to the NPC</param>
        /// <param name="schemaName">Name of the schema to use</param>
        /// <param name="cancellationToken">Cancellation token (defaults to OnDestroyCancellationToken)</param>
        /// <returns>The structured response as JObject, or null if failed</returns>
        public async UniTask<Newtonsoft.Json.Linq.JObject> TalkStructured(string message, string schemaName, CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            _isTalking = true;

            if (_chatClient == null)
            {
                Debug.LogError("[NPCClient] Chat client not initialized. Please call DW_SDK.InitializeAsync() first and wait for it to complete.");
                _isTalking = false;
                return null;
            }

            await UniTask.WaitUntil(() => IsReady);

            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError("NPC client is not active");
                _isTalking = false;
                return null;
            }

            if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(schemaName))
            {
                _isTalking = false;
                return null;
            }

            // Build the conversation context from history
            string conversationContext = BuildConversationContext();
            string fullPrompt = string.IsNullOrEmpty(conversationContext) ? message : $"{conversationContext}\n\nUser: {message}";

            try
            {
                // Use ChatClient's structured output capability
                var result = await _chatClient.GenerateStructuredAsync(schemaName, fullPrompt, _currentPrompt, cancellationToken: token);

                if (result != null)
                {
                    // Add user message to history
                    _conversationHistory.Add(new DW_ChatMessage
                    {
                        Role = "user",
                        Content = message
                    });

                    // Smart handling: Look for .talk field in structured response
                    string responseContent = ExtractTalkFromStructuredResponse(result, schemaName);

                    // Add assistant response to history
                    _conversationHistory.Add(new DW_ChatMessage
                    {
                        Role = "assistant",
                        Content = responseContent
                    });

                    _isTalking = false;
                    return result;
                }
                else
                {
                    _isTalking = false;
                    return null;
                }
            }
            catch (Exception ex)
            {
                _isTalking = false;
                UnityEngine.Debug.LogError($"[NPCClient] Error in TalkStructured: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Send a message to the NPC and get a structured response, then deserialize to a specific type.
        /// The conversation history is automatically managed.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the structured response to</typeparam>
        /// <param name="message">The message to send to the NPC</param>
        /// <param name="schemaName">The name of the schema to use</param>
        /// <param name="cancellationToken">Cancellation token (defaults to OnDestroyCancellationToken)</param>
        /// <returns>The structured response deserialized to type T</returns>
        public async UniTask<T> TalkStructured<T>(string message, string schemaName, CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            _isTalking = true;

            if (_chatClient == null)
            {
                Debug.LogError("[NPCClient] Chat client not initialized. Please call DW_SDK.InitializeAsync() first and wait for it to complete.");
                _isTalking = false;
                return default;
            }

            await UniTask.WaitUntil(() => IsReady);

            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError("NPC client is not active");
                _isTalking = false;
                return default;
            }

            if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(schemaName))
            {
                _isTalking = false;
                return default;
            }

            // Build the conversation context from history
            string conversationContext = BuildConversationContext();
            string fullPrompt = string.IsNullOrEmpty(conversationContext) ? message : $"{conversationContext}\n\nUser: {message}";

            try
            {
                // Use ChatClient's structured output capability with generic type
                var result = await _chatClient.GenerateStructuredAsync<T>(schemaName, fullPrompt, _currentPrompt, cancellationToken: token);

                // Add user message to history
                _conversationHistory.Add(new DW_ChatMessage
                {
                    Role = "user",
                    Content = message
                });

                // Smart handling: Look for .talk field in structured response
                var jobject = Newtonsoft.Json.Linq.JObject.FromObject(result);
                string responseContent = ExtractTalkFromStructuredResponse(jobject, schemaName);

                // Add assistant response to history
                _conversationHistory.Add(new DW_ChatMessage
                {
                    Role = "assistant",
                    Content = responseContent
                });

                _isTalking = false;
                return result;
            }
            catch (Exception ex)
            {
                _isTalking = false;
                UnityEngine.Debug.LogError($"[NPCClient] Error in TalkStructured<T>: {ex.Message}");
                return default;
            }
        }


        /// <summary>
        /// Build conversation context from history for structured generation
        /// </summary>
        private string BuildConversationContext()
        {
            if (_conversationHistory.Count == 0) return string.Empty;

            var contextBuilder = new System.Text.StringBuilder();
            
            // Skip system messages when building context for structured generation
            foreach (var message in _conversationHistory)
            {
                if (message.Role == "system") continue;
                
                string role = message.Role == "user" ? "User" : "Assistant";
                contextBuilder.AppendLine($"{role}: {message.Content}");
            }

            return contextBuilder.ToString().Trim();
        }

        /// <summary>
        /// Smart extraction of talk content from structured response.
        /// Looks for [Tt]alk or [Dd]ialogue fields (case-insensitive) and uses them as the conversation content.
        /// Falls back to raw JSON if no talk field is found.
        /// </summary>
        /// <param name="structuredResponse">The structured response JObject</param>
        /// <param name="schemaName">The schema name for logging</param>
        /// <returns>The content to add to conversation history</returns>
        private string ExtractTalkFromStructuredResponse(Newtonsoft.Json.Linq.JObject structuredResponse, string schemaName)
        {
            if (structuredResponse == null)
            {
                return $"[Structured Response: {schemaName}]";
            }

            // Priority fields: [Tt]alk or [Dd]ialogue (case-insensitive)
            string[] priorityFields = { "talk", "Talk", "dialogue", "Dialogue" };
            
            // Check priority fields first
            foreach (string field in priorityFields)
            {
                var talkToken = structuredResponse[field];
                if (talkToken != null && !string.IsNullOrWhiteSpace(talkToken.ToString()))
                {
                    string talkContent = talkToken.ToString();
                    Debug.Log($"[NPCClient] Using '{field}' field from structured response as conversation content");
                    return talkContent;
                }
            }
            
            // Fallback: check other common dialogue fields
            string[] fallbackFields = { "response", "message", "content", "text", "speech", "say" };
            foreach (string field in fallbackFields)
            {
                var talkToken = structuredResponse[field];
                if (talkToken != null && !string.IsNullOrWhiteSpace(talkToken.ToString()))
                {
                    string talkContent = talkToken.ToString();
                    Debug.Log($"[NPCClient] Using fallback '{field}' field from structured response as conversation content");
                    return talkContent;
                }
            }
            
            // No talk field found, use the raw structured response
            Debug.Log($"[NPCClient] No talk/dialogue field found in structured response, using raw JSON");
            return $"[Structured Response: {structuredResponse.ToString(Newtonsoft.Json.Formatting.None)}]";
        }

        /// <summary>
        /// Send a message to the NPC and get a streaming response.
        /// The conversation history is automatically managed.
        /// </summary>
        /// <param name="message">The message to send to the NPC</param>
        /// <param name="onChunk">Called for each piece of the response as it streams in</param>
        /// <param name="onComplete">Called when the complete response is ready</param>
        /// <param name="cancellationToken">Cancellation token (defaults to OnDestroyCancellationToken)</param>
        public async UniTask TalkStream(string message, Action<string> onChunk, Action<string> onComplete, CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            _isTalking = true;

            if (_chatClient == null)
            {
                Debug.LogError("[NPCClient] Chat client not initialized. Please call DW_SDK.InitializeAsync() first and wait for it to complete.");
                _isTalking = false;
                onChunk?.Invoke(null);
                onComplete?.Invoke(null);
                return;
            }

            await UniTask.WaitUntil(() => IsReady);
            if (string.IsNullOrEmpty(message))
            {
                onChunk?.Invoke(null);
                onComplete?.Invoke(null);
                return;
            }

            // Add user message to history
            _conversationHistory.Add(new DW_ChatMessage
            {
                Role = "user",
                Content = message
            });

            try
            {
                var config = new DW_ChatStreamConfig(_conversationHistory.ToList());
                
                await _chatClient.TextChatStreamAsync(config,
                    chunk =>
                    {
                        // Forward each chunk to the caller
                        onChunk?.Invoke(chunk);
                    },
                    completeResponse =>
                    {
                        _isTalking = false;
                        // Add the complete response to conversation history
                        if (!string.IsNullOrEmpty(completeResponse))
                        {
                            _conversationHistory.Add(new DW_ChatMessage
                            {
                                Role = "assistant",
                                Content = completeResponse
                            });
                        }

                        // Notify caller that response is complete
                        onComplete?.Invoke(completeResponse);
                    },
                    token
                );
            }
            catch (Exception ex)
            {
                _isTalking = false;
                UnityEngine.Debug.LogError($"[NPCClient] Error in streaming Talk: {ex.Message}");
                onChunk?.Invoke(null);
                onComplete?.Invoke(null);
            }
        }

        /// <summary>
        /// Set the system prompt for the NPC character.
        /// This will update the conversation history with the new prompt.
        /// </summary>
        /// <param name="prompt">The new system prompt</param>
        public void SetSystemPrompt(string prompt)
        {
            _currentPrompt = prompt;
            
            // Remove existing system message if any
            for (int i = _conversationHistory.Count - 1; i >= 0; i--)
            {
                if (_conversationHistory[i].Role == "system")
                {
                    _conversationHistory.RemoveAt(i);
                }
            }
            
            // Add new system message if we have a prompt
            if (!string.IsNullOrEmpty(_currentPrompt))
            {
                _conversationHistory.Insert(0, new DW_ChatMessage
                {
                    Role = "system",
                    Content = _currentPrompt
                });
            }
        }

        /// <summary>
        /// Revert the last exchange (user message and assistant response) from history.
        /// </summary>
        /// <returns>True if successfully reverted, false if no history to revert</returns>
        public bool RevertHistory()
        {
            // Find the last assistant message and the user message before it
            int lastAssistantIndex = -1;
            int lastUserIndex = -1;
            
            for (int i = _conversationHistory.Count - 1; i >= 0; i--)
            {
                if (_conversationHistory[i].Role == "assistant" && lastAssistantIndex == -1)
                {
                    lastAssistantIndex = i;
                }
                else if (_conversationHistory[i].Role == "user" && lastAssistantIndex != -1 && lastUserIndex == -1)
                {
                    lastUserIndex = i;
                    break;
                }
            }
            
            if (lastAssistantIndex != -1 && lastUserIndex != -1)
            {
                // Remove both messages (assistant first, then user)
                _conversationHistory.RemoveAt(lastAssistantIndex);
                _conversationHistory.RemoveAt(lastUserIndex);
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Save the current conversation history to a serializable format.
        /// </summary>
        /// <returns>Serialized conversation data</returns>
        public string SaveHistory()
        {
            var saveData = new ConversationSaveData
            {
                Prompt = _currentPrompt,
                History = _conversationHistory.ToArray()
            };
            
            return UnityEngine.JsonUtility.ToJson(saveData);
        }

        /// <summary>
        /// Load conversation history from serialized data.
        /// </summary>
        /// <param name="saveData">Serialized conversation data</param>
        /// <returns>True if successfully loaded, false if data is invalid</returns>
        public bool LoadHistory(string saveData)
        {
            try
            {
                var data = UnityEngine.JsonUtility.FromJson<ConversationSaveData>(saveData);
                if (data == null) return false;
                
                _conversationHistory.Clear();
                
                // Set the prompt first
                SetSystemPrompt(data.Prompt);
                
                // Add all non-system messages (system message is already added by SetPrompt)
                foreach (var message in data.History)
                {
                    if (message.Role != "system")
                    {
                        _conversationHistory.Add(message);
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[NPCClient] Failed to load history: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clear the conversation history, starting fresh.
        /// The system prompt (character) will be preserved.
        /// </summary>
        public void ClearHistory()
        {
            _conversationHistory.Clear();
            
            // Re-add system message if we have a prompt
            if (!string.IsNullOrEmpty(_currentPrompt))
            {
                _conversationHistory.Add(new DW_ChatMessage
                {
                    Role = "system",
                    Content = _currentPrompt
                });
            }
        }

        /// <summary>
        /// Get the current conversation history
        /// </summary>
        public DW_ChatMessage[] GetHistory()
        {
            return _conversationHistory.ToArray();
        }

        /// <summary>
        /// Get the number of messages in the conversation history
        /// </summary>
        public int GetHistoryLength()
        {
            return _conversationHistory.Count;
        }

        /// <summary>
        /// Manually append a chat message to the conversation history
        /// </summary>
        /// <param name="role">The role of the message (system, user, assistant)</param>
        /// <param name="content">The content of the message</param>
        public void AppendChatMessage(string role, string content)
        {
            if (string.IsNullOrEmpty(role) || string.IsNullOrEmpty(content))
            {
                Debug.LogWarning("[NPCClient] Role and content cannot be empty when appending chat message");
                return;
            }

            _conversationHistory.Add(new DW_ChatMessage
            {
                Role = role,
                Content = content
            });
        }

        /// <summary>
        /// Revert (remove) the last N chat messages from history
        /// </summary>
        /// <param name="count">Number of messages to remove from the end</param>
        /// <returns>Number of messages actually removed</returns>
        public int RevertChatMessages(int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int messagesToRemove = Mathf.Min(count, _conversationHistory.Count);
            int originalCount = _conversationHistory.Count;

            // Remove from the end
            for (int i = 0; i < messagesToRemove; i++)
            {
                _conversationHistory.RemoveAt(_conversationHistory.Count - 1);
            }

            int actuallyRemoved = originalCount - _conversationHistory.Count;
            Debug.Log($"[NPCClient] Reverted {actuallyRemoved} messages from history. Remaining: {_conversationHistory.Count}");
            
            return actuallyRemoved;
        }

        /// <summary>
        /// Print the current conversation history in a pretty format for debugging
        /// </summary>
        /// <param name="title">Optional title for the chat log</param>
        public void PrintPrettyChatMessages(string title = null)
        {
            string displayTitle = title ?? $"NPC '{gameObject.name}' Conversation History";
            PlayKit_AIChatClient.PrintPrettyChatMessages(_conversationHistory, displayTitle);
        }

        /// <summary>
        /// Send a message to the NPC and get a structured response using the full conversation history as messages.
        /// This method automatically uses the maintained conversation history with messages format.
        /// </summary>
        /// <param name="message">The message to send to the NPC</param>
        /// <param name="schemaName">Name of the schema to use</param>
        /// <param name="cancellationToken">Cancellation token (defaults to OnDestroyCancellationToken)</param>
        /// <returns>The structured response as JObject, or null if failed</returns>
        public async UniTask<Newtonsoft.Json.Linq.JObject> TalkStructuredWithHistory(string message, string schemaName, CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            _isTalking = true;

            if (_chatClient == null)
            {
                Debug.LogError("[NPCClient] Chat client not initialized. Please call DW_SDK.InitializeAsync() first and wait for it to complete.");
                _isTalking = false;
                return null;
            }

            await UniTask.WaitUntil(() => IsReady);

            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError("NPC client is not active");
                _isTalking = false;
                return null;
            }

            if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(schemaName))
            {
                _isTalking = false;
                return null;
            }

            // Add user message to history first
            _conversationHistory.Add(new DW_ChatMessage
            {
                Role = "user",
                Content = message
            });

            try
            {
                // Use ChatClient's structured output with full message history
                var result = await _chatClient.GenerateStructuredAsync(schemaName, _conversationHistory, cancellationToken: token);

                if (result != null)
                {
                    // Smart handling: Look for .talk field in structured response
                    string responseContent = ExtractTalkFromStructuredResponse(result, schemaName);

                    // Add assistant response to history
                    _conversationHistory.Add(new DW_ChatMessage
                    {
                        Role = "assistant",
                        Content = responseContent
                    });

                    _isTalking = false;
                    return result;
                }
                else
                {
                    _isTalking = false;
                    return null;
                }
            }
            catch (Exception ex)
            {
                _isTalking = false;
                UnityEngine.Debug.LogError($"[NPCClient] Error in TalkStructuredWithHistory: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Send a message to the NPC and get a structured response using the full conversation history, then deserialize to a specific type.
        /// This method automatically uses the maintained conversation history with messages format.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the structured response to</typeparam>
        /// <param name="message">The message to send to the NPC</param>
        /// <param name="schemaName">The name of the schema to use</param>
        /// <param name="cancellationToken">Cancellation token (defaults to OnDestroyCancellationToken)</param>
        /// <returns>The structured response deserialized to type T</returns>
        public async UniTask<T> TalkStructuredWithHistory<T>(string message, string schemaName, CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            _isTalking = true;

            if (_chatClient == null)
            {
                Debug.LogError("[NPCClient] Chat client not initialized. Please call DW_SDK.InitializeAsync() first and wait for it to complete.");
                _isTalking = false;
                return default;
            }

            await UniTask.WaitUntil(() => IsReady);

            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError("NPC client is not active");
                _isTalking = false;
                return default;
            }

            if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(schemaName))
            {
                _isTalking = false;
                return default;
            }

            // Add user message to history first
            _conversationHistory.Add(new DW_ChatMessage
            {
                Role = "user",
                Content = message
            });

            try
            {
                // Use ChatClient's structured output with full message history and generic type
                var result = await _chatClient.GenerateStructuredAsync<T>(schemaName, _conversationHistory, cancellationToken: token);

                // Smart handling: Look for .talk field in structured response
                var jobject = Newtonsoft.Json.Linq.JObject.FromObject(result);
                string responseContent = ExtractTalkFromStructuredResponse(jobject, schemaName);

                // Add assistant response to history
                _conversationHistory.Add(new DW_ChatMessage
                {
                    Role = "assistant",
                    Content = responseContent
                });

                _isTalking = false;
                return result;
            }
            catch (Exception ex)
            {
                _isTalking = false;
                UnityEngine.Debug.LogError($"[NPCClient] Error in TalkStructuredWithHistory<T>: {ex.Message}");
                return default;
            }
        }

    }

    /// <summary>
    /// Data structure for saving and loading conversation history
    /// </summary>
    [System.Serializable]
    public class ConversationSaveData
    {
        public string Prompt;
        public DW_ChatMessage[] History;
    }
}