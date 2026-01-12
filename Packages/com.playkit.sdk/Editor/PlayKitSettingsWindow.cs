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
            Addons,
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
        private List<ModelInfo> _transcriptionModelsList = new List<ModelInfo>();
        private List<ModelInfo> _3dModelsList = new List<ModelInfo>();
        private string[] _textModelsDisplayNames = new string[0];
        private string[] _imageModelsDisplayNames = new string[0];
        private string[] _transcriptionModelsDisplayNames = new string[0];
        private string[] _3dModelsDisplayNames = new string[0];
        private int _selectedTextModelIndex = -1;
        private int _selectedImageModelIndex = -1;
        private int _selectedTranscriptionModelIndex = -1;
        private int _selected3DModelIndex = -1;
        private bool _isLoadingModels = false;
        private string _modelsLoadError = "";

        // Addons state
        private const string STEAM_ADDON_PACKAGE_NAME = "com.playkit.sdk.steam";
        private const string STEAM_ADDON_GIT_URL = "https://github.com/playkit-ai/playkit-unity-steam.git";
        private const string STEAM_ADDON_DOCS_URL = "https://docs.playkit.ai/unity/addons/steam";
        private bool _isSteamAddonInstalled = false;
        private bool _isSteamAddonInstalling = false;
        private UnityEditor.PackageManager.Requests.ListRequest _packageListRequest;
        private UnityEditor.PackageManager.Requests.AddRequest _packageAddRequest;
        private UnityEditor.PackageManager.Requests.RemoveRequest _packageRemoveRequest;

        [System.Serializable]
        private class GameInfo
        {
            public string id;
            public string name;
            public string description;
            public bool is_suspended;
            public string channel; // Distribution channel (standalone, steam_release, steam_demo, etc.)
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
            // Check addon installation status
            CheckSteamAddonInstalled();
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
                case Tab.Addons:
                    DrawAddonsTab();
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

            if (GUILayout.Toggle(currentTab == Tab.Addons, L10n.Get("tab.addons"), tabStyle))
            {
                currentTab = Tab.Addons;
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

            // Recharge Configuration
            DrawRechargeConfiguration();

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

                EditorGUILayout.Space(5);

                // Transcription Model Dropdown
                if (_transcriptionModelsList.Count > 0)
                {
                    EditorGUI.BeginChangeCheck();
                    _selectedTranscriptionModelIndex = EditorGUILayout.Popup(
                        new GUIContent(L10n.Get("config.models.transcription.label"), L10n.Get("config.models.transcription.tooltip")),
                        _selectedTranscriptionModelIndex,
                        _transcriptionModelsDisplayNames
                    );

                    if (EditorGUI.EndChangeCheck() && _selectedTranscriptionModelIndex >= 0 && _selectedTranscriptionModelIndex < _transcriptionModelsList.Count)
                    {
                        var selectedModel = _transcriptionModelsList[_selectedTranscriptionModelIndex];
                        SerializedProperty transcriptionModelProp = serializedSettings.FindProperty("defaultTranscriptionModel");
                        transcriptionModelProp.stringValue = selectedModel.id;
                        serializedSettings.ApplyModifiedProperties();
                        EditorUtility.SetDirty(settings);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(L10n.Get("config.models.transcription.label"), L10n.Get("config.models.none_available"));
                }

                EditorGUILayout.Space(5);

                // 3D Model Dropdown
                if (_3dModelsList.Count > 0)
                {
                    EditorGUI.BeginChangeCheck();
                    _selected3DModelIndex = EditorGUILayout.Popup(
                        new GUIContent(L10n.Get("config.models.3d.label"), L10n.Get("config.models.3d.tooltip")),
                        _selected3DModelIndex,
                        _3dModelsDisplayNames
                    );

                    if (EditorGUI.EndChangeCheck() && _selected3DModelIndex >= 0 && _selected3DModelIndex < _3dModelsList.Count)
                    {
                        var selectedModel = _3dModelsList[_selected3DModelIndex];
                        SerializedProperty model3dProp = serializedSettings.FindProperty("default3DModel");
                        model3dProp.stringValue = selectedModel.id;
                        serializedSettings.ApplyModifiedProperties();
                        EditorUtility.SetDirty(settings);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(L10n.Get("config.models.3d.label"), L10n.Get("config.models.none_available"));
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
                        _transcriptionModelsList.Clear();
                        _3dModelsList.Clear();
                        _textModelsDisplayNames = new string[0];
                        _imageModelsDisplayNames = new string[0];
                        _transcriptionModelsDisplayNames = new string[0];
                        _3dModelsDisplayNames = new string[0];
                        _selectedTextModelIndex = -1;
                        _selectedImageModelIndex = -1;
                        _selectedTranscriptionModelIndex = -1;
                        _selected3DModelIndex = -1;
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

                    // Auto-sync channel type from selected game
                    SerializedProperty channelTypeProp = serializedSettings.FindProperty("channelType");
                    if (channelTypeProp != null && !string.IsNullOrEmpty(selectedGame.channel))
                    {
                        channelTypeProp.stringValue = selectedGame.channel;
                        serializedSettings.ApplyModifiedProperties();

                        // Notify addons about game selection change
                        NotifyAddonsGameSelectionChanged(selectedGame.id, selectedGame.channel);
                    }

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

                    // Display channel type
                    if (!string.IsNullOrEmpty(game.channel))
                    {
                        string channelDisplay = game.channel;
                        // Format channel name for better readability
                        if (game.channel == "standalone") channelDisplay = "Standalone";
                        else if (game.channel.StartsWith("steam")) channelDisplay = "Steam (" + game.channel.Replace("steam_", "") + ")";
                        else channelDisplay = char.ToUpper(game.channel[0]) + game.channel.Substring(1);

                        EditorGUILayout.LabelField("Channel", channelDisplay, EditorStyles.miniLabel);
                    }

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

        private void DrawRechargeConfiguration()
        {
            GUILayout.Label(L10n.Get("config.recharge.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Enable Default Recharge Handler
            SerializedProperty enableHandlerProp = serializedSettings.FindProperty("enableDefaultRechargeHandler");
            EditorGUILayout.PropertyField(enableHandlerProp, new GUIContent(
                L10n.Get("config.recharge.enable_default_handler"),
                L10n.Get("config.recharge.enable_default_handler.tooltip")
            ));

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

            EditorGUILayout.Space(10);

            // Build Token Injection (DANGER ZONE)
            DrawBuildTokenInjection();

            EditorGUILayout.EndVertical();
        }

        private void DrawBuildTokenInjection()
        {
            GUILayout.Label("Build Token Injection", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

          

            EditorGUILayout.Space(5);

            // Toggle
            SerializedProperty forceBuildTokenProp = serializedSettings.FindProperty("forceDeveloperTokenInBuild");

            EditorGUI.BeginChangeCheck();
            bool currentValue = forceBuildTokenProp.boolValue;

            // Style for the toggle (red when enabled)
            GUIStyle toggleStyle = new GUIStyle(EditorStyles.toggle);
            if (currentValue)
            {
                toggleStyle.normal.textColor = Color.red;
                toggleStyle.onNormal.textColor = Color.red;
            }

            bool newValue = EditorGUILayout.Toggle(
                new GUIContent(
                    "Force Developer Token Injection At Build",
                    "Embeds developer token from EditorPrefs into the build. Token will be in the build binary."
                ),
                currentValue,
                toggleStyle
            );

            if (EditorGUI.EndChangeCheck())
            {
                if (newValue && !currentValue)
                {
                    // Enabling - show confirmation
                    bool confirm = EditorUtility.DisplayDialog(
                        "⚠️ Enable Token Injection?",
                        "This will embed your developer token in ALL future builds until disabled.\n\n" +
                        "Your token will be extractable from the build binary.\n\n" +
                        "Only use this for internal testing. Never distribute these builds publicly.\n\n" +
                        "Enable anyway?",
                        "Yes, Enable",
                        "Cancel"
                    );

                    if (confirm)
                    {
                        forceBuildTokenProp.boolValue = true;
                    }
                }
                else
                {
                    forceBuildTokenProp.boolValue = newValue;
                }

                serializedSettings.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            // Show current status
            if (forceBuildTokenProp.boolValue)
            {
                EditorGUILayout.Space(5);

                GUIStyle warningStyle = new GUIStyle(EditorStyles.label);
                warningStyle.normal.textColor = Color.red;
                warningStyle.fontStyle = FontStyle.Bold;

                EditorGUILayout.LabelField("⚠️ TOKEN INJECTION ENABLED", warningStyle);

                string currentToken = PlayKitSettings.LocalDeveloperToken;
                if (!string.IsNullOrEmpty(currentToken))
                {
                    string maskedToken = currentToken.Length > 10
                        ? "***" + currentToken.Substring(currentToken.Length - 10)
                        : "***" + currentToken;
                    EditorGUILayout.LabelField("Token to inject:", maskedToken, EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "No developer token found in EditorPrefs. Build will fail if you attempt to build.",
                        MessageType.Warning
                    );
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSteamAddonRow()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Title row
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(L10n.Get("addons.steam.title"), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            // Status badge
            GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel);
            if (_isSteamAddonInstalled)
            {
                statusStyle.normal.textColor = new Color(0.2f, 0.7f, 0.2f);
                GUILayout.Label(L10n.Get("addons.steam.status.installed"), statusStyle);
            }
            else
            {
                statusStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                GUILayout.Label(L10n.Get("addons.steam.status.not_installed"), statusStyle);
            }
            EditorGUILayout.EndHorizontal();

            // Description
            EditorGUILayout.LabelField(L10n.Get("addons.steam.description"), EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(5);

            // Action buttons
            EditorGUILayout.BeginHorizontal();

            if (_isSteamAddonInstalled)
            {
                // Documentation button
                if (GUILayout.Button(L10n.Get("addons.steam.open_docs"), GUILayout.Height(25), GUILayout.Width(120)))
                {
                    Application.OpenURL(STEAM_ADDON_DOCS_URL);
                }

                GUILayout.FlexibleSpace();

                // Remove button
                if (GUILayout.Button(L10n.Get("addons.steam.remove"), GUILayout.Height(25), GUILayout.Width(80)))
                {
                    if (EditorUtility.DisplayDialog(
                        L10n.Get("addons.steam.remove_confirm.title"),
                        L10n.Get("addons.steam.remove_confirm.message"),
                        L10n.Get("common.yes"),
                        L10n.Get("common.cancel")))
                    {
                        RemoveSteamAddon();
                    }
                }
            }
            else
            {
                GUI.enabled = !_isSteamAddonInstalling;

                if (GUILayout.Button(
                    _isSteamAddonInstalling ? L10n.Get("addons.steam.installing") : L10n.Get("addons.steam.install"),
                    GUILayout.Height(30)))
                {
                    InstallSteamAddon();
                }

                GUI.enabled = true;

                GUILayout.FlexibleSpace();

                // Documentation button
                if (GUILayout.Button(L10n.Get("addons.steam.open_docs"), GUILayout.Height(30), GUILayout.Width(120)))
                {
                    Application.OpenURL(STEAM_ADDON_DOCS_URL);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void CheckSteamAddonInstalled()
        {
            // Check if Steam Addon types exist (works for both UPM and local installations)
            var steamAuthType = System.Type.GetType("PlayKit_SDK.Steam.PlayKit_SteamAuthManager, PlayKit.Steam");
            _isSteamAddonInstalled = steamAuthType != null;

            // Also check UPM packages as fallback
            if (!_isSteamAddonInstalled)
            {
                _packageListRequest = UnityEditor.PackageManager.Client.List(true);
                EditorApplication.update += OnPackageListProgress;
            }
        }

        private void OnPackageListProgress()
        {
            if (_packageListRequest == null || !_packageListRequest.IsCompleted)
                return;

            EditorApplication.update -= OnPackageListProgress;

            if (_packageListRequest.Status == UnityEditor.PackageManager.StatusCode.Success)
            {
                foreach (var package in _packageListRequest.Result)
                {
                    if (package.name == STEAM_ADDON_PACKAGE_NAME)
                    {
                        _isSteamAddonInstalled = true;
                        break;
                    }
                }
                Repaint();
            }

            _packageListRequest = null;
        }

        private void InstallSteamAddon()
        {
            _isSteamAddonInstalling = true;
            _packageAddRequest = UnityEditor.PackageManager.Client.Add(STEAM_ADDON_GIT_URL);
            EditorApplication.update += OnPackageAddProgress;
        }

        private void OnPackageAddProgress()
        {
            if (_packageAddRequest == null || !_packageAddRequest.IsCompleted)
                return;

            EditorApplication.update -= OnPackageAddProgress;

            _isSteamAddonInstalling = false;

            if (_packageAddRequest.Status == UnityEditor.PackageManager.StatusCode.Success)
            {
                _isSteamAddonInstalled = true;
                EditorUtility.DisplayDialog(
                    L10n.Get("addons.steam.install_success.title"),
                    L10n.Get("addons.steam.install_success.message"),
                    L10n.Get("common.ok")
                );
            }
            else
            {
                EditorUtility.DisplayDialog(
                    L10n.Get("addons.steam.install_error.title"),
                    L10n.GetFormat("addons.steam.install_error.message", _packageAddRequest.Error?.message ?? "Unknown error"),
                    L10n.Get("common.ok")
                );
            }

            _packageAddRequest = null;
            Repaint();
        }

        private void RemoveSteamAddon()
        {
            _packageRemoveRequest = UnityEditor.PackageManager.Client.Remove(STEAM_ADDON_PACKAGE_NAME);
            EditorApplication.update += OnPackageRemoveProgress;
        }

        private void OnPackageRemoveProgress()
        {
            if (_packageRemoveRequest == null || !_packageRemoveRequest.IsCompleted)
                return;

            EditorApplication.update -= OnPackageRemoveProgress;

            if (_packageRemoveRequest.Status == UnityEditor.PackageManager.StatusCode.Success)
            {
                _isSteamAddonInstalled = false;
            }

            _packageRemoveRequest = null;
            Repaint();
        }

        #endregion

        #region Addons Tab

        private void DrawAddonsTab()
        {
            EditorGUILayout.Space(10);

            GUILayout.Label(L10n.Get("addons.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Get all registered addons
            var allAddons = AddonRegistry.Instance.GetAllAddons();
            string currentChannelType = settings?.ChannelType ?? "standalone";

            if (allAddons.Count == 0)
            {
                EditorGUILayout.HelpBox(L10n.Get("addons.none_registered"), MessageType.Warning);
            }
            else
            {
                foreach (var kvp in allAddons)
                {
                    DrawAddonCard(kvp.Value, currentChannelType);
                    EditorGUILayout.Space(5);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAddonCard(IPlayKitAddon addon, string currentChannelType)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Title row with toggle
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            bool isEnabled = settings.EnabledAddons.GetValueOrDefault(addon.AddonId, false);
            bool newValue = EditorGUILayout.Toggle(isEnabled, GUILayout.Width(20));

            GUILayout.Label(addon.DisplayName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            // Status badges
            GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel);
            if (addon.IsInstalled)
            {
                statusStyle.normal.textColor = new Color(0.2f, 0.7f, 0.2f);
                GUILayout.Label($"v{addon.Version}", statusStyle);
            }
            else
            {
                statusStyle.normal.textColor = new Color(0.8f, 0.4f, 0.2f);
                GUILayout.Label(L10n.Get("addons.not_installed"), statusStyle);
            }

            EditorGUILayout.EndHorizontal();

            // Description
            EditorGUILayout.LabelField(addon.Description, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(3);

            // Exclusion group info
            if (!string.IsNullOrEmpty(addon.ExclusionGroup))
            {
                EditorGUILayout.LabelField(
                    L10n.Get("addons.exclusion_group"),
                    addon.ExclusionGroup,
                    EditorStyles.miniLabel
                );
            }

            // Required channel types
            if (addon.RequiredChannelTypes != null && addon.RequiredChannelTypes.Length > 0)
            {
                EditorGUILayout.LabelField(
                    L10n.Get("addons.required_channels"),
                    string.Join(", ", addon.RequiredChannelTypes),
                    EditorStyles.miniLabel
                );
            }

            // Channel mismatch warning
            if (isEnabled && addon.RequiredChannelTypes != null && addon.RequiredChannelTypes.Length > 0)
            {
                bool channelMatches = AddonRegistry.CheckChannelMatch(currentChannelType, addon.RequiredChannelTypes);
                if (!channelMatches)
                {
                    EditorGUILayout.Space(3);
                    EditorGUILayout.HelpBox(
                        L10n.GetFormat("config.validation.addon_channel_mismatch",
                            addon.DisplayName,
                            string.Join(", ", addon.RequiredChannelTypes),
                            currentChannelType),
                        MessageType.Warning
                    );
                }
            }

            // Handle toggle change with conflict resolution
            if (EditorGUI.EndChangeCheck())
            {
                HandleAddonToggle(addon, newValue);
            }

            // Allow addon to draw custom settings UI
            if (addon is IPlayKitAddonEditor addonEditor && addon.IsInstalled)
            {
                addonEditor.DrawAddonSettings(currentChannelType);
            }

            EditorGUILayout.EndVertical();
        }

        private void HandleAddonToggle(IPlayKitAddon addon, bool newValue)
        {
            if (newValue && !string.IsNullOrEmpty(addon.ExclusionGroup))
            {
                // Check for conflicts
                var conflicts = AddonRegistry.Instance.GetConflictingAddons(
                    addon.AddonId,
                    settings.EnabledAddons.ToDictionary()
                );

                // Auto-disable conflicting addons
                foreach (var conflictId in conflicts)
                {
                    settings.SetAddonEnabled(conflictId, false);
                    var conflictAddon = AddonRegistry.Instance.GetAllAddons().GetValueOrDefault(conflictId);
                    if (conflictAddon != null)
                    {
                        Debug.LogWarning(L10n.GetFormat("addons.conflict_disabled",
                            conflictAddon.DisplayName,
                            addon.DisplayName,
                            addon.ExclusionGroup));
                    }
                }
            }

            // Set the new value
            settings.SetAddonEnabled(addon.AddonId, newValue);
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
                Application.OpenURL("https://docs.playkit.ai");
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
                Application.OpenURL("https://playkit.ai");
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
                            _gamesDisplayNames = _gamesList.Select(g => FormatGameDisplayName(g)).ToArray();

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
                            _transcriptionModelsList = response.models.Where(m => m.type == "transcription").ToList();
                            _3dModelsList = response.models.Where(m => m.type == "3d").ToList();

                            // Build display names (show recommended tag)
                            _textModelsDisplayNames = _textModelsList.Select(m =>
                                m.is_recommended ? $"{m.name} (Recommended)" : m.name
                            ).ToArray();
                            _imageModelsDisplayNames = _imageModelsList.Select(m =>
                                m.is_recommended ? $"{m.name} (Recommended)" : m.name
                            ).ToArray();
                            _transcriptionModelsDisplayNames = _transcriptionModelsList.Select(m =>
                                m.is_recommended ? $"{m.name} (Recommended)" : m.name
                            ).ToArray();
                            _3dModelsDisplayNames = _3dModelsList.Select(m =>
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

                            // Find current selection for transcription model
                            if (!string.IsNullOrEmpty(settings.DefaultTranscriptionModel))
                            {
                                _selectedTranscriptionModelIndex = _transcriptionModelsList.FindIndex(m => m.id == settings.DefaultTranscriptionModel);
                            }

                            // Auto-select default-transcription-model if no selection and it exists
                            if (_selectedTranscriptionModelIndex < 0)
                            {
                                int transcriptionModelIndex = _transcriptionModelsList.FindIndex(m => m.id == "default-transcription-model");
                                if (transcriptionModelIndex >= 0)
                                {
                                    _selectedTranscriptionModelIndex = transcriptionModelIndex;
                                    // Save to settings
                                    SerializedObject serializedSettings = new SerializedObject(settings);
                                    SerializedProperty transcriptionModelProp = serializedSettings.FindProperty("defaultTranscriptionModel");
                                    transcriptionModelProp.stringValue = "default-transcription-model";
                                    serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                                }
                            }

                            // Find current selection for 3D model
                            if (!string.IsNullOrEmpty(settings.Default3DModel))
                            {
                                _selected3DModelIndex = _3dModelsList.FindIndex(m => m.id == settings.Default3DModel);
                            }

                            // Auto-select default-3d-model if no selection and it exists
                            if (_selected3DModelIndex < 0)
                            {
                                int model3dIndex = _3dModelsList.FindIndex(m => m.id == "default-3d-model");
                                if (model3dIndex >= 0)
                                {
                                    _selected3DModelIndex = model3dIndex;
                                    // Save to settings
                                    SerializedObject serializedSettings = new SerializedObject(settings);
                                    SerializedProperty model3dProp = serializedSettings.FindProperty("default3DModel");
                                    model3dProp.stringValue = "default-3d-model";
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

        /// <summary>
        /// Notifies all addons that implement IPlayKitAddonEditor about game selection changes.
        /// </summary>
        private void NotifyAddonsGameSelectionChanged(string gameId, string channelType)
        {
            var allAddons = AddonRegistry.Instance.GetAllAddons();
            foreach (var kvp in allAddons)
            {
                if (kvp.Value is IPlayKitAddonEditor addonEditor)
                {
                    addonEditor.OnGameSelectionChanged(gameId, channelType);
                }
            }
        }

        #endregion

        #region Helpers

        private string FormatGameDisplayName(GameInfo game)
        {
            string channelBadge = GetChannelBadge(game.channel);
            string gameName = game.name ?? game.id;

            if (!string.IsNullOrEmpty(channelBadge))
            {
                return $"{gameName} [{channelBadge}]";
            }

            return gameName;
        }

        private string GetChannelBadge(string channel)
        {
            if (string.IsNullOrEmpty(channel))
                return "";

            switch (channel.ToLower())
            {
                case "standalone":
                    return "Standalone";
                case "steam_release":
                    return "Steam";
                case "steam_demo":
                    return "Steam Demo";
                case "steam_playtest":
                    return "Steam Playtest";
                case "ios":
                    return "iOS";
                case "android":
                    return "Android";
                case "xbox":
                    return "Xbox";
                case "playstation":
                    return "PlayStation";
                case "nintendo":
                    return "Nintendo";
                case "epic":
                    return "Epic";
                default:
                    return channel;
            }
        }

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
