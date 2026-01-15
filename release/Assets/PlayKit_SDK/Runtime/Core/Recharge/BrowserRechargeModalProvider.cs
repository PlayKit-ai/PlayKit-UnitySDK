using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PlayKit_SDK.Recharge
{
    /// <summary>
    /// Modal provider for browser-based recharge.
    /// Shows available products with purchase buttons.
    /// Clicking any product's purchase button opens the browser.
    /// </summary>
    public class BrowserRechargeModalProvider : IRechargeModalProvider
    {
        private readonly BrowserRechargeProvider _provider;

        public BrowserRechargeModalProvider(BrowserRechargeProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// Browser recharge does not strictly require product selection.
        /// </summary>
        public bool RequiresProductSelection => false;

        public async UniTask<RechargeModalContent> GetModalContentAsync(float currentBalance, string language)
        {
            var strings = GetLocalizedStrings(language);

            // Fetch available products
            var productResult = await _provider.GetAvailableProductsAsync();
            bool hasProducts = productResult.Success &&
                               productResult.Products != null &&
                               productResult.Products.Count > 0;

            if (hasProducts)
            {
                return RechargeModalContent.CreateWithProducts(
                    title: strings.Title,
                    cancelButtonText: strings.CancelText,
                    products: productResult.Products,
                    purchaseButtonText: strings.PurchaseButtonText
                );
            }
            else
            {
                return RechargeModalContent.CreateSimple(
                    title: strings.Title,
                    confirmButtonText: strings.ConfirmText,
                    cancelButtonText: strings.CancelText
                );
            }
        }

        public async UniTask<RechargeModalResult> HandleUserConfirmAsync(string selectedSku)
        {
            bool originalShowModal = _provider.ShowModal;
            _provider.SetShowModal(false);

            try
            {
                var result = await _provider.RechargeAsync(selectedSku);

                if (result.Initiated)
                {
                    return RechargeModalResult.Success(selectedSku);
                }
                else
                {
                    return RechargeModalResult.Failed(result.Error);
                }
            }
            finally
            {
                _provider.SetShowModal(originalShowModal);
            }
        }

        private LocalizedStrings GetLocalizedStrings(string language)
        {
            string lang = (language ?? "en-US").ToLower();

            switch (lang)
            {
                case "zh-cn":
                    return new LocalizedStrings
                    {
                        Title = "充值",
                        ConfirmText = "立即充值",
                        CancelText = "取消",
                        PurchaseButtonText = "购买"
                    };

                case "zh-tw":
                    return new LocalizedStrings
                    {
                        Title = "儲值",
                        ConfirmText = "立即儲值",
                        CancelText = "取消",
                        PurchaseButtonText = "購買"
                    };

                case "ja-jp":
                    return new LocalizedStrings
                    {
                        Title = "チャージ",
                        ConfirmText = "チャージする",
                        CancelText = "キャンセル",
                        PurchaseButtonText = "購入"
                    };

                case "ko-kr":
                    return new LocalizedStrings
                    {
                        Title = "충전",
                        ConfirmText = "충전하기",
                        CancelText = "취소",
                        PurchaseButtonText = "구매"
                    };

                default: // en-US
                    return new LocalizedStrings
                    {
                        Title = "Recharge",
                        ConfirmText = "Recharge Now",
                        CancelText = "Cancel",
                        PurchaseButtonText = "Purchase"
                    };
            }
        }

        private struct LocalizedStrings
        {
            public string Title;
            public string ConfirmText;
            public string CancelText;
            public string PurchaseButtonText;
        }
    }
}
