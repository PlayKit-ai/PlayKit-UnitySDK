using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PlayKit_SDK
{
    public class PlayKit_SDK : MonoBehaviour
    {
        public const string VERSION = "v0.2.0.1-beta";

        // Configuration is now loaded from PlayKitSettings ScriptableObject
        // No need to manually place prefabs in scenes - SDK initializes automatically at runtime
        // Configure via: Tools > PlayKit SDK > Settings

        public static PlayKit_SDK Instance { get; private set; }

        // Auth manager is created dynamically instead of being serialized
        private Auth.PlayKit_AuthManager authManager;

        /// <summary>
        /// Automatically creates SDK instance at runtime startup.
        /// No manual prefab placement needed.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            // Check if an instance already exists in the scene (for backward compatibility)
            if (Instance != null)
            {
                Debug.LogWarning("[PlayKit SDK] SDK instance already exists in scene. Auto-initialization skipped. Consider removing the old prefab.");
                return;
            }

            // Create SDK GameObject automatically
            GameObject sdkObject = new GameObject("PlayKit_SDK");
            Instance = sdkObject.AddComponent<PlayKit_SDK>();
            DontDestroyOnLoad(sdkObject);

            Debug.Log("[PlayKit SDK] SDK instance created automatically. Configure via Tools > PlayKit SDK > Settings");
        }

        private void Awake()
        {
            // Handle manual prefab instances (backward compatibility)
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Debug.LogWarning("[PlayKit SDK] SDK initialized from scene prefab. Consider removing the prefab - SDK now initializes automatically.");
            }
            else if (Instance != this)
            {
                Debug.LogWarning("[PlayKit SDK] Duplicate SDK instance detected. Destroying duplicate.");
                Destroy(gameObject);
            }

            // Create AuthManager component if not exists
            if (authManager == null)
            {
                authManager = gameObject.AddComponent<Auth.PlayKit_AuthManager>();
            }
        }

        private static bool _isInitialized = false;
        private static Auth.PlayKit_AuthManager PlayKitAuthManager => Instance.authManager;
        private static Provider.IChatProvider _chatProvider;
        private static Provider.IImageProvider _imageProvider;
        private static Provider.AI.IObjectProvider _objectProvider;
        private static Provider.ITranscriptionProvider _transcriptionProvider;

        /// <summary>
        /// Asynchronously initializes the SDK. This must complete successfully before creating clients.
        /// It handles configuration loading and user authentication.
        /// Configuration is loaded from PlayKitSettings (Tools > PlayKit SDK > Settings).
        /// </summary>
        /// <param name="developerToken">Optional developer token. If not provided, uses token from EditorPrefs (editor only).</param>
        /// <returns>True if initialization and authentication were successful, otherwise false.</returns>
        public static async UniTask<bool> InitializeAsync(string developerToken = null)
        {
            if (!Instance)
            {
                Debug.LogError("[PlayKit SDK] SDK instance not found. This should not happen with auto-initialization.");
                return false;
            }

            Debug.Log("[PlayKit SDK] Initializing...");
            if (_isInitialized) return true;

            // Load settings from PlayKitSettings ScriptableObject
            var settings = PlayKitSettings.Instance;
            if (settings == null)
            {
                Debug.LogError("[PlayKit SDK] PlayKitSettings not found. Please configure the SDK via Tools > PlayKit SDK > Settings");
                return false;
            }

            // Validate settings
            if (!settings.Validate(out string errorMessage))
            {
                Debug.LogError($"[PlayKit SDK] Configuration error: {errorMessage}");
                return false;
            }

            string gameId = settings.GameId;

            // Use developer token from settings if not explicitly provided
            if (developerToken == null && !settings.IgnoreDeveloperToken)
            {
                string settingsToken = settings.DeveloperToken;
                Debug.Log($"[PlayKit SDK] Developer token from settings: {(string.IsNullOrEmpty(settingsToken) ? "EMPTY" : "***" + settingsToken.Substring(settingsToken.Length > 10 ? settingsToken.Length - 10 : 0))}");
                if (!string.IsNullOrEmpty(settingsToken))
                {
                    developerToken = settingsToken;
                    Debug.Log("[PlayKit SDK] Using developer token from settings for development.");
                }
                else
                {
                    Debug.Log("[PlayKit SDK] No developer token found in settings. Will use player authentication.");
                }
            }

            if (developerToken != null && !settings.IgnoreDeveloperToken)
            {
                Debug.Log($"[PlayKit SDK] You are loading a developer token {developerToken}, this has strict rate limit and should not be used for production...");
                PlayKitAuthManager.Setup(gameId, developerToken);

                // Show developer key warning in non-editor builds
#if !UNITY_EDITOR
                ShowDeveloperKeyWarning();
#endif
            }
            else
            {
                PlayKitAuthManager.Setup(gameId);
            }

            bool authSuccess = await PlayKitAuthManager.AuthenticateAsync();

            if (!authSuccess)
            {
                Debug.LogError("[PlayKit SDK] SDK Authentication Failed. Cannot proceed.");
                return false;
            }

            _chatProvider = new Provider.AI.AIChatProvider(PlayKitAuthManager);
            _imageProvider = new Provider.AI.AIImageProvider(PlayKitAuthManager);
            _objectProvider = new Provider.AI.AIObjectProvider(PlayKitAuthManager);
            _transcriptionProvider = new Provider.AI.AITranscriptionProvider(PlayKitAuthManager);
            _isInitialized = true;
            Debug.Log("[PlayKit SDK] PlayKit_SDK Initialized Successfully");
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

                }
                else
                {
                    Debug.LogWarning("[PlayKit SDK] DeveloperKeyWarning prefab not found in Resources.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayKit SDK] Failed to show developer key warning: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the PlayerClient for querying user information and managing player data.
        /// This can be used to check user credits, get user info, etc.
        /// </summary>
        /// <returns>The PlayerClient instance, or null if SDK not initialized or user not authenticated</returns>
        public static PlayKit_PlayerClient GetPlayerClient()
        {
            if (!_isInitialized || PlayKitAuthManager == null)
            {
                Debug.LogWarning("SDK not initialized. Please call PlayKit_SDK.InitializeAsync() first.");
                return null;
            }

            return PlayKitAuthManager.GetPlayerClient();
        }

        /// <summary>
        /// Checks if the SDK is initialized and the user is authenticated
        /// </summary>
        /// <returns>True if ready to use, false otherwise</returns>
        public static bool IsReady()
        {
            return _isInitialized && PlayKitAuthManager != null;
        }

        public static class Factory
        {
            /// <summary>
            /// Creates a standard chat client with both text and structured output capabilities
            /// </summary>
            public static PlayKit_AIChatClient CreateChatClient(string modelName = null)
            {
                if (!Instance)
                {
                    Debug.LogError("[PlayKit SDK] SDK instance not found. This should not happen with auto-initialization.");
                    return null;
                }
                if (!_isInitialized)
                {
                    Debug.LogError("[PlayKit SDK] SDK not initialized. Please call PlayKit_SDK.InitializeAsync() and wait for it to complete first.");
                    return null;
                }

                // Load default model from settings if not specified
                string model = modelName ?? PlayKitSettings.Instance?.DefaultChatModel;
                var chatService = new Services.ChatService(_chatProvider);
                return new PlayKit_AIChatClient(model, chatService, _objectProvider);
            }

            /// <summary>
            /// Creates an image generation client for AI-powered image creation
            /// </summary>
            public static PlayKit_AIImageClient CreateImageClient(string modelName = null)
            {
                if (!Instance)
                {
                    Debug.LogError("[PlayKit SDK] SDK instance not found. This should not happen with auto-initialization.");
                    return null;
                }
                if (!_isInitialized)
                {
                    Debug.LogError("[PlayKit SDK] SDK not initialized. Please call PlayKit_SDK.InitializeAsync() and wait for it to complete first.");
                    return null;
                }

                // Load default model from settings if not specified
                string model = modelName ?? PlayKitSettings.Instance?.DefaultImageModel;
                if (string.IsNullOrEmpty(model))
                {
                    Debug.LogError("[PlayKit SDK] No image model specified. Please set Default Image Model in Tools > PlayKit SDK > Settings or provide a model name.");
                    return null;
                }

                return new PlayKit_AIImageClient(model, _imageProvider);
            }

            /// <summary>
            /// Creates an audio transcription client for speech-to-text conversion
            /// </summary>
            /// <param name="modelName">The transcription model to use (e.g., "whisper-1")</param>
            /// <returns>An audio transcription client</returns>
            public static PlayKit_AudioTranscriptionClient CreateTranscriptionClient(string modelName)
            {
                if (!Instance)
                {
                    Debug.LogError("[PlayKit SDK] SDK instance not found. This should not happen with auto-initialization.");
                    return null;
                }
                if (!_isInitialized)
                {
                    Debug.LogError("[PlayKit SDK] SDK not initialized. Please call PlayKit_SDK.InitializeAsync() and wait for it to complete first.");
                    return null;
                }

                if (string.IsNullOrEmpty(modelName))
                {
                    Debug.LogError("[PlayKit SDK] Transcription model name cannot be empty. Please specify a model like 'whisper-1'.");
                    return null;
                }

                var transcriptionService = new Services.TranscriptionService(_transcriptionProvider);
                return new PlayKit_AudioTranscriptionClient(modelName, transcriptionService);
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
            public static void CreateNpc(PlayKit_NPCClient recipient, string modelName = null)
            {
                if (!Instance)
                {
                    Debug.LogError("[PlayKit SDK] SDK instance not found. This should not happen with auto-initialization.");
                    return;
                }
                if (!_isInitialized)
                {
                    Debug.LogError("[PlayKit SDK] SDK not initialized. Please call PlayKit_SDK.InitializeAsync() and wait for it to complete first.");
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
        public static PlayKit_AudioTranscriptionClient CreateTranscriptionClient(string modelName)
        {
            return Factory.CreateTranscriptionClient(modelName);
        }
    }
}
