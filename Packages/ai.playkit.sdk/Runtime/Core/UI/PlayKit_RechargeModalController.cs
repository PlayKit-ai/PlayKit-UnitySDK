using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PlayKit_SDK.Recharge;

namespace PlayKit_SDK.UI
{
    /// <summary>
    /// Controller for the Recharge Modal UI prefab.
    /// Prefab location: Resources/RechargeModal.prefab
    ///
    /// Shows a product list where each product has its own purchase button.
    /// Balance is displayed via PlayKit_BalancePopupManager (persistent mode).
    /// </summary>
    public class PlayKit_RechargeModalController : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Root GameObject to show/hide the modal content")]
        public GameObject modalRoot;

        [Tooltip("Loading spinner - shown during network requests")]
        public GameObject spinner;

        [Tooltip("Modal title text")]
        public Text titleText;

        [Tooltip("Recharge button (shown when no product list)")]
        public Button rechargeButton;

        [Tooltip("Cancel button")]
        public Button cancelButton;

        [Header("Optional - Button Text")]
        [Tooltip("Optional: Text on recharge button")]
        public Text rechargeButtonText;

        [Tooltip("Optional: Text on cancel button")]
        public Text cancelButtonText;

        [Header("Product List")]
        [Tooltip("Root container for product list")]
        public GameObject productListRoot;

        [Tooltip("Parent transform for product items")]
        public Transform productListContent;

        [Tooltip("Prefab for individual product items")]
        public GameObject productItemPrefab;

        /// <summary>
        /// Event fired when recharge button is clicked (for simple mode without products)
        /// </summary>
        public event Action OnRechargeClicked;

        /// <summary>
        /// Event fired when cancel button is clicked
        /// </summary>
        public event Action OnCancelClicked;

        /// <summary>
        /// Event fired when a product's purchase button is clicked
        /// </summary>
        public event Action<string> OnProductPurchaseClicked;

        private List<ProductItemController> _productItems = new List<ProductItemController>();
        private string _purchaseButtonText = "Purchase";

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

            // Hide everything by default
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }

            if (spinner != null)
            {
                spinner.SetActive(false);
            }
        }

        /// <summary>
        /// Show the loading spinner and hide modal content
        /// </summary>
        public void ShowLoading()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }

            if (spinner != null)
            {
                spinner.SetActive(true);
            }
        }

        /// <summary>
        /// Hide the loading spinner
        /// </summary>
        public void HideLoading()
        {
            if (spinner != null)
            {
                spinner.SetActive(false);
            }
        }

        /// <summary>
        /// Show the modal with content from IRechargeModalProvider
        /// </summary>
        /// <param name="content">Modal content to display</param>
        public void Show(RechargeModalContent content)
        {
            // Hide spinner, show modal content
            HideLoading();

            if (modalRoot != null)
            {
                modalRoot.SetActive(true);
            }

            // Title
            if (titleText != null)
            {
                titleText.text = content.Title ?? "";
            }

            // Button text
            if (rechargeButtonText != null)
            {
                rechargeButtonText.text = content.ConfirmButtonText ?? "Confirm";
            }

            if (cancelButtonText != null)
            {
                cancelButtonText.text = content.CancelButtonText ?? "Cancel";
            }

            // Store purchase button text for product items
            _purchaseButtonText = content.PurchaseButtonText ?? "Purchase";

            // Product list
            bool showProducts = content.ShowProductList && content.Products != null && content.Products.Count > 0;

            if (productListRoot != null)
            {
                productListRoot.SetActive(showProducts);

                if (showProducts)
                {
                    PopulateProductList(content.Products);
                }
            }

            // Hide main recharge button when showing product list (each product has its own button)
            if (rechargeButton != null)
            {
                rechargeButton.gameObject.SetActive(!showProducts);
            }
        }

        /// <summary>
        /// Show the modal with localized text (legacy method for backward compatibility)
        /// </summary>
        /// <param name="balance">Current balance (not displayed, for compatibility)</param>
        /// <param name="language">Language code (e.g., "en-US", "zh-CN")</param>
        public void Show(float balance, string language = "en-US")
        {
            // Hide spinner, show modal content
            HideLoading();

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

            if (rechargeButtonText != null)
            {
                rechargeButtonText.text = localizedStrings.RechargeButtonText;
            }

            if (cancelButtonText != null)
            {
                cancelButtonText.text = localizedStrings.CancelButtonText;
            }

            // Show recharge button in legacy mode
            if (rechargeButton != null)
            {
                rechargeButton.gameObject.SetActive(true);
            }

            // Hide product list for legacy mode
            if (productListRoot != null)
            {
                productListRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Hide the modal completely (including spinner)
        /// </summary>
        public void Hide()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }

            if (spinner != null)
            {
                spinner.SetActive(false);
            }
        }

        private void PopulateProductList(List<IAPProduct> products)
        {
            // Clear existing items
            ClearProductItems();

            if (productListContent == null || productItemPrefab == null)
            {
                Debug.LogWarning("[PlayKit_RechargeModalController] Product list content or prefab not set");
                return;
            }

            // Create product items
            foreach (var product in products)
            {
                var itemObj = Instantiate(productItemPrefab, productListContent);
                var itemController = itemObj.GetComponent<ProductItemController>();

                if (itemController != null)
                {
                    // Setup with direct purchase callback
                    itemController.Setup(product, OnProductItemPurchaseClicked);
                    itemController.SetPurchaseButtonText(_purchaseButtonText);
                    _productItems.Add(itemController);
                }
                else
                {
                    Debug.LogWarning("[PlayKit_RechargeModalController] Product item prefab missing ProductItemController");
                }
            }
        }

        private void ClearProductItems()
        {
            foreach (var item in _productItems)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }
            _productItems.Clear();
        }

        private void OnProductItemPurchaseClicked(string sku)
        {
            Debug.Log($"[PlayKit_RechargeModalController] Product purchase clicked: {sku}");
            OnProductPurchaseClicked?.Invoke(sku);
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
                        Title = "充值",
                        RechargeButtonText = "立即充值",
                        CancelButtonText = "取消"
                    };

                case "zh-tw":
                    return new LocalizedStrings
                    {
                        Title = "儲值",
                        RechargeButtonText = "立即儲值",
                        CancelButtonText = "取消"
                    };

                case "ja-jp":
                    return new LocalizedStrings
                    {
                        Title = "チャージ",
                        RechargeButtonText = "チャージする",
                        CancelButtonText = "キャンセル"
                    };

                case "ko-kr":
                    return new LocalizedStrings
                    {
                        Title = "충전",
                        RechargeButtonText = "충전하기",
                        CancelButtonText = "취소"
                    };

                default: // en-US
                    return new LocalizedStrings
                    {
                        Title = "Recharge",
                        RechargeButtonText = "Recharge Now",
                        CancelButtonText = "Cancel"
                    };
            }
        }

        private struct LocalizedStrings
        {
            public string Title;
            public string RechargeButtonText;
            public string CancelButtonText;
        }

        private void OnDestroy()
        {
            ClearProductItems();

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
