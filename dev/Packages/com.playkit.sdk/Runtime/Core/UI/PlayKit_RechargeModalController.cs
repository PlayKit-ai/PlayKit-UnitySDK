using System;
using UnityEngine;
using UnityEngine.UI;

namespace PlayKit_SDK.UI
{
    /// <summary>
    /// Controller for the Recharge Modal UI prefab.
    /// Prefab location: Resources/PlayKit/UI/RechargeModal.prefab
    ///
    /// Required UI elements:
    /// - modalRoot (GameObject) - Root object to show/hide
    /// - titleText (Text) - Modal title
    /// - messageText (Text) - Message text
    /// - balanceLabelText (Text) - "Current Balance:" label
    /// - balanceValueText (Text) - Balance value
    /// - rechargeButton (Button) - Recharge button
    /// - cancelButton (Button) - Cancel button
    /// </summary>
    public class PlayKit_RechargeModalController : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Root GameObject to show/hide the modal")]
        public GameObject modalRoot;

        [Tooltip("Modal title text")]
        public Text titleText;

        [Tooltip("Main message text")]
        public Text messageText;

        [Tooltip("'Current Balance:' label")]
        public Text balanceLabelText;

        [Tooltip("Balance value display")]
        public Text balanceValueText;

        [Tooltip("Recharge button")]
        public Button rechargeButton;

        [Tooltip("Cancel button")]
        public Button cancelButton;

        [Header("Optional - Recharge Button Text")]
        [Tooltip("Optional: Text on recharge button to update text")]
        public Text rechargeButtonText;

        [Tooltip("Optional: Text on cancel button to update text")]
        public Text cancelButtonText;

        /// <summary>
        /// Event fired when recharge button is clicked
        /// </summary>
        public event Action OnRechargeClicked;

        /// <summary>
        /// Event fired when cancel button is clicked
        /// </summary>
        public event Action OnCancelClicked;

        private void Awake()
        {
            // Subscribe to button clicks
            if (rechargeButton != null)
            {
                rechargeButton.onClick.AddListener(() =>
                {
                    OnRechargeClicked?.Invoke();
                });
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(() =>
                {
                    OnCancelClicked?.Invoke();
                });
            }

            // Hide modal by default
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Show the modal with localized text and balance
        /// </summary>
        /// <param name="balance">Current balance to display</param>
        /// <param name="language">Language code (e.g., "en-US", "zh-CN")</param>
        public void Show(float balance, string language = "en-US")
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(true);
            }

            // Get localized strings
            var localizedStrings = GetLocalizedStrings(language);

            // Update UI text
            if (titleText != null)
            {
                titleText.text = localizedStrings.Title;
            }

            if (messageText != null)
            {
                messageText.text = localizedStrings.Message;
            }

            if (balanceLabelText != null)
            {
                balanceLabelText.text = localizedStrings.BalanceLabel;
            }

            if (balanceValueText != null)
            {
                balanceValueText.text = balance.ToString("F2");
            }

            if (rechargeButtonText != null)
            {
                rechargeButtonText.text = localizedStrings.RechargeButtonText;
            }

            if (cancelButtonText != null)
            {
                cancelButtonText.text = localizedStrings.CancelButtonText;
            }
        }

        /// <summary>
        /// Hide the modal
        /// </summary>
        public void Hide()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Get localized strings for the modal based on language
        /// </summary>
        private LocalizedStrings GetLocalizedStrings(string language)
        {
            switch (language.ToLower())
            {
                case "zh-cn":
                    return new LocalizedStrings
                    {
                        Title = "充值提示",
                        Message = "您的余额不足，是否前往充值？",
                        BalanceLabel = "当前余额：",
                        RechargeButtonText = "立即充值",
                        CancelButtonText = "取消"
                    };

                case "zh-tw":
                    return new LocalizedStrings
                    {
                        Title = "儲值提示",
                        Message = "您的餘額不足，是否前往儲值？",
                        BalanceLabel = "目前餘額：",
                        RechargeButtonText = "立即儲值",
                        CancelButtonText = "取消"
                    };

                case "ja-jp":
                    return new LocalizedStrings
                    {
                        Title = "チャージ確認",
                        Message = "残高が不足しています。チャージしますか？",
                        BalanceLabel = "現在の残高：",
                        RechargeButtonText = "チャージする",
                        CancelButtonText = "キャンセル"
                    };

                case "ko-kr":
                    return new LocalizedStrings
                    {
                        Title = "충전 확인",
                        Message = "잔액이 부족합니다. 충전하시겠습니까？",
                        BalanceLabel = "현재 잔액：",
                        RechargeButtonText = "충전하기",
                        CancelButtonText = "취소"
                    };

                default: // en-US
                    return new LocalizedStrings
                    {
                        Title = "Recharge Confirmation",
                        Message = "Your balance is low. Would you like to recharge?",
                        BalanceLabel = "Current Balance:",
                        RechargeButtonText = "Recharge Now",
                        CancelButtonText = "Cancel"
                    };
            }
        }

        private struct LocalizedStrings
        {
            public string Title;
            public string Message;
            public string BalanceLabel;
            public string RechargeButtonText;
            public string CancelButtonText;
        }

        private void OnDestroy()
        {
            // Unsubscribe from button clicks
            if (rechargeButton != null)
            {
                rechargeButton.onClick.RemoveAllListeners();
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
            }
        }
    }
}
