using System;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

namespace PlayKit_SDK.UI
{
    /// <summary>
    /// Controller for the balance change popup.
    /// Displays animated balance changes: old value -> new value with change amount.
    ///
    /// Animation flow (fast mode):
    /// 1. Show original balance (0.3s)
    /// 2. Animate to new balance with change indicator (0.5s)
    /// 3. Stay visible (1s)
    /// 4. Fade out (0.3s)
    /// </summary>
    public class PlayKit_BalancePopupController : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Root GameObject to show/hide the popup")]
        public GameObject popupRoot;

        [Tooltip("Text displaying the balance value")]
        public Text balanceText;

        [Tooltip("Text displaying the change amount (e.g., -10 or +5)")]
        public Text changeText;

        [Tooltip("CanvasGroup for fade animation")]
        public CanvasGroup canvasGroup;

        [Header("Recharge Button (Optional)")]
        [Tooltip("Recharge button - clicking triggers recharge flow")]
        public Button rechargeButton;

        [Tooltip("Text on the recharge button")]
        public Text rechargeButtonText;

        /// <summary>
        /// Event fired when recharge button is clicked
        /// </summary>
        public event Action OnRechargeClicked;

        [Header("Animation Settings (Fast Mode)")]
        [Tooltip("Duration to show original balance")]
        public float showOriginalDuration = 0.3f;

        [Tooltip("Duration of the value change animation")]
        public float animationDuration = 0.5f;

        [Tooltip("Duration to stay visible after animation")]
        public float stayDuration = 1.0f;

        [Tooltip("Duration of fade out")]
        public float fadeOutDuration = 0.3f;

        // Pending change for merging multiple rapid changes
        private float _pendingOldBalance;
        private float _pendingNewBalance;
        private float _pendingTotalChange;
        private bool _hasPendingChange;
        private bool _isAnimating;
        private bool _isPersistent;

        private void Awake()
        {
            if (popupRoot != null)
            {
                popupRoot.SetActive(false);
            }

            // Subscribe to recharge button click
            if (rechargeButton != null)
            {
                rechargeButton.onClick.AddListener(() => OnRechargeClicked?.Invoke());
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from button click
            if (rechargeButton != null)
            {
                rechargeButton.onClick.RemoveAllListeners();
            }
        }

        /// <summary>
        /// Show the balance change popup with animation
        /// </summary>
        /// <param name="oldBalance">Balance before the change</param>
        /// <param name="newBalance">Balance after the change</param>
        public void Show(float oldBalance, float newBalance)
        {
            float change = newBalance - oldBalance;

            if (_isAnimating)
            {
                // Currently animating, merge this change
                _pendingNewBalance = newBalance;
                _pendingTotalChange += change;
                _hasPendingChange = true;
                return;
            }

            // Start new animation
            PlayAnimationAsync(oldBalance, newBalance, change).Forget();
        }

        private async UniTaskVoid PlayAnimationAsync(float oldBalance, float newBalance, float change)
        {
            _isAnimating = true;
            _hasPendingChange = false;
            _pendingTotalChange = 0;

            // Reset state
            if (popupRoot != null) popupRoot.SetActive(true);
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            if (changeText != null) changeText.gameObject.SetActive(false);

            // Phase 1: Show original balance (0.3s)
            if (balanceText != null) balanceText.text = oldBalance.ToString("F0");
            await UniTask.Delay(TimeSpan.FromSeconds(showOriginalDuration));

            // Check if there are pending merged changes
            if (_hasPendingChange)
            {
                newBalance = _pendingNewBalance;
                change = _pendingTotalChange + change;
                _hasPendingChange = false;
                _pendingTotalChange = 0;
            }

            // Phase 2: Animate value change (0.5s)
            if (changeText != null)
            {
                changeText.gameObject.SetActive(true);
                string sign = change >= 0 ? "+" : "";
                changeText.text = $"{sign}{change:F0}";
            }

            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);

                // Smooth interpolation
                float easedT = 1f - Mathf.Pow(1f - t, 3f); // Ease out cubic
                float currentValue = Mathf.Lerp(oldBalance, newBalance, easedT);

                if (balanceText != null)
                {
                    balanceText.text = currentValue.ToString("F0");
                }

                await UniTask.Yield();
            }

            // Ensure final value is exact
            if (balanceText != null) balanceText.text = newBalance.ToString("F0");

            // Phase 3: Stay visible (1s)
            await UniTask.Delay(TimeSpan.FromSeconds(stayDuration));

            // Phase 4: Fade out (0.3s)
            if (canvasGroup != null)
            {
                elapsed = 0f;
                while (elapsed < fadeOutDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
                    canvasGroup.alpha = t;
                    await UniTask.Yield();
                }
            }

            // Hide popup
            if (popupRoot != null) popupRoot.SetActive(false);

            _isAnimating = false;

            // Check if there are new pending changes
            if (_hasPendingChange)
            {
                float pendingOld = newBalance;
                float pendingNew = _pendingNewBalance;
                _hasPendingChange = false;
                Show(pendingOld, pendingNew);
            }
        }

        /// <summary>
        /// Immediately hide the popup without animation
        /// </summary>
        public void Hide()
        {
            _isPersistent = false;
            if (popupRoot != null) popupRoot.SetActive(false);
            _isAnimating = false;
            _hasPendingChange = false;
            _pendingTotalChange = 0;
        }

        /// <summary>
        /// Show the popup in persistent mode (stays visible until manually hidden)
        /// </summary>
        /// <param name="balance">Current balance to display</param>
        public void ShowPersistent(float balance)
        {
            // Stop any ongoing animation
            _isAnimating = false;
            _hasPendingChange = false;
            _pendingTotalChange = 0;
            _isPersistent = true;

            // Show popup
            if (popupRoot != null) popupRoot.SetActive(true);
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            if (balanceText != null) balanceText.text = balance.ToString("F0");
            if (changeText != null) changeText.gameObject.SetActive(false);
        }

        /// <summary>
        /// Update the balance display (for persistent mode)
        /// </summary>
        /// <param name="balance">New balance value</param>
        public void UpdateBalance(float balance)
        {
            if (balanceText != null)
            {
                balanceText.text = balance.ToString("F0");
            }
        }

        /// <summary>
        /// Check if the popup is currently showing/animating
        /// </summary>
        public bool IsShowing => _isAnimating || _isPersistent || (popupRoot != null && popupRoot.activeSelf);

        /// <summary>
        /// Check if the popup is in persistent mode
        /// </summary>
        public bool IsPersistent => _isPersistent;
    }
}
