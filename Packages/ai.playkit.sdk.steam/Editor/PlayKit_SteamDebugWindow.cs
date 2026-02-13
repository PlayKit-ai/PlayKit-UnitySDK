using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Steamworks;
using PlayKit_SDK.Steam;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;
using L10n = PlayKit.SDK.Editor.L10n;

namespace PlayKit_SDK.Steam.Editor
{
    /// <summary>
    /// Debug/Configuration window for PlayKit Steam Addon.
    /// Shows Steam status, App ID, products, and allows testing purchases.
    /// </summary>
    public class PlayKit_SteamDebugWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private bool _isLoadingProducts;
        private string _productsError;
        private List<ProductInfo> _products = new List<ProductInfo>();

        // Steam status
        private bool _steamInitialized;
        private string _steamId;
        private string _steamName;
        private string _steamAppId;

        // Test purchase
        private string _selectedSku;
        private bool _isPurchasing;

        [System.Serializable]
        private class ProductInfo
        {
            public string sku;
            public string name;
            public string description;
            public int price_cents;
            public string currency;

            /// <summary>
            /// Get localized name, parsing i18n JSON if present
            /// </summary>
            public string LocalizedName => PlayKit_SDK.Recharge.IAPProduct.GetLocalizedTextStatic(name, GetEditorLanguageCode());

            /// <summary>
            /// Get localized description, parsing i18n JSON if present
            /// </summary>
            public string LocalizedDescription => PlayKit_SDK.Recharge.IAPProduct.GetLocalizedTextStatic(description, GetEditorLanguageCode());

            private static string GetEditorLanguageCode()
            {
                // Use EditorLocalization's current language if available
                try
                {
                    return PlayKit.SDK.Editor.EditorLocalization.GetCurrentLanguage() ?? "en-US";
                }
                catch
                {
                    return "en-US";
                }
            }
        }

        [System.Serializable]
        private class ProductsResponse
        {
            public bool success;
            public List<ProductInfo> products;
            public string error;
        }

