using System;
using Cysharp.Threading.Tasks;
using PlayKit_SDK.Recharge;
using UnityEngine;

namespace PlayKit_SDK.Steam
{
    /// <summary>
    /// Modal provider for Steam-based recharge.
    /// Shows a product list with purchase buttons.
    /// Each product has its own purchase button that initiates Steam Overlay payment.
    /// </summary>
    public class SteamRechargeModalProvider : IRechargeModalProvider
    {
        private readonly SteamRechargeProvider _provider;

        public SteamRechargeModalProvider(SteamRechargeProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// Steam recharge requires product selection.
        /// </summary>
        public bool RequiresProductSelection => true;

        public async UniTask<RechargeModalContent> GetModalContentAsync(float currentBalance, string language)
        {
            var strings = GetLocalizedStrings(language);

            // Load available products from API
            var productResult = await _provider.GetAvailableProductsAsync();

            if (!productResult.Success || productResult.Products == null || productResult.Products.Count == 0)
            {
                Debug.LogWarning("[SteamRechargeModalProvider] No products available");

                return RechargeModalContent.CreateSimple(
                    title: strings.Title,
                    confirmButtonText: strings.ConfirmText,
                    cancelButtonText: strings.CancelText
                );
            }

            return RechargeModalContent.CreateWithProducts(
                title: strings.Title,
                cancelButtonText: strings.CancelText,
                products: productResult.Products,
                purchaseButtonText: strings.PurchaseButtonText
            );
        }

        public async UniTask<RechargeModalResult> HandleUserConfirmAsync(string selectedSku)
        {
            if (string.IsNullOrEmpty(selectedSku))
            {
                return RechargeModalResult.Failed("Please select a product");
            }

            Debug.Log($"[SteamRechargeModalProvider] Initiating Steam purchase for SKU: {selectedSku}");

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

        private LocalizedStrings GetLocalizedStrings(string language)
        {
            string lang = (language ?? "en-US").ToLower();

            switch (lang)
            {
                case "zh-cn":
                    return new LocalizedStrings
                    {
                        Title = "您的余额低",
                        ConfirmText = "确定",
                        CancelText = "取消",
                        PurchaseButtonText = "购买"
                    };

                case "zh-tw":
                    return new LocalizedStrings
                    {
                        Title = "您的餘額低",
                        ConfirmText = "確定",
                        CancelText = "取消",
                        PurchaseButtonText = "購買"
                    };

                case "ja-jp":
                    return new LocalizedStrings
                    {
                        Title = "残高が不足しています",
                        ConfirmText = "OK",
                        CancelText = "キャンセル",
                        PurchaseButtonText = "購入"
                    };

                case "ko-kr":
                    return new LocalizedStrings
                    {
                        Title = "잔액이 부족합니다",
                        ConfirmText = "확인",
                        CancelText = "취소",
                        PurchaseButtonText = "구매"
                    };

                default: // en-US
                    return new LocalizedStrings
                    {
                        Title = "Your balance is low",
                        ConfirmText = "OK",
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
