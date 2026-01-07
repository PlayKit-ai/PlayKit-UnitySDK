using System;
using System.Collections.Generic;
using Newtonsoft.Json;

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
        /// Display name for the product
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Product description
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
