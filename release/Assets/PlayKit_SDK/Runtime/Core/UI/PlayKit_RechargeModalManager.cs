using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using PlayKit_SDK.Recharge;

namespace PlayKit_SDK.UI
{
    /// <summary>
    /// Manager for loading and displaying the recharge modal.
    /// Loads the modal prefab from Resources and manages its lifecycle.
    ///
    /// Shows Balance Popup in persistent mode when modal is opened.
    /// Shows spinner during network loading.
    ///
    /// Prefab path: Resources/RechargeModal.prefab
    /// </summary>
    public class PlayKit_RechargeModalManager : MonoBehaviour
    {
        private static PlayKit_RechargeModalManager _instance;
        private PlayKit_RechargeModalController _currentModal;
        private bool _isWaitingForResponse;
        private bool _userConfirmed;

        // For provider-based modal with direct product purchase
        private IRechargeModalProvider _currentProvider;
        private UniTaskCompletionSource<RechargeModalResult> _modalCompletionSource;

        /// <summary>
        /// Singleton instance (created on demand)
        /// </summary>
        public static PlayKit_RechargeModalManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[PlayKit_RechargeModalManager]");
                    _instance = go.AddComponent<PlayKit_RechargeModalManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Show the recharge modal using a modal provider (for channel customization).
        /// This is the recommended method for new implementations.
        /// </summary>
        /// <param name="modalProvider">Provider that supplies modal content and handles confirmation</param>
        /// <param name="currentBalance">Current user balance (displayed via Balance Popup)</param>
        /// <param name="language">Language code (e.g., "en-US", "zh-CN"). Defaults to system language.</param>
        /// <returns>Result containing user choice and selected SKU</returns>
        public async UniTask<RechargeModalResult> ShowModalAsync(
            IRechargeModalProvider modalProvider,
            float currentBalance,
            string language = null)
        {
            if (modalProvider == null)
            {
                Debug.LogError("[PlayKit_RechargeModalManager] Modal provider is null");
                return RechargeModalResult.Failed("Modal provider is null");
            }

            // Use system language if not specified
            if (string.IsNullOrEmpty(language))
            {
                language = GetSystemLanguage();
            }

            try
            {
                // 1. Load modal UI if not already loaded
                if (_currentModal == null)
                {
                    bool loaded = await LoadModalAsync();
                    if (!loaded)
                    {
                        Debug.LogError("[PlayKit_RechargeModalManager] Failed to load modal prefab");
                        return RechargeModalResult.Failed("Failed to load modal UI");
                    }
                }

                // 2. Show Balance Popup in persistent mode
                PlayKit_BalancePopupManager.Show(currentBalance);

                // 3. Show loading spinner
                _currentModal.ShowLoading();

                // 4. Get modal content from provider (network request)
                var content = await modalProvider.GetModalContentAsync(currentBalance, language);

                // 5. Store current provider and reset state
                _currentProvider = modalProvider;
                _modalCompletionSource = new UniTaskCompletionSource<RechargeModalResult>();

                // 6. Show modal content (hides spinner)
                _currentModal.Show(content);

                // 7. Wait for result (either from product purchase, simple confirm, or cancel)
                var result = await _modalCompletionSource.Task;

                // 8. Hide Balance Popup when modal closes
                PlayKit_BalancePopupManager.Hide();

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_RechargeModalManager] Exception: {ex.Message}");

                // Hide spinner and balance popup on error
                if (_currentModal != null)
                {
                    _currentModal.Hide();
                }
                PlayKit_BalancePopupManager.Hide();

                return RechargeModalResult.Failed($"Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Show the recharge modal and wait for user response (legacy method for backward compatibility)
        /// </summary>
        /// <param name="balance">Current balance to display</param>
        /// <param name="language">Language code (e.g., "en-US", "zh-CN"). Defaults to system language.</param>
        /// <returns>True if user clicked Recharge, false if user clicked Cancel</returns>
        public async UniTask<bool> ShowModalAsync(float balance, string language = null)
        {
            // Use system language if not specified
            if (string.IsNullOrEmpty(language))
            {
                language = GetSystemLanguage();
            }

            // Load modal if not already loaded
            if (_currentModal == null)
            {
                bool loaded = await LoadModalAsync();
                if (!loaded)
                {
                    Debug.LogError("[PlayKit_RechargeModalManager] Failed to load modal prefab. Defaulting to no confirmation.");
                    return true;
                }
            }

            // Show Balance Popup in persistent mode
            PlayKit_BalancePopupManager.Show(balance);

            // Reset state
            _isWaitingForResponse = true;
            _userConfirmed = false;
            _currentProvider = null;

            // Show modal (legacy method)
            _currentModal.Show(balance, language);

            // Wait for user response
            await UniTask.WaitUntil(() => !_isWaitingForResponse);

            // Hide Balance Popup
            PlayKit_BalancePopupManager.Hide();

            return _userConfirmed;
        }

        /// <summary>
        /// Load the modal prefab from Resources
        /// </summary>
        private async UniTask<bool> LoadModalAsync()
        {
            try
            {
                string prefabPath = GetPrefabPath();

                Debug.Log($"[PlayKit_RechargeModalManager] Loading modal from: {prefabPath}");

                var prefab = Resources.Load<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"[PlayKit_RechargeModalManager] Failed to load modal prefab at Resources/{prefabPath}.prefab");
                    return false;
                }

                var modalObj = Instantiate(prefab);
                DontDestroyOnLoad(modalObj);

                _currentModal = modalObj.GetComponent<PlayKit_RechargeModalController>();
                if (_currentModal == null)
                {
                    Debug.LogError("[PlayKit_RechargeModalManager] Modal prefab is missing PlayKit_RechargeModalController component");
                    Destroy(modalObj);
                    return false;
                }

                // Subscribe to events
                _currentModal.OnRechargeClicked += OnRechargeClicked;
                _currentModal.OnCancelClicked += OnCancelClicked;
                _currentModal.OnProductPurchaseClicked += OnProductPurchaseClicked;

                Debug.Log("[PlayKit_RechargeModalManager] Modal loaded successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_RechargeModalManager] Exception loading modal: {ex.Message}");
                return false;
            }
        }

