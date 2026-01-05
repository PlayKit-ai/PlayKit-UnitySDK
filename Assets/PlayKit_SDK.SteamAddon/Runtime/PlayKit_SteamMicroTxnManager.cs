using System;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using PlayKit_SDK.Recharge;
using Steamworks;
using UnityEngine;

namespace PlayKit_SDK.Steam
{
    /// <summary>
    /// Manages Steam Microtransaction flow.
    /// Handles Steam overlay callbacks and coordinates with SteamRechargeProvider.
    ///
    /// Usage:
    /// 1. Add this component to a GameObject in your scene
    /// 2. Call Initialize() with your SteamRechargeProvider
    /// 3. Steam overlay will automatically handle authorization
    ///
    /// Requires Facepunch.Steamworks and SteamClient to be initialized.
    /// </summary>
    public class PlayKit_SteamMicroTxnManager : MonoBehaviour
    {
        private SteamRechargeProvider _steamProvider;
        private string _pendingOrderId;
        private bool _isInitialized;

        /// <summary>
        /// Event fired when a Steam purchase is authorized by the user
        /// </summary>
        public event Action<string, bool> OnPurchaseAuthorized;

        /// <summary>
        /// Event fired when a purchase is successfully completed
        /// </summary>
        public event Action<float> OnPurchaseSuccess;

        /// <summary>
        /// Event fired when a purchase fails
        /// </summary>
        public event Action<string> OnPurchaseError;

        /// <summary>
        /// Event fired when a purchase is cancelled
        /// </summary>
        public event Action OnPurchaseCancelled;

        /// <summary>
        /// Whether the manager is initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Whether Steam is available
        /// </summary>
        public bool IsSteamAvailable => SteamClient.IsValid;

        /// <summary>
        /// Get the player's Steam64 ID
        /// </summary>
        public string GetSteamId()
        {
            return SteamClient.IsValid ? SteamClient.SteamId.ToString() : null;
        }

        /// <summary>
        /// Initialize the manager with a SteamRechargeProvider
        /// </summary>
        public void Initialize(SteamRechargeProvider steamProvider)
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[PlayKit_SteamMicroTxn] Already initialized");
                return;
            }

            if (steamProvider == null)
            {
                Debug.LogError("[PlayKit_SteamMicroTxn] SteamRechargeProvider is null");
                return;
            }

            _steamProvider = steamProvider;

            // Subscribe to MicroTxnAuthorizationResponse using Facepunch.Steamworks
            SteamUser.OnMicroTxnAuthorizationResponse += HandleMicroTxnAuthorizationResponse;

            _isInitialized = true;
            Debug.Log("[PlayKit_SteamMicroTxn] Initialized successfully");
        }

        /// <summary>
        /// Start a Steam purchase with the given SKU
        /// </summary>
        /// <param name="sku">Product SKU</param>
        public async UniTask<RechargeResult> StartPurchaseAsync(string sku)
        {
            if (!_isInitialized)
            {
                return new RechargeResult
                {
                    Initiated = false,
                    Error = "SteamMicroTxnManager not initialized"
                };
            }

            if (!IsSteamAvailable)
            {
                return new RechargeResult
                {
                    Initiated = false,
                    Error = "Steam is not available"
                };
            }

            Debug.Log($"[PlayKit_SteamMicroTxn] Starting purchase for SKU: {sku}");

            // Initiate the purchase through the provider
            var result = await _steamProvider.RechargeAsync(sku);

            if (result.Initiated)
            {
                // Store the order ID for callback handling
                _pendingOrderId = result.Data;
                Debug.Log($"[PlayKit_SteamMicroTxn] Purchase initiated, orderId: {_pendingOrderId}");
            }

            return result;
        }

        /// <summary>
        /// Called by Facepunch.Steamworks when the user authorizes or cancels a microtransaction
        /// </summary>
        private void HandleMicroTxnAuthorizationResponse(AppId appId, ulong orderId, bool authorized)
        {
            Debug.Log($"[PlayKit_SteamMicroTxn] MicroTxnAuthorizationResponse: AppID={appId}, OrderID={orderId}, Authorized={authorized}");

            string orderIdStr = orderId.ToString();

            // Fire authorization event
            OnPurchaseAuthorized?.Invoke(orderIdStr, authorized);

            if (authorized)
            {
                // User authorized, finalize the transaction
                FinalizeTransactionAsync(orderIdStr).Forget();
            }
            else
            {
                // User cancelled
                Debug.Log("[PlayKit_SteamMicroTxn] User cancelled the purchase");
                OnPurchaseCancelled?.Invoke();
                _steamProvider?.NotifyCancelled();
                _pendingOrderId = null;
            }
        }

        /// <summary>
        /// Finalize a Steam transaction after user authorization
        /// </summary>
        private async UniTaskVoid FinalizeTransactionAsync(string orderId)
        {
            Debug.Log($"[PlayKit_SteamMicroTxn] Finalizing transaction: {orderId}");

            var result = await _steamProvider.FinalizeAsync(orderId, true);

            if (result.Initiated)
            {
                Debug.Log($"[PlayKit_SteamMicroTxn] Transaction finalized successfully");

                // Try to parse the new balance from the result data
                try
                {
                    var finalizeData = JsonConvert.DeserializeObject<SteamRechargeProvider.FinalizeResponse>(result.Data);
                    OnPurchaseSuccess?.Invoke(finalizeData?.NewBalance ?? 0);
                }
                catch
                {
                    OnPurchaseSuccess?.Invoke(0);
                }
            }
            else
            {
                Debug.LogError($"[PlayKit_SteamMicroTxn] Failed to finalize transaction: {result.Error}");
                OnPurchaseError?.Invoke(result.Error ?? "Failed to finalize transaction");
            }

            _pendingOrderId = null;
        }

        private void OnDestroy()
        {
            if (_isInitialized)
            {
                SteamUser.OnMicroTxnAuthorizationResponse -= HandleMicroTxnAuthorizationResponse;
            }
        }
    }
}
