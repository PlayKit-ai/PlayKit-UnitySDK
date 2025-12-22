using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlayKit.SDK.Editor
{
    /// <summary>
    /// Editor localization manager for PlayKit SDK.
    /// Supports multiple languages with automatic detection and persistence.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorLocalization
    {
        private const string LANGUAGE_PREF_KEY = "PlayKit_EditorLanguage";
        private static string languagesFolder = null;

        // Supported languages
        public static readonly Dictionary<string, string> SupportedLanguages = new Dictionary<string, string>
        {
            { "en-US", "English" },
            { "zh-CN", "简体中文" },
            { "zh-TW", "繁體中文" },
            { "ja-JP", "日本語" },
            { "ko-KR", "한국어" }
        };

        private static string currentLanguage = "en-US";
        private static Dictionary<string, string> translations = new Dictionary<string, string>();
        private static bool isInitialized = false;

        // Short alias for easy use
        public static string Get(string key) => GetText(key);

        static EditorLocalization()
        {
            // Try immediate initialization
            Initialize();

            // Also register for delayed initialization in case AssetDatabase wasn't ready
            EditorApplication.delayCall += DelayedInitialize;
        }

        private static void DelayedInitialize()
        {
            EditorApplication.delayCall -= DelayedInitialize;

            // Re-initialize if no translations were loaded
            if (translations.Count == 0)
            {
                languagesFolder = null; // Reset cached folder
                isInitialized = false;
                Initialize();
            }
        }

        /// <summary>
        /// Find the Languages folder dynamically based on the script location
        /// </summary>
        private static string GetLanguagesFolder()
        {
            if (languagesFolder != null)
                return languagesFolder;

            // Try to find this script's location using GUID (may not work during early initialization)
            try
            {
                var guids = AssetDatabase.FindAssets("EditorLocalization t:MonoScript");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith("EditorLocalization.cs"))
                    {
                        // Go up one level from the script location to get the Localization folder
                        // Then add Languages subfolder
                        var localizationDir = Path.GetDirectoryName(path);
                        // Unity uses forward slashes
                        languagesFolder = Path.Combine(localizationDir, "Languages").Replace("\\", "/");
                        if (Directory.Exists(languagesFolder))
                        {
                            return languagesFolder;
                        }
                    }
                }
            }
            catch
            {
                // AssetDatabase may not be ready during early initialization
            }

            // Fallback: try common paths
            string[] possiblePaths = new[]
            {
                "Assets/PlayKit_SDK/Editor/Localization/Languages",
                "Assets/SDKs/Unity/Editor/Localization/Languages",
                "Packages/com.playkit.sdk/Editor/Localization/Languages"
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    languagesFolder = path;
                    return languagesFolder;
                }
            }

            // Last resort: use known path without checking
            languagesFolder = "Assets/PlayKit_SDK/Editor/Localization/Languages";
            return languagesFolder;
        }

        /// <summary>
        /// Initialize the localization system with auto-detection
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized && translations.Count > 0) return;

            // Reset state for re-initialization
            isInitialized = false;
            languagesFolder = null;

            // Try to load saved language preference
            string savedLanguage = EditorPrefs.GetString(LANGUAGE_PREF_KEY, "");

            if (!string.IsNullOrEmpty(savedLanguage) && SupportedLanguages.ContainsKey(savedLanguage))
            {
                currentLanguage = savedLanguage;
            }
            else
            {
                // Auto-detect from system language
                currentLanguage = DetectSystemLanguage();
                EditorPrefs.SetString(LANGUAGE_PREF_KEY, currentLanguage);
            }

            LoadLanguage(currentLanguage);

            // Only mark as initialized if we actually loaded translations
            isInitialized = translations.Count > 0;
        }

        /// <summary>
        /// Detect system language and map to supported language code
        /// </summary>
        private static string DetectSystemLanguage()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.Chinese:
                    return "zh-CN";

                case SystemLanguage.Japanese:
                    return "ja-JP";

                case SystemLanguage.Korean:
                    return "ko-KR";

                case SystemLanguage.English:
                default:
                    return "en-US";
            }
        }

        /// <summary>
        /// Load language file and parse translations
        /// </summary>
        private static void LoadLanguage(string languageCode)
        {
            translations.Clear();

            string folder = GetLanguagesFolder();
            // Ensure forward slashes for Unity path consistency
            string filePath = Path.Combine(folder, $"{languageCode}.json").Replace("\\", "/");

            // Debug: log the path being tried
            Debug.Log($"[PlayKit SDK] Trying to load language from: {filePath} (exists: {File.Exists(filePath)})");

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[PlayKit SDK] Language file not found: {filePath}. Falling back to en-US.");

                if (languageCode != "en-US")
                {
                    filePath = Path.Combine(folder, "en-US.json").Replace("\\", "/");
                    if (!File.Exists(filePath))
                    {
                        Debug.LogError($"[PlayKit SDK] Fallback language file not found: {filePath}. Languages folder: {folder}");
                        return;
                    }
                }
                else
                {
                    Debug.LogError($"[PlayKit SDK] en-US language file not found. Languages folder: {folder}");
                    return;
                }
            }

            try
            {
                string jsonContent = File.ReadAllText(filePath);

                // Simple JSON parsing for key-value pairs
                ParseJsonTranslations(jsonContent);

                if (translations.Count > 0)
                {
                    Debug.Log($"[PlayKit SDK] Loaded {translations.Count} translations for language: {languageCode}");
                }
                else
                {
                    Debug.LogWarning($"[PlayKit SDK] Parsed 0 translations from: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit SDK] Failed to load language file {languageCode}: {ex.Message}");
            }
        }

        /// <summary>
        /// Simple JSON parser for translations (key-value pairs)
        /// </summary>
        private static void ParseJsonTranslations(string json)
        {
            // Remove outer braces and whitespace
            json = json.Trim().TrimStart('{').TrimEnd('}');

            // Split by comma (handling escaped commas in strings)
            var lines = json.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim().TrimEnd(',');
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                // Find the colon separator
                int colonIndex = trimmed.IndexOf(':');
                if (colonIndex <= 0) continue;

                // Extract key (remove quotes)
                string key = trimmed.Substring(0, colonIndex).Trim().Trim('"');

                // Extract value (remove quotes, handle escape sequences)
                string value = trimmed.Substring(colonIndex + 1).Trim().Trim('"');
                value = value.Replace("\\n", "\n").Replace("\\\"", "\"");

                translations[key] = value;
            }
        }

        /// <summary>
        /// Set the current language and reload translations
        /// </summary>
        public static void SetLanguage(string languageCode)
        {
            if (!SupportedLanguages.ContainsKey(languageCode))
            {
                Debug.LogWarning($"[PlayKit SDK] Unsupported language code: {languageCode}");
                return;
            }

            currentLanguage = languageCode;
            EditorPrefs.SetString(LANGUAGE_PREF_KEY, languageCode);
            LoadLanguage(languageCode);
        }

        /// <summary>
        /// Get localized text for a key
        /// </summary>
        public static string GetText(string key)
        {
            if (!isInitialized)
            {
                Initialize();
            }

            if (translations.TryGetValue(key, out string value))
            {
                return value;
            }

            // Return key as fallback for debugging
            Debug.LogWarning($"[PlayKit SDK] Missing translation key: {key}");
            return $"[{key}]";
        }

        /// <summary>
        /// Get localized text with string formatting
        /// </summary>
        public static string GetFormat(string key, params object[] args)
        {
            string text = GetText(key);
            try
            {
                return string.Format(text, args);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit SDK] Format error for key '{key}': {ex.Message}");
                return text;
            }
        }

        /// <summary>
        /// Get current language code
        /// </summary>
        public static string GetCurrentLanguage()
        {
            return currentLanguage;
        }

        /// <summary>
        /// Get current language display name
        /// </summary>
        public static string GetCurrentLanguageName()
        {
            return SupportedLanguages.TryGetValue(currentLanguage, out string name) ? name : currentLanguage;
        }

        /// <summary>
        /// Get all available languages
        /// </summary>
        public static Dictionary<string, string> GetAvailableLanguages()
        {
            return new Dictionary<string, string>(SupportedLanguages);
        }

        /// <summary>
        /// Reload the localization system (useful after SDK is moved)
        /// </summary>
        public static void Reload()
        {
            isInitialized = false;
            languagesFolder = null;
            translations.Clear();
            Initialize();
        }
    }

    /// <summary>
    /// Short alias for EditorLocalization
    /// </summary>
    public static class L10n
    {
        public static string Get(string key) => EditorLocalization.GetText(key);
        public static string GetFormat(string key, params object[] args) => EditorLocalization.GetFormat(key, args);
    }
}
