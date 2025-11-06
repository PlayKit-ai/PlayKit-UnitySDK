using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Developerworks_SDK
{
    public class DW_SDK : MonoBehaviour
    {
        public const string VERSION = "v0.1.7.2-beta";

        [Tooltip("Your game ID from Developerworks platform 从Developerworks平台获取的游戏ID")]
        [SerializeField] private string gameId;
        [Tooltip("Default chat model to use (e.g., gpt-4o-mini) 默认使用的对话模型（例如：gpt-4o-mini）")]
        [SerializeField] private string defaultChatModel;
        [Tooltip("Default image generation model to use 默认使用的图像生成模型")]
        [SerializeField] private string defaultImageModel;
        [Tooltip("Reference to DW_AuthManager component DW_AuthManager组件引用")]
        [SerializeField] private Auth.DW_AuthManager authManager;
        [Tooltip("Ignore developer token (use player tokens only) 忽略开发者令牌（仅使用玩家令牌）")]
        [SerializeField] private bool ignoreDeveloperToken;
        
        public static DW_SDK Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private static bool _isInitialized = false;
        private static Auth.DW_AuthManager _dwAuthManager => Instance.authManager;
        private static Provider.IChatProvider _chatProvider;
        private static Provider.IImageProvider _imageProvider;
        private static Provider.AI.IObjectProvider _objectProvider;
        private static Provider.ITranscriptionProvider _transcriptionProvider;

        /// <summary>
        /// Asynchronously initializes the SDK. This must complete successfully before creating clients.
        /// It handles configuration loading and user authentication.
        /// </summary>
        /// <returns>True if initialization and authentication were successful, otherwise false.</returns>
        public static async UniTask<bool> InitializeAsync(string developerToken = null)
        {
            if (!Instance)
            {
                Debug.LogError("Please place DW_SDK object in your FIRST scene.");
                return false;
            }

            if (string.IsNullOrEmpty(Instance.gameId))
            {
                Debug.LogError("Please fill in gameId from your game. Get one from https://developerworks.agentlandlab.com");
                return false;
            }
            
            Debug.Log("[Developerworks SDK] Initializing...");
            if (_isInitialized) return true;



            if (developerToken != null && !Instance.ignoreDeveloperToken)
            {
                Debug.Log("[Developerworks SDK] You are loading a developer token, this can cost you money, but is fine for development...");
                _dwAuthManager.Setup(Instance.gameId, developerToken);

                // Show developer key warning in non-editor builds
#if !UNITY_EDITOR
                ShowDeveloperKeyWarning();
#endif
            }
            else
            {
                
                _dwAuthManager.Setup(Instance.gameId);

            }
            bool authSuccess = await _dwAuthManager.AuthenticateAsync();

            if (!authSuccess)
            {
                Debug.LogError("[Developerworks SDK] SDK Authentication Failed. Cannot proceed.");
                return false;
            }

            _chatProvider = new Provider.AI.AIChatProvider(_dwAuthManager);
            _imageProvider = new Provider.AI.AIImageProvider(_dwAuthManager);
            _objectProvider = new Provider.AI.AIObjectProvider(_dwAuthManager);
            _transcriptionProvider = new Provider.AI.AITranscriptionProvider(_dwAuthManager);
            _isInitialized = true;
            Debug.Log("[Developerworks SDK] Developerworks_SDK Initialized Successfully");
            return true;
        }

        /// <summary>
        /// Shows the developer key warning UI in non-editor builds.
        /// The warning automatically disappears after 5 seconds.
        /// </summary>
        private static void ShowDeveloperKeyWarning()
        {
            try
            {
                var warningPrefab = Resources.Load<GameObject>("DeveloperKeyWarning");
                if (warningPrefab != null)
                {
                    var warningInstance = Instantiate(warningPrefab);
                    DontDestroyOnLoad(warningInstance);

                    // Auto-hide warning after 5 seconds
                    Instance.StartCoroutine(HideWarningAfterDelay(warningInstance, 5f));
                }
                else
                {
                    Debug.LogWarning("[Developerworks SDK] DeveloperKeyWarning prefab not found in Resources.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Developerworks SDK] Failed to show developer key warning: {ex.Message}");
            }
        }

        /// <summary>
        /// Coroutine to hide the warning UI after a delay
        /// </summary>
        private static System.Collections.IEnumerator HideWarningAfterDelay(GameObject warningObject, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (warningObject != null)
            {
                Destroy(warningObject);
            }
        }

        /// <summary>
        /// Gets the PlayerClient for querying user information and managing player data.
        /// This can be used to check user credits, get user info, etc.
        /// </summary>
        /// <returns>The PlayerClient instance, or null if SDK not initialized or user not authenticated</returns>
        public static DW_PlayerClient GetPlayerClient()
        {
            if (!_isInitialized || _dwAuthManager == null)
            {
                Debug.LogWarning("SDK not initialized. Please call DW_SDK.InitializeAsync() first.");
                return null;
            }

            return _dwAuthManager.GetPlayerClient();
        }

        /// <summary>
        /// Checks if the SDK is initialized and the user is authenticated
        /// </summary>
        /// <returns>True if ready to use, false otherwise</returns>
        public static bool IsReady()
        {
            return _isInitialized && _dwAuthManager != null;
        }

        public static class Factory
        {
            /// <summary>
            /// Creates a standard chat client with both text and structured output capabilities
            /// </summary>
            public static DW_AIChatClient CreateChatClient(string modelName = null)
            {
                if (!Instance)
                {
                    Debug.LogError("Please place DW_SDK object in your FIRST scene.");
                }
                if (!_isInitialized)
                {
                    Debug.LogError("SDK not initialized. Please call DW_SDK.InitializeAsync() and wait for it to complete first.");
                    return null;
                }
                
                string model = modelName ?? Instance.defaultChatModel;
                var chatService = new Services.ChatService(_chatProvider);
                return new DW_AIChatClient(model, chatService, _objectProvider);
            }

            /// <summary>
            /// Creates an image generation client for AI-powered image creation
            /// </summary>
            public static DW_AIImageClient CreateImageClient(string modelName = null)
            {
                if (!Instance)
                {
                    Debug.LogError("Please place DW_SDK object in your FIRST scene.");
                    return null;
                }
                if (!_isInitialized)
                {
                    Debug.LogError("SDK not initialized. Please call DW_SDK.InitializeAsync() and wait for it to complete first.");
                    return null;
                }
                
                string model = modelName ?? Instance.defaultImageModel;
                if (string.IsNullOrEmpty(model))
                {
                    Debug.LogError("No image model specified. Please set defaultImageModel in DW_SDK or provide a model name.");
                    return null;
                }
                
                return new DW_AIImageClient(model, _imageProvider);
            }

            /// <summary>
            /// Creates an audio transcription client for speech-to-text conversion
            /// </summary>
            /// <param name="modelName">The transcription model to use (e.g., "whisper-1")</param>
            /// <returns>An audio transcription client</returns>
            public static DW_AudioTranscriptionClient CreateTranscriptionClient(string modelName)
            {
                if (!Instance)
                {
                    Debug.LogError("Please place DW_SDK object in your FIRST scene.");
                    return null;
                }
                if (!_isInitialized)
                {
                    Debug.LogError("SDK not initialized. Please call DW_SDK.InitializeAsync() and wait for it to complete first.");
                    return null;
                }

                if (string.IsNullOrEmpty(modelName))
                {
                    Debug.LogError("Transcription model name cannot be empty. Please specify a model like 'whisper-1'.");
                    return null;
                }

                var transcriptionService = new Services.TranscriptionService(_transcriptionProvider);
                return new DW_AudioTranscriptionClient(modelName, transcriptionService);
            }

        }

        public static class Populate
        {
            /// <summary>
            /// Set up a NPC client that automatically manages conversation history.
            /// This is a simplified interface perfect for game NPCs and characters.
            /// </summary>
            /// <param name="recipient">The NPC Object</param>
            /// <param name="modelName">Optional specific model to use</param>
            /// <returns>An NPC client ready for conversation</returns>
            public static void CreateNpc(DW_NPCClient recipient, string modelName = null)
            {
                if (!Instance)
                {
                    Debug.LogError("Please place DW_SDK object in your FIRST scene.");
                }
                if (!_isInitialized)
                {
                    Debug.LogError("SDK not initialized. Please call DW_SDK.InitializeAsync() and wait for it to complete first.");
                    return;
                }

                // Create underlying chat client
                var chatClient = Factory.CreateChatClient(modelName);
                if (chatClient == null)
                {
                    return;
                }

                recipient.Setup(chatClient);
            }
        }

        /// <summary>
        /// Quick access to create a transcription client
        /// </summary>
        /// <param name="modelName">The transcription model to use (e.g., "whisper-1")</param>
        /// <returns>An audio transcription client</returns>
        public static DW_AudioTranscriptionClient CreateTranscriptionClient(string modelName)
        {
            return Factory.CreateTranscriptionClient(modelName);
        }
    }
}
