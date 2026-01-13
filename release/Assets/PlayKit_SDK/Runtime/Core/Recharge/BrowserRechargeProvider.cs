using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace PlayKit_SDK.Recharge
{
    /// <summary>
    /// Browser-based recharge provider for standalone/web platforms.
    /// Opens the recharge portal in the system's default browser.
    /// </summary>
    public class BrowserRechargeProvider : IRechargeProvider
    {
        private string _baseUrl;
        private string _gameId;
        private Func<string> _getPlayerToken;
        private Func<float> _getCurrentBalance;

        /// <summary>
        /// Custom recharge portal URL (optional, uses default if not set)
        /// </summary>
        public string RechargePortalUrl { get; set; }

        /// <summary>
        /// Whether to show recharge confirmation modal before opening browser
        /// </summary>
        public bool ShowModal { get; set; } = true;

        public string RechargeMethod => "browser";

        public bool IsAvailable => true; // Browser is always available

        public event Action OnRechargeInitiated;
        public event Action<RechargeResult> OnRechargeCompleted;
        public event Action OnRechargeCancelled;

        public void Initialize(string baseUrl, string gameId, Func<string> getPlayerToken)
        {
            _baseUrl = baseUrl;
            _gameId = gameId;
            _getPlayerToken = getPlayerToken;

            Debug.Log("[BrowserRechargeProvider] Initialized");
        }

        /// <summary>
        /// Initialize with balance getter for modal support
        /// </summary>
        public void Initialize(string baseUrl, string gameId, Func<string> getPlayerToken, Func<float> getCurrentBalance)
        {
            _baseUrl = baseUrl;
            _gameId = gameId;
            _getPlayerToken = getPlayerToken;
            _getCurrentBalance = getCurrentBalance;

            Debug.Log("[BrowserRechargeProvider] Initialized with balance getter");
        }

        /// <summary>
        /// Set whether to show the recharge confirmation modal
        /// </summary>
        public void SetShowModal(bool showModal)
        {
            ShowModal = showModal;
        }

        /// <summary>
        /// Get the recharge URL with authentication token
        /// </summary>
        public string GetRechargeUrl()
        {
            string baseRechargeUrl = RechargePortalUrl ?? $"{_baseUrl}/recharge";
            string playerToken = _getPlayerToken?.Invoke();

            if (string.IsNullOrEmpty(playerToken))
            {
                Debug.LogWarning("[BrowserRechargeProvider] No player token available for recharge URL");
                return baseRechargeUrl;
            }

            // Build URL with query parameters
            string separator = baseRechargeUrl.Contains("?") ? "&" : "?";
            return $"{baseRechargeUrl}{separator}token={Uri.EscapeDataString(playerToken)}&gameId={Uri.EscapeDataString(_gameId)}";
        }

        public async UniTask<RechargeResult> RechargeAsync(string sku = null)
        {
            // CRITICAL: Prevent Steam channels from opening browser
            // Steam games must use Steam overlay for purchases
            string channelType = PlayKitSettings.Instance?.ChannelType ?? "standalone";
            if (channelType.StartsWith("steam"))
            {
                string errorMsg = "Browser recharge not available for Steam channel. Steam games must use Steam overlay for purchases.";
                Debug.LogError($"[BrowserRechargeProvider] {errorMsg}");

                return new RechargeResult
                {
                    Initiated = false,
                    Error = errorMsg
                };
            }

            // Show modal if enabled
            if (ShowModal)
            {
                float balance = _getCurrentBalance?.Invoke() ?? 0f;
                bool userConfirmed = await UI.PlayKit_RechargeModalManager.Instance.ShowModalAsync(balance);

                if (!userConfirmed)
                {
                    Debug.Log("[BrowserRechargeProvider] User cancelled recharge via modal");
                    OnRechargeCancelled?.Invoke();

                    return new RechargeResult
                    {
                        Initiated = false,
                        Error = "User cancelled"
                    };
                }
            }

            // For browser, SKU is ignored - user selects products in the web portal
            string url = GetRechargeUrl();

            Debug.Log($"[BrowserRechargeProvider] Opening recharge window: {url}");

            Application.OpenURL(url);

            OnRechargeInitiated?.Invoke();

            // Browser recharge is async - we can't track completion
            // The user will close the browser and the game should poll for balance updates
            return new RechargeResult
            {
                Initiated = true,
                Data = url
            };
        }

        /// <summary>
        /// Get available IAP products from backend.
        /// For browser recharge, this is optional - users select products in the web portal.
        /// </summary>
        public async UniTask<ProductListResult> GetAvailableProductsAsync()
        {
            string playerToken = _getPlayerToken?.Invoke();
            if (string.IsNullOrEmpty(playerToken))
            {
                Debug.LogError("[BrowserRechargeProvider] No player token available for GetAvailableProductsAsync");
                return new ProductListResult
                {
                    Success = false,
                    Error = "No player token available"
                };
            }

            try
            {
                string url = $"{_baseUrl}/api/external/games/{_gameId}/products";

                using (var request = UnityWebRequest.Get(url))
                {
                    // Add Authorization header with player token
                    request.SetRequestHeader("Authorization", $"Bearer {playerToken}");

                    await request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var response = JsonConvert.DeserializeObject<ProductsApiResponse>(request.downloadHandler.text);

                        if (response != null && response.Success)
                        {
                            return new ProductListResult
                            {
                                Success = true,
                                Products = response.Products ?? new System.Collections.Generic.List<IAPProduct>()
                            };
                        }
                        else
                        {
                            return new ProductListResult
                            {
                                Success = false,
                                Error = response?.Error ?? "Failed to load products"
                            };
                        }
                    }
                    else
                    {
                        string error = request.downloadHandler?.text ?? request.error;
                        Debug.LogError($"[BrowserRechargeProvider] Failed to load products: {error}");
                        return new ProductListResult
                        {
                            Success = false,
                            Error = $"Network error: {error}"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return new ProductListResult
                {
                    Success = false,
                    Error = $"Exception: {ex.Message}"
                };
            }
        }

        [Serializable]
        private class ProductsApiResponse
        {
            [JsonProperty("success")] public bool Success { get; set; }
            [JsonProperty("products")] public System.Collections.Generic.List<IAPProduct> Products { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
        }
    }
}