        [MenuItem("PlayKit SDK/Steam Addon/Debug Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<PlayKit_SteamDebugWindow>(L10n.Get("steam.window.title"));
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshSteamStatus();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawSteamStatus();
            EditorGUILayout.Space(10);

            DrawSteamAppId();
            EditorGUILayout.Space(10);

            DrawProducts();
            EditorGUILayout.Space(10);

            DrawTestPurchase();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };

            GUILayout.Label(L10n.Get("steam.header.title"), titleStyle);
            GUILayout.Label(L10n.Get("steam.header.subtitle"), EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawSteamStatus()
        {
            GUILayout.Label(L10n.Get("steam.status.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Refresh button
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(L10n.Get("steam.status.connection"), GUILayout.Width(100));

            if (_steamInitialized)
            {
                GUIStyle successStyle = new GUIStyle(EditorStyles.label);
                successStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                GUILayout.Label(L10n.Get("steam.status.connected"), successStyle);
            }
            else
            {
                GUIStyle errorStyle = new GUIStyle(EditorStyles.label);
                errorStyle.normal.textColor = new Color(0.8f, 0.2f, 0.2f);
                GUILayout.Label(L10n.Get("steam.status.not_connected"), errorStyle);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(L10n.Get("steam.status.refresh"), GUILayout.Width(80)))
            {
                RefreshSteamStatus();
            }

            EditorGUILayout.EndHorizontal();

            if (_steamInitialized)
            {
                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField(L10n.Get("steam.status.steam_id"), _steamId ?? L10n.Get("common.n_a"));
                EditorGUILayout.LabelField(L10n.Get("steam.status.display_name"), _steamName ?? L10n.Get("common.n_a"));

                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    L10n.Get("steam.status.initialized"),
                    MessageType.Info
                );
            }
            else
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    L10n.Get("steam.status.not_initialized"),
                    MessageType.Warning
                );
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSteamAppId()
        {
            GUILayout.Label(L10n.Get("steam.config.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(L10n.Get("steam.config.app_id"), GUILayout.Width(100));

            if (!string.IsNullOrEmpty(_steamAppId))
            {
                EditorGUILayout.SelectableLabel(_steamAppId, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
            else
            {
                GUILayout.Label(L10n.Get("steam.config.not_configured"), EditorStyles.miniLabel);
            }

            if (GUILayout.Button(L10n.Get("steam.config.load_from_server"), GUILayout.Width(120)))
            {
                LoadSteamConfigFromServer();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // steam_appid.txt sync status and button
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("steam_appid.txt:", GUILayout.Width(100));

            string currentFileAppId = SteamAppIdWriter.ReadCurrentAppId();
            if (!string.IsNullOrEmpty(currentFileAppId))
            {
                // Show sync status
                bool isSynced = !string.IsNullOrEmpty(_steamAppId) && currentFileAppId == _steamAppId;

                GUIStyle statusStyle = new GUIStyle(EditorStyles.label);
                statusStyle.normal.textColor = isSynced
                    ? new Color(0.2f, 0.8f, 0.2f)
                    : new Color(0.8f, 0.6f, 0.2f);

                GUILayout.Label(currentFileAppId + (isSynced ? " ✓" : " (outdated)"), statusStyle);
            }
            else
            {
                GUILayout.Label(L10n.Get("steam.appid.not_created"), EditorStyles.miniLabel);
            }

            GUILayout.FlexibleSpace();

            GUI.enabled = !string.IsNullOrEmpty(_steamAppId);
            if (GUILayout.Button("Sync to File", GUILayout.Width(100)))
            {
                bool success = SteamAppIdWriter.WriteSteamAppId(_steamAppId);
                if (success)
                {
                    EditorUtility.DisplayDialog(
                        L10n.Get("common.success"),
                        $"steam_appid.txt updated with App ID: {_steamAppId}",
                        L10n.Get("common.ok")
                    );
                }
                Repaint();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                L10n.Get("steam.config.help"),
                MessageType.Info
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawProducts()
        {
            GUILayout.Label(L10n.Get("steam.products.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(string.Format(L10n.Get("steam.products.available"), _products.Count), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            GUI.enabled = !_isLoadingProducts;
            if (GUILayout.Button(_isLoadingProducts ? L10n.Get("steam.products.loading") : L10n.Get("steam.products.refresh"), GUILayout.Width(120)))
            {
                LoadProducts();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            if (_isLoadingProducts)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(L10n.Get("steam.products.loading_msg"), MessageType.Info);
            }
            else if (!string.IsNullOrEmpty(_productsError))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(string.Format(L10n.Get("steam.products.error"), _productsError), MessageType.Error);
            }
            else if (_products.Count == 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    L10n.Get("steam.products.no_products"),
                    MessageType.Warning
                );
            }
            else
            {
                EditorGUILayout.Space(5);

                foreach (var product in _products)
                {
                    DrawProductRow(product);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawProductRow(ProductInfo product)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Product info - use localized name/description
            EditorGUILayout.BeginVertical();
            GUILayout.Label(product.LocalizedName ?? product.sku, EditorStyles.boldLabel);
            string localizedDesc = product.LocalizedDescription;
            if (!string.IsNullOrEmpty(localizedDesc))
            {
                GUILayout.Label(localizedDesc, EditorStyles.miniLabel);
            }
            GUILayout.Label(string.Format(L10n.Get("steam.products.sku"), product.sku), EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Price
            string priceText = product.price_cents > 0
                ? $"{product.price_cents / 100.0:F2} {product.currency ?? "USD"}"
                : L10n.Get("steam.products.free");
            GUILayout.Label(priceText, EditorStyles.boldLabel, GUILayout.Width(80));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTestPurchase()
        {
            GUILayout.Label(L10n.Get("steam.purchase.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    L10n.Get("steam.purchase.need_play_mode"),
                    MessageType.Info
                );
            }
            else if (!_steamInitialized)
            {
                EditorGUILayout.HelpBox(
                    L10n.Get("steam.purchase.not_initialized"),
                    MessageType.Warning
                );
            }
            else if (_products.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    L10n.Get("steam.purchase.no_products"),
                    MessageType.Warning
                );
            }
            else
            {
                EditorGUILayout.LabelField(L10n.Get("steam.purchase.select_product"));

                string[] productNames = new string[_products.Count];
                for (int i = 0; i < _products.Count; i++)
                {
                    productNames[i] = $"{_products[i].LocalizedName ?? _products[i].sku} ({_products[i].sku})";
                }

                int selectedIndex = -1;
                if (!string.IsNullOrEmpty(_selectedSku))
                {
                    selectedIndex = _products.FindIndex(p => p.sku == _selectedSku);
                }

                selectedIndex = EditorGUILayout.Popup(selectedIndex, productNames);

                if (selectedIndex >= 0 && selectedIndex < _products.Count)
                {
                    _selectedSku = _products[selectedIndex].sku;
                }

                EditorGUILayout.Space(5);

                GUI.enabled = !_isPurchasing && !string.IsNullOrEmpty(_selectedSku);
                if (GUILayout.Button(_isPurchasing ? L10n.Get("steam.purchase.purchasing") : L10n.Get("steam.purchase.button"), GUILayout.Height(30)))
                {
                    TestPurchase(_selectedSku);
                }
                GUI.enabled = true;

                EditorGUILayout.Space(5);

                EditorGUILayout.HelpBox(
                    L10n.Get("steam.purchase.warning"),
                    MessageType.Info
                );
            }

            EditorGUILayout.EndVertical();
        }

        private void RefreshSteamStatus()
        {
            _steamInitialized = SteamClient.IsValid;

            if (_steamInitialized)
            {
                _steamId = SteamClient.SteamId.ToString();
                _steamName = SteamClient.Name;
            }
            else
            {
                _steamId = null;
                _steamName = null;
            }

            Repaint();
        }

        private async void LoadSteamConfigFromServer()
        {
            try
            {
                var settings = Resources.Load<ScriptableObject>("PlayKitSettings");
                if (settings == null)
                {
                    EditorUtility.DisplayDialog(L10n.Get("common.error"), L10n.Get("steam.error.settings_not_found"), L10n.Get("common.ok"));
                    return;
                }

                // Get GameId using reflection
                var gameIdProp = settings.GetType().GetProperty("GameId");
                string gameId = gameIdProp?.GetValue(settings) as string;

                if (string.IsNullOrEmpty(gameId))
                {
                    EditorUtility.DisplayDialog(L10n.Get("common.error"), L10n.Get("steam.error.no_game_selected"), L10n.Get("common.ok"));
                    return;
                }

                // Get BaseUrl
                var baseUrlProp = settings.GetType().GetProperty("BaseUrl");
                string baseUrl = baseUrlProp?.GetValue(settings) as string ?? "https://api.playkit.ai";

                // Get developer token
                string developerToken = PlayKitSettings.LocalDeveloperToken;

                if (string.IsNullOrEmpty(developerToken))
                {
                    Debug.LogError(L10n.Get("steam.error.load_config_failed") + " No developer token available");
                    return;
                }

                string endpoint = $"{baseUrl}/api/external/steam-config/{gameId}";

                using (var request = UnityWebRequest.Get(endpoint))
                {
                    // Add Authorization header
                    request.SetRequestHeader("Authorization", $"Bearer {developerToken}");

                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        await UniTask.Yield();
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.downloadHandler.text);
                        if (response != null && response.ContainsKey("steamConfig"))
                        {
                            var config = JsonConvert.DeserializeObject<Dictionary<string, object>>(response["steamConfig"].ToString());
                            _steamAppId = config.ContainsKey("releaseAppId") ? config["releaseAppId"]?.ToString() : null;

                            // Auto-sync to file after loading
                            if (!string.IsNullOrEmpty(_steamAppId))
                            {
                                SteamAppIdWriter.WriteSteamAppId(_steamAppId);
                                Debug.Log($"[PlayKit Steam] ✓ Synced steam_appid.txt with App ID: {_steamAppId}");
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError(string.Format(L10n.Get("steam.error.load_config_failed"), request.error));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(string.Format(L10n.Get("steam.error.load_config_exception"), ex.Message));
            }

            Repaint();
        }

        private async void LoadProducts()
        {
            _isLoadingProducts = true;
            _productsError = null;
            Repaint();

            try
            {
                var settings = Resources.Load<ScriptableObject>("PlayKitSettings");
                if (settings == null)
                {
                    _productsError = L10n.Get("steam.error.settings_not_found");
                    return;
                }

                var gameIdProp = settings.GetType().GetProperty("GameId");
                string gameId = gameIdProp?.GetValue(settings) as string;

                if (string.IsNullOrEmpty(gameId))
                {
                    _productsError = L10n.Get("steam.error.no_game_selected");
                    return;
                }

                var baseUrlProp = settings.GetType().GetProperty("BaseUrl");
                string baseUrl = baseUrlProp?.GetValue(settings) as string ?? "https://api.playkit.ai";

                // Get developer token
                string developerToken = PlayKitSettings.LocalDeveloperToken;

                if (string.IsNullOrEmpty(developerToken))
                {
                    _productsError = "No developer token available";
                    return;
                }

                string endpoint = $"{baseUrl}/api/external/games/{gameId}/products";

                using (var request = UnityWebRequest.Get(endpoint))
                {
                    // Add Authorization header
                    request.SetRequestHeader("Authorization", $"Bearer {developerToken}");

                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        await UniTask.Yield();
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var response = JsonConvert.DeserializeObject<ProductsResponse>(request.downloadHandler.text);
                        if (response != null && response.success)
                        {
                            _products = response.products ?? new List<ProductInfo>();
                        }
                        else
                        {
                            _productsError = response?.error ?? L10n.Get("steam.error.load_products_failed");
                        }
                    }
                    else
                    {
                        _productsError = string.Format(L10n.Get("steam.error.api_error"), request.error);
                    }
                }
            }
            catch (Exception ex)
            {
                _productsError = ex.Message;
                Debug.LogError(string.Format(L10n.Get("steam.error.load_products_exception"), ex.Message));
            }
            finally
            {
                _isLoadingProducts = false;
                Repaint();
            }
        }

        private async void TestPurchase(string sku)
        {
            _isPurchasing = true;
            Repaint();

            try
            {
                Debug.Log($"[Steam Test Purchase] Starting test purchase for SKU: {sku}");

                // Find PlayKit SDK instance in scene
                var sdkType = System.Type.GetType("PlayKit_SDK.PlayKit_SDK, PlayKit_SDK");
                if (sdkType == null)
                {
                    Debug.LogError("[Steam Test Purchase] PlayKit SDK type not found");
                    EditorUtility.DisplayDialog(
                        L10n.Get("common.error"),
                        "PlayKit SDK type not found.\n" +
                        "Make sure PlayKit SDK is properly installed.",
                        L10n.Get("common.ok")
                    );
                    return;
                }

                Debug.Log("[Steam Test Purchase] SDK type found");

                var sdkInstance = FindObjectOfType(sdkType);
                if (sdkInstance == null)
                {
                    Debug.LogError("[Steam Test Purchase] SDK instance not found in scene");
                    EditorUtility.DisplayDialog(
                        L10n.Get("common.error"),
                        "PlayKit SDK not found in scene.\n" +
                        "Make sure PlayKit_SDK is initialized.",
                        L10n.Get("common.ok")
                    );
                    return;
                }

                Debug.Log("[Steam Test Purchase] SDK instance found");

                // Get RechargeManager via GetRechargeManager() method
                var getRechargeManagerMethod = sdkType.GetMethod("GetRechargeManager", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (getRechargeManagerMethod == null)
                {
                    Debug.LogError("[Steam Test Purchase] GetRechargeManager method not found");
                    EditorUtility.DisplayDialog(
                        L10n.Get("common.error"),
                        "GetRechargeManager method not found.",
                        L10n.Get("common.ok")
                    );
                    return;
                }

                Debug.Log("[Steam Test Purchase] GetRechargeManager method found, calling it...");

                var rechargeManager = getRechargeManagerMethod.Invoke(null, null);
                if (rechargeManager == null)
                {
                    Debug.LogError("[Steam Test Purchase] RechargeManager is null (SDK may not be initialized)");
                    EditorUtility.DisplayDialog(
                        L10n.Get("common.error"),
                        "RechargeManager not initialized.\n" +
                        "Make sure PlayKit_SDK.InitializeAsync() has been called and completed successfully.",
                        L10n.Get("common.ok")
                    );
                    return;
                }

                Debug.Log($"[Steam Test Purchase] RechargeManager obtained: {rechargeManager.GetType().Name}");

                // Call RechargeAsync
                var rechargeMethod = rechargeManager.GetType().GetMethod("RechargeAsync");
                if (rechargeMethod == null)
                {
                    Debug.LogError("[Steam Test Purchase] RechargeAsync method not found on RechargeManager");
                    EditorUtility.DisplayDialog(
                        L10n.Get("common.error"),
                        "RechargeAsync method not found on RechargeManager.",
                        L10n.Get("common.ok")
                    );
                    return;
                }

                Debug.Log($"[Steam Test Purchase] Calling RechargeAsync with SKU: {sku}");

                // Invoke and await the result without explicit type casting
                var taskObj = rechargeMethod.Invoke(rechargeManager, new object[] { sku });

                Debug.Log("[Steam Test Purchase] Awaiting purchase result...");
                var result = await (dynamic)taskObj;

                // Use reflection to safely access result properties
                var resultType = result.GetType();
                var initiatedProp = resultType.GetProperty("Initiated");
                var errorProp = resultType.GetProperty("Error");
                var dataProp = resultType.GetProperty("Data");

                bool initiated = initiatedProp != null ? (bool)initiatedProp.GetValue(result) : false;
                string error = errorProp != null ? (string)errorProp.GetValue(result) : null;
                string data = dataProp != null ? (string)dataProp.GetValue(result) : null;

                Debug.Log($"[Steam Test Purchase] Result received - Initiated: {initiated}, Error: {error}, Data: {data}");

                if (initiated)
                {
                    Debug.Log($"[Steam Test Purchase] Purchase initiated successfully! Data: {data}");
                    EditorUtility.DisplayDialog(
                        "Purchase Initiated",
                        $"Purchase initiated successfully!\n" +
                        $"The Steam overlay should appear to complete the purchase.\n" +
                        $"Order ID: {data}",
                        L10n.Get("common.ok")
                    );
                }
                else
                {
                    Debug.LogError($"[Steam Test Purchase] Purchase failed: {error}");
                    EditorUtility.DisplayDialog(
                        "Purchase Failed",
                        $"Error: {error ?? "Unknown error"}",
                        L10n.Get("common.ok")
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Steam Test Purchase] Exception during purchase: {ex}");
                EditorUtility.DisplayDialog(
                    L10n.Get("common.error"),
                    $"Failed to test purchase:\n{ex.Message}\n\nSee console for full stack trace.",
                    L10n.Get("common.ok")
                );
            }
            finally
            {
                _isPurchasing = false;
                Repaint();
            }
        }

        private void OnInspectorUpdate()
        {
            // Refresh Steam status periodically when in Play Mode
            if (Application.isPlaying && Time.frameCount % 60 == 0)
            {
                RefreshSteamStatus();
            }
        }
    }
}
