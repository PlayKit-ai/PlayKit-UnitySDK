using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using PlayKit_SDK.Recharge;
using UnityEngine;

namespace PlayKit_SDK
{
    /// <summary>
    /// Manages recharge functionality using platform-specific providers.
    /// Automatically selects the appropriate provider based on rechargeMethod from player-info API.
    ///
    /// Default: BrowserRechargeProvider (opens web portal)
    /// External providers (Steam, iOS, Android) can be registered via RegisterProvider()
    /// </summary>
    public class PlayKit_RechargeManager
    {
        private string _baseUrl;
        private string _gameId;
        private Func<string> _getPlayerToken;

        private IRechargeProvider _currentProvider;
        private BrowserRechargeProvider _browserProvider;
        private Dictionary<string, IRechargeProvider> _providers = new Dictionary<string, IRechargeProvider>();

        /// <summary>
        /// The currently active recharge method
        /// </summary>
        public string CurrentRechargeMethod => _currentProvider?.RechargeMethod ?? "unknown";

        /// <summary>
        /// Event fired when recharge is initiated
        /// </summary>
        public event Action OnRechargeInitiated;

        /// <summary>
        /// Event fired when recharge is completed (balance updated)
        /// </summary>
        public event Action<RechargeResult> OnRechargeCompleted;

        /// <summary>
        /// Event fired when recharge is cancelled
        /// </summary>
        public event Action OnRechargeCancelled;

        /// <summary>
        /// Event fired when recharge window is opened (for browser provider)
        /// </summary>
        [Obsolete("Use OnRechargeInitiated instead")]
        public event Action OnRechargeOpened;

        /// <summary>
        /// Custom recharge portal URL (for browser provider)
        /// </summary>
        public string RechargePortalUrl
        {
            get => _browserProvider?.RechargePortalUrl;
            set
            {
                if (_browserProvider != null)
                    _browserProvider.RechargePortalUrl = value;
            }
        }

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

            // Initialize default browser provider
            _browserProvider = new BrowserRechargeProvider();
            _browserProvider.Initialize(baseUrl, gameId, getPlayerToken);
            SubscribeToProvider(_browserProvider);

            // Register browser as default provider
            _providers["browser"] = _browserProvider;
            _currentProvider = _browserProvider;

            Debug.Log("[PlayKit_RechargeManager] Initialized with default browser provider");
        }

        /// <summary>
        /// Register an external recharge provider (e.g., Steam, iOS, Android).
        /// Called by platform-specific addons (SteamAddon, iOSAddon, etc.)
        /// </summary>
        /// <param name="provider">The provider to register</param>
        public void RegisterProvider(IRechargeProvider provider)
        {
            if (provider == null)
            {
                Debug.LogWarning("[PlayKit_RechargeManager] Cannot register null provider");
                return;
            }

            string method = provider.RechargeMethod?.ToLower() ?? "unknown";
            _providers[method] = provider;
            SubscribeToProvider(provider);

            Debug.Log($"[PlayKit_RechargeManager] Registered provider: {method}");
        }

        /// <summary>
        /// Get a registered provider by method name
        /// </summary>
        /// <typeparam name="T">The provider type</typeparam>
        /// <param name="method">The recharge method (e.g., "steam", "ios")</param>
        /// <returns>The provider, or null if not found</returns>
        public T GetProvider<T>(string method) where T : class, IRechargeProvider
        {
            if (_providers.TryGetValue(method?.ToLower() ?? "", out var provider))
            {
                return provider as T;
            }
            return null;
        }

        /// <summary>
        /// Set the recharge method (called automatically from player-info response).
        /// Valid values: "browser", "steam", "ios", "android"
        /// </summary>
        /// <param name="rechargeMethod">The recharge method to use</param>
        public void SetRechargeMethod(string rechargeMethod)
        {
            string method = rechargeMethod?.ToLower() ?? "browser";

            if (_providers.TryGetValue(method, out var provider) && provider.IsAvailable)
            {
                _currentProvider = provider;
                Debug.Log($"[PlayKit_RechargeManager] Switched to {method} provider");
            }
            else
            {
                // Fallback to browser
                _currentProvider = _browserProvider;
                if (method != "browser")
                {
                    Debug.LogWarning($"[PlayKit_RechargeManager] {method} provider not available, falling back to browser");
                }
            }
        }

        /// <summary>
        /// Get the recharge URL with authentication token (for browser provider)
        /// </summary>
        /// <returns>Full recharge URL, or null if not using browser provider</returns>
        public string GetRechargeUrl()
        {
            return _browserProvider?.GetRechargeUrl();
        }

        /// <summary>
        /// Open the recharge window/overlay
        /// </summary>
        /// <param name="sku">Product SKU (required for Steam, optional for browser)</param>
        public void OpenRechargeWindow(string sku = null)
        {
            RechargeAsync(sku).Forget();
        }

        /// <summary>
        /// Initiate a recharge operation
        /// </summary>
        /// <param name="sku">Product SKU (required for Steam, optional for browser)</param>
        /// <returns>Result of the recharge initiation</returns>
        public async UniTask<RechargeResult> RechargeAsync(string sku = null)
        {
            if (_currentProvider == null)
            {
                Debug.LogError("[PlayKit_RechargeManager] No recharge provider available");
                return new RechargeResult
                {
                    Initiated = false,
                    Error = "No recharge provider initialized"
                };
            }

            Debug.Log($"[PlayKit_RechargeManager] Initiating recharge via {CurrentRechargeMethod}");
            return await _currentProvider.RechargeAsync(sku);
        }

        private void SubscribeToProvider(IRechargeProvider provider)
        {
            provider.OnRechargeInitiated += () =>
            {
                OnRechargeInitiated?.Invoke();
#pragma warning disable CS0618 // Type or member is obsolete
                OnRechargeOpened?.Invoke();
#pragma warning restore CS0618
            };
            provider.OnRechargeCompleted += result => OnRechargeCompleted?.Invoke(result);
            provider.OnRechargeCancelled += () => OnRechargeCancelled?.Invoke();
        }
    }
}
