using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayKit_SDK
{
    public class PlayKitSDK : MonoBehaviour
    {
        public const string VERSION = "v0.2.5.4";

        /// <summary>
        /// Sets standard SDK identification headers on a UnityWebRequest.
        /// Call this on every request before SendWebRequest().
        /// </summary>
        public static void SetSDKHeaders(UnityWebRequest request)
        {
            request.SetRequestHeader("X-SDK-Type", "Unity");
            request.SetRequestHeader("X-SDK-Version", VERSION);
        }

        // Configuration is now loaded from PlayKitSettings ScriptableObject
        // No need to manually place prefabs in scenes - SDK initializes automatically at runtime
        // Configure via: Tools > PlayKit SDK > Settings

        public static PlayKitSDK Instance { get; private set; }

        // Auth manager is created dynamically instead of being serialized
        private Auth.PlayKit_AuthManager authManager;

        // Flag to track if this instance was created by auto-initialization
        private static bool _isAutoInitializing = false;

        /// <summary>
        /// Automatically creates SDK instance at runtime startup.
        /// No manual prefab placement needed.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            // Check if auto-initialization is enabled in settings
            var settings = PlayKitSettings.Instance;
            if (settings != null && !settings.AutoInitialize)
            {
                return;
            }

            // Check if an instance already exists in the scene (for backward compatibility)
            if (Instance != null)
            {
                Debug.LogWarning("[PlayKit SDK] SDK instance already exists in scene. Auto-initialization skipped. Consider removing the old prefab.");
                return;
            }

            // Set flag before creating components to prevent warnings
            _isAutoInitializing = true;

            // Create SDK GameObject automatically
            GameObject sdkObject = new GameObject("PlayKit_SDK");

            // AddComponent<PlayKit_SDK>() triggers Awake() synchronously
            // Awake() will add AIContextManager and set Instance
            sdkObject.AddComponent<PlayKitSDK>();

            DontDestroyOnLoad(sdkObject);

            // Clear flag after initialization
            _isAutoInitializing = false;

            Debug.Log("[PlayKit SDK] SDK instance created. Starting async initialization...");

            // Automatically trigger async initialization (fire-and-forget)
            InitializeAsync().Forget();
        }

        private void Awake()
        {
            // Handle instance assignment
            if (Instance == null)
            {
                Instance = this;
                
                // Only call DontDestroyOnLoad if not already called in AutoInitialize
                if (!_isAutoInitializing)
                {
                    DontDestroyOnLoad(gameObject);
                    // Warn about scene prefab only if this wasn't auto-initialized
                    Debug.LogWarning("[PlayKit SDK] SDK initialized from scene prefab. Consider removing the prefab - SDK now initializes automatically.");
                }
            }
            else if (Instance != this)
            {
                // This is a duplicate - destroy it silently
                Destroy(gameObject);
                return;
            }

            // Create AuthManager component if not exists
            if (authManager == null)
            {
                authManager = gameObject.AddComponent<Auth.PlayKit_AuthManager>();
            }

            // Create AIContextManager component if not exists
            if (GetComponent<AIContextManager>() == null)
            {
                gameObject.AddComponent<AIContextManager>();
            }
        }

        private static bool _isInitialized = false;
        private static bool _isInitializing = false;
        private static UniTaskCompletionSource<bool> _initializationTask;
        private static Auth.PlayKit_AuthManager PlayKitAuthManager => Instance.authManager;
        private static Provider.IChatProvider _chatProvider;
        private static Provider.IImageProvider _imageProvider;
        private static Provider.AI.IObjectProvider _objectProvider;
        private static Provider.ITranscriptionProvider _transcriptionProvider;
        private static PlayKit_RechargeManager _rechargeManager;
        private static string _lastInitializationError;

        /// <summary>
        /// Event fired when SDK initialization fails. Subscribe to receive detailed error information.
        /// </summary>
        public static event Action<string> OnInitializationFailed;

        /// <summary>
        /// Event fired when SDK initialization succeeds.
        /// </summary>
        public static event Action OnInitializationSucceeded;

        /// <summary>
        /// Get the last initialization error message, if any.
        /// </summary>
        public static string LastInitializationError => _lastInitializationError;

        /// <summary>
        /// Asynchronously initializes the SDK. This must complete successfully before creating clients.
        /// It handles configuration loading and user authentication.
        /// Configuration is loaded from PlayKitSettings (Tools > PlayKit SDK > Settings).
        ///
        /// Note: SDK now auto-initializes at startup. You can still call this method explicitly -
        /// it will return immediately if already initialized, or wait for ongoing initialization to complete.
        /// This ensures backward compatibility with existing code.
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

            // Already initialized - return immediately (backward compatible)
            if (_isInitialized) return true;

            // Another initialization is in progress - wait for it to complete
            if (_isInitializing && _initializationTask != null)
            {
                Debug.Log("[PlayKit SDK] Initialization already in progress, waiting...");
                return await _initializationTask.Task;
            }

            // Start initialization
            _isInitializing = true;
            _initializationTask = new UniTaskCompletionSource<bool>();
            _lastInitializationError = null;
            Debug.Log("[PlayKit SDK] Initializing...");

            bool success = false;
            try
            {
                success = await DoInitializeAsync(developerToken);
            }
            finally
            {
                _isInitialized = success;
                _isInitializing = false;
                _initializationTask.TrySetResult(success);

                // Fire appropriate event
                if (success)
                {
                    OnInitializationSucceeded?.Invoke();
                }
                else if (!string.IsNullOrEmpty(_lastInitializationError))
                {
                    OnInitializationFailed?.Invoke(_lastInitializationError);
                }
            }

            return success;
        }

        /// <summary>
        /// Internal initialization logic. Called by InitializeAsync with proper concurrency handling.
        /// </summary>
        private static async UniTask<bool> DoInitializeAsync(string developerToken)
        {
            // Load settings from PlayKitSettings ScriptableObject
            var settings = PlayKitSettings.Instance;
            if (settings == null)
            {
                _lastInitializationError = "PlayKitSettings not found. Please configure the SDK via Tools > PlayKit SDK > Settings";
                Debug.LogError($"[PlayKit SDK] {_lastInitializationError}");
                return false;
            }

            // Validate settings
            if (!settings.Validate(out string errorMessage))
            {
                _lastInitializationError = $"Configuration error: {errorMessage}";
                Debug.LogError($"[PlayKit SDK] {_lastInitializationError}");
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

            // Discover platform addon for authentication and IAP
            var platformAddon = DiscoverPlatformAddon();

            bool authSuccess;

            // Check if developer token is configured
            if (developerToken != null && !settings.IgnoreDeveloperToken)
            {
                // Developer token provided - skip platform authentication
                Debug.Log("[PlayKit SDK] Using developer token, skipping platform authentication");
                authSuccess = await PlayKitAuthManager.AuthenticateAsync();

                if (!authSuccess)
                {
                    _lastInitializationError = "Developer token authentication failed. Please check your developer token.";
                    Debug.LogError($"[PlayKit SDK] {_lastInitializationError}");
                    return false;
                }

                // Initialize platform services for developer mode (without authentication)
                if (platformAddon != null)
                {
                    Debug.Log($"[PlayKit SDK] Initializing platform addon '{platformAddon.AddonId}' for developer mode");
                    bool platformInitialized = await platformAddon.InitializeForDeveloperModeAsync();

                    if (!platformInitialized)
                    {
                        Debug.LogWarning($"[PlayKit SDK] Platform addon '{platformAddon.AddonId}' initialization failed. Some features may not work.");
                        // Don't return false - developer token auth succeeded, so continue
                    }
                }
            }
            else if (platformAddon != null)
            {
                // No developer token - use platform-specific authentication
                var authProvider = platformAddon.GetAuthProvider();

                if (authProvider == null)
                {
                    _lastInitializationError = $"Platform addon '{platformAddon.AddonId}' does not provide authentication.";
                    Debug.LogError($"[PlayKit SDK] {_lastInitializationError}");
                    return false;
                }

                if (!authProvider.IsAvailable)
                {
                    _lastInitializationError = $"{authProvider.DisplayName} is not available. Please ensure the platform is properly configured.";
                    Debug.LogError($"[PlayKit SDK] {_lastInitializationError}");
                    return false;
                }

                Debug.Log($"[PlayKit SDK] Using platform authentication: {authProvider.DisplayName}");
                authSuccess = await PlayKitAuthManager.AuthenticateWithProviderAsync(authProvider);

                if (!authSuccess)
                {
                    _lastInitializationError = $"Platform authentication failed ({authProvider.DisplayName}). User may have cancelled or credentials are invalid.";
                    Debug.LogError($"[PlayKit SDK] {_lastInitializationError}");
                    return false;
                }
            }
            else
            {
                // No platform addon - use default browser authentication
                Debug.Log("[PlayKit SDK] Using default browser authentication");
                authSuccess = await PlayKitAuthManager.AuthenticateAsync();

                if (!authSuccess)
                {
                    _lastInitializationError = "Browser authentication failed. User may have cancelled or login failed.";
                    Debug.LogError($"[PlayKit SDK] {_lastInitializationError}");
                    return false;
                }
            }

            _chatProvider = new Provider.AI.AIChatProvider(PlayKitAuthManager);
            _imageProvider = new Provider.AI.AIImageProvider(PlayKitAuthManager);
            _objectProvider = new Provider.AI.AIObjectProvider(PlayKitAuthManager);
            _transcriptionProvider = new Provider.AI.AITranscriptionProvider(PlayKitAuthManager);

            // Initialize RechargeManager with balance getter for modal support
            _rechargeManager = new PlayKit_RechargeManager();
            _rechargeManager.Initialize(
                settings.BaseUrl,
                gameId,
                () => PlayKitAuthManager.GetPlayerClient()?.GetPlayerToken(),
                () => PlayKitAuthManager.GetPlayerClient()?.GetDisplayBalance() ?? 0f
            );

            // Register platform-specific recharge provider if available
            if (platformAddon != null)
            {
                var rechargeProvider = platformAddon.GetRechargeProvider();
                if (rechargeProvider != null && rechargeProvider.IsAvailable)
                {
                    Debug.Log($"[PlayKit SDK] Registering platform recharge provider: {rechargeProvider.RechargeMethod}");
                    _rechargeManager.RegisterProvider(rechargeProvider);
                    _rechargeManager.SetRechargeMethod(rechargeProvider.RechargeMethod);
                }
            }

            // Wire up PlayerClient with RechargeManager for auto-prompt recharge feature
            var playerClient = PlayKitAuthManager.GetPlayerClient();
            if (playerClient != null)
            {
                playerClient.SetRechargeManager(_rechargeManager);
                playerClient.AutoPromptRecharge = settings.EnableDefaultRechargeHandler;
            }

            Debug.Log("[PlayKit SDK] PlayKit_SDK Initialized Successfully");
            return true;
        }

        /// <summary>
        /// Discover and return a platform addon that can provide services for the current channel.
        /// Returns null if no matching platform addon is found.
        /// </summary>
        private static IPlayKitPlatformAddon DiscoverPlatformAddon()
        {
            var settings = PlayKitSettings.Instance;
            if (settings == null)
            {
                return null;
            }

            string channelType = settings.ChannelType;
            if (string.IsNullOrEmpty(channelType))
            {
                Debug.Log("[PlayKit SDK] No channel type configured");
                return null;
            }

            // Get all registered addons
            var allAddons = AddonRegistry.Instance.GetAllAddons();

            foreach (var kvp in allAddons)
            {
                var addon = kvp.Value;

                // Check if addon is enabled
                if (!settings.EnabledAddons.GetValueOrDefault(addon.AddonId, false))
                    continue;

                // Check if addon is installed
                if (!addon.IsInstalled)
                    continue;

                // Check if addon implements IPlayKitPlatformAddon
                if (addon is IPlayKitPlatformAddon platformAddon)
                {
                    // Check if it can provide services for this channel
                    if (platformAddon.CanProvideServicesForChannel(channelType))
                    {
                        Debug.Log($"[PlayKit SDK] Found platform addon: {addon.DisplayName} for channel: {channelType}");
                        return platformAddon;
                    }
                }
            }

            // standalone channel doesn't need any addon, so don't log warning
            if (channelType != "standalone")
            {
                Debug.Log($"[PlayKit SDK] No platform addon found for channel: {channelType}");
            }
            return null;
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
        /// Gets the RechargeManager for opening recharge portal.
        /// </summary>
        /// <returns>The RechargeManager instance, or null if SDK not initialized</returns>
        public static PlayKit_RechargeManager GetRechargeManager()
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("SDK not initialized. Please call PlayKit_SDK.InitializeAsync() first.");
                return null;
            }

            return _rechargeManager;
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
            /// <param name="modelName">The transcription model to use (e.g., "whisper-large")</param>
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
                    Debug.LogError("[PlayKit SDK] Transcription model name cannot be empty. Please specify a model like 'whisper-large'.");
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
            public static void CreateNpc(PlayKit_NPC recipient, string modelName = null)
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
        /// <param name="modelName">The transcription model to use (e.g., "whisper-large")</param>
        /// <returns>An audio transcription client</returns>
        public static PlayKit_AudioTranscriptionClient CreateTranscriptionClient(string modelName)
        {
            return Factory.CreateTranscriptionClient(modelName);
        }
    }
}
