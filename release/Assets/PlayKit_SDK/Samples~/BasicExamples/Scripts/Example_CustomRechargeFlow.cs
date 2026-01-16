using PlayKit_SDK;
using PlayKit_SDK.Recharge;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace PlayKit_SDK.Examples
{
    /// <summary>
    /// Example: Custom recharge flow implementation
    ///
    /// This example demonstrates how developers can create their own recharge UI:
    /// - Standalone mode: Show product list + open browser for payment
    /// - Non-Standalone (Steam/iOS/Android): Show product list + purchase in-game
    ///
    /// Features:
    /// - Automatic low balance detection
    /// - Product list display from backend
    /// - Channel-specific purchase flow
    /// - Balance refresh after purchase
    /// </summary>
    public class Example_CustomRechargeFlow : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Recharge panel container")]
        public GameObject rechargePanel;

        [Tooltip("Container for product list items")]
        public Transform productListContainer;

        [Tooltip("Prefab for each product item")]
        public GameObject productItemPrefab;

        [Tooltip("Current balance display")]
        public Text balanceText;

        [Tooltip("Channel type display")]
        public Text channelTypeText;

        [Tooltip("Close button")]
        public Button closeButton;

        [Header("Settings")]
        [Tooltip("Enable debug logs")]
        public bool enableDebugLogs = true;

        private PlayKit_PlayerClient playerClient;
        private PlayKit_RechargeManager rechargeManager;
        private string currentChannelType;
        private bool isStandaloneChannel;

        void Start()
        {
            // Get SDK references
            playerClient = PlayKitSDK.GetPlayerClient();
            rechargeManager = PlayKitSDK.GetRechargeManager();
            currentChannelType = PlayKitSettings.Instance.ChannelType ?? "standalone";
            isStandaloneChannel = currentChannelType == "standalone";

            // Disable SDK's automatic recharge UI (we're using custom UI)
            playerClient.AutoPromptRecharge = false;

            // Subscribe to low balance events
            playerClient.OnBalanceLow += OnBalanceLowDetected;
            playerClient.OnInsufficientCredits += OnInsufficientCreditsDetected;

            // Subscribe to recharge result events
            rechargeManager.OnRechargeInitiated += OnRechargeInitiated;
            rechargeManager.OnRechargeCompleted += OnRechargeCompleted;
            rechargeManager.OnRechargeCancelled += OnRechargeCancelled;

            // Setup close button
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => rechargePanel.SetActive(false));
            }

            // Hide panel initially
            if (rechargePanel != null)
            {
                rechargePanel.SetActive(false);
            }

            LogDebug($"[CustomRecharge] Initialized for channel: {currentChannelType}");
        }

        #region Event Handlers

        /// <summary>
        /// Called when player's balance falls below threshold
        /// </summary>
        private async void OnBalanceLowDetected(float balance)
        {
            LogDebug($"[CustomRecharge] Balance low detected: {balance}");
            await ShowRechargeUI();
        }

        /// <summary>
        /// Called when API call fails due to insufficient credits
        /// </summary>
        private async void OnInsufficientCreditsDetected(PlayKitException exception)
        {
            LogDebug($"[CustomRecharge] Insufficient credits: {exception.Message}");
            await ShowRechargeUI();
        }

        /// <summary>
        /// Called when recharge is initiated
        /// </summary>
        private void OnRechargeInitiated()
        {
            LogDebug("[CustomRecharge] Recharge initiated");
            // Optional: Pause game, show loading, etc.
        }

        /// <summary>
        /// Called when recharge is completed
        /// </summary>
        private void OnRechargeCompleted(RechargeResult result)
        {
            LogDebug($"[CustomRecharge] Recharge completed: {result.Data}");

            // Refresh balance display
            RefreshBalance();

            // Show success message
            ShowMessage("Recharge successful!", Color.green);
        }

        /// <summary>
        /// Called when user cancels recharge
        /// </summary>
        private void OnRechargeCancelled()
        {
            LogDebug("[CustomRecharge] Recharge cancelled");
            ShowMessage("Recharge cancelled", Color.yellow);
        }

        #endregion

        #region UI Display

        /// <summary>
        /// Show recharge UI with product list
        /// </summary>
        public async UniTask ShowRechargeUI()
        {
            if (rechargePanel == null)
            {
                Debug.LogError("[CustomRecharge] Recharge panel is not assigned!");
                return;
            }

            // Update balance and channel display
            RefreshBalance();
            if (channelTypeText != null)
            {
                channelTypeText.text = $"Channel: {currentChannelType}";
            }

            // Fetch product list from backend
            var result = await rechargeManager.GetAvailableProductsAsync();

            if (!result.Success)
            {
                Debug.LogError($"[CustomRecharge] Failed to fetch products: {result.Error}");
                ShowMessage($"Failed to load products: {result.Error}", Color.red);
                return;
            }

            // Clear existing product list
            ClearProductList();

            // Display products based on channel type
            DisplayProducts(result.Products);

            // Show panel
            rechargePanel.SetActive(true);
        }

        /// <summary>
        /// Display product list
        /// </summary>
        private void DisplayProducts(List<IAPProduct> products)
        {
            if (products == null || products.Count == 0)
            {
                ShowMessage("No products available", Color.yellow);
                return;
            }

            // Add info header for Standalone mode
            if (isStandaloneChannel)
            {
                AddInfoHeader();
            }

            // Create UI for each product
            foreach (var product in products)
            {
                CreateProductItem(product);
            }
        }

        /// <summary>
        /// Add info header for Standalone mode
        /// </summary>
        private void AddInfoHeader()
        {
            if (productItemPrefab == null || productListContainer == null) return;

            var headerObj = Instantiate(productItemPrefab, productListContainer);
            headerObj.name = "InfoHeader";

            // Configure as info text
            var texts = headerObj.GetComponentsInChildren<Text>();
            if (texts.Length > 0)
            {
                texts[0].text = "💡 Click any product to open the recharge page in your browser";
                texts[0].fontSize = 12;
                texts[0].color = new Color(0.6f, 0.6f, 0.6f);
                texts[0].alignment = TextAnchor.MiddleCenter;
            }

            // Remove button if exists
            var button = headerObj.GetComponentInChildren<Button>();
            if (button != null) Destroy(button.gameObject);
        }

        /// <summary>
        /// Create UI item for a product
        /// </summary>
        private void CreateProductItem(IAPProduct product)
        {
            if (productItemPrefab == null || productListContainer == null) return;

            var itemObj = Instantiate(productItemPrefab, productListContainer);
            itemObj.name = $"Product_{product.Sku}";

            // Find and configure UI elements
            // Note: Adjust these based on your prefab structure
            var nameText = itemObj.transform.Find("NameText")?.GetComponent<Text>();
            if (nameText != null) nameText.text = product.Name;

            var descText = itemObj.transform.Find("DescriptionText")?.GetComponent<Text>();
            if (descText != null) descText.text = product.Description;

            var priceText = itemObj.transform.Find("PriceText")?.GetComponent<Text>();
            if (priceText != null) priceText.text = product.FormattedPrice;

            // Configure buy button
            var buyButton = itemObj.GetComponentInChildren<Button>();
            if (buyButton != null)
            {
                var buttonText = buyButton.GetComponentInChildren<Text>();

                if (isStandaloneChannel)
                {
                    // Standalone: All products open browser
                    if (buttonText != null) buttonText.text = "Go to Recharge";
                    buyButton.onClick.AddListener(async () => await PurchaseStandalone());
                }
                else
                {
                    // Non-Standalone: Purchase specific product
                    if (buttonText != null) buttonText.text = "Buy";
                    string sku = product.Sku; // Capture SKU
                    buyButton.onClick.AddListener(async () => await PurchaseProduct(sku, product.Name));
                }
            }
        }

        /// <summary>
        /// Clear product list UI
        /// </summary>
        private void ClearProductList()
        {
            if (productListContainer == null) return;

            foreach (Transform child in productListContainer)
            {
                Destroy(child.gameObject);
            }
        }

        /// <summary>
        /// Refresh balance display
        /// </summary>
        private void RefreshBalance()
        {
            if (balanceText != null && playerClient != null)
            {
                float balance = playerClient.GetDisplayBalance();
                balanceText.text = $"Balance: {balance:F2} USD";
            }
        }

        #endregion

        #region Purchase Flow

        /// <summary>
        /// Standalone mode: Open browser (no SKU)
        /// </summary>
        private async UniTask PurchaseStandalone()
        {
            LogDebug("[CustomRecharge] Opening browser for recharge (Standalone)");

            var result = await rechargeManager.RechargeAsync(); // No SKU

            if (result.Initiated)
            {
                LogDebug($"[CustomRecharge] Browser opened: {result.Data}");
                rechargePanel.SetActive(false);
                ShowMessage("Recharge page opened in browser", Color.blue);
            }
            else
            {
                Debug.LogError($"[CustomRecharge] Failed to open browser: {result.Error}");
                ShowMessage($"Error: {result.Error}", Color.red);
            }
        }

        /// <summary>
        /// Non-Standalone mode: Purchase specific product (with SKU)
        /// </summary>
        private async UniTask PurchaseProduct(string sku, string productName)
        {
            LogDebug($"[CustomRecharge] Purchasing product: {sku} ({productName})");

            var result = await rechargeManager.RechargeAsync(sku); // Pass SKU

            if (result.Initiated)
            {
                LogDebug($"[CustomRecharge] Purchase initiated: {result.Data}");
                rechargePanel.SetActive(false);
                ShowMessage($"Processing purchase: {productName}", Color.blue);
            }
            else
            {
                Debug.LogError($"[CustomRecharge] Purchase failed: {result.Error}");
                ShowMessage($"Purchase failed: {result.Error}", Color.red);
            }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Show message to player (implement your own UI toast/notification)
        /// </summary>
        private void ShowMessage(string message, Color color)
        {
            LogDebug($"[CustomRecharge] Message: {message}");
            // TODO: Implement your own toast/notification UI
        }

        /// <summary>
        /// Log debug message
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log(message);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Manually trigger recharge UI (for button click, etc.)
        /// </summary>
        public void OnRechargeButtonClicked()
        {
            ShowRechargeUI().Forget();
        }

        #endregion

        void OnDestroy()
        {
            // Unsubscribe from events
            if (playerClient != null)
            {
                playerClient.OnBalanceLow -= OnBalanceLowDetected;
                playerClient.OnInsufficientCredits -= OnInsufficientCreditsDetected;
            }

            if (rechargeManager != null)
            {
                rechargeManager.OnRechargeInitiated -= OnRechargeInitiated;
                rechargeManager.OnRechargeCompleted -= OnRechargeCompleted;
                rechargeManager.OnRechargeCancelled -= OnRechargeCancelled;
            }
        }
    }
}
