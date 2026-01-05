using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Steamworks;
using PlayKit_SDK.Steam;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;

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
            var window = GetWindow<PlayKit_SteamDebugWindow>("Steam Debug");
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

            GUILayout.Label("PlayKit Steam Addon - Debug Window", titleStyle);
            GUILayout.Label("Monitor Steam status and test IAP purchases", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawSteamStatus()
        {
            GUILayout.Label("Steam Status", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Refresh button
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Connection:", GUILayout.Width(100));

            if (_steamInitialized)
            {
                GUIStyle successStyle = new GUIStyle(EditorStyles.label);
                successStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                GUILayout.Label("✓ Connected", successStyle);
            }
            else
            {
                GUIStyle errorStyle = new GUIStyle(EditorStyles.label);
                errorStyle.normal.textColor = new Color(0.8f, 0.2f, 0.2f);
                GUILayout.Label("✗ Not Connected", errorStyle);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                RefreshSteamStatus();
            }

            EditorGUILayout.EndHorizontal();

            if (_steamInitialized)
            {
                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("Steam ID:", _steamId ?? "N/A");
                EditorGUILayout.LabelField("Display Name:", _steamName ?? "N/A");

                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "Steam is running and initialized. You can test purchases in Play Mode.",
                    MessageType.Info
                );
            }
            else
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "Steam is not running or not initialized.\n\n" +
                    "To test Steam features:\n" +
                    "1. Launch Steam client\n" +
                    "2. Enter Play Mode\n" +
                    "3. Steam will auto-initialize",
                    MessageType.Warning
                );
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSteamAppId()
        {
            GUILayout.Label("Steam Configuration", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Steam App ID:", GUILayout.Width(100));

            if (!string.IsNullOrEmpty(_steamAppId))
            {
                EditorGUILayout.SelectableLabel(_steamAppId, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
            else
            {
                GUILayout.Label("Not configured", EditorStyles.miniLabel);
            }

            if (GUILayout.Button("Load from Server", GUILayout.Width(120)))
            {
                LoadSteamConfigFromServer();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "Steam App ID is configured in PlayKit Dashboard.\n" +
                "Select a Steam channel game in PlayKit Settings to use Steam features.",
                MessageType.Info
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawProducts()
        {
            GUILayout.Label("IAP Products", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Available Products: {_products.Count}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            GUI.enabled = !_isLoadingProducts;
            if (GUILayout.Button(_isLoadingProducts ? "Loading..." : "Refresh Products", GUILayout.Width(120)))
            {
                LoadProducts();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            if (_isLoadingProducts)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Loading products from server...", MessageType.Info);
            }
            else if (!string.IsNullOrEmpty(_productsError))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox($"Error: {_productsError}", MessageType.Error);
            }
            else if (_products.Count == 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "No products configured.\n" +
                    "Configure IAP products in PlayKit Dashboard.",
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

            // Product info
            EditorGUILayout.BeginVertical();
            GUILayout.Label(product.name ?? product.sku, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(product.description))
            {
                GUILayout.Label(product.description, EditorStyles.miniLabel);
            }
            GUILayout.Label($"SKU: {product.sku}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Price
            string priceText = product.price_cents > 0
                ? $"{product.price_cents / 100.0:F2} {product.currency ?? "USD"}"
                : "Free";
            GUILayout.Label(priceText, EditorStyles.boldLabel, GUILayout.Width(80));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTestPurchase()
        {
            GUILayout.Label("Test Purchase", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to test purchases.\n" +
                    "Steam purchases can only be tested at runtime.",
                    MessageType.Info
                );
            }
            else if (!_steamInitialized)
            {
                EditorGUILayout.HelpBox(
                    "Steam is not initialized.\n" +
                    "Make sure Steam client is running.",
                    MessageType.Warning
                );
            }
            else if (_products.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No products available.\n" +
                    "Load products first.",
                    MessageType.Warning
                );
            }
            else
            {
                EditorGUILayout.LabelField("Select Product:");

                string[] productNames = new string[_products.Count];
                for (int i = 0; i < _products.Count; i++)
                {
                    productNames[i] = $"{_products[i].name ?? _products[i].sku} ({_products[i].sku})";
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
                if (GUILayout.Button(_isPurchasing ? "Purchasing..." : "Test Purchase", GUILayout.Height(30)))
                {
                    TestPurchase(_selectedSku);
                }
                GUI.enabled = true;

                EditorGUILayout.Space(5);

                EditorGUILayout.HelpBox(
                    "This will initiate a real Steam purchase flow.\n" +
                    "The Steam overlay will appear if configured correctly.\n" +
                    "Make sure you're testing with a development build.",
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
                    EditorUtility.DisplayDialog("Error", "PlayKitSettings not found", "OK");
                    return;
                }

                // Get GameId using reflection
                var gameIdProp = settings.GetType().GetProperty("GameId");
                string gameId = gameIdProp?.GetValue(settings) as string;

                if (string.IsNullOrEmpty(gameId))
                {
                    EditorUtility.DisplayDialog("Error", "No game selected in PlayKit Settings", "OK");
                    return;
                }

                // Get BaseUrl
                var baseUrlProp = settings.GetType().GetProperty("BaseUrl");
                string baseUrl = baseUrlProp?.GetValue(settings) as string ?? "https://playkit.ai";

                string endpoint = $"{baseUrl}/api/games/{gameId}/steam-config";

                using (var request = UnityWebRequest.Get(endpoint))
                {
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
                        }
                    }
                    else
                    {
                        Debug.LogError($"Failed to load Steam config: {request.error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading Steam config: {ex.Message}");
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
                    _productsError = "PlayKitSettings not found";
                    return;
                }

                var gameIdProp = settings.GetType().GetProperty("GameId");
                string gameId = gameIdProp?.GetValue(settings) as string;

                if (string.IsNullOrEmpty(gameId))
                {
                    _productsError = "No game selected";
                    return;
                }

                var baseUrlProp = settings.GetType().GetProperty("BaseUrl");
                string baseUrl = baseUrlProp?.GetValue(settings) as string ?? "https://playkit.ai";

                string endpoint = $"{baseUrl}/api/games/{gameId}/products";

                using (var request = UnityWebRequest.Get(endpoint))
                {
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
                            _productsError = response?.error ?? "Failed to load products";
                        }
                    }
                    else
                    {
                        _productsError = $"API Error: {request.error}";
                    }
                }
            }
            catch (Exception ex)
            {
                _productsError = ex.Message;
                Debug.LogError($"Error loading products: {ex.Message}");
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
                // Find PlayKit SDK instance in scene
                var sdkType = System.Type.GetType("PlayKit_SDK.PlayKit_SDK, PlayKit_SDK");
                if (sdkType == null)
                {
                    EditorUtility.DisplayDialog(
                        "Error",
                        "PlayKit SDK type not found.\n" +
                        "Make sure PlayKit SDK is properly installed.",
                        "OK"
                    );
                    return;
                }

                var sdkInstance = FindObjectOfType(sdkType);
                if (sdkInstance == null)
                {
                    EditorUtility.DisplayDialog(
                        "Error",
                        "PlayKit SDK not found in scene.\n" +
                        "Make sure PlayKit_SDK is initialized.",
                        "OK"
                    );
                    return;
                }

                // Get RechargeManager property
                var rechargeManagerProp = sdkType.GetProperty("RechargeManager");
                if (rechargeManagerProp == null)
                {
                    EditorUtility.DisplayDialog(
                        "Error",
                        "RechargeManager property not found.",
                        "OK"
                    );
                    return;
                }

                var rechargeManager = rechargeManagerProp.GetValue(sdkInstance);
                if (rechargeManager == null)
                {
                    EditorUtility.DisplayDialog(
                        "Error",
                        "RechargeManager not initialized.",
                        "OK"
                    );
                    return;
                }

                // Call RechargeAsync
                var rechargeMethod = rechargeManager.GetType().GetMethod("RechargeAsync");
                if (rechargeMethod != null)
                {
                    // Invoke and await the result without explicit type casting
                    var taskObj = rechargeMethod.Invoke(rechargeManager, new object[] { sku });
                    var result = await (dynamic)taskObj;

                    if (result.Success)
                    {
                        EditorUtility.DisplayDialog(
                            "Purchase Complete",
                            $"Purchase successful!\n" +
                            $"Credited: {result.CreditedAmount} coins",
                            "OK"
                        );
                    }
                    else
                    {
                        EditorUtility.DisplayDialog(
                            "Purchase Failed",
                            $"Error: {result.Error}",
                            "OK"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    $"Failed to test purchase:\n{ex.Message}",
                    "OK"
                );
                Debug.LogError($"Error testing purchase: {ex}");
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
