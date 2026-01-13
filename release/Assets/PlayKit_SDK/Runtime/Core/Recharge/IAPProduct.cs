using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace PlayKit_SDK.Recharge
{
    /// <summary>
    /// Represents an IAP product available for purchase.
    /// Matches backend /api/games/{gameId}/products response structure.
    /// </summary>
    [Serializable]
    public class IAPProduct
    {
        /// <summary>
        /// Product SKU identifier
        /// </summary>
        [JsonProperty("sku")]
        public string Sku { get; set; }

        /// <summary>
        /// Display name for the product (may be i18n JSON string)
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Product description (may be i18n JSON string)
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>
        /// Price in cents (e.g., 999 = $9.99)
        /// </summary>
        [JsonProperty("price_cents")]
        public int PriceCents { get; set; }

        /// <summary>
        /// Currency code (e.g., "USD")
        /// </summary>
        [JsonProperty("currency")]
        public string Currency { get; set; }

        /// <summary>
        /// Formatted price string for display (e.g., "$9.99")
        /// </summary>
        public string FormattedPrice => PriceCents > 0 ? $"{PriceCents / 100.0:F2} {Currency ?? "USD"}" : "Free";

        /// <summary>
        /// Get localized product name based on current system language.
        /// Falls back to raw Name if not i18n format.
        /// </summary>
        public string LocalizedName => GetLocalizedName(GetSystemLanguageCode());

        /// <summary>
        /// Get localized product description based on current system language.
        /// Falls back to raw Description if not i18n format.
        /// </summary>
        public string LocalizedDescription => GetLocalizedDescription(GetSystemLanguageCode());

        /// <summary>
        /// Get localized product name for specified language.
        /// Supports i18n JSON format: {"en-US": "English", "zh-CN": "中文"}
        /// </summary>
        /// <param name="languageCode">Language code (e.g., "en-US", "zh-CN")</param>
        /// <returns>Localized name, or raw Name if parsing fails</returns>
        public string GetLocalizedName(string languageCode)
        {
            return GetLocalizedTextStatic(Name, languageCode);
        }

        /// <summary>
        /// Get localized product description for specified language.
        /// Supports i18n JSON format: {"en-US": "English", "zh-CN": "中文"}
        /// </summary>
        /// <param name="languageCode">Language code (e.g., "en-US", "zh-CN")</param>
        /// <returns>Localized description, or raw Description if parsing fails</returns>
        public string GetLocalizedDescription(string languageCode)
        {
            return GetLocalizedTextStatic(Description, languageCode);
        }

        /// <summary>
        /// Parse i18n JSON text and return localized value.
        /// Public static method for use by other classes.
        /// </summary>
        /// <param name="text">Text that may be i18n JSON format</param>
        /// <param name="languageCode">Language code (e.g., "en-US", "zh-CN")</param>
        /// <returns>Localized text, or original text if not i18n format</returns>
        public static string GetLocalizedTextStatic(string text, string languageCode)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Quick check: if doesn't start with '{', it's not JSON
            if (!text.TrimStart().StartsWith("{"))
                return text;

            try
            {
                var i18nDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(text);
                if (i18nDict == null || i18nDict.Count == 0)
                    return text;

                // Try exact match
                if (i18nDict.TryGetValue(languageCode, out string exactMatch) && !string.IsNullOrEmpty(exactMatch))
                    return exactMatch;

                // Try language prefix match (e.g., "zh" for "zh-CN")
                string languagePrefix = languageCode?.Split('-')[0];
                if (!string.IsNullOrEmpty(languagePrefix))
                {
                    foreach (var kvp in i18nDict)
                    {
                        if (kvp.Key.StartsWith(languagePrefix) && !string.IsNullOrEmpty(kvp.Value))
                            return kvp.Value;
                    }
                }

                // Fallback to en-US
                if (i18nDict.TryGetValue("en-US", out string enUs) && !string.IsNullOrEmpty(enUs))
                    return enUs;

                // Fallback to any English
                foreach (var kvp in i18nDict)
                {
                    if (kvp.Key.StartsWith("en") && !string.IsNullOrEmpty(kvp.Value))
                        return kvp.Value;
                }

                // Return first non-empty value
                foreach (var kvp in i18nDict)
                {
                    if (!string.IsNullOrEmpty(kvp.Value))
                        return kvp.Value;
                }

                return text;
            }
            catch
            {
                // Not valid JSON, return as-is
                return text;
            }
        }

        /// <summary>
        /// Get language code from Unity's system language.
        /// </summary>
        private static string GetSystemLanguageCode()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                    return "zh-CN";
                case SystemLanguage.ChineseTraditional:
                    return "zh-TW";
                case SystemLanguage.Japanese:
                    return "ja-JP";
                case SystemLanguage.Korean:
                    return "ko-KR";
                case SystemLanguage.German:
                    return "de-DE";
                case SystemLanguage.French:
                    return "fr-FR";
                case SystemLanguage.Spanish:
                    return "es-ES";
                case SystemLanguage.Portuguese:
                    return "pt-BR";
                case SystemLanguage.Russian:
                    return "ru-RU";
                case SystemLanguage.English:
                default:
                    return "en-US";
            }
        }
    }

    /// <summary>
    /// Result of a product list query
    /// </summary>
    [Serializable]
    public class ProductListResult
    {
        /// <summary>
        /// Whether the query was successful
        /// </summary>
        [JsonProperty("success")]
        public bool Success { get; set; }

        /// <summary>
        /// List of available products
        /// </summary>
        [JsonProperty("products")]
        public List<IAPProduct> Products { get; set; }

        /// <summary>
        /// Error message if query failed
        /// </summary>
        [JsonProperty("error")]
        public string Error { get; set; }
    }
}
