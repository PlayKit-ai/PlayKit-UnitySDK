using System;
using UnityEngine;

namespace PlayKit_SDK.UI
{
    /// <summary>
    /// Singleton manager for balance change popups.
    /// Automatically listens for balance changes and shows popup when enabled in settings.
    /// Loads the PlayKit_BalancePopup prefab from Resources folder.
    ///
    /// Supports two modes:
    /// 1. Normal mode: Shows popup on balance change, auto-hides after animation
    /// 2. Persistent mode: Popup stays visible from game start until manually hidden
    /// </summary>
    public class PlayKit_BalancePopupManager : MonoBehaviour
    {
        private const string PREFAB_PATH = "PlayKit_BalancePopup";

        private static PlayKit_BalancePopupManager _instance;
        private PlayKit_BalancePopupController _controller;
        private float _lastKnownBalance = -1f;
        private bool _initialized;
        private bool _persistentMode;

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static PlayKit_BalancePopupManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[PlayKit_BalancePopupManager]");
                    _instance = go.AddComponent<PlayKit_BalancePopupManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Initialize the popup manager and start listening for balance changes.
        /// Should be called after SDK initialization.
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
            {
                Debug.Log("[PlayKit_BalancePopupManager] Already initialized.");
                return;
            }

            // Check if feature is enabled in settings
            var settings = PlayKitSettings.Instance;
            if (settings == null)
            {
                Debug.LogWarning("[PlayKit_BalancePopupManager] PlayKitSettings not found.");
                return;
            }

            // Check if any balance popup feature is enabled
            bool showBalanceChange = settings.ShowBalanceChangePopup;
            bool keepPersistent = settings.KeepBalancePopupPersistent;

            if (!showBalanceChange && !keepPersistent)
            {
                Debug.Log("[PlayKit_BalancePopupManager] Balance popup is disabled in settings.");
                return;
            }

            var playerClient = PlayKitSDK.GetPlayerClient();
            if (playerClient == null)
            {
                Debug.LogWarning("[PlayKit_BalancePopupManager] PlayerClient not available. SDK may not be initialized.");
                return;
            }

            // Record initial balance
            _lastKnownBalance = playerClient.GetDisplayBalance();

            // Subscribe to balance updates
            playerClient.OnBalanceUpdated += OnBalanceUpdated;

            _initialized = true;
            _persistentMode = keepPersistent;

            Debug.Log($"[PlayKit_BalancePopupManager] Initialized. Persistent mode: {_persistentMode}");

            // If persistent mode is enabled, show popup immediately
            if (_persistentMode)
            {
                ShowPersistentPopup(_lastKnownBalance);
            }
        }

        private void OnBalanceUpdated(float newBalance)
        {
            // Skip if this is the first balance update (initialization)
            if (_lastKnownBalance < 0)
            {
                _lastKnownBalance = newBalance;

                // If persistent mode, update the display
                if (_persistentMode && _controller != null)
                {
                    _controller.UpdateBalance(newBalance);
                }
                return;
            }

            // Skip if balance hasn't actually changed
            if (Math.Abs(newBalance - _lastKnownBalance) < 0.001f)
            {
                return;
            }

            float oldBalance = _lastKnownBalance;
            _lastKnownBalance = newBalance;

            // In persistent mode, just update the display value
            if (_persistentMode)
            {
                if (_controller != null)
                {
                    _controller.UpdateBalance(newBalance);
                }
            }
            // In normal mode, show the change animation
            else if (PlayKitSettings.Instance?.ShowBalanceChangePopup == true)
            {
                ShowPopup(oldBalance, newBalance);
            }
        }

        private void ShowPopup(float oldBalance, float newBalance)
        {
            // Lazy load controller
            if (_controller == null)
            {
                LoadPrefab();
            }

            if (_controller != null)
            {
                _controller.Show(oldBalance, newBalance);
            }
        }

        private void ShowPersistentPopup(float balance)
        {
            // Lazy load controller
            if (_controller == null)
            {
                LoadPrefab();
            }

            if (_controller != null)
            {
                _controller.ShowPersistent(balance);
                Debug.Log($"[PlayKit_BalancePopupManager] Showing persistent popup with balance: {balance}");
            }
        }

        private void LoadPrefab()
        {
            var prefab = Resources.Load<GameObject>(PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogWarning($"[PlayKit_BalancePopupManager] Prefab not found at Resources/{PREFAB_PATH}. " +
                    "Please create a prefab with PlayKit_BalancePopupController component.");
                return;
            }

            var instance = Instantiate(prefab);
            DontDestroyOnLoad(instance);

            _controller = instance.GetComponent<PlayKit_BalancePopupController>();
            if (_controller == null)
            {
                Debug.LogError("[PlayKit_BalancePopupManager] Prefab is missing PlayKit_BalancePopupController component.");
                Destroy(instance);
                return;
            }

            // Subscribe to recharge button click
            _controller.OnRechargeClicked += OnRechargeButtonClicked;
        }

