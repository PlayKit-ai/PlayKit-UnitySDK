using System;
using UnityEngine;
using UnityEngine.UI;
using PlayKit_SDK.Recharge;

namespace PlayKit_SDK.UI
{
    /// <summary>
    /// Controller for individual product items in the recharge modal product list.
    /// Displays product information and handles direct purchase.
    /// </summary>
    public class ProductItemController : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Product name text")]
        public Text productNameText;

        [Tooltip("Product price text")]
        public Text productPriceText;

        [Tooltip("Product description text (optional)")]
        public Text productDescriptionText;

        [Tooltip("Purchase button - clicking this directly initiates purchase")]
        public Button purchaseButton;

        [Tooltip("Optional: Text on purchase button")]
        public Text purchaseButtonText;

        [Header("Colors")]
        [Tooltip("Normal background color")]
        public Color normalColor = new Color(1f, 1f, 1f, 0.1f);

        [Tooltip("Hover/highlight background color")]
        public Color highlightColor = new Color(0.2f, 0.6f, 1f, 0.2f);

        private IAPProduct _product;
        private Action<string> _onPurchaseClicked;
        private Image _backgroundImage;

        private void Awake()
        {
            _backgroundImage = GetComponent<Image>();

            if (purchaseButton != null)
            {
                purchaseButton.onClick.AddListener(OnPurchaseButtonClicked);
            }
        }

        /// <summary>
        /// Setup the product item with data and purchase callback
        /// </summary>
        /// <param name="product">Product data</param>
        /// <param name="onPurchaseClicked">Callback when purchase button clicked (receives SKU)</param>
        public void Setup(IAPProduct product, Action<string> onPurchaseClicked)
        {
            _product = product;
            _onPurchaseClicked = onPurchaseClicked;

            // Update UI
            if (productNameText != null)
            {
                productNameText.text = product.LocalizedName;
            }

            if (productPriceText != null)
            {
                productPriceText.text = product.FormattedPrice;
            }

            if (productDescriptionText != null)
            {
                productDescriptionText.text = product.LocalizedDescription;
                productDescriptionText.gameObject.SetActive(!string.IsNullOrEmpty(product.Description));
            }

            // Set background color
            if (_backgroundImage != null)
            {
                _backgroundImage.color = normalColor;
            }
        }

        /// <summary>
        /// Set the purchase button text (for localization)
        /// </summary>
        public void SetPurchaseButtonText(string text)
        {
            if (purchaseButtonText != null)
            {
                purchaseButtonText.text = text;
            }
        }

        /// <summary>
        /// Get the SKU of this product
        /// </summary>
        public string GetSku()
        {
            return _product?.Sku;
        }

        /// <summary>
        /// Get the product data
        /// </summary>
        public IAPProduct GetProduct()
        {
            return _product;
        }

        private void OnPurchaseButtonClicked()
        {
            Debug.Log($"[ProductItemController] Purchase clicked for SKU: {_product?.Sku}");
            _onPurchaseClicked?.Invoke(_product?.Sku);
        }

        private void OnDestroy()
        {
            if (purchaseButton != null)
            {
                purchaseButton.onClick.RemoveAllListeners();
            }
        }
    }
}
