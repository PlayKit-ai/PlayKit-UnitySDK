using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using PlayKit_SDK.Public;
using UnityEngine;
using UnityEngine.Events;

namespace PlayKit_SDK
{
    /// <summary>
    /// MonoBehaviour wrapper for PlayKit_AIChatClient.
    /// Provides Inspector configuration, UnityEvent support, and automatic lifecycle management.
    ///
    /// For advanced usage, use PlayKit_AIChatClient directly via GetUnderlyingClient().
    /// </summary>
    public class PlayKit_Chat : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Chat Configuration")]
        [Tooltip("Chat model name (leave empty to use SDK default) 对话模型名称（留空则使用SDK默认值）")]
        [SerializeField] private string chatModel;

        [Tooltip("System prompt for this chat client 系统提示词")]
        [TextArea(3, 8)]
        [SerializeField] private string systemPrompt;

        [Header("Chat Options")]
        [Tooltip("Temperature for response generation (0.0-2.0) 响应生成温度")]
        [Range(0f, 2f)]
        [SerializeField] private float temperature = 0.7f;

        [Tooltip("Automatically maintain conversation history 自动管理对话历史")]
        [SerializeField] private bool maintainHistory = false;

        [Header("Debug Options")]
        [Tooltip("Log chat messages to console 在控制台输出聊天消息")]
        [SerializeField] private bool logMessages = false;

        #endregion

        #region UnityEvents

        [Header("Events")]
        [Tooltip("Called when a chat response is received (non-streaming) 收到响应时触发")]
        public UnityEvent<string> OnResponseReceived;

        [Tooltip("Called for each chunk during streaming response 流式响应的每个块触发")]
        public UnityEvent<string> OnStreamChunk;

        [Tooltip("Called when streaming completes with full response 流式响应完成时触发")]
        public UnityEvent<string> OnStreamComplete;

        [Tooltip("Called when an error occurs 发生错误时触发")]
        public UnityEvent<string> OnError;

        [Tooltip("Called when chat request starts 请求开始时触发")]
        public UnityEvent OnRequestStarted;

        [Tooltip("Called when chat request ends (success or failure) 请求结束时触发")]
        public UnityEvent OnRequestEnded;

        #endregion

        #region Private Fields

        private PlayKit_AIChatClient _chatClient;
        private List<PlayKit_ChatMessage> _conversationHistory;
        private bool _isReady;
        private bool _isProcessing;

        #endregion

        #region Public Properties

        /// <summary>
        /// Whether the chat client is ready to use
        /// </summary>
        public bool IsReady => _isReady;

        /// <summary>
        /// Whether a request is currently being processed
        /// </summary>
        public bool IsProcessing => _isProcessing;

        /// <summary>
        /// The model name being used
        /// </summary>
        public string ModelName => _chatClient?.ModelName;

        /// <summary>
        /// Current conversation history length
        /// </summary>
        public int HistoryLength => _conversationHistory?.Count ?? 0;

        /// <summary>
        /// Temperature setting for response generation
        /// </summary>
        public float Temperature
        {
            get => temperature;
            set => temperature = Mathf.Clamp(value, 0f, 2f);
        }

        /// <summary>
        /// Whether to maintain conversation history
        /// </summary>
        public bool MaintainHistory
        {
            get => maintainHistory;
            set => maintainHistory = value;
        }

        #endregion

        #region Lifecycle

        private void Start()
        {
            _conversationHistory = new List<PlayKit_ChatMessage>();
            Initialize().Forget();
        }

        private void OnDestroy()
        {
            _conversationHistory?.Clear();
        }

        private async UniTask Initialize()
        {
            await UniTask.WaitUntil(() => PlayKitSDK.IsReady());

            string model = string.IsNullOrEmpty(chatModel) ? null : chatModel;
            _chatClient = PlayKitSDK.Factory.CreateChatClient(model);

            if (_chatClient == null)
            {
                Debug.LogError("[PlayKit_Chat] Failed to create chat client");
                return;
            }

            // Apply initial system prompt
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                _conversationHistory.Add(new PlayKit_ChatMessage
                {
                    Role = "system",
                    Content = systemPrompt
                });
            }

            _isReady = true;

