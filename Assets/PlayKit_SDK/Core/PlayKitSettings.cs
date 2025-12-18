using UnityEngine;

namespace PlayKit_SDK
{
    /// <summary>
    /// ScriptableObject that stores PlayKit SDK configuration.
    /// Create via Assets/Create/PlayKit SDK/Settings or access via Tools/PlayKit SDK/Settings window.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayKitSettings", menuName = "PlayKit SDK/Settings", order = 1)]
    public class PlayKitSettings : ScriptableObject
    {
        private const string SETTINGS_PATH = "PlayKitSettings";
        private const string SETTINGS_FULL_PATH = "Assets/PlayKit_SDK/Resources/PlayKitSettings.asset";

        [Header("Game Configuration")]
        [Tooltip("Your Game ID from the PlayKit dashboard")]
        [SerializeField] private string gameId = "";

        [Header("AI Model Defaults")]
        [Tooltip("Default chat model. Leave empty to use server default.")]
        [SerializeField] private string defaultChatModel = "chat-default";

        [Tooltip("Default image generation model. Leave empty to use server default.")]
        [SerializeField] private string defaultImageModel = "image-default";

        [Header("Development Options")]
        [Tooltip("When enabled, ignores developer tokens and forces player authentication flow")]
        [SerializeField] private bool ignoreDeveloperToken = false;

        [Header("Advanced Settings")]
        [Tooltip("Override the default API base URL. Leave empty to use default (https://playkit.ai).")]
        [SerializeField] private string customBaseUrl = "";

        // Singleton instance
        private static PlayKitSettings _instance;

        /// <summary>
        /// Gets the singleton instance of PlayKitSettings.
        /// Loads from Resources/PlayKitSettings.asset or creates a new one if not found.
        /// </summary>
        public static PlayKitSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<PlayKitSettings>(SETTINGS_PATH);

#if UNITY_EDITOR
                    // Create default settings asset if it doesn't exist
                    if (_instance == null)
                    {
                        _instance = CreateInstance<PlayKitSettings>();

                        // Ensure Resources folder exists
                        string resourcesPath = "Assets/PlayKit_SDK/Resources";
                        if (!UnityEditor.AssetDatabase.IsValidFolder(resourcesPath))
                        {
                            string[] folders = resourcesPath.Split('/');
                            string currentPath = folders[0];
                            for (int i = 1; i < folders.Length; i++)
                            {
                                string parentPath = currentPath;
                                currentPath += "/" + folders[i];
                                if (!UnityEditor.AssetDatabase.IsValidFolder(currentPath))
                                {
                                    UnityEditor.AssetDatabase.CreateFolder(parentPath, folders[i]);
                                }
                            }
                        }

                        UnityEditor.AssetDatabase.CreateAsset(_instance, SETTINGS_FULL_PATH);
                        UnityEditor.AssetDatabase.SaveAssets();
                        Debug.Log($"PlayKit SDK: Created default settings at {SETTINGS_FULL_PATH}");
                    }
#else
                    if (_instance == null)
                    {
                        Debug.LogError("PlayKit SDK: Settings file not found! Please configure the SDK via Tools > PlayKit SDK > Settings in the Unity Editor.");
                    }
#endif
                }

                return _instance;
            }
        }

        // Constants
        private const string DEFAULT_BASE_URL = "https://playkit.ai";

        // Public properties
        public string GameId
        {
            get => gameId;
#if UNITY_EDITOR
            set
            {
                gameId = value;
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }

        public string DefaultChatModel => defaultChatModel;
        public string DefaultImageModel => defaultImageModel;
        public bool IgnoreDeveloperToken => ignoreDeveloperToken;
        public string CustomBaseUrl => customBaseUrl;

        /// <summary>
        /// Gets the effective base URL for API calls.
        /// Returns custom URL if set, otherwise returns the default (https://playkit.ai).
        /// </summary>
        public string BaseUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(customBaseUrl))
                    return customBaseUrl.TrimEnd('/');
                return DEFAULT_BASE_URL;
            }
        }

        /// <summary>
        /// Gets the base URL for AI API endpoints.
        /// Format: {BaseUrl}/ai/{GameId}
        /// </summary>
        public string AIBaseUrl => $"{BaseUrl}/ai/{GameId}";

        /// <summary>
        /// Gets the base URL for Auth API endpoints.
        /// Format: {BaseUrl}/api
        /// </summary>
        public string AuthBaseUrl => $"{BaseUrl}/api";

        /// <summary>
        /// Gets the developer token from local storage (EditorPrefs).
        /// Tokens are always stored locally and never committed to version control.
        /// </summary>
        public string DeveloperToken
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetString("PlayKit_LocalDeveloperToken", "");
#else
                return "";
#endif
            }
        }

        /// <summary>
        /// Validates the settings configuration.
        /// </summary>
        /// <returns>True if settings are valid, false otherwise</returns>
        public bool Validate(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                errorMessage = "Game ID is required. Please select a game in Tools > PlayKit SDK > Settings";
                return false;
            }

            errorMessage = null;
            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Gets or sets the local developer token (stored in EditorPrefs, not committed to version control).
        /// </summary>
        public static string LocalDeveloperToken
        {
            get => UnityEditor.EditorPrefs.GetString("PlayKit_LocalDeveloperToken", "");
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    UnityEditor.EditorPrefs.DeleteKey("PlayKit_LocalDeveloperToken");
                }
                else
                {
                    UnityEditor.EditorPrefs.SetString("PlayKit_LocalDeveloperToken", value);
                }
            }
        }

        /// <summary>
        /// Clears the local developer token from EditorPrefs.
        /// </summary>
        public static void ClearLocalDeveloperToken()
        {
            UnityEditor.EditorPrefs.DeleteKey("PlayKit_LocalDeveloperToken");
        }

        /// <summary>
        /// Opens the PlayKit SDK settings window.
        /// Note: The MenuItem is defined in PlayKitSettingsWindow.cs to avoid duplicate entries.
        /// </summary>
        public static void OpenSettingsWindow()
        {
            var windowType = System.Type.GetType("PlayKit_SDK.PlayKitSettingsWindow, PlayKit_SDK.Editor");
            if (windowType != null)
            {
                var window = UnityEditor.EditorWindow.GetWindow(windowType, false, "PlayKit SDK Settings");
                window.Show();
            }
            else
            {
                UnityEngine.Debug.LogError("[PlayKit SDK] Could not find PlayKitSettingsWindow type.");
            }
        }
#endif
    }
}
