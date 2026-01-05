using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

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

        /// <summary>
        /// Custom recharge portal URL (optional, uses default if not set)
        /// </summary>
        public string RechargePortalUrl { get; set; }

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

        public UniTask<RechargeResult> RechargeAsync(string sku = null)
        {
            // For browser, SKU is ignored - user selects products in the web portal
            string url = GetRechargeUrl();

            Debug.Log($"[BrowserRechargeProvider] Opening recharge window: {url}");

            Application.OpenURL(url);

            OnRechargeInitiated?.Invoke();

            // Browser recharge is async - we can't track completion
            // The user will close the browser and the game should poll for balance updates
            return UniTask.FromResult(new RechargeResult
            {
                Initiated = true,
                Data = url
            });
        }
    }
}
