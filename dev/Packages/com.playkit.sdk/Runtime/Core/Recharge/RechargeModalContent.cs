using System.Collections.Generic;

namespace PlayKit_SDK.Recharge
{
    /// <summary>
    /// Data class representing the content to display in the recharge modal.
    /// Different channels can provide different content through IRechargeModalProvider.
    ///
    /// Note: Balance is displayed via PlayKit_BalancePopupManager (persistent mode),
    /// not in the modal itself.
    /// </summary>
    public class RechargeModalContent
    {
        /// <summary>Modal title (e.g., "Recharge", "Steam Recharge")</summary>
        public string Title { get; set; }

        // Buttons
        /// <summary>Confirm button text for simple mode (e.g., "Recharge Now")</summary>
        public string ConfirmButtonText { get; set; }

        /// <summary>Cancel button text (e.g., "Cancel")</summary>
        public string CancelButtonText { get; set; }

        /// <summary>Purchase button text for each product item (e.g., "Purchase", "Buy")</summary>
        public string PurchaseButtonText { get; set; }

        // Product list
        /// <summary>Whether to show the product list</summary>
        public bool ShowProductList { get; set; }

        /// <summary>Available products for purchase</summary>
        public List<IAPProduct> Products { get; set; }

        /// <summary>
        /// Create a simple confirmation modal content (no product list)
        /// </summary>
        public static RechargeModalContent CreateSimple(
            string title,
            string confirmButtonText,
            string cancelButtonText)
        {
            return new RechargeModalContent
            {
                Title = title,
                ConfirmButtonText = confirmButtonText,
                CancelButtonText = cancelButtonText,
                ShowProductList = false
            };
        }

        /// <summary>
        /// Create a modal content with product list
        /// </summary>
        public static RechargeModalContent CreateWithProducts(
            string title,
            string cancelButtonText,
            List<IAPProduct> products,
            string purchaseButtonText = "Purchase")
        {
            return new RechargeModalContent
            {
                Title = title,
                CancelButtonText = cancelButtonText,
                ShowProductList = true,
                Products = products,
                PurchaseButtonText = purchaseButtonText
            };
        }
    }
}
