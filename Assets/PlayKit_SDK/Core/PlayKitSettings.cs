using UnityEngine;

namespace Developerworks.SDK
{
    /// <summary>
    /// ScriptableObject that stores PlayKit SDK configuration.
    /// Create via Assets/Create/PlayKit SDK/Settings or access via Tools/PlayKit SDK/Settings window.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayKitSettings", menuName = "PlayKit SDK/Settings", order = 1)]
    public class PlayKitSettings : ScriptableObject
    {
        private const string SETTINGS_PATH = "PlayKitSettings";
        private const string SETTINGS_FULL_PATH = "Assets/Developerworks_SDK/Resources/PlayKitSettings.asset";

        [Header("Game Configuration")]
        [Tooltip("Your Game ID from the PlayKit dashboard")]
        [SerializeField] private string gameId = "";

        [Header("AI Model Defaults")]
        [Tooltip("Default chat model (e.g., 'gpt-4o-mini'). Leave empty to use server default.")]
        [SerializeField] private string defaultChatModel = "";

        [Tooltip("Default image generation model. Leave empty to use server default.")]
        [SerializeField] private string defaultImageModel = "";

        [Header("Development Options")]
        [Tooltip("Developer token for testing (optional). Stored in project settings, can be committed to version control.")]
        [SerializeField] private string developerToken = "";

        [Tooltip("Use local developer token from EditorPrefs instead of project settings (not tracked by version control)")]
        [SerializeField] private bool useLocalDeveloperToken = false;

        [Tooltip("When enabled, ignores developer tokens and forces player authentication flow")]
        [SerializeField] private bool ignoreDeveloperToken = false;

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
                        string resourcesPath = "Assets/Developerworks_SDK/Resources";
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

        // Public properties
        public string GameId => gameId;
        public string DefaultChatModel => defaultChatModel;
        public string DefaultImageModel => defaultImageModel;
        public bool UseLocalDeveloperToken => useLocalDeveloperToken;
        public bool IgnoreDeveloperToken => ignoreDeveloperToken;

        /// <summary>
        /// Gets the developer token - either from project settings or EditorPrefs based on useLocalDeveloperToken flag.
        /// </summary>
        public string DeveloperToken
        {
            get
            {
#if UNITY_EDITOR
                if (useLocalDeveloperToken)
                {
                    return UnityEditor.EditorPrefs.GetString("PlayKit_LocalDeveloperToken", "");
                }
#endif
                return developerToken;
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
                errorMessage = "Game ID is required. Please configure it in Tools > PlayKit SDK > Settings";
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
                    // Delete the key when setting to empty to ensure clean state
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
        /// </summary>
        [UnityEditor.MenuItem("PlayKit SDK/Settings", priority = 0)]
        public static void OpenSettingsWindow()
        {
            var windowType = System.Type.GetType("Developerworks.SDK.PlayKitSettingsWindow, Developerworks_SDK.Editor");
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
