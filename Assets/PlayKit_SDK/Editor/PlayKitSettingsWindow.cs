using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlayKit_SDK.Editor;
using PlayKit_SDK.Auth;
using UnityEngine;
using UnityEditor;
using System.Linq;
using Newtonsoft.Json;
using PlayKit.SDK.Editor;
using UnityEngine.Networking;
using L10n = PlayKit.SDK.Editor.L10n;

namespace PlayKit_SDK
{
    /// <summary>
    /// Editor window for configuring PlayKit SDK settings.
    /// Access via PlayKit SDK > Settings
    /// </summary>
    public class PlayKitSettingsWindow : EditorWindow
    {
        private PlayKitSettings settings;
        private SerializedObject serializedSettings;
        private Vector2 scrollPosition;

        // Tab navigation
        private enum Tab
        {
            Configuration,
            About
        }
        private Tab currentTab = Tab.Configuration;

        // Device Auth state
        private DeviceAuthEditorFlow _deviceAuthFlow;
        private bool _isDeviceAuthInProgress = false;
        private string _deviceAuthStatus = "";
        private MessageType _deviceAuthStatusType = MessageType.Info;
        private bool _deviceAuthHandlersAttached = false;

        // UI State
        private bool _showAdvancedSettings = false;

        // Games list state
        private List<GameInfo> _gamesList = new List<GameInfo>();
        private string[] _gamesDisplayNames = new string[0];
        private int _selectedGameIndex = -1;
        private bool _isLoadingGames = false;
        private string _gamesLoadError = "";

        // Models list state
        private List<ModelInfo> _textModelsList = new List<ModelInfo>();
        private List<ModelInfo> _imageModelsList = new List<ModelInfo>();
        private string[] _textModelsDisplayNames = new string[0];
        private string[] _imageModelsDisplayNames = new string[0];
        private int _selectedTextModelIndex = -1;
        private int _selectedImageModelIndex = -1;
        private bool _isLoadingModels = false;
        private string _modelsLoadError = "";

        [System.Serializable]
        private class GameInfo
        {
            public string id;
            public string name;
            public string description;
            public bool is_suspended;
        }

        [System.Serializable]
        private class GamesListResponse
        {
            public bool success;
            public List<GameInfo> games;
            public string error;
        }

        [System.Serializable]
        private class ModelInfo
        {
            public string id;
            public string name;
            public string description;
            public string provider;
            public string type;
            public bool is_recommended;
        }

        [System.Serializable]
        private class ModelsListResponse
        {
            public List<ModelInfo> models;
            public Dictionary<string, List<ModelInfo>> by_type;
            public int count;
            public ModelsErrorInfo error;
        }

        [System.Serializable]
        private class ModelsErrorInfo
        {
            public string code;
            public string message;
        }

        [MenuItem("PlayKit SDK/Settings", priority = 0)]
        public static void ShowWindow()
        {
            PlayKitSettingsWindow window = GetWindow<PlayKitSettingsWindow>(L10n.Get("window.title"));
            window.minSize = new Vector2(500, 500);
            window.Show();
        }

        private void OnEnable()
        {
            LoadSettings();
            // If already logged in, load games list
            if (!string.IsNullOrEmpty(PlayKitSettings.LocalDeveloperToken))
            {
                LoadGamesList();
                // If a game is already selected, load models
                if (!string.IsNullOrEmpty(settings?.GameId))
                {
                    LoadModelsList();
                }
            }
        }

        private void OnDisable()
        {
            // Clean up event handlers
            DetachDeviceAuthHandlers();
        }

        private void LoadSettings()
        {
            settings = PlayKitSettings.Instance;
            if (settings != null)
            {
                serializedSettings = new SerializedObject(settings);
            }
        }

