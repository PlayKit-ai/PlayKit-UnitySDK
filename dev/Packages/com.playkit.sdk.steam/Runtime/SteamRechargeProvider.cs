using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using PlayKit_SDK.Recharge;
using Steamworks;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayKit_SDK.Steam
{
    /// <summary>
    /// Steam-based recharge provider using Steam Microtransaction API.
    /// Initiates purchases through Steam overlay.
    /// </summary>
    public class SteamRechargeProvider : IRechargeProvider
    {
        private string _baseUrl;
        private string _gameId;
        private Func<string> _getPlayerToken;

        public string RechargeMethod => "steam";

        /// <summary>
        /// Whether Steamworks is available and initialized
        /// </summary>
        public bool IsAvailable => SteamClient.IsValid;

        public event Action OnRechargeInitiated;
        public event Action<RechargeResult> OnRechargeCompleted;
        public event Action OnRechargeCancelled;

        /// <summary>
        /// Get the player's Steam64 ID
        /// </summary>
        public string GetSteamId()
        {
            return SteamClient.IsValid ? SteamClient.SteamId.ToString() : null;
        }

        public void Initialize(string baseUrl, string gameId, Func<string> getPlayerToken)
        {
            _baseUrl = baseUrl;
            _gameId = gameId;
            _getPlayerToken = getPlayerToken;

            Debug.Log("[SteamRechargeProvider] Initialized");
        }

        /// <summary>
        /// Initiate a Steam purchase for the given SKU
        /// </summary>
        /// <param name="sku">Product SKU (required for Steam)</param>
        public async UniTask<RechargeResult> RechargeAsync(string sku = null)
        {
            if (string.IsNullOrEmpty(sku))
            {
                // Use warning instead of error - SKU might intentionally not be configured
                Debug.LogWarning("[SteamRechargeProvider] SKU is required for Steam purchases. " +
                    "Please configure IAP products in PlayKit Dashboard and pass a valid SKU.");
                return new RechargeResult
                {
                    Initiated = false,
                    Error = "SKU is required for Steam purchases"
                };
            }

            string playerToken = _getPlayerToken?.Invoke();
            if (string.IsNullOrEmpty(playerToken))
            {
                Debug.LogError("[SteamRechargeProvider] No player token available");
                return new RechargeResult
                {
                    Initiated = false,
                    Error = "No player token available"
                };
            }

            if (!SteamClient.IsValid)
            {
                Debug.LogError("[SteamRechargeProvider] Steam client not initialized");
                return new RechargeResult
                {
                    Initiated = false,
                    Error = "Steam client not initialized"
                };
            }

            string steamId = GetSteamId();
            if (string.IsNullOrEmpty(steamId))
            {
                Debug.LogError("[SteamRechargeProvider] No Steam ID available");
                return new RechargeResult
                {
                    Initiated = false,
                    Error = "Steam ID not available"
                };
            }

            // Call our backend to initiate the Steam transaction
            string url = $"{_baseUrl}/api/steam/{_gameId}/initiate";

            var requestBody = new InitiateRequest
            {
                SteamId = steamId,
                Sku = sku
            };

            try
            {
                string jsonData = JsonConvert.SerializeObject(requestBody);
                byte[] postData = Encoding.UTF8.GetBytes(jsonData);

                using (var request = new UnityWebRequest(url, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(postData);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Authorization", $"Bearer {playerToken}");

                    await request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var response = JsonConvert.DeserializeObject<InitiateResponse>(request.downloadHandler.text);

                        if (response.Success)
                        {
                            Debug.Log($"[SteamRechargeProvider] Transaction initiated: orderId={response.OrderId}");
                            OnRechargeInitiated?.Invoke();

                            // The actual payment happens in Steam overlay
                            // PlayKit_SteamMicroTxnManager will handle the callback
                            return new RechargeResult
                            {
                                Initiated = true,
                                Data = response.OrderId
                            };
                        }
                        else
                        {
                            Debug.LogError($"[SteamRechargeProvider] Failed to initiate: {response.Error}");
                            return new RechargeResult
                            {
                                Initiated = false,
                                Error = response.Error ?? "Failed to initiate Steam transaction"
                            };
                        }
                    }
                    else
                    {
                        string error = request.downloadHandler?.text ?? request.error;
                        Debug.LogError($"[SteamRechargeProvider] Request failed: {error}");
                        return new RechargeResult
                        {
                            Initiated = false,
                            Error = $"Network error: {error}"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return new RechargeResult
                {
                    Initiated = false,
                    Error = $"Exception: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Finalize a Steam purchase after receiving MicroTxnAuthorizationResponse.
        /// Called by PlayKit_SteamMicroTxnManager.
        /// </summary>
        /// <param name="orderId">The order ID from the initiate response</param>
        /// <param name="authorized">Whether the user authorized the purchase</param>
        public async UniTask<RechargeResult> FinalizeAsync(string orderId, bool authorized)
        {
            if (!authorized)
            {
                OnRechargeCancelled?.Invoke();
                return new RechargeResult
                {
                    Initiated = false,
                    Error = "User cancelled the purchase"
                };
            }

            string playerToken = _getPlayerToken?.Invoke();
            if (string.IsNullOrEmpty(playerToken))
            {
                return new RechargeResult
                {
                    Initiated = false,
                    Error = "No player token available"
                };
            }

            string url = $"{_baseUrl}/api/steam/{_gameId}/finalize";

            var requestBody = new FinalizeRequest
            {
                OrderId = orderId
            };

            try
            {
                string jsonData = JsonConvert.SerializeObject(requestBody);
                byte[] postData = Encoding.UTF8.GetBytes(jsonData);

                using (var request = new UnityWebRequest(url, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(postData);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Authorization", $"Bearer {playerToken}");

                    await request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var response = JsonConvert.DeserializeObject<FinalizeResponse>(request.downloadHandler.text);

                        if (response.Success)
                        {
                            Debug.Log($"[SteamRechargeProvider] Purchase finalized: credited={response.CreditedAmount}, newBalance={response.NewBalance}");

                            var result = new RechargeResult
                            {
                                Initiated = true,
                                Data = JsonConvert.SerializeObject(response)
                            };

                            OnRechargeCompleted?.Invoke(result);
                            return result;
                        }
                        else
                        {
                            Debug.LogError($"[SteamRechargeProvider] Failed to finalize: {response.Error}");
                            return new RechargeResult
                            {
                                Initiated = false,
                                Error = response.Error ?? "Failed to finalize Steam transaction"
                            };
                        }
                    }
                    else
                    {
                        string error = request.downloadHandler?.text ?? request.error;
                        Debug.LogError($"[SteamRechargeProvider] Finalize request failed: {error}");
                        return new RechargeResult
                        {
                            Initiated = false,
                            Error = $"Network error: {error}"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return new RechargeResult
                {
                    Initiated = false,
                    Error = $"Exception: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Notify that a recharge was cancelled
        /// </summary>
        internal void NotifyCancelled()
        {
            OnRechargeCancelled?.Invoke();
        }

        /// <summary>
        /// Get available IAP products from backend
        /// </summary>
        public async UniTask<ProductListResult> GetAvailableProductsAsync()
        {
            string playerToken = _getPlayerToken?.Invoke();
            if (string.IsNullOrEmpty(playerToken))
            {
                Debug.LogError("[SteamRechargeProvider] No player token available for GetAvailableProductsAsync");
                return new ProductListResult
                {
                    Success = false,
                    Error = "No player token available"
                };
            }

            try
            {
                string url = $"{_baseUrl}/api/external/games/{_gameId}/products";

                using (var request = UnityWebRequest.Get(url))
                {
                    // Add Authorization header with player token
                    request.SetRequestHeader("Authorization", $"Bearer {playerToken}");

                    await request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var response = JsonConvert.DeserializeObject<ProductsApiResponse>(request.downloadHandler.text);

                        if (response != null && response.Success)
                        {
                            return new ProductListResult
                            {
                                Success = true,
                                Products = response.Products ?? new System.Collections.Generic.List<IAPProduct>()
                            };
                        }
                        else
                        {
                            return new ProductListResult
                            {
                                Success = false,
                                Error = response?.Error ?? "Failed to load products"
                            };
                        }
                    }
                    else
                    {
                        string error = request.downloadHandler?.text ?? request.error;
                        Debug.LogError($"[SteamRechargeProvider] Failed to load products: {error}");
                        return new ProductListResult
                        {
                            Success = false,
                            Error = $"Network error: {error}"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return new ProductListResult
                {
                    Success = false,
                    Error = $"Exception: {ex.Message}"
                };
            }
        }

        #region Request/Response DTOs

        [Serializable]
        private class InitiateRequest
        {
            [JsonProperty("steamId")] public string SteamId { get; set; }
            [JsonProperty("sku")] public string Sku { get; set; }
        }

        [Serializable]
        private class InitiateResponse
        {
            [JsonProperty("success")] public bool Success { get; set; }
            [JsonProperty("orderId")] public string OrderId { get; set; }
            [JsonProperty("transId")] public string TransId { get; set; }
            [JsonProperty("sandbox")] public bool Sandbox { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
        }

        [Serializable]
        private class FinalizeRequest
        {
            [JsonProperty("orderId")] public string OrderId { get; set; }
        }

        [Serializable]
        public class FinalizeResponse
        {
            [JsonProperty("success")] public bool Success { get; set; }
            [JsonProperty("orderId")] public string OrderId { get; set; }
            [JsonProperty("transId")] public string TransId { get; set; }
            [JsonProperty("creditedAmount")] public float CreditedAmount { get; set; }
            [JsonProperty("newBalance")] public float NewBalance { get; set; }
            [JsonProperty("sandbox")] public bool Sandbox { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
        }

        [Serializable]
        private class ProductsApiResponse
        {
            [JsonProperty("success")] public bool Success { get; set; }
            [JsonProperty("products")] public System.Collections.Generic.List<IAPProduct> Products { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
        }

        #endregion
    }
}
