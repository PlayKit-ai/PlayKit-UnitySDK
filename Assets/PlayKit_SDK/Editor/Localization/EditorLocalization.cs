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
        private const string LANGUAGES_FOLDER = "Assets/PlayKit_SDK/Editor/Localization/Languages";

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
            Initialize();
        }

        /// <summary>
        /// Initialize the localization system with auto-detection
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized) return;

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
            isInitialized = true;
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

            string filePath = Path.Combine(LANGUAGES_FOLDER, $"{languageCode}.json");

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[PlayKit SDK] Language file not found: {filePath}. Falling back to en-US.");

                if (languageCode != "en-US")
                {
                    filePath = Path.Combine(LANGUAGES_FOLDER, "en-US.json");
                    if (!File.Exists(filePath))
                    {
                        Debug.LogError($"[PlayKit SDK] Fallback language file not found: {filePath}");
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            try
            {
                string jsonContent = File.ReadAllText(filePath);

                // Simple JSON parsing for key-value pairs
                ParseJsonTranslations(jsonContent);

                Debug.Log($"[PlayKit SDK] Loaded {translations.Count} translations for language: {languageCode}");
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