        private async void OnRechargeButtonClicked()
        {
            Debug.Log("[PlayKit_BalancePopupManager] Recharge button clicked.");

            var rechargeManager = PlayKitSDK.GetRechargeManager();
            if (rechargeManager != null)
            {
                // In non-persistent mode, hide popup before opening recharge
                if (!_persistentMode)
                {
                    HidePopup();
                }
                await rechargeManager.RechargeAsync();
            }
            else
            {
                Debug.LogWarning("[PlayKit_BalancePopupManager] RechargeManager not available.");
            }
        }

        /// <summary>
        /// Manually trigger a balance change popup (for testing or custom use)
        /// </summary>
        /// <param name="oldBalance">Previous balance value</param>
        /// <param name="newBalance">New balance value</param>
        public void ShowBalanceChange(float oldBalance, float newBalance)
        {
            ShowPopup(oldBalance, newBalance);
        }

        /// <summary>
        /// Hide any currently showing popup
        /// </summary>
        public void HidePopup()
        {
            if (_controller != null)
            {
                _controller.Hide();
            }
            _persistentMode = false;
        }

        /// <summary>
        /// Check if the popup is currently showing
        /// </summary>
        public bool IsPopupShowing => _controller != null && _controller.IsShowing;

        /// <summary>
        /// Check if the popup is in persistent mode
        /// </summary>
        public bool IsPersistent => _persistentMode;

        #region Static API

        /// <summary>
        /// Show the balance popup in persistent mode (stays visible until manually hidden).
        /// Displays the current balance from PlayerClient.
        /// </summary>
        public static void Show()
        {
            var playerClient = PlayKitSDK.GetPlayerClient();
            float balance = playerClient?.GetDisplayBalance() ?? 0f;
            Show(balance);
        }

        /// <summary>
        /// Show the balance popup in persistent mode with a specific balance value.
        /// </summary>
        /// <param name="balance">Balance value to display</param>
        public static void Show(float balance)
        {
            Instance.EnsureControllerLoaded();
            if (Instance._controller != null)
            {
                Instance._persistentMode = true;
                Instance._controller.ShowPersistent(balance);
            }
        }

        /// <summary>
        /// Hide the balance popup.
        /// </summary>
        public static void Hide()
        {
            if (_instance != null)
            {
                _instance.HidePopup();
            }
        }

        /// <summary>
        /// Update the displayed balance value (for persistent mode).
        /// </summary>
        /// <param name="balance">New balance value to display</param>
        public static void UpdateBalance(float balance)
        {
            if (_instance != null && _instance._controller != null)
            {
                _instance._controller.UpdateBalance(balance);
            }
        }

        /// <summary>
        /// Show a balance change animation (old value -> new value with change indicator).
        /// The popup will auto-hide after the animation completes (unless in persistent mode).
        /// </summary>
        /// <param name="oldBalance">Previous balance value</param>
        /// <param name="newBalance">New balance value</param>
        public static void ShowChange(float oldBalance, float newBalance)
        {
            Instance.ShowPopup(oldBalance, newBalance);
        }

        /// <summary>
        /// Check if the popup is currently visible.
        /// </summary>
        public static bool IsVisible => _instance != null && _instance.IsPopupShowing;

        /// <summary>
        /// Set persistent mode on or off.
        /// When enabled, the popup stays visible and only updates the balance value.
        /// When disabled, the popup shows change animations and auto-hides.
        /// </summary>
        /// <param name="persistent">Whether to enable persistent mode</param>
        public static void SetPersistent(bool persistent)
        {
            if (_instance != null)
            {
                _instance._persistentMode = persistent;

                if (persistent)
                {
                    // Show persistent popup with current balance
                    var playerClient = PlayKitSDK.GetPlayerClient();
                    float balance = playerClient?.GetDisplayBalance() ?? 0f;
                    Show(balance);
                }
            }
        }

        #endregion

        private void EnsureControllerLoaded()
        {
            if (_controller == null)
            {
                LoadPrefab();
            }
        }

        private void OnDestroy()
        {
            var playerClient = PlayKitSDK.GetPlayerClient();
            if (playerClient != null)
            {
                playerClient.OnBalanceUpdated -= OnBalanceUpdated;
            }

            // Unsubscribe from recharge button click
            if (_controller != null)
            {
                _controller.OnRechargeClicked -= OnRechargeButtonClicked;
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
