using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayKit_SDK.Provider.AI;
using PlayKit_SDK.Public;
using UnityEngine;

namespace PlayKit_SDK
{
    /// <summary>
    /// A simplified NPC chat client that automatically manages conversation history.
    /// This is a "sugar" wrapper around DW_AIChatClient for easier usage.
    ///
    /// Key Features:
    /// - Call Talk() for all interactions - actions are handled automatically
    /// - Add PlayKit_NPCClient_ActionsModule to same GameObject for action support
    /// - Subscribe to OnActionTriggered event to handle action callbacks
    /// </summary>
    public class PlayKit_NPCClient : MonoBehaviour
    {
        [Tooltip("Character design/system prompt for this NPC 该NPC的角色设定/系统提示词")]
        [SerializeField] private string characterDesign;

        [Tooltip("Chat model name to use (leave empty to use SDK default) 使用的对话模型名称（留空则使用SDK默认值）")]
        [SerializeField] private string chatModel;

        public string CharacterDesign => characterDesign;

        private PlayKit_AIChatClient _chatClient;
        private List<DW_ChatMessage> _conversationHistory;
        private string _currentPrompt;
        private bool _isTalking;
        private bool _isReady;

        // Actions integration
        private PlayKit_NPCClient_ActionsModule _actionsModule;

        /// <summary>
        /// Event fired when an action is triggered by the NPC.
        /// Subscribe to this to handle action callbacks.
        /// </summary>
        public event Action<NpcActionCallArgs> OnActionTriggered;

        public bool IsTalking => _isTalking;
        public bool IsReady => _isReady;

        /// <summary>
        /// Whether this NPC has actions configured and enabled
        /// </summary>
        public bool HasEnabledActions => _actionsModule != null && _actionsModule.EnabledActions.Count > 0;

        public void Setup(PlayKit_AIChatClient chatClient)
        {
            _chatClient = chatClient;
            _isReady = true;
            Debug.Log($"[NPCClient] Using model '{chatClient.ModelName}' for chat");
        }

        private void Start()
        {
            _conversationHistory = new List<DW_ChatMessage>();
            Initialize().Forget();
        }

        private async UniTask Initialize()
        {
            await UniTask.WaitUntil(() => PlayKit_SDK.IsReady());

            // Auto-detect ActionsModule on same GameObject
            _actionsModule = GetComponent<PlayKit_NPCClient_ActionsModule>();
            if (_actionsModule != null)
            {
                Debug.Log($"[NPCClient] ActionsModule detected on '{gameObject.name}'");
            }

            if (!string.IsNullOrEmpty(characterDesign))
                SetSystemPrompt(characterDesign);

            if (!string.IsNullOrEmpty(chatModel))
            {
                PlayKit_SDK.Populate.CreateNpc(this, chatModel);
            }
            else
            {
                PlayKit_SDK.Populate.CreateNpc(this);
            }
        }

        #region Main API - Talk Methods

        /// <summary>
        /// Send a message to the NPC and get a response.
        /// If ActionsModule is attached and has enabled actions, tool calling is automatically used.
        /// The conversation history is automatically managed.
        /// </summary>
        /// <param name="message">The message to send to the NPC</param>
        /// <param name="cancellationToken">Cancellation token (defaults to OnDestroyCancellationToken)</param>
        /// <returns>The NPC's text response</returns>
        public async UniTask<string> Talk(string message, CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            _isTalking = true;

            if (_chatClient == null)
            {
                Debug.LogError("[NPCClient] Chat client not initialized. Please call DW_SDK.InitializeAsync() first.");
                _isTalking = false;
                return null;
            }

            await UniTask.WaitUntil(() => IsReady);

            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError("[NPCClient] NPC client is not active");
                _isTalking = false;
                return null;
            }

            if (string.IsNullOrEmpty(message))
            {
                _isTalking = false;
                return null;
            }

            // Check if we should use actions
            if (HasEnabledActions)
            {
                return await TalkWithActionsInternal(message, token);
            }
            else
            {
                return await TalkSimpleInternal(message, token);
            }
        }

