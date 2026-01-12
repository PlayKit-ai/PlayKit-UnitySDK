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
    /// This is a "sugar" wrapper around PlayKit_AIChatClient for easier usage.
    ///
    /// Key Features:
    /// - Call Talk() for all interactions - actions are handled automatically
    /// - Add PlayKit_NPC_ActionsModule to same GameObject for action support
    /// - Subscribe to OnActionTriggered event to handle action callbacks
    /// </summary>
    public class PlayKit_NPC : MonoBehaviour
    {
        [Tooltip("Character design/system prompt for this NPC 该NPC的角色设定/系统提示词")]
        [SerializeField] private string characterDesign;

        [Tooltip("Chat model name to use (leave empty to use SDK default) 使用的对话模型名称（留空则使用SDK默认值）")]
        [SerializeField] private string chatModel;

        [Header("Reply Prediction Settings 回复预测设置")]
        [Tooltip("Automatically generate player reply predictions after NPC responds 在NPC回复后自动生成玩家回复预测")]
        [SerializeField] private bool generateReplyPrediction = false;

        [Tooltip("Number of reply predictions to generate (3-5 recommended) 生成的回复预测数量（建议3-5个）")]
        [Range(2, 6)]
        [SerializeField] private int predictionCount = 4;

        public string CharacterDesign => characterDesign;

        private PlayKit_AIChatClient _chatClient;
        private List<PlayKit_ChatMessage> _conversationHistory;
        private string _currentCharacterDesign;
        private Dictionary<string, string> _memories = new Dictionary<string, string>();
        private bool _isTalking;
        private bool _isReady;

        // Actions integration
        private PlayKit_NPC_ActionsModule _actionsModule;

        /// <summary>
        /// Event fired when an action is triggered by the NPC.
        /// Subscribe to this to handle action callbacks.
        /// </summary>
        public event Action<NpcActionCallArgs> OnActionTriggered;

        /// <summary>
        /// Event fired when reply predictions are generated.
        /// Subscribe to receive suggested player responses.
        /// </summary>
        public event Action<string[]> OnReplyPredictionGenerated;

        public bool IsTalking => _isTalking;
        public bool IsReady => _isReady;

        /// <summary>
        /// Whether this NPC has actions configured and enabled
        /// </summary>
        public bool HasEnabledActions => _actionsModule != null && _actionsModule.EnabledActions.Count > 0;

        /// <summary>
        /// Whether to automatically generate reply predictions after NPC responds.
        /// </summary>
        public bool GenerateReplyPrediction
        {
            get => generateReplyPrediction;
            set => generateReplyPrediction = value;
        }

        /// <summary>
        /// Number of reply predictions to generate.
        /// </summary>
        public int PredictionCount
        {
            get => predictionCount;
            set => predictionCount = Mathf.Clamp(value, 2, 6);
        }

        public void Setup(PlayKit_AIChatClient chatClient)
        {
            _chatClient = chatClient;
            _isReady = true;
            Debug.Log($"[NPCClient] Using model '{chatClient.ModelName}' for chat");
        }

        private void Start()
        {
            _conversationHistory = new List<PlayKit_ChatMessage>();
            Initialize().Forget();
        }

        private void OnDestroy()
        {
            // Unregister from AIContextManager when destroyed
            AIContextManager.Instance?.UnregisterNpc(this);
        }

        private async UniTask Initialize()
        {
            await UniTask.WaitUntil(() => PlayKit_SDK.IsReady());

            // Auto-detect ActionsModule on same GameObject
            _actionsModule = GetComponent<PlayKit_NPC_ActionsModule>();
            if (_actionsModule != null)
            {
                Debug.Log($"[NPCClient] ActionsModule detected on '{gameObject.name}'");
            }

            if (!string.IsNullOrEmpty(characterDesign))
                SetCharacterDesign(characterDesign);

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
            if (_isTalking)
            {
                Debug.LogWarning("[NPCClient] Already processing a request.");
                return null;
            }

            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            _isTalking = true;

            if (_chatClient == null)
            {
                Debug.LogError("[NPCClient] Chat client not initialized. Please call PlayKit_SDK.InitializeAsync() first.");
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
            if (_isTalking)
            {
                Debug.LogWarning("[NPCClient] Already processing a request.");
                return;
            }

            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            _isTalking = true;

            if (_chatClient == null)
            {
                Debug.LogError("[NPCClient] Chat client not initialized. Please call PlayKit_SDK.InitializeAsync() first.");
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

        #region Main API - Multimodal Talk Methods (with Images)

        /// <summary>
        /// Send a message with a single image to the NPC and get a response.
        /// Useful for asking the NPC about visual content or providing context through images.
        /// </summary>
        /// <param name="message">The text message to send to the NPC</param>
        /// <param name="image">The image to include with the message</param>
        /// <param name="cancellationToken">Cancellation token (defaults to OnDestroyCancellationToken)</param>
        /// <returns>The NPC's text response</returns>
        public async UniTask<string> Talk(string message, Texture2D image, CancellationToken? cancellationToken = null)
        {
            return await Talk(message, new List<Texture2D> { image }, cancellationToken);
        }

        /// <summary>
        /// Send a message with multiple images to the NPC and get a response.
        /// Useful for asking the NPC about visual content or providing context through images.
        /// </summary>
        /// <param name="message">The text message to send to the NPC</param>
        /// <param name="images">The images to include with the message</param>
        /// <param name="cancellationToken">Cancellation token (defaults to OnDestroyCancellationToken)</param>
        /// <returns>The NPC's text response</returns>
        public async UniTask<string> Talk(string message, List<Texture2D> images, CancellationToken? cancellationToken = null)
        {
            if (_isTalking)
            {
                Debug.LogWarning("[NPCClient] Already processing a request.");
                return null;
            }

            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            _isTalking = true;

            if (_chatClient == null)
            {
                Debug.LogError("[NPCClient] Chat client not initialized. Please call PlayKit_SDK.InitializeAsync() first.");
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
                return await TalkWithActionsAndImagesInternal(message, images, token);
            }
            else
            {
                return await TalkSimpleWithImagesInternal(message, images, token);
            }
        }

        /// <summary>
        /// Send a message with a single image to the NPC and get a streaming response.
        /// </summary>
        /// <param name="message">The text message to send to the NPC</param>
        /// <param name="image">The image to include with the message</param>
        /// <param name="onChunk">Called for each piece of the response as it streams in</param>
        /// <param name="onComplete">Called when the complete response is ready</param>
        /// <param name="cancellationToken">Cancellation token (defaults to OnDestroyCancellationToken)</param>
        public async UniTask TalkStream(string message, Texture2D image, Action<string> onChunk, Action<string> onComplete, CancellationToken? cancellationToken = null)
        {
            await TalkStream(message, new List<Texture2D> { image }, onChunk, onComplete, cancellationToken);
        }

        /// <summary>
        /// Send a message with multiple images to the NPC and get a streaming response.
        /// </summary>
        /// <param name="message">The text message to send to the NPC</param>
        /// <param name="images">The images to include with the message</param>
        /// <param name="onChunk">Called for each piece of the response as it streams in</param>
        /// <param name="onComplete">Called when the complete response is ready</param>
        /// <param name="cancellationToken">Cancellation token (defaults to OnDestroyCancellationToken)</param>
        public async UniTask TalkStream(string message, List<Texture2D> images, Action<string> onChunk, Action<string> onComplete, CancellationToken? cancellationToken = null)
        {
            if (_isTalking)
            {
                Debug.LogWarning("[NPCClient] Already processing a request.");
                return;
            }

            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            _isTalking = true;

            if (_chatClient == null)
            {
                Debug.LogError("[NPCClient] Chat client not initialized. Please call PlayKit_SDK.InitializeAsync() first.");
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
                await TalkWithActionsAndImagesStreamInternal(message, images, onChunk, onComplete, token);
            }
            else
            {
                await TalkSimpleWithImagesStreamInternal(message, images, onChunk, onComplete, token);
            }
        }

        #endregion

        #region Internal Implementation

        /// <summary>
        /// Simple talk without actions
        /// </summary>
        private async UniTask<string> TalkSimpleInternal(string message, CancellationToken token)
        {
            // Record conversation with AIContextManager
            AIContextManager.Instance?.RecordConversation(this);

            // Add user message to history
            _conversationHistory.Add(new PlayKit_ChatMessage
            {
                Role = "user",
                Content = message
            });

            try
            {
                var config = new PlayKit_ChatConfig(_conversationHistory.ToList());
                var result = await _chatClient.TextGenerationAsync(config, token);

                if (result.Success && !string.IsNullOrEmpty(result.Response))
                {
                    _conversationHistory.Add(new PlayKit_ChatMessage
                    {
                        Role = "assistant",
                        Content = result.Response
                    });
                    _isTalking = false;

                    // Trigger reply prediction generation (fire and forget)
                    if (generateReplyPrediction)
                    {
                        TriggerReplyPredictionAsync(token).Forget();
                    }

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
            // Record conversation with AIContextManager
            AIContextManager.Instance?.RecordConversation(this);

            // Add user message to history
            _conversationHistory.Add(new PlayKit_ChatMessage
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

                var config = new PlayKit_ChatConfig(_conversationHistory.ToList());
                var result = await _chatClient.TextGenerationWithToolsAsync(config, tools, "auto", token);

                if (result.Success && result.Response?.Choices?.Count > 0)
                {
                    var choice = result.Response.Choices[0];
                    var responseText = choice.Message?.GetTextContent() ?? "";

                    // Add assistant response to history
                    _conversationHistory.Add(new PlayKit_ChatMessage
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

                    // Trigger reply prediction generation (fire and forget)
                    if (generateReplyPrediction)
                    {
                        TriggerReplyPredictionAsync(token).Forget();
                    }

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
            // Record conversation with AIContextManager
            AIContextManager.Instance?.RecordConversation(this);

            // Add user message to history
            _conversationHistory.Add(new PlayKit_ChatMessage
            {
                Role = "user",
                Content = message
            });

            try
            {
                var config = new PlayKit_ChatStreamConfig(_conversationHistory.ToList());

                await _chatClient.TextChatStreamAsync(config,
                    chunk => onChunk?.Invoke(chunk),
                    completeResponse =>
                    {
                        _isTalking = false;
                        if (!string.IsNullOrEmpty(completeResponse))
                        {
                            _conversationHistory.Add(new PlayKit_ChatMessage
                            {
                                Role = "assistant",
                                Content = completeResponse
                            });

                            // Trigger reply prediction generation (fire and forget)
                            if (generateReplyPrediction)
                            {
                                TriggerReplyPredictionAsync(token).Forget();
                            }
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

        #region Internal Implementation - Multimodal (with Images)

        /// <summary>
        /// Simple talk with images (no actions)
        /// </summary>
        private async UniTask<string> TalkSimpleWithImagesInternal(string message, List<Texture2D> images, CancellationToken token)
        {
            // Record conversation with AIContextManager
            AIContextManager.Instance?.RecordConversation(this);

            // Add user message with images to history
            var userMsg = new PlayKit_ChatMessage
            {
                Role = "user",
                Content = message
            };
            if (images != null)
            {
                foreach (var img in images)
                {
                    if (img != null) userMsg.AddImage(img);
                }
            }
            _conversationHistory.Add(userMsg);

            try
            {
                var config = new PlayKit_ChatConfig(_conversationHistory.ToList());
                var result = await _chatClient.TextGenerationAsync(config, token);

                if (result.Success && !string.IsNullOrEmpty(result.Response))
                {
                    _conversationHistory.Add(new PlayKit_ChatMessage
                    {
                        Role = "assistant",
                        Content = result.Response
                    });
                    _isTalking = false;

                    // Trigger reply prediction generation (fire and forget)
                    if (generateReplyPrediction)
                    {
                        TriggerReplyPredictionAsync(token).Forget();
                    }

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
                Debug.LogError($"[NPCClient] Error in Talk with images: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Talk with actions and images
        /// </summary>
        private async UniTask<string> TalkWithActionsAndImagesInternal(string message, List<Texture2D> images, CancellationToken token)
        {
            // Record conversation with AIContextManager
            AIContextManager.Instance?.RecordConversation(this);

            // Add user message with images to history
            var userMsg = new PlayKit_ChatMessage
            {
                Role = "user",
                Content = message
            };
            if (images != null)
            {
                foreach (var img in images)
                {
                    if (img != null) userMsg.AddImage(img);
                }
            }
            _conversationHistory.Add(userMsg);

            try
            {
                // Get enabled actions from ActionsModule
                var actions = _actionsModule.EnabledActions;
                var tools = actions
                    .Where(a => a != null && a.enabled)
                    .Select(a => a.ToTool())
                    .ToList();

                var config = new PlayKit_ChatConfig(_conversationHistory.ToList());
                var result = await _chatClient.TextGenerationWithToolsAsync(config, tools, "auto", token);

                if (result.Success && result.Response?.Choices?.Count > 0)
                {
                    var choice = result.Response.Choices[0];
                    var responseText = choice.Message?.GetTextContent() ?? "";

                    // Add assistant response to history
                    _conversationHistory.Add(new PlayKit_ChatMessage
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

                    // Trigger reply prediction generation (fire and forget)
                    if (generateReplyPrediction)
                    {
                        TriggerReplyPredictionAsync(token).Forget();
                    }

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
                Debug.LogError($"[NPCClient] Error in Talk with actions and images: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Simple streaming talk with images (no actions)
        /// </summary>
        private async UniTask TalkSimpleWithImagesStreamInternal(string message, List<Texture2D> images, Action<string> onChunk, Action<string> onComplete, CancellationToken token)
        {
            // Record conversation with AIContextManager
            AIContextManager.Instance?.RecordConversation(this);

            // Add user message with images to history
            var userMsg = new PlayKit_ChatMessage
            {
                Role = "user",
                Content = message
            };
            if (images != null)
            {
                foreach (var img in images)
                {
                    if (img != null) userMsg.AddImage(img);
                }
            }
            _conversationHistory.Add(userMsg);

            try
            {
                var config = new PlayKit_ChatStreamConfig(_conversationHistory.ToList());

                await _chatClient.TextChatStreamAsync(config,
                    chunk => onChunk?.Invoke(chunk),
                    completeResponse =>
                    {
                        _isTalking = false;
                        if (!string.IsNullOrEmpty(completeResponse))
                        {
                            _conversationHistory.Add(new PlayKit_ChatMessage
                            {
                                Role = "assistant",
                                Content = completeResponse
                            });

                            // Trigger reply prediction generation (fire and forget)
                            if (generateReplyPrediction)
                            {
                                TriggerReplyPredictionAsync(token).Forget();
                            }
                        }
                        onComplete?.Invoke(completeResponse);
                    },
                    token
                );
            }
            catch (Exception ex)
            {
                _isTalking = false;
                Debug.LogError($"[NPCClient] Error in streaming Talk with images: {ex.Message}");
                onChunk?.Invoke(null);
                onComplete?.Invoke(null);
            }
        }

        /// <summary>
        /// Streaming talk with actions and images
        /// </summary>
        private async UniTask TalkWithActionsAndImagesStreamInternal(string message, List<Texture2D> images, Action<string> onChunk, Action<string> onComplete, CancellationToken token)
        {
            // Record conversation with AIContextManager
            AIContextManager.Instance?.RecordConversation(this);

            // Add user message with images to history
            var userMsg = new PlayKit_ChatMessage
            {
                Role = "user",
                Content = message
            };
            if (images != null)
            {
                foreach (var img in images)
                {
                    if (img != null) userMsg.AddImage(img);
                }
            }
            _conversationHistory.Add(userMsg);

            try
            {
                var actions = _actionsModule.EnabledActions;
                var tools = actions
                    .Where(a => a != null && a.enabled)
                    .Select(a => a.ToTool())
                    .ToList();

                var config = new PlayKit_ChatStreamConfig(_conversationHistory.ToList());

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
                            var responseText = choice.Message?.GetTextContent() ?? "";

                            // Add assistant response to history
                            _conversationHistory.Add(new PlayKit_ChatMessage
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

                            // Trigger reply prediction generation (fire and forget)
                            if (generateReplyPrediction)
                            {
                                TriggerReplyPredictionAsync(token).Forget();
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
                Debug.LogError($"[NPCClient] Error in streaming Talk with actions and images: {ex.Message}");
                onChunk?.Invoke(null);
                onComplete?.Invoke(null);
            }
        }

        #endregion

        /// <summary>
        /// Streaming talk with actions
        /// </summary>
        private async UniTask TalkWithActionsStreamInternal(string message, Action<string> onChunk, Action<string> onComplete, CancellationToken token)
        {
            // Record conversation with AIContextManager
            AIContextManager.Instance?.RecordConversation(this);

            // Add user message to history
            _conversationHistory.Add(new PlayKit_ChatMessage
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

                var config = new PlayKit_ChatStreamConfig(_conversationHistory.ToList());

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
                            var responseText = choice.Message?.GetTextContent() ?? "";

                            // Add assistant response to history
                            _conversationHistory.Add(new PlayKit_ChatMessage
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

                            // Trigger reply prediction generation (fire and forget)
                            if (generateReplyPrediction)
                            {
                                TriggerReplyPredictionAsync(token).Forget();
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
                _conversationHistory.Add(new PlayKit_ChatMessage
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

            _conversationHistory.Add(new PlayKit_ChatMessage
            {
                Role = "tool",
                ToolCallId = callId,
                Content = result ?? ""
            });
        }

        #endregion

        #region Reply Prediction (Suggestion)

        /// <summary>
        /// Manually generate reply predictions based on current conversation.
        /// Uses the fast model configured in PlayKitSettings for quick generation.
        /// </summary>
        /// <param name="count">Number of predictions to generate (default: uses PredictionCount property)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Array of predicted player replies, or empty array on failure</returns>
        public async UniTask<string[]> GenerateReplyPredictionsAsync(int? count = null, CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            int predictionNum = count ?? predictionCount;

            if (_conversationHistory == null || _conversationHistory.Count < 2)
            {
                Debug.Log("[NPCClient] Not enough conversation history to generate predictions");
                return Array.Empty<string>();
            }

            try
            {
                // Get player description from AIContextManager
                var playerDesc = AIContextManager.Instance?.GetPlayerDescription();
                var playerContext = string.IsNullOrEmpty(playerDesc)
                    ? ""
                    : $"- Player description: {playerDesc}\n";

                // Get last NPC message
                var lastNpcMessage = _conversationHistory
                    .LastOrDefault(m => m.Role == "assistant")?.Content ?? "";

                if (string.IsNullOrEmpty(lastNpcMessage))
                {
                    Debug.Log("[NPCClient] No NPC message found to generate predictions from");
                    return Array.Empty<string>();
                }

                // Build recent history (last 6 non-system messages)
                var recentHistory = _conversationHistory
                    .Where(m => m.Role != "system")
                    .TakeLast(6)
                    .Select(m => $"{m.Role}: {m.Content}")
                    .ToList();

                // Build prompt for prediction generation
                var prompt = $@"Based on the conversation history below, generate exactly {predictionNum} natural and contextually appropriate responses that the player might say next.

Context:
- This is a conversation between a player and an NPC in a game
{playerContext}- The NPC just said: ""{lastNpcMessage}""

Conversation history:
{string.Join("\n", recentHistory)}

Requirements:
1. Each response should be 1-2 sentences maximum
2. Responses should be diverse in tone and intent
3. Include a mix of questions, statements, and action-oriented responses
4. Responses should feel natural for a player character

Output ONLY a JSON array of {predictionNum} strings, nothing else:
[""response1"", ""response2"", ""response3"", ""response4""]";

                // Use fast model for prediction
                var settings = PlayKitSettings.Instance;
                var chatClient = PlayKit_SDK.Factory.CreateChatClient(settings?.FastModel ?? "default-chat-fast");

                if (chatClient == null)
                {
                    Debug.LogError("[NPCClient] Failed to create chat client for predictions");
                    return Array.Empty<string>();
                }

                var config = new PlayKit_ChatConfig(new List<PlayKit_ChatMessage>
                {
                    new PlayKit_ChatMessage { Role = "user", Content = prompt }
                });

                var result = await chatClient.TextGenerationAsync(config, token);

                if (!result.Success || string.IsNullOrEmpty(result.Response))
                {
                    Debug.LogWarning($"[NPCClient] Failed to generate predictions: {result.ErrorMessage}");
                    return Array.Empty<string>();
                }

                // Parse JSON response
                var predictions = ParsePredictionsFromJson(result.Response, predictionNum);

                if (predictions.Length > 0)
                {
                    Debug.Log($"[NPCClient] Generated {predictions.Length} reply predictions");
                }

                return predictions;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NPCClient] Error generating predictions: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Parse predictions from JSON array response
        /// </summary>
        private string[] ParsePredictionsFromJson(string response, int expectedCount)
        {
            try
            {
                // Try to find JSON array in response
                var startIndex = response.IndexOf('[');
                var endIndex = response.LastIndexOf(']');

                if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex)
                {
                    Debug.LogWarning("[NPCClient] Could not find JSON array in prediction response");
                    return ExtractPredictionsFromText(response, expectedCount);
                }

                var jsonArray = response.Substring(startIndex, endIndex - startIndex + 1);

                // Simple JSON array parsing without external dependencies
                var predictions = new List<string>();
                var inString = false;
                var currentString = new System.Text.StringBuilder();
                var escaped = false;

                for (int i = 1; i < jsonArray.Length - 1; i++)
                {
                    char c = jsonArray[i];

                    if (escaped)
                    {
                        currentString.Append(c);
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        if (inString)
                        {
                            // End of string
                            var pred = currentString.ToString().Trim();
                            if (!string.IsNullOrEmpty(pred))
                            {
                                predictions.Add(pred);
                            }
                            currentString.Clear();
                        }
                        inString = !inString;
                        continue;
                    }

                    if (inString)
                    {
                        currentString.Append(c);
                    }
                }

                return predictions.ToArray();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NPCClient] Failed to parse predictions JSON: {ex.Message}");
                return ExtractPredictionsFromText(response, expectedCount);
            }
        }

        /// <summary>
        /// Fallback: Extract predictions from text when JSON parsing fails
        /// </summary>
        private string[] ExtractPredictionsFromText(string response, int expectedCount)
        {
            var predictions = new List<string>();
            var lines = response.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Skip empty lines and JSON brackets
                if (string.IsNullOrEmpty(trimmed) || trimmed == "[" || trimmed == "]")
                    continue;

                // Remove common prefixes like "1.", "- ", etc.
                var cleaned = trimmed;
                if (cleaned.Length > 2 && char.IsDigit(cleaned[0]) && cleaned[1] == '.')
                {
                    cleaned = cleaned.Substring(2).Trim();
                }
                else if (cleaned.StartsWith("- "))
                {
                    cleaned = cleaned.Substring(2).Trim();
                }

                // Remove surrounding quotes
                if (cleaned.StartsWith("\"") && cleaned.EndsWith("\""))
                {
                    cleaned = cleaned.Substring(1, cleaned.Length - 2);
                }

                // Remove trailing comma
                if (cleaned.EndsWith(","))
                {
                    cleaned = cleaned.Substring(0, cleaned.Length - 1).Trim();
                }

                if (!string.IsNullOrEmpty(cleaned) && predictions.Count < expectedCount)
                {
                    predictions.Add(cleaned);
                }
            }

            return predictions.ToArray();
        }

        /// <summary>
        /// Internal method to trigger prediction generation after NPC response
        /// </summary>
        private async UniTask TriggerReplyPredictionAsync(CancellationToken token)
        {
            if (!generateReplyPrediction) return;

            var predictions = await GenerateReplyPredictionsAsync(predictionCount, token);

            if (predictions.Length > 0)
            {
                OnReplyPredictionGenerated?.Invoke(predictions);
            }
        }

        #endregion

        #region Conversation History Management

        /// <summary>
        /// Set the character design for the NPC.
        /// The system prompt is composed of CharacterDesign + all Memories.
        /// </summary>
        /// <param name="design">The character design/persona for this NPC</param>
        public void SetCharacterDesign(string design)
        {
            _currentCharacterDesign = design;
            RebuildSystemPrompt();
        }

        /// <summary>
        /// [Deprecated] Use SetCharacterDesign instead.
        /// This method is kept for backwards compatibility.
        /// </summary>
        /// <param name="prompt">The character design/persona for this NPC</param>
        [Obsolete("Use SetCharacterDesign instead. SetSystemPrompt is deprecated.")]
        public void SetSystemPrompt(string prompt)
        {
            Debug.LogWarning("[NPCClient] SetSystemPrompt is deprecated. Use SetCharacterDesign instead.");
            SetCharacterDesign(prompt);
        }

        /// <summary>
        /// Set or update a memory for the NPC.
        /// Memories are appended to the character design to form the system prompt.
        /// Set memoryContent to null or empty to remove the memory.
        /// </summary>
        /// <param name="memoryName">The name/key of the memory</param>
        /// <param name="memoryContent">The content of the memory. Null or empty to remove.</param>
        public void SetMemory(string memoryName, string memoryContent)
        {
            if (string.IsNullOrEmpty(memoryName))
            {
                Debug.LogWarning("[NPCClient] Memory name cannot be empty");
                return;
            }

            if (string.IsNullOrEmpty(memoryContent))
            {
                // Remove memory if content is null or empty
                if (_memories.ContainsKey(memoryName))
                {
                    _memories.Remove(memoryName);
                    Debug.Log($"[NPCClient] Memory '{memoryName}' removed");
                }
            }
            else
            {
                // Add or update memory
                _memories[memoryName] = memoryContent;
                Debug.Log($"[NPCClient] Memory '{memoryName}' set");
            }

            RebuildSystemPrompt();
        }

        /// <summary>
        /// Get a specific memory by name.
        /// </summary>
        /// <param name="memoryName">The name of the memory to retrieve</param>
        /// <returns>The memory content, or null if not found</returns>
        public string GetMemory(string memoryName)
        {
            return _memories.TryGetValue(memoryName, out var content) ? content : null;
        }

        /// <summary>
        /// Get all memory names currently stored.
        /// </summary>
        /// <returns>Array of memory names</returns>
        public string[] GetMemoryNames()
        {
            return _memories.Keys.ToArray();
        }

        /// <summary>
        /// Clear all memories (but keep character design).
        /// </summary>
        public void ClearMemories()
        {
            _memories.Clear();
            RebuildSystemPrompt();
            Debug.Log("[NPCClient] All memories cleared");
        }

        /// <summary>
        /// Rebuild the system prompt from CharacterDesign + Memories.
        /// This is called automatically when SetCharacterDesign or SetMemory is called.
        /// </summary>
        private void RebuildSystemPrompt()
        {
            // Remove existing system message if any
            for (int i = _conversationHistory.Count - 1; i >= 0; i--)
            {
                if (_conversationHistory[i].Role == "system")
                {
                    _conversationHistory.RemoveAt(i);
                }
            }

            // Build combined system prompt
            var promptParts = new List<string>();

            if (!string.IsNullOrEmpty(_currentCharacterDesign))
            {
                promptParts.Add(_currentCharacterDesign);
            }

            if (_memories.Count > 0)
            {
                var memoryStrings = _memories.Select(kvp => $"[{kvp.Key}]: {kvp.Value}");
                promptParts.Add("Memories:\n" + string.Join("\n", memoryStrings));
            }

            var combinedPrompt = string.Join("\n\n", promptParts);

            // Add new system message if we have content
            if (!string.IsNullOrEmpty(combinedPrompt))
            {
                _conversationHistory.Insert(0, new PlayKit_ChatMessage
                {
                    Role = "system",
                    Content = combinedPrompt
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
            var memoryEntries = _memories.Select(kvp => new MemoryEntry
            {
                Name = kvp.Key,
                Content = kvp.Value
            }).ToArray();

            var saveData = new ConversationSaveData
            {
                CharacterDesign = _currentCharacterDesign,
                Memories = memoryEntries,
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
                _memories.Clear();

                // Load character design (with backwards compatibility for old Prompt field)
                var design = !string.IsNullOrEmpty(data.CharacterDesign) ? data.CharacterDesign : data.Prompt;
                _currentCharacterDesign = design;

                // Load memories
                if (data.Memories != null)
                {
                    foreach (var memory in data.Memories)
                    {
                        if (!string.IsNullOrEmpty(memory.Name) && !string.IsNullOrEmpty(memory.Content))
                        {
                            _memories[memory.Name] = memory.Content;
                        }
                    }
                }

                // Rebuild system prompt from CharacterDesign + Memories
                RebuildSystemPrompt();

                // Load non-system messages
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
        /// The system prompt (CharacterDesign + Memories) will be preserved.
        /// </summary>
        public void ClearHistory()
        {
            _conversationHistory.Clear();
            RebuildSystemPrompt();
        }

        /// <summary>
        /// Get the current conversation history
        /// </summary>
        public PlayKit_ChatMessage[] GetHistory() => _conversationHistory.ToArray();

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

            _conversationHistory.Add(new PlayKit_ChatMessage
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
        public string Prompt; // Kept for backwards compatibility
        public string CharacterDesign;
        public MemoryEntry[] Memories;
        public PlayKit_ChatMessage[] History;
    }

    /// <summary>
    /// Data structure for serializing a memory entry
    /// </summary>
    [Serializable]
    public class MemoryEntry
    {
        public string Name;
        public string Content;
    }
}