            if (logMessages)
            {
                Debug.Log($"[PlayKit_Chat] Ready with model '{_chatClient.ModelName}'");
            }
        }

        #endregion

        #region Public API - Chat Methods

        /// <summary>
        /// Send a message and get a response (non-streaming)
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The AI response</returns>
        public async UniTask<string> ChatAsync(string message, CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();

            if (!ValidateState(message))
            {
                return null;
            }

            _isProcessing = true;
            OnRequestStarted?.Invoke();

            try
            {
                // Build messages for request
                var messages = BuildMessages(message);

                var config = new PlayKit_ChatConfig(messages)
                {
                    Temperature = temperature
                };

                var result = await _chatClient.TextGenerationAsync(config, token);

                if (result.Success && !string.IsNullOrEmpty(result.Response))
                {
                    // Add to history if enabled
                    if (maintainHistory)
                    {
                        _conversationHistory.Add(new PlayKit_ChatMessage
                        {
                            Role = "user",
                            Content = message
                        });
                        _conversationHistory.Add(new PlayKit_ChatMessage
                        {
                            Role = "assistant",
                            Content = result.Response
                        });
                    }

                    if (logMessages)
                    {
                        Debug.Log($"[PlayKit_Chat] User: {message}");
                        Debug.Log($"[PlayKit_Chat] Assistant: {result.Response}");
                    }

                    OnResponseReceived?.Invoke(result.Response);
                    return result.Response;
                }
                else
                {
                    string error = result.ErrorMessage ?? "Unknown error";
                    OnError?.Invoke(error);
                    return null;
                }
            }
            catch (OperationCanceledException)
            {
                if (logMessages)
                {
                    Debug.Log("[PlayKit_Chat] Request cancelled");
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_Chat] Error: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
            finally
            {
                _isProcessing = false;
                OnRequestEnded?.Invoke();
            }
        }

        /// <summary>
        /// Send a message and get a streaming response
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        public async UniTask ChatStreamAsync(string message, CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();

            if (!ValidateState(message))
            {
                return;
            }

            _isProcessing = true;
            OnRequestStarted?.Invoke();

            try
            {
                // Build messages for request
                var messages = BuildMessages(message);

                var config = new PlayKit_ChatStreamConfig(messages)
                {
                    Temperature = temperature
                };

                string fullResponse = "";

                await _chatClient.TextChatStreamAsync(
                    config,
                    chunk =>
                    {
                        fullResponse += chunk;
                        OnStreamChunk?.Invoke(chunk);
                    },
                    complete =>
                    {
                        // Add to history if enabled
                        if (maintainHistory)
                        {
                            _conversationHistory.Add(new PlayKit_ChatMessage
                            {
                                Role = "user",
                                Content = message
                            });
                            _conversationHistory.Add(new PlayKit_ChatMessage
                            {
                                Role = "assistant",
                                Content = complete
                            });
                        }

                        if (logMessages)
                        {
                            Debug.Log($"[PlayKit_Chat] User: {message}");
                            Debug.Log($"[PlayKit_Chat] Assistant: {complete}");
                        }

                        OnStreamComplete?.Invoke(complete);
                    },
                    token
                );
            }
            catch (OperationCanceledException)
            {
                if (logMessages)
                {
                    Debug.Log("[PlayKit_Chat] Stream cancelled");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_Chat] Stream error: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
            finally
            {
                _isProcessing = false;
                OnRequestEnded?.Invoke();
            }
        }

        /// <summary>
        /// Send a message with custom callback handlers
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="onChunk">Called for each chunk</param>
        /// <param name="onComplete">Called when complete</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        public async UniTask ChatStreamAsync(
            string message,
            Action<string> onChunk,
            Action<string> onComplete,
            CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();

            if (!ValidateState(message))
            {
                onComplete?.Invoke(null);
                return;
            }

            _isProcessing = true;
            OnRequestStarted?.Invoke();

            try
            {
                var messages = BuildMessages(message);

                var config = new PlayKit_ChatStreamConfig(messages)
                {
                    Temperature = temperature
                };

                await _chatClient.TextChatStreamAsync(
                    config,
                    chunk =>
                    {
                        onChunk?.Invoke(chunk);
                        OnStreamChunk?.Invoke(chunk);
                    },
                    complete =>
                    {
                        if (maintainHistory)
                        {
                            _conversationHistory.Add(new PlayKit_ChatMessage
                            {
                                Role = "user",
                                Content = message
                            });
                            _conversationHistory.Add(new PlayKit_ChatMessage
                            {
                                Role = "assistant",
                                Content = complete
                            });
                        }

                        onComplete?.Invoke(complete);
                        OnStreamComplete?.Invoke(complete);
                    },
                    token
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_Chat] Stream error: {ex.Message}");
                OnError?.Invoke(ex.Message);
                onComplete?.Invoke(null);
            }
            finally
            {
                _isProcessing = false;
                OnRequestEnded?.Invoke();
            }
        }

        #endregion

        #region Public API - Multimodal Chat Methods (with Images)

        /// <summary>
        /// Send a message with a single image and get a response (non-streaming)
        /// </summary>
        /// <param name="message">The text message to send</param>
        /// <param name="image">The image to include in the message</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The AI response</returns>
        public async UniTask<string> ChatWithImageAsync(string message, Texture2D image, CancellationToken? cancellationToken = null)
        {
            return await ChatWithImagesAsync(message, new List<Texture2D> { image }, cancellationToken);
        }

        /// <summary>
        /// Send a message with multiple images and get a response (non-streaming)
        /// </summary>
        /// <param name="message">The text message to send</param>
        /// <param name="images">The images to include in the message</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The AI response</returns>
        public async UniTask<string> ChatWithImagesAsync(string message, List<Texture2D> images, CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();

            if (!ValidateState(message))
            {
                return null;
            }

            _isProcessing = true;
            OnRequestStarted?.Invoke();

            try
            {
                // Build messages with images
                var messages = BuildMessagesWithImages(message, images);

                var config = new PlayKit_ChatConfig(messages)
                {
                    Temperature = temperature
                };

                var result = await _chatClient.TextGenerationAsync(config, token);

                if (result.Success && !string.IsNullOrEmpty(result.Response))
                {
                    // Add to history if enabled (images are included in history)
                    if (maintainHistory)
                    {
                        var userMsg = new PlayKit_ChatMessage
                        {
                            Role = "user",
                            Content = message
                        };
                        // Add images to the user message
                        foreach (var img in images)
                        {
                            if (img != null) userMsg.AddImage(img);
                        }
                        _conversationHistory.Add(userMsg);
                        _conversationHistory.Add(new PlayKit_ChatMessage
                        {
                            Role = "assistant",
                            Content = result.Response
                        });
                    }

                    if (logMessages)
                    {
                        Debug.Log($"[PlayKit_Chat] User (with {images?.Count ?? 0} images): {message}");
                        Debug.Log($"[PlayKit_Chat] Assistant: {result.Response}");
                    }

                    OnResponseReceived?.Invoke(result.Response);
                    return result.Response;
                }
                else
                {
                    string error = result.ErrorMessage ?? "Unknown error";
                    OnError?.Invoke(error);
                    return null;
                }
            }
            catch (OperationCanceledException)
            {
                if (logMessages)
                {
                    Debug.Log("[PlayKit_Chat] Request cancelled");
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_Chat] Error: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
            finally
            {
                _isProcessing = false;
                OnRequestEnded?.Invoke();
            }
        }

        /// <summary>
        /// Send a message with a single image and get a streaming response
        /// </summary>
        /// <param name="message">The text message to send</param>
        /// <param name="image">The image to include in the message</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        public async UniTask ChatStreamWithImageAsync(string message, Texture2D image, CancellationToken? cancellationToken = null)
        {
            await ChatStreamWithImagesAsync(message, new List<Texture2D> { image }, cancellationToken);
        }

        /// <summary>
        /// Send a message with multiple images and get a streaming response
        /// </summary>
        /// <param name="message">The text message to send</param>
        /// <param name="images">The images to include in the message</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        public async UniTask ChatStreamWithImagesAsync(string message, List<Texture2D> images, CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();

            if (!ValidateState(message))
            {
                return;
            }

            _isProcessing = true;
            OnRequestStarted?.Invoke();

            try
            {
                // Build messages with images
                var messages = BuildMessagesWithImages(message, images);

                var config = new PlayKit_ChatStreamConfig(messages)
                {
                    Temperature = temperature
                };

                await _chatClient.TextChatStreamAsync(
                    config,
                    chunk =>
                    {
                        OnStreamChunk?.Invoke(chunk);
                    },
                    complete =>
                    {
                        // Add to history if enabled
                        if (maintainHistory)
                        {
                            var userMsg = new PlayKit_ChatMessage
                            {
                                Role = "user",
                                Content = message
                            };
                            foreach (var img in images)
                            {
                                if (img != null) userMsg.AddImage(img);
                            }
                            _conversationHistory.Add(userMsg);
                            _conversationHistory.Add(new PlayKit_ChatMessage
                            {
                                Role = "assistant",
                                Content = complete
                            });
                        }

                        if (logMessages)
                        {
                            Debug.Log($"[PlayKit_Chat] User (with {images?.Count ?? 0} images): {message}");
                            Debug.Log($"[PlayKit_Chat] Assistant: {complete}");
                        }

                        OnStreamComplete?.Invoke(complete);
                    },
                    token
                );
            }
            catch (OperationCanceledException)
            {
                if (logMessages)
                {
                    Debug.Log("[PlayKit_Chat] Stream cancelled");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_Chat] Stream error: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
            finally
            {
                _isProcessing = false;
                OnRequestEnded?.Invoke();
            }
        }

        /// <summary>
        /// Build messages list including images for multimodal requests
        /// </summary>
        private List<PlayKit_ChatMessage> BuildMessagesWithImages(string message, List<Texture2D> images)
        {
            var messages = new List<PlayKit_ChatMessage>();

            // Copy conversation history
            if (maintainHistory && _conversationHistory != null)
            {
                messages.AddRange(_conversationHistory);
            }
            else if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new PlayKit_ChatMessage
                {
                    Role = "system",
                    Content = systemPrompt
                });
            }

            // Add current user message with images
            var userMessage = new PlayKit_ChatMessage
            {
                Role = "user",
                Content = message
            };

            if (images != null)
            {
                foreach (var img in images)
                {
                    if (img != null)
                    {
                        userMessage.AddImage(img);
                    }
                }
            }

            messages.Add(userMessage);

            return messages;
        }

        #endregion

        #region History Management

        /// <summary>
        /// Set or update the system prompt
        /// </summary>
        /// <param name="prompt">The new system prompt</param>
        public void SetSystemPrompt(string prompt)
        {
            systemPrompt = prompt;

            // Update or add system message in history
            if (_conversationHistory != null && _conversationHistory.Count > 0)
            {
                if (_conversationHistory[0].Role == "system")
                {
                    _conversationHistory[0] = new PlayKit_ChatMessage
                    {
                        Role = "system",
                        Content = prompt
                    };
                }
                else if (!string.IsNullOrEmpty(prompt))
                {
                    _conversationHistory.Insert(0, new PlayKit_ChatMessage
                    {
                        Role = "system",
                        Content = prompt
                    });
                }
            }
            else if (_conversationHistory != null && !string.IsNullOrEmpty(prompt))
            {
                _conversationHistory.Add(new PlayKit_ChatMessage
                {
                    Role = "system",
                    Content = prompt
                });
            }
        }

        /// <summary>
        /// Clear all conversation history (except system prompt if set)
        /// </summary>
        public void ClearHistory()
        {
            if (_conversationHistory == null) return;

            // Keep system prompt if it exists
            PlayKit_ChatMessage systemMessage = null;
            if (_conversationHistory.Count > 0 && _conversationHistory[0].Role == "system")
            {
                systemMessage = _conversationHistory[0];
            }

            _conversationHistory.Clear();

            if (systemMessage != null)
            {
                _conversationHistory.Add(systemMessage);
            }

            if (logMessages)
            {
                Debug.Log("[PlayKit_Chat] History cleared");
            }
        }

        /// <summary>
        /// Get a copy of the current conversation history
        /// </summary>
        /// <returns>Array of chat messages</returns>
        public PlayKit_ChatMessage[] GetHistory()
        {
            return _conversationHistory?.ToArray() ?? new PlayKit_ChatMessage[0];
        }

        /// <summary>
        /// Append a message to the conversation history manually
        /// </summary>
        /// <param name="role">Message role (user, assistant, system)</param>
        /// <param name="content">Message content</param>
        public void AppendMessage(string role, string content)
        {
            _conversationHistory?.Add(new PlayKit_ChatMessage
            {
                Role = role,
                Content = content
            });
        }

        /// <summary>
        /// Remove the last exchange (user message + assistant response) from history
        /// </summary>
        /// <returns>True if successful</returns>
        public bool RevertLastExchange()
        {
            if (_conversationHistory == null || _conversationHistory.Count < 2)
            {
                return false;
            }

            // Check if last two messages are user + assistant
            int lastIndex = _conversationHistory.Count - 1;
            if (_conversationHistory[lastIndex].Role == "assistant" &&
                _conversationHistory[lastIndex - 1].Role == "user")
            {
                _conversationHistory.RemoveAt(lastIndex);
                _conversationHistory.RemoveAt(lastIndex - 1);

                if (logMessages)
                {
                    Debug.Log("[PlayKit_Chat] Last exchange reverted");
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Save conversation history to JSON string
        /// </summary>
        /// <returns>JSON string of the conversation</returns>
        public string SaveHistory()
        {
            if (_conversationHistory == null || _conversationHistory.Count == 0)
            {
                return "[]";
            }

            return JsonConvert.SerializeObject(_conversationHistory);
        }

        /// <summary>
        /// Load conversation history from JSON string
        /// </summary>
        /// <param name="saveData">JSON string of conversation history</param>
        /// <returns>True if successful</returns>
        public bool LoadHistory(string saveData)
        {
            if (string.IsNullOrEmpty(saveData))
            {
                return false;
            }

            try
            {
                var history = JsonConvert.DeserializeObject<List<PlayKit_ChatMessage>>(saveData);
                if (history != null)
                {
                    _conversationHistory = history;

                    if (logMessages)
                    {
                        Debug.Log($"[PlayKit_Chat] Loaded {history.Count} messages from save");
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_Chat] Failed to load history: {ex.Message}");
            }

            return false;
        }

        #endregion

        #region Advanced Access

        /// <summary>
        /// Get the underlying PlayKit_AIChatClient for advanced operations
        /// </summary>
        /// <returns>The underlying chat client</returns>
        public PlayKit_AIChatClient GetUnderlyingClient()
        {
            return _chatClient;
        }

        /// <summary>
        /// Setup with an existing chat client (for advanced scenarios)
        /// </summary>
        /// <param name="client">The chat client to use</param>
        public void Setup(PlayKit_AIChatClient client)
        {
            _chatClient = client;
            _isReady = true;

            if (logMessages)
            {
                Debug.Log($"[PlayKit_Chat] Setup with model '{client.ModelName}'");
            }
        }

        #endregion

        #region Private Helpers

        private bool ValidateState(string message)
        {
            if (!_isReady)
            {
                Debug.LogWarning("[PlayKit_Chat] Client not ready. Please wait for initialization.");
                OnError?.Invoke("Client not ready");
                return false;
            }

            if (_chatClient == null)
            {
                Debug.LogError("[PlayKit_Chat] Chat client not initialized.");
                OnError?.Invoke("Client not initialized");
                return false;
            }

            if (string.IsNullOrEmpty(message))
            {
                Debug.LogWarning("[PlayKit_Chat] Message cannot be empty.");
                OnError?.Invoke("Message cannot be empty");
                return false;
            }

            if (_isProcessing)
            {
                Debug.LogWarning("[PlayKit_Chat] A request is already in progress.");
                OnError?.Invoke("Request already in progress");
                return false;
            }

            return true;
        }

        private List<PlayKit_ChatMessage> BuildMessages(string userMessage)
        {
            var messages = new List<PlayKit_ChatMessage>();

            if (maintainHistory && _conversationHistory != null)
            {
                messages.AddRange(_conversationHistory);
            }
            else if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new PlayKit_ChatMessage
                {
                    Role = "system",
                    Content = systemPrompt
                });
            }

            messages.Add(new PlayKit_ChatMessage
            {
                Role = "user",
                Content = userMessage
            });

            return messages;
        }

        #endregion
    }
}
