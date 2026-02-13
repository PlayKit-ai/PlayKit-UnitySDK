using Cysharp.Threading.Tasks;

namespace PlayKit_SDK.Recharge
{
    /// <summary>
    /// Interface for providing recharge modal content and handling user interactions.
    /// Different channels (Browser, Steam, iOS, Android) can implement this to customize
    /// the recharge modal behavior.
    ///
    /// Browser: Shows simple confirmation, opens browser on confirm
    /// Steam: Shows product list, requires SKU selection, uses Steam Overlay
    /// iOS/Android: Shows product list, uses native IAP
    /// </summary>
    public interface IRechargeModalProvider
    {
        /// <summary>
        /// Whether this provider requires the user to select a product (SKU).
        /// Browser: false (no SKU needed, just opens recharge page)
        /// Steam/iOS/Android: true (user must select a product)
        /// </summary>
        bool RequiresProductSelection { get; }

        /// <summary>
        /// Get the content to display in the recharge modal.
        /// This includes title, message, products (if applicable), etc.
        /// </summary>
        /// <param name="currentBalance">Current user balance</param>
        /// <param name="language">Language code (e.g., "en-US", "zh-CN")</param>
        /// <returns>Modal content to display</returns>
        UniTask<RechargeModalContent> GetModalContentAsync(float currentBalance, string language);

        /// <summary>
        /// Handle user confirmation from the modal.
        /// For Browser: Opens the recharge page
        /// For Steam: Initiates Steam purchase with selected SKU
        /// </summary>
        /// <param name="selectedSku">Selected product SKU (may be null for Browser)</param>
        /// <returns>Result of the recharge action</returns>
        UniTask<RechargeModalResult> HandleUserConfirmAsync(string selectedSku);
    }
}
