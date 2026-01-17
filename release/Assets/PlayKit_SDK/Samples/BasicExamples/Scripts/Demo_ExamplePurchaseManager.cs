using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PlayKit_SDK;
using PlayKit_SDK.Recharge;
using Cysharp.Threading.Tasks;

/// <summary>
/// Example purchase manager that demonstrates how to:
/// 1. Wait for SDK initialization
/// 2. Fetch available products (SKUs)
/// 3. Display products in a dropdown
/// 4. Initiate purchases when user clicks a button
/// </summary>
public class Demo_ExamplePurchaseManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Dropdown to display available products")]
    [SerializeField] private Dropdown productDropdown;

    [Tooltip("Button to trigger purchase of selected product")]
    [SerializeField] private Button purchaseButton;

    [Header("Status")]
    [Tooltip("Optional text to display status messages")]
    [SerializeField] private Text statusText;

    private List<IAPProduct> availableProducts = new List<IAPProduct>();
    private PlayKit_RechargeManager rechargeManager;

    private async void Start()
    {
        SetUIEnabled(false);
        UpdateStatus("Waiting for SDK initialization...");
        var result = await PlayKitSDK.InitializeAsync();

        if (!result)
        {
            Debug.LogError("initialization failed. You should ask us for help. Look for community banner at the dashboard. 初始化失败，你可以联系我们寻求帮助。你可以在控制台找到社群链接。");
            return;
        }
        // Disable UI until ready
        

        // Wait for SDK to initialize
        await WaitForSDKInitialization();

        // Get RechargeManager
        rechargeManager = PlayKitSDK.GetRechargeManager();
        if (rechargeManager == null)
        {
            UpdateStatus("Error: RechargeManager not available");
            Debug.LogError("[PurchaseManager] RechargeManager not available");
            return;
        }

        // Subscribe to recharge events
        rechargeManager.OnRechargeInitiated += OnPurchaseInitiated;
        rechargeManager.OnRechargeCompleted += OnPurchaseCompleted;
        rechargeManager.OnRechargeCancelled += OnPurchaseCancelled;

        // Load available products
        await LoadProducts();

        // Setup button click listener
        if (purchaseButton != null)
        {
            purchaseButton.onClick.AddListener(OnPurchaseButtonClicked);
        }

        SetUIEnabled(true);
        UpdateStatus("Ready to purchase");
    }

    /// <summary>
    /// Wait for SDK initialization to complete
    /// </summary>
    private async UniTask WaitForSDKInitialization()
    {
        // Wait until SDK is ready
        while (!PlayKitSDK.IsReady())
        {
            await UniTask.Delay(100);
        }

        Debug.Log("[PurchaseManager] SDK is ready");
    }

    /// <summary>
    /// Load available products from the server
    /// </summary>
    private async UniTask LoadProducts()
    {
        UpdateStatus("Loading products...");

        var result = await rechargeManager.GetAvailableProductsAsync();

        if (!result.Success)
        {
            UpdateStatus($"Failed to load products: {result.Error}");
            Debug.LogError($"[PurchaseManager] Failed to load products: {result.Error}");
            return;
        }

        if (result.Products == null || result.Products.Count == 0)
        {
            UpdateStatus("No products available");
            Debug.LogWarning("[PurchaseManager] No products available");
            return;
        }

        availableProducts = result.Products;
        PopulateDropdown();

        UpdateStatus($"Loaded {availableProducts.Count} products");
        Debug.Log($"[PurchaseManager] Loaded {availableProducts.Count} products");
    }

    /// <summary>
    /// Populate the dropdown with available products
    /// </summary>
    private void PopulateDropdown()
    {
        if (productDropdown == null)
        {
            Debug.LogWarning("[PurchaseManager] Product dropdown not assigned");
            return;
        }

        productDropdown.ClearOptions();

        var options = new List<Dropdown.OptionData>();
        foreach (var product in availableProducts)
        {
            string displayText = $"{product.Name} - {product.FormattedPrice}";
            options.Add(new Dropdown.OptionData(displayText));
        }

        productDropdown.AddOptions(options);

        // Select first product by default
        if (options.Count > 0)
        {
            productDropdown.value = 0;
        }
    }

    /// <summary>
    /// Handle purchase button click
    /// </summary>
    private void OnPurchaseButtonClicked()
    {
        if (availableProducts.Count == 0)
        {
            UpdateStatus("No products available");
            return;
        }

        int selectedIndex = productDropdown != null ? productDropdown.value : 0;
        if (selectedIndex < 0 || selectedIndex >= availableProducts.Count)
        {
            UpdateStatus("Invalid product selection");
            return;
        }

        var selectedProduct = availableProducts[selectedIndex];
        InitiatePurchase(selectedProduct).Forget();
    }

    /// <summary>
    /// Initiate purchase for the selected product
    /// </summary>
    private async UniTask InitiatePurchase(IAPProduct product)
    {
        UpdateStatus($"Purchasing {product.Name}...");
        Debug.Log($"[PurchaseManager] Initiating purchase for SKU: {product.Sku}");

        SetUIEnabled(false);

        var result = await rechargeManager.RechargeAsync(product.Sku);

        if (!result.Initiated)
        {
            UpdateStatus($"Purchase failed: {result.Error}");
            Debug.LogError($"[PurchaseManager] Purchase failed: {result.Error}");
            SetUIEnabled(true);
        }
        else
        {
            Debug.Log($"[PurchaseManager] Purchase initiated. Order ID: {result.Data}");
            // UI will be re-enabled when purchase completes or is cancelled
        }
    }

    #region Event Handlers

    private void OnPurchaseInitiated()
    {
        UpdateStatus("Purchase initiated, waiting for completion...");
        Debug.Log("[PurchaseManager] Purchase initiated");
    }

    private void OnPurchaseCompleted(RechargeResult result)
    {
        if (result.Initiated)
        {
            UpdateStatus($"Purchase completed! Order: {result.Data}");
            Debug.Log($"[PurchaseManager] Purchase completed. Order: {result.Data}");
        }
        else
        {
            UpdateStatus($"Purchase failed: {result.Error}");
            Debug.LogError($"[PurchaseManager] Purchase failed: {result.Error}");
        }

        SetUIEnabled(true);
    }

    private void OnPurchaseCancelled()
    {
        UpdateStatus("Purchase cancelled");
        Debug.Log("[PurchaseManager] Purchase cancelled");
        SetUIEnabled(true);
    }

    #endregion

    #region UI Helpers

    private void SetUIEnabled(bool enabled)
    {
        if (productDropdown != null)
        {
            productDropdown.interactable = enabled;
        }

        if (purchaseButton != null)
        {
            purchaseButton.interactable = enabled;
        }
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.Log($"[PurchaseManager] Status: {message}");
    }

    #endregion

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (rechargeManager != null)
        {
            rechargeManager.OnRechargeInitiated -= OnPurchaseInitiated;
            rechargeManager.OnRechargeCompleted -= OnPurchaseCompleted;
            rechargeManager.OnRechargeCancelled -= OnPurchaseCancelled;
        }

        // Remove button listener
        if (purchaseButton != null)
        {
            purchaseButton.onClick.RemoveListener(OnPurchaseButtonClicked);
        }
    }
}