        /// <summary>
        /// Send a message to the NPC and get a streaming response.
        /// If ActionsModule is attached and has enabled actions, tool calling is automatically used.
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
                Debug.LogError("[NPCClient] Chat client not initialized. Please call DW_SDK.InitializeAsync() first.");
                _isTalking = false;
                onChunk?.Invoke(null);
                onComplete?.Invoke(null);
                return;
            }

            await UniTask.WaitUntil(() => IsReady);

            if (string.IsNullOrEmpty(message))
            {
                _isTalking = false;
                onChunk?.Invoke(null);
                onComplete?.Invoke(null);
                return;
            }

            // Check if we should use actions
            if (HasEnabledActions)
            {
                await TalkWithActionsStreamInternal(message, onChunk, onComplete, token);
            }
            else
            {
                await TalkSimpleStreamInternal(message, onChunk, onComplete, token);
            }
        }

        #endregion

        #region Internal Implementation

        /// <summary>
        /// Simple talk without actions
        /// </summary>
        private async UniTask<string> TalkSimpleInternal(string message, CancellationToken token)
        {
            // Add user message to history
            _conversationHistory.Add(new DW_ChatMessage
            {
                Role = "user",
                Content = message
            });

            try
            {
                var config = new DW_ChatConfig(_conversationHistory.ToList());
                var result = await _chatClient.TextGenerationAsync(config, token);

                if (result.Success && !string.IsNullOrEmpty(result.Response))
                {
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
                Debug.LogError($"[NPCClient] Error in Talk: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Talk with actions (tool calling)
        /// </summary>
        private async UniTask<string> TalkWithActionsInternal(string message, CancellationToken token)
        {
            // Add user message to history
            _conversationHistory.Add(new DW_ChatMessage
            {
                Role = "user",
                Content = message
            });

            try
            {
                // Get enabled actions from ActionsModule
                var actions = _actionsModule.EnabledActions;
                var tools = actions
                    .Where(a => a != null && a.enabled)
                    .Select(a => a.ToTool())
                    .ToList();

                var config = new DW_ChatConfig(_conversationHistory.ToList());
                var result = await _chatClient.TextGenerationWithToolsAsync(config, tools, "auto", token);

                if (result.Success && result.Response?.Choices?.Count > 0)
                {
                    var choice = result.Response.Choices[0];
                    var responseText = choice.Message?.Content ?? "";

                    // Add assistant response to history
                    _conversationHistory.Add(new DW_ChatMessage
                    {
                        Role = "assistant",
                        Content = responseText,
                        ToolCalls = choice.Message?.ToolCalls
                    });

                    // Process action calls
                    if (choice.Message?.ToolCalls != null)
                    {
                        ProcessActionCalls(choice.Message.ToolCalls);
                    }

                    _isTalking = false;
                    return responseText;
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
                Debug.LogError($"[NPCClient] Error in Talk with actions: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Simple streaming talk without actions
        /// </summary>
        private async UniTask TalkSimpleStreamInternal(string message, Action<string> onChunk, Action<string> onComplete, CancellationToken token)
        {
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
                    chunk => onChunk?.Invoke(chunk),
                    completeResponse =>
                    {
                        _isTalking = false;
                        if (!string.IsNullOrEmpty(completeResponse))
                        {
                            _conversationHistory.Add(new DW_ChatMessage
                            {
                                Role = "assistant",
                                Content = completeResponse
                            });
                        }
                        onComplete?.Invoke(completeResponse);
                    },
                    token
                );
            }
            catch (Exception ex)
            {
                _isTalking = false;
                Debug.LogError($"[NPCClient] Error in streaming Talk: {ex.Message}");
                onChunk?.Invoke(null);
                onComplete?.Invoke(null);
            }
        }

        /// <summary>
        /// Streaming talk with actions
        /// </summary>
        private async UniTask TalkWithActionsStreamInternal(string message, Action<string> onChunk, Action<string> onComplete, CancellationToken token)
        {
            // Add user message to history
            _conversationHistory.Add(new DW_ChatMessage
            {
                Role = "user",
                Content = message
            });

            try
            {
                var actions = _actionsModule.EnabledActions;
                var tools = actions
                    .Where(a => a != null && a.enabled)
                    .Select(a => a.ToTool())
                    .ToList();

                var config = new DW_ChatStreamConfig(_conversationHistory.ToList());

                await _chatClient.TextGenerationWithToolsStreamAsync(
                    config,
                    tools,
                    chunk => onChunk?.Invoke(chunk),
                    completionResponse =>
                    {
                        _isTalking = false;

                        if (completionResponse?.Choices?.Count > 0)
                        {
                            var choice = completionResponse.Choices[0];
                            var responseText = choice.Message?.Content ?? "";

                            // Add assistant response to history
                            _conversationHistory.Add(new DW_ChatMessage
                            {
                                Role = "assistant",
                                Content = responseText,
                                ToolCalls = choice.Message?.ToolCalls
                            });

                            // Process action calls
                            if (choice.Message?.ToolCalls != null)
                            {
                                ProcessActionCalls(choice.Message.ToolCalls);
                            }

                            onComplete?.Invoke(responseText);
                        }
                        else
                        {
                            onComplete?.Invoke(null);
                        }
                    },
                    "auto",
                    token
                );
            }
            catch (Exception ex)
            {
                _isTalking = false;
                Debug.LogError($"[NPCClient] Error in streaming Talk with actions: {ex.Message}");
                onChunk?.Invoke(null);
                onComplete?.Invoke(null);
            }
        }

        /// <summary>
        /// Process action calls and fire events
        /// </summary>
        private void ProcessActionCalls(List<ChatToolCall> toolCalls)
        {
            if (toolCalls == null || toolCalls.Count == 0) return;

            foreach (var toolCall in toolCalls)
            {
                var args = new NpcActionCallArgs(toolCall);

                Debug.Log($"[NPCClient] Action triggered: {args.ActionName} (ID: {args.CallId})");

                // Fire event for external subscribers
                OnActionTriggered?.Invoke(args);

                // Also notify ActionsModule if present (for UnityEvent bindings)
                _actionsModule?.HandleActionCall(args);
            }
        }

        #endregion

        #region Action Results Reporting

        /// <summary>
        /// Report action results back to the conversation.
        /// Call this after executing actions to let the NPC know the results.
        /// </summary>
        /// <param name="results">Dictionary of action call IDs to their results</param>
        public void ReportActionResults(Dictionary<string, string> results)
        {
            if (results == null || results.Count == 0) return;

            foreach (var kvp in results)
            {
                _conversationHistory.Add(new DW_ChatMessage
                {
                    Role = "tool",
                    ToolCallId = kvp.Key,
                    Content = kvp.Value
                });
            }
        }

        /// <summary>
        /// Report a single action result back to the conversation.
        /// </summary>
        /// <param name="callId">The action call ID</param>
        /// <param name="result">The result of the action execution</param>
        public void ReportActionResult(string callId, string result)
        {
            if (string.IsNullOrEmpty(callId)) return;

            _conversationHistory.Add(new DW_ChatMessage
            {
                Role = "tool",
                ToolCallId = callId,
                Content = result ?? ""
            });
        }

        #endregion

        #region Conversation History Management

        /// <summary>
        /// Set the system prompt for the NPC character.
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
        public bool RevertHistory()
        {
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
                _conversationHistory.RemoveAt(lastAssistantIndex);
                _conversationHistory.RemoveAt(lastUserIndex);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Save the current conversation history to a serializable format.
        /// </summary>
        public string SaveHistory()
        {
            var saveData = new ConversationSaveData
            {
                Prompt = _currentPrompt,
                History = _conversationHistory.ToArray()
            };
            return JsonUtility.ToJson(saveData);
        }

        /// <summary>
        /// Load conversation history from serialized data.
        /// </summary>
        public bool LoadHistory(string saveData)
        {
            try
            {
                var data = JsonUtility.FromJson<ConversationSaveData>(saveData);
                if (data == null) return false;

                _conversationHistory.Clear();
                SetSystemPrompt(data.Prompt);

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
                Debug.LogError($"[NPCClient] Failed to load history: {ex.Message}");
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
        public DW_ChatMessage[] GetHistory() => _conversationHistory.ToArray();

        /// <summary>
        /// Get the number of messages in the conversation history
        /// </summary>
        public int GetHistoryLength() => _conversationHistory.Count;

        /// <summary>
        /// Manually append a chat message to the conversation history
        /// </summary>
        public void AppendChatMessage(string role, string content)
        {
            if (string.IsNullOrEmpty(role) || string.IsNullOrEmpty(content))
            {
                Debug.LogWarning("[NPCClient] Role and content cannot be empty");
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
        public int RevertChatMessages(int count)
        {
            if (count <= 0) return 0;

            int messagesToRemove = Mathf.Min(count, _conversationHistory.Count);
            int originalCount = _conversationHistory.Count;

            for (int i = 0; i < messagesToRemove; i++)
            {
                _conversationHistory.RemoveAt(_conversationHistory.Count - 1);
            }

            int actuallyRemoved = originalCount - _conversationHistory.Count;
            Debug.Log($"[NPCClient] Reverted {actuallyRemoved} messages. Remaining: {_conversationHistory.Count}");

            return actuallyRemoved;
        }

        /// <summary>
        /// Print the current conversation history for debugging
        /// </summary>
        public void PrintPrettyChatMessages(string title = null)
        {
            string displayTitle = title ?? $"NPC '{gameObject.name}' Conversation History";
            PlayKit_AIChatClient.PrintPrettyChatMessages(_conversationHistory, displayTitle);
        }

        #endregion
    }

    /// <summary>
    /// Data structure for saving and loading conversation history
    /// </summary>
    [Serializable]
    public class ConversationSaveData
    {
        public string Prompt;
        public DW_ChatMessage[] History;
    }
}
