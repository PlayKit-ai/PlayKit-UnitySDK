using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PlayKit_SDK.UI
{
    /// <summary>
    /// Manager for loading and displaying the recharge modal.
    /// Loads the modal prefab from Resources and manages its lifecycle.
    ///
    /// Prefab path: Resources/RechargeModal.prefab
    /// </summary>
    public class PlayKit_RechargeModalManager : MonoBehaviour
    {
        private static PlayKit_RechargeModalManager _instance;
        private PlayKit_RechargeModalController _currentModal;
        private bool _isWaitingForResponse;
        private bool _userConfirmed;

        /// <summary>
        /// Singleton instance (created on demand)
        /// </summary>
        public static PlayKit_RechargeModalManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Create a new GameObject for the manager
                    var go = new GameObject("[PlayKit_RechargeModalManager]");
                    _instance = go.AddComponent<PlayKit_RechargeModalManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Show the recharge modal and wait for user response
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
                    return true; // Default to recharge if modal fails
                }
            }

            // Reset state
            _isWaitingForResponse = true;
            _userConfirmed = false;

            // Show modal
            _currentModal.Show(balance, language);

            // Wait for user response
            await UniTask.WaitUntil(() => !_isWaitingForResponse);

            return _userConfirmed;
        }

        /// <summary>
        /// Load the modal prefab from Resources
        /// </summary>
        private async UniTask<bool> LoadModalAsync()
        {
            try
            {
                // Get prefab path from settings
                string prefabPath = GetPrefabPath();

                Debug.Log($"[PlayKit_RechargeModalManager] Loading modal from: {prefabPath}");

                // Load prefab
                var prefab = Resources.Load<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"[PlayKit_RechargeModalManager] Failed to load modal prefab at Resources/{prefabPath}.prefab");
                    return false;
                }

                // Instantiate prefab
                var modalObj = Instantiate(prefab);
                DontDestroyOnLoad(modalObj);

                // Get controller component
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

                Debug.Log("[PlayKit_RechargeModalManager] Modal loaded successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_RechargeModalManager] Exception loading modal: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the prefab path
        /// </summary>
        private string GetPrefabPath()
        {
            return "RechargeModal";
        }

        /// <summary>
        /// Get system language in format compatible with PlayKit localization
        /// </summary>
        private string GetSystemLanguage()
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
            _userConfirmed = true;
            _isWaitingForResponse = false;

            if (_currentModal != null)
            {
                _currentModal.Hide();
            }
        }

        private void OnCancelClicked()
        {
            _userConfirmed = false;
            _isWaitingForResponse = false;

            if (_currentModal != null)
            {
                _currentModal.Hide();
            }
        }

        private void OnDestroy()
        {
            if (_currentModal != null)
            {
                _currentModal.OnRechargeClicked -= OnRechargeClicked;
                _currentModal.OnCancelClicked -= OnCancelClicked;
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