        private string GetPrefabPath()
        {
            return "RechargeModal";
        }

        /// <summary>
        /// Get system language in format compatible with PlayKit localization
        /// </summary>
        public static string GetSystemLanguage()
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
                default:
                    return "en-US";
            }
        }

        private void OnRechargeClicked()
        {
            // For legacy mode
            _userConfirmed = true;
            _isWaitingForResponse = false;

            if (_currentModal != null)
            {
                _currentModal.Hide();
            }

            // For provider-based mode (simple confirmation without products)
            if (_currentProvider != null && _modalCompletionSource != null)
            {
                HandleProviderConfirmAsync(null).Forget();
            }
        }

        private void OnCancelClicked()
        {
            // For legacy mode
            _userConfirmed = false;
            _isWaitingForResponse = false;

            if (_currentModal != null)
            {
                _currentModal.Hide();
            }

            // For provider-based mode
            if (_modalCompletionSource != null)
            {
                _modalCompletionSource.TrySetResult(RechargeModalResult.Cancelled());
                _modalCompletionSource = null;
            }
        }

        private void OnProductPurchaseClicked(string sku)
        {
            Debug.Log($"[PlayKit_RechargeModalManager] Product purchase clicked: {sku}");

            if (_currentModal != null)
            {
                _currentModal.Hide();
            }

            // Handle the purchase through the provider
            if (_currentProvider != null && _modalCompletionSource != null)
            {
                HandleProviderConfirmAsync(sku).Forget();
            }
        }

        private async UniTaskVoid HandleProviderConfirmAsync(string sku)
        {
            try
            {
                var result = await _currentProvider.HandleUserConfirmAsync(sku);

                if (_modalCompletionSource != null)
                {
                    _modalCompletionSource.TrySetResult(result);
                    _modalCompletionSource = null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_RechargeModalManager] HandleProviderConfirmAsync exception: {ex.Message}");

                if (_modalCompletionSource != null)
                {
                    _modalCompletionSource.TrySetResult(RechargeModalResult.Failed($"Exception: {ex.Message}"));
                    _modalCompletionSource = null;
                }
            }
        }

        private void OnDestroy()
        {
            if (_currentModal != null)
            {
                _currentModal.OnRechargeClicked -= OnRechargeClicked;
                _currentModal.OnCancelClicked -= OnCancelClicked;
                _currentModal.OnProductPurchaseClicked -= OnProductPurchaseClicked;
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