        private void OnGUI()
        {
            if (settings == null || serializedSettings == null)
            {
                LoadSettings();
                if (settings == null)
                {
                    EditorGUILayout.HelpBox(L10n.Get("common.failed"), MessageType.Error);
                    return;
                }
            }

            serializedSettings.Update();

            DrawHeader();
            EditorGUILayout.Space(5);
            DrawTabNavigation();
            EditorGUILayout.Space(5);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            switch (currentTab)
            {
                case Tab.Configuration:
                    DrawConfigurationTab();
                    break;
                case Tab.About:
                    DrawAboutTab();
                    break;
            }

            EditorGUILayout.EndScrollView();

            if (serializedSettings.hasModifiedProperties)
            {
                serializedSettings.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        // Cached banner texture
        private Texture2D _bannerTexture;

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Load and display banner image
            if (_bannerTexture == null)
            {
                // Try to load the banner from the Art folder
                string[] guids = AssetDatabase.FindAssets("Playkit_Editor_Banner t:Texture2D");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _bannerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }

            if (_bannerTexture != null)
            {
                // Calculate aspect ratio to fit in the window
                float maxWidth = EditorGUIUtility.currentViewWidth - 40;
                float aspectRatio = (float)_bannerTexture.width / _bannerTexture.height;
                float displayHeight = Mathf.Min(80, maxWidth / aspectRatio);
                float displayWidth = displayHeight * aspectRatio;

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                Rect rect = GUILayoutUtility.GetRect(displayWidth, displayHeight);
                GUI.DrawTexture(rect, _bannerTexture, ScaleMode.ScaleToFit);

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTabNavigation()
        {
            EditorGUILayout.BeginHorizontal();

            GUIStyle tabStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fixedHeight = 30
            };

            if (GUILayout.Toggle(currentTab == Tab.Configuration, L10n.Get("tab.configuration"), tabStyle))
            {
                currentTab = Tab.Configuration;
            }

            if (GUILayout.Toggle(currentTab == Tab.About, L10n.Get("tab.about"), tabStyle))
            {
                currentTab = Tab.About;
            }

            EditorGUILayout.EndHorizontal();
        }

        #region Configuration Tab

        private void DrawConfigurationTab()
        {
            EditorGUILayout.Space(10);

            // Authentication Section
            DrawAuthenticationSection();

            EditorGUILayout.Space(10);

            // Game Selection (only if logged in)
            if (!string.IsNullOrEmpty(PlayKitSettings.LocalDeveloperToken))
            {
                DrawGameSelectionSection();
                EditorGUILayout.Space(10);
            }

            // Basic Settings (Language + AI Model Defaults)
            DrawBasicSettings();

            EditorGUILayout.Space(10);

            // Developer Tools
            DrawDeveloperTools();

            EditorGUILayout.Space(10);

            // Advanced Settings (collapsible, Custom Base URL only)
            DrawAdvancedSettings();
        }

        private void DrawBasicSettings()
        {
            GUILayout.Label(L10n.Get("config.basic.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Language selector
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(L10n.Get("config.basic.language"), GUILayout.Width(100));

            EditorGUI.BeginChangeCheck();
            string currentLang = EditorLocalization.GetCurrentLanguage();
            int currentIndex = Array.IndexOf(EditorLocalization.SupportedLanguages.Keys.ToArray(), currentLang);
            if (currentIndex < 0) currentIndex = 0;

            string[] languageNames = EditorLocalization.SupportedLanguages.Values.ToArray();
            int newIndex = EditorGUILayout.Popup(currentIndex, languageNames, GUILayout.Width(150));

            if (EditorGUI.EndChangeCheck())
            {
                string newLang = EditorLocalization.SupportedLanguages.Keys.ToArray()[newIndex];
                EditorLocalization.SetLanguage(newLang);
                Repaint();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // AI Model Defaults
            EditorGUILayout.Space(10);
            GUILayout.Label(L10n.Get("config.models.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Check if we can show model dropdowns
            bool hasGameSelected = !string.IsNullOrEmpty(settings?.GameId);
            bool isLoggedIn = !string.IsNullOrEmpty(PlayKitSettings.LocalDeveloperToken);

            if (!isLoggedIn || !hasGameSelected)
            {
                EditorGUILayout.HelpBox(L10n.Get("config.models.select_game_first"), MessageType.Warning);
            }
            else if (_isLoadingModels)
            {
                EditorGUILayout.HelpBox(L10n.Get("config.models.loading"), MessageType.Info);
            }
            else if (!string.IsNullOrEmpty(_modelsLoadError))
            {
                EditorGUILayout.HelpBox($"{L10n.Get("config.models.load_error")}: {_modelsLoadError}", MessageType.Error);
                if (GUILayout.Button(L10n.Get("config.models.refresh"), GUILayout.Height(25)))
                {
                    LoadModelsList();
                }
            }
            else
            {
                // Chat Model Dropdown
                if (_textModelsList.Count > 0)
                {
                    EditorGUI.BeginChangeCheck();
                    _selectedTextModelIndex = EditorGUILayout.Popup(
                        new GUIContent(L10n.Get("config.models.chat.label"), L10n.Get("config.models.chat.tooltip")),
                        _selectedTextModelIndex,
                        _textModelsDisplayNames
                    );

                    if (EditorGUI.EndChangeCheck() && _selectedTextModelIndex >= 0 && _selectedTextModelIndex < _textModelsList.Count)
                    {
                        var selectedModel = _textModelsList[_selectedTextModelIndex];
                        SerializedProperty chatModelProp = serializedSettings.FindProperty("defaultChatModel");
                        chatModelProp.stringValue = selectedModel.id;
                        serializedSettings.ApplyModifiedProperties();
                        EditorUtility.SetDirty(settings);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(L10n.Get("config.models.chat.label"), L10n.Get("config.models.none_available"));
                }

                EditorGUILayout.Space(5);

                // Image Model Dropdown
                if (_imageModelsList.Count > 0)
                {
                    EditorGUI.BeginChangeCheck();
                    _selectedImageModelIndex = EditorGUILayout.Popup(
                        new GUIContent(L10n.Get("config.models.image.label"), L10n.Get("config.models.image.tooltip")),
                        _selectedImageModelIndex,
                        _imageModelsDisplayNames
                    );

                    if (EditorGUI.EndChangeCheck() && _selectedImageModelIndex >= 0 && _selectedImageModelIndex < _imageModelsList.Count)
                    {
                        var selectedModel = _imageModelsList[_selectedImageModelIndex];
                        SerializedProperty imageModelProp = serializedSettings.FindProperty("defaultImageModel");
                        imageModelProp.stringValue = selectedModel.id;
                        serializedSettings.ApplyModifiedProperties();
                        EditorUtility.SetDirty(settings);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(L10n.Get("config.models.image.label"), L10n.Get("config.models.none_available"));
                }

                // Refresh button
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L10n.Get("config.models.refresh"), GUILayout.Height(25), GUILayout.Width(120)))
                {
                    LoadModelsList();
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAuthenticationSection()
        {
            GUILayout.Label(L10n.Get("config.auth.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            bool isLoggedIn = !string.IsNullOrEmpty(PlayKitSettings.LocalDeveloperToken);

            if (isLoggedIn)
            {
                // Show logged in state
                EditorGUILayout.HelpBox(L10n.Get("config.auth.logged_in"), MessageType.Info);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(L10n.Get("config.auth.logout"), GUILayout.Height(30), GUILayout.Width(150)))
                {
                    if (EditorUtility.DisplayDialog(
                        L10n.Get("config.auth.logout_title"),
                        L10n.Get("config.auth.logout_confirm"),
                        L10n.Get("common.yes"),
                        L10n.Get("common.cancel")))
                    {
                        PlayKitSettings.ClearLocalDeveloperToken();
                        _gamesList.Clear();
                        _gamesDisplayNames = new string[0];
                        _selectedGameIndex = -1;
                        // Clear models list
                        _textModelsList.Clear();
                        _imageModelsList.Clear();
                        _textModelsDisplayNames = new string[0];
                        _imageModelsDisplayNames = new string[0];
                        _selectedTextModelIndex = -1;
                        _selectedImageModelIndex = -1;
                        _modelsLoadError = "";
                        settings.GameId = "";
                        Repaint();
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // Show login section
                EditorGUILayout.HelpBox(L10n.Get("config.auth.not_logged_in"), MessageType.Warning);

                // Show status in infobox (no popup dialogs)
                if (!string.IsNullOrEmpty(_deviceAuthStatus))
                {
                    EditorGUILayout.HelpBox(_deviceAuthStatus, _deviceAuthStatusType);
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                GUI.enabled = !_isDeviceAuthInProgress;

                if (GUILayout.Button(
                    _isDeviceAuthInProgress ? L10n.Get("dev.device_auth.authenticating") : L10n.Get("dev.device_auth.login"),
                    GUILayout.Height(35),
                    GUILayout.Width(220)))
                {
                    StartDeviceAuthFlow();
                }

                GUI.enabled = true;

                if (_isDeviceAuthInProgress)
                {
                    if (GUILayout.Button(L10n.Get("dev.device_auth.cancel"), GUILayout.Height(35), GUILayout.Width(80)))
                    {
                        CancelDeviceAuthFlow();
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGameSelectionSection()
        {
            GUILayout.Label(L10n.Get("config.game.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_isLoadingGames)
            {
                EditorGUILayout.HelpBox(L10n.Get("config.game.loading"), MessageType.Info);
            }
            else if (!string.IsNullOrEmpty(_gamesLoadError))
            {
                EditorGUILayout.HelpBox(_gamesLoadError, MessageType.Error);

                if (GUILayout.Button(L10n.Get("config.game.retry"), GUILayout.Height(25)))
                {
                    LoadGamesList();
                }
            }
            else if (_gamesList.Count == 0)
            {
                EditorGUILayout.HelpBox(L10n.Get("config.game.no_games"), MessageType.Warning);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🌐 Open Dashboard", GUILayout.Height(30)))
                {
                    Application.OpenURL("https://playkit.ai/dashboard");
                }
                if (GUILayout.Button(L10n.Get("config.game.refresh"), GUILayout.Height(30)))
                {
                    LoadGamesList();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // Find current selection
                if (_selectedGameIndex < 0 && !string.IsNullOrEmpty(settings.GameId))
                {
                    _selectedGameIndex = _gamesList.FindIndex(g => g.id == settings.GameId);
                }

                EditorGUI.BeginChangeCheck();
                _selectedGameIndex = EditorGUILayout.Popup(
                    L10n.Get("config.game.select"),
                    _selectedGameIndex,
                    _gamesDisplayNames
                );

                if (EditorGUI.EndChangeCheck() && _selectedGameIndex >= 0 && _selectedGameIndex < _gamesList.Count)
                {
                    var selectedGame = _gamesList[_selectedGameIndex];
                    settings.GameId = selectedGame.id;
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                    // Load models for the newly selected game
                    LoadModelsList();
                }

                // Show selected game info
                if (_selectedGameIndex >= 0 && _selectedGameIndex < _gamesList.Count)
                {
                    var game = _gamesList[_selectedGameIndex];
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField(L10n.Get("config.game.id_label"), game.id, EditorStyles.miniLabel);

                    if (!string.IsNullOrEmpty(game.description))
                    {
                        EditorGUILayout.LabelField(L10n.Get("config.game.description"), game.description, EditorStyles.wordWrappedMiniLabel);
                    }

                    if (game.is_suspended)
                    {
                        EditorGUILayout.HelpBox(L10n.Get("config.validation.suspended"), MessageType.Warning);
                    }
                }

                EditorGUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L10n.Get("config.game.refresh"), GUILayout.Height(25), GUILayout.Width(100)))
                {
                    LoadGamesList();
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawModelDefaults()
        {
            GUILayout.Label(L10n.Get("config.models.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.HelpBox(L10n.Get("config.models.info"), MessageType.Info);

            SerializedProperty chatModelProp = serializedSettings.FindProperty("defaultChatModel");
            EditorGUILayout.PropertyField(chatModelProp, new GUIContent(
                L10n.Get("config.models.chat.label"),
                L10n.Get("config.models.chat.tooltip")
            ));

            EditorGUILayout.Space(5);

            SerializedProperty imageModelProp = serializedSettings.FindProperty("defaultImageModel");
            EditorGUILayout.PropertyField(imageModelProp, new GUIContent(
                L10n.Get("config.models.image.label"),
                L10n.Get("config.models.image.tooltip")
            ));

            EditorGUILayout.EndVertical();
        }

        private void DrawDeveloperTools()
        {
            GUILayout.Label(L10n.Get("dev.tools.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            SerializedProperty ignoreProp = serializedSettings.FindProperty("ignoreDeveloperToken");
            EditorGUILayout.PropertyField(ignoreProp, new GUIContent(
                L10n.Get("dev.token.ignore"),
                L10n.Get("dev.token.ignore.tooltip")
            ));

            EditorGUILayout.Space(10);

            if (GUILayout.Button(L10n.Get("dev.player_token.clear"), GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog(
                    L10n.Get("dev.player_token.clear.title"),
                    L10n.Get("dev.player_token.clear.confirm"),
                    L10n.Get("common.yes"),
                    L10n.Get("common.cancel")))
                {
                    PlayKit_AuthManager.ClearPlayerToken();
                    EditorUtility.DisplayDialog(
                        L10n.Get("dev.player_token.clear.success.title"),
                        L10n.Get("dev.player_token.clear.success.message"),
                        L10n.Get("common.ok")
                    );
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAdvancedSettings()
        {
            // Collapsible foldout header
            _showAdvancedSettings = EditorGUILayout.Foldout(_showAdvancedSettings, L10n.Get("config.advanced.title"), true, EditorStyles.foldoutHeader);

            if (!_showAdvancedSettings) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.HelpBox(L10n.Get("config.advanced.info"), MessageType.Info);

            // Custom Base URL
            EditorGUILayout.Space(5);
            GUILayout.Label(L10n.Get("config.advanced.custom_url.label"), EditorStyles.boldLabel);

            SerializedProperty customUrlProp = serializedSettings.FindProperty("customBaseUrl");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(customUrlProp, new GUIContent(
                L10n.Get("config.advanced.custom_url.label"),
                L10n.Get("config.advanced.custom_url.tooltip")
            ));

            if (EditorGUI.EndChangeCheck())
            {
                // Explicitly apply and save changes when Base URL is modified
                serializedSettings.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.Space(5);

            string effectiveUrl = settings.BaseUrl;
            EditorGUILayout.LabelField(L10n.Get("config.advanced.effective_url"), effectiveUrl, EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region About Tab

        private void DrawAboutTab()
        {
            EditorGUILayout.Space(10);

            DrawVersionInfo();
            EditorGUILayout.Space(10);
            DrawQuickLinks();
            EditorGUILayout.Space(10);
            DrawResources();
        }

        private void DrawVersionInfo()
        {
            GUILayout.Label(L10n.Get("about.version.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField(L10n.Get("about.version.sdk"), global::PlayKit_SDK.PlayKit_SDK.VERSION);
            EditorGUILayout.LabelField(L10n.Get("about.version.unity"), Application.unityVersion);

            EditorGUILayout.Space(5);

            if (GUILayout.Button(L10n.Get("about.version.check_updates"), GUILayout.Height(30)))
            {
                PlayKit_UpdateChecker.CheckForUpdates(true);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawQuickLinks()
        {
            GUILayout.Label(L10n.Get("about.links.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(L10n.Get("about.links.documentation"), GUILayout.Height(30)))
            {
                Application.OpenURL("https://docs.playkit.dev");
            }
            if (GUILayout.Button(L10n.Get("about.links.examples"), GUILayout.Height(30)))
            {
                OpenExampleScenes();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(L10n.Get("about.links.report_issue"), GUILayout.Height(30)))
            {
                Application.OpenURL("https://github.com/playkit/unity-sdk/issues");
            }
            if (GUILayout.Button(L10n.Get("about.links.website"), GUILayout.Height(30)))
            {
                Application.OpenURL("https://playkit.dev");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawResources()
        {
            GUILayout.Label(L10n.Get("about.resources.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.HelpBox(L10n.Get("about.resources.email"), MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Device Auth

        private void AttachDeviceAuthHandlers()
        {
            if (_deviceAuthHandlersAttached || _deviceAuthFlow == null) return;

            _deviceAuthFlow.OnStatusUpdate += HandleDeviceAuthStatusUpdate;
            _deviceAuthFlow.OnSuccess += HandleDeviceAuthSuccess;
            _deviceAuthFlow.OnError += HandleDeviceAuthError;
            _deviceAuthFlow.OnCancelled += HandleDeviceAuthCancelled;

            _deviceAuthHandlersAttached = true;
        }

        private void DetachDeviceAuthHandlers()
        {
            if (!_deviceAuthHandlersAttached || _deviceAuthFlow == null) return;

            _deviceAuthFlow.OnStatusUpdate -= HandleDeviceAuthStatusUpdate;
            _deviceAuthFlow.OnSuccess -= HandleDeviceAuthSuccess;
            _deviceAuthFlow.OnError -= HandleDeviceAuthError;
            _deviceAuthFlow.OnCancelled -= HandleDeviceAuthCancelled;

            _deviceAuthHandlersAttached = false;
        }

        private void HandleDeviceAuthStatusUpdate(string status)
        {
            _deviceAuthStatus = status;
            _deviceAuthStatusType = MessageType.Info;
            Repaint();
        }

        private void HandleDeviceAuthSuccess(Editor.DeviceAuthResult result)
        {
            PlayKitSettings.LocalDeveloperToken = result.AccessToken;

            _deviceAuthStatus = L10n.Get("dev.device_auth.success");
            _deviceAuthStatusType = MessageType.Info;
            _isDeviceAuthInProgress = false;

            // Load games list after successful login
            LoadGamesList();

            // If a game is already selected, load models
            if (!string.IsNullOrEmpty(settings?.GameId))
            {
                LoadModelsList();
            }

            Repaint();

            // No popup - status is shown in UI
            Debug.Log("[PlayKit SDK] Device auth successful");
        }

        private void HandleDeviceAuthError(string error)
        {
            _deviceAuthStatus = error;
            _deviceAuthStatusType = MessageType.Error;
            _isDeviceAuthInProgress = false;
            Repaint();

            // No popup - error is shown in UI
            Debug.LogWarning($"[PlayKit SDK] Device auth error: {error}");
        }

        private void HandleDeviceAuthCancelled()
        {
            _deviceAuthStatus = L10n.Get("dev.device_auth.cancelled");
            _deviceAuthStatusType = MessageType.Warning;
            _isDeviceAuthInProgress = false;
            Repaint();
        }

        private async void StartDeviceAuthFlow()
        {
            if (_isDeviceAuthInProgress) return;

            _isDeviceAuthInProgress = true;
            _deviceAuthStatus = L10n.Get("dev.device_auth.starting");
            _deviceAuthStatusType = MessageType.Info;
            Repaint();

            try
            {
                // Create flow instance if needed, and attach handlers only once
                if (_deviceAuthFlow == null)
                {
                    _deviceAuthFlow = new DeviceAuthEditorFlow();
                    _deviceAuthHandlersAttached = false;
                }

                AttachDeviceAuthHandlers();

                // No gameId needed for global token
                await _deviceAuthFlow.StartFlowAsync("developer:full");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit SDK] Device auth error: {ex.Message}");
                _deviceAuthStatus = ex.Message;
                _deviceAuthStatusType = MessageType.Error;
                _isDeviceAuthInProgress = false;
                Repaint();
            }
        }

        private void CancelDeviceAuthFlow()
        {
            _deviceAuthFlow?.Cancel();
            _isDeviceAuthInProgress = false;
            _deviceAuthStatus = L10n.Get("dev.device_auth.cancelled");
            Repaint();
        }

        #endregion

        #region Games List

        private async void LoadGamesList()
        {
            _isLoadingGames = true;
            _gamesLoadError = "";
            Repaint();

            try
            {
                var token = PlayKitSettings.LocalDeveloperToken;
                if (string.IsNullOrEmpty(token))
                {
                    _gamesLoadError = "Not logged in";
                    _isLoadingGames = false;
                    Repaint();
                    return;
                }

                var baseUrl = PlayKitSettings.Instance?.BaseUrl ?? "https://playkit.ai";
                var endpoint = $"{baseUrl}/api/external/developer-games";

                using (var webRequest = UnityWebRequest.Get(endpoint))
                {
                    webRequest.SetRequestHeader("Authorization", $"Bearer {token}");

                    var operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Delay(100);
                    }

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        var response = JsonConvert.DeserializeObject<GamesListResponse>(webRequest.downloadHandler.text);

                        if (response != null && response.success && response.games != null)
                        {
                            _gamesList = response.games;
                            _gamesDisplayNames = _gamesList.Select(g => g.name ?? g.id).ToArray();

                            // Find current selection
                            if (!string.IsNullOrEmpty(settings.GameId))
                            {
                                _selectedGameIndex = _gamesList.FindIndex(g => g.id == settings.GameId);
                            }
                        }
                        else
                        {
                            _gamesLoadError = response?.error ?? "Failed to load games";
                        }
                    }
                    else
                    {
                        _gamesLoadError = $"API Error: {webRequest.error}";
                    }
                }
            }
            catch (Exception ex)
            {
                _gamesLoadError = ex.Message;
                Debug.LogError($"[PlayKit SDK] Failed to load games: {ex.Message}");
            }
            finally
            {
                _isLoadingGames = false;
                Repaint();
            }
        }

        private async void LoadModelsList()
        {
            if (string.IsNullOrEmpty(settings?.GameId))
            {
                _modelsLoadError = "";
                _textModelsList.Clear();
                _imageModelsList.Clear();
                _textModelsDisplayNames = new string[0];
                _imageModelsDisplayNames = new string[0];
                _selectedTextModelIndex = -1;
                _selectedImageModelIndex = -1;
                Repaint();
                return;
            }

            _isLoadingModels = true;
            _modelsLoadError = "";
            Repaint();

            try
            {
                var token = PlayKitSettings.LocalDeveloperToken;
                if (string.IsNullOrEmpty(token))
                {
                    _modelsLoadError = "Not logged in";
                    _isLoadingModels = false;
                    Repaint();
                    return;
                }

                var baseUrl = PlayKitSettings.Instance?.BaseUrl ?? "https://playkit.ai";
                var endpoint = $"{baseUrl}/ai/{settings.GameId}/models";

                using (var webRequest = UnityWebRequest.Get(endpoint))
                {
                    webRequest.SetRequestHeader("Authorization", $"Bearer {token}");

                    var operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Delay(100);
                    }

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        var response = JsonConvert.DeserializeObject<ModelsListResponse>(webRequest.downloadHandler.text);

                        if (response != null && response.models != null)
                        {
                            // Separate models by type
                            _textModelsList = response.models.Where(m => m.type == "text").ToList();
                            _imageModelsList = response.models.Where(m => m.type == "image").ToList();

                            // Build display names (show recommended tag)
                            _textModelsDisplayNames = _textModelsList.Select(m =>
                                m.is_recommended ? $"{m.name} (Recommended)" : m.name
                            ).ToArray();
                            _imageModelsDisplayNames = _imageModelsList.Select(m =>
                                m.is_recommended ? $"{m.name} (Recommended)" : m.name
                            ).ToArray();

                            // Find current selection for text model
                            if (!string.IsNullOrEmpty(settings.DefaultChatModel))
                            {
                                _selectedTextModelIndex = _textModelsList.FindIndex(m => m.id == settings.DefaultChatModel);
                            }

                            // Auto-select chat-model if no selection and it exists
                            if (_selectedTextModelIndex < 0)
                            {
                                int chatModelIndex = _textModelsList.FindIndex(m => m.id == "chat-model");
                                if (chatModelIndex >= 0)
                                {
                                    _selectedTextModelIndex = chatModelIndex;
                                    // Save to settings
                                    SerializedObject serializedSettings = new SerializedObject(settings);
                                    SerializedProperty chatModelProp = serializedSettings.FindProperty("defaultChatModel");
                                    chatModelProp.stringValue = "chat-model";
                                    serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                                }
                            }

                            // Find current selection for image model
                            if (!string.IsNullOrEmpty(settings.DefaultImageModel))
                            {
                                _selectedImageModelIndex = _imageModelsList.FindIndex(m => m.id == settings.DefaultImageModel);
                            }

                            // Auto-select image-model if no selection and it exists
                            if (_selectedImageModelIndex < 0)
                            {
                                int imageModelIndex = _imageModelsList.FindIndex(m => m.id == "image-model");
                                if (imageModelIndex >= 0)
                                {
                                    _selectedImageModelIndex = imageModelIndex;
                                    // Save to settings
                                    SerializedObject serializedSettings = new SerializedObject(settings);
                                    SerializedProperty imageModelProp = serializedSettings.FindProperty("defaultImageModel");
                                    imageModelProp.stringValue = "image-model";
                                    serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                                }
                            }
                        }
                        else if (response?.error != null)
                        {
                            _modelsLoadError = response.error.message ?? response.error.code;
                        }
                    }
                    else
                    {
                        // Try to parse error response
                        try
                        {
                            var errorResponse = JsonConvert.DeserializeObject<ModelsListResponse>(webRequest.downloadHandler.text);
                            if (errorResponse?.error != null)
                            {
                                _modelsLoadError = errorResponse.error.message ?? errorResponse.error.code;
                            }
                            else
                            {
                                _modelsLoadError = $"API Error: {webRequest.error}";
                            }
                        }
                        catch
                        {
                            _modelsLoadError = $"API Error: {webRequest.error}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _modelsLoadError = ex.Message;
                Debug.LogError($"[PlayKit SDK] Failed to load models: {ex.Message}");
            }
            finally
            {
                _isLoadingModels = false;
                Repaint();
            }
        }

        #endregion

        #region Helpers

        private void OpenExampleScenes()
        {
            string examplePath = "Assets/PlayKit_SDK/Example";
            UnityEngine.Object exampleFolder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(examplePath);
            if (exampleFolder != null)
            {
                EditorGUIUtility.PingObject(exampleFolder);
                Selection.activeObject = exampleFolder;
            }
            else
            {
                EditorUtility.DisplayDialog(
                    L10n.Get("about.examples.title"),
                    L10n.Get("about.examples.not_found"),
                    L10n.Get("common.ok")
                );
            }
        }

        #endregion
    }
}
