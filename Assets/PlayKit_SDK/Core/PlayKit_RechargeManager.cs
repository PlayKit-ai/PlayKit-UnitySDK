using System;
using UnityEngine;

namespace PlayKit_SDK
{
    /// <summary>
    /// Manages recharge functionality - opening recharge portal in browser.
    /// This is an API-only implementation without UI components.
    /// </summary>
    public class PlayKit_RechargeManager
    {
        private string _baseUrl;
        private string _gameId;
        private Func<string> _getPlayerToken;

        /// <summary>
        /// Event fired when recharge window is opened
        /// </summary>
        public event Action OnRechargeOpened;

        /// <summary>
        /// Custom recharge portal URL (optional, uses default if not set)
        /// </summary>
        public string RechargePortalUrl { get; set; }

        /// <summary>
        /// Initialize the RechargeManager
        /// </summary>
        /// <param name="baseUrl">Base URL for the API</param>
        /// <param name="gameId">Game ID</param>
        /// <param name="getPlayerToken">Function to get the current player token</param>
        public void Initialize(string baseUrl, string gameId, Func<string> getPlayerToken)
        {
            _baseUrl = baseUrl;
            _gameId = gameId;
            _getPlayerToken = getPlayerToken;
        }

        /// <summary>
        /// Get the recharge URL with authentication token
        /// </summary>
        /// <returns>Full recharge URL</returns>
        public string GetRechargeUrl()
        {
            string baseRechargeUrl = RechargePortalUrl ?? $"{_baseUrl}/recharge";
            string playerToken = _getPlayerToken?.Invoke();

            if (string.IsNullOrEmpty(playerToken))
            {
                Debug.LogWarning("[PlayKit_RechargeManager] No player token available for recharge URL");
                return baseRechargeUrl;
            }

            // Build URL with query parameters
            string separator = baseRechargeUrl.Contains("?") ? "&" : "?";
            return $"{baseRechargeUrl}{separator}token={Uri.EscapeDataString(playerToken)}&gameId={Uri.EscapeDataString(_gameId)}";
        }

        /// <summary>
        /// Open the recharge window in the default browser
        /// </summary>
        public void OpenRechargeWindow()
        {
            string url = GetRechargeUrl();

            Debug.Log($"[PlayKit_RechargeManager] Opening recharge window: {url}");

            Application.OpenURL(url);

            OnRechargeOpened?.Invoke();
        }
    }
}
