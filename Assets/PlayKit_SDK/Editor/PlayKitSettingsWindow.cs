using PlayKit_SDK.Editor;
using UnityEngine;
using UnityEditor;
using System.Linq;
using PlayKit.SDK.Editor;
using L10n = PlayKit.SDK.Editor.L10n;

namespace Developerworks.SDK
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
            Configuration,  // 配置
            About          // 关于
        }
        private Tab currentTab = Tab.Configuration;

        // Developer token visibility toggle
        private bool showDeveloperToken = false;

        // Auto validation state
        private string lastValidatedGameId = "";
        private string lastValidatedToken = "";
        private bool isValidating = false;
        private ValidationResult validationResult = null;

        [System.Serializable]
        private class ValidationResult
        {
            public bool success;
            public bool tokenValid;
            public string tokenError;
            public GameInfo game;
            public TokenInfo token;
            public string error;
            public bool tokenWasProvided; // Track if a token was provided for validation
        }

        [System.Serializable]
        private class GameInfo
        {
            public string id;
            public string name;
            public string description;
            public bool is_suspended;
            public bool is_hosted;
            public bool enable_steam_auth;
            public string steam_app_id;
        }

        [System.Serializable]
        private class TokenInfo
        {
            public string id;
            public string name;
            public string created_at;
        }

        [MenuItem("PlayKit SDK/Settings", priority = 0)]
        public static void ShowWindow()
        {
            PlayKitSettingsWindow window = GetWindow<PlayKitSettingsWindow>(L10n.Get("window.title"));
            
            window.minSize = new Vector2(500, 550);
            window.Show();
        }

        private void OnEnable()
        {
            LoadSettings();
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

            // Update serialized object at the start of OnGUI
            serializedSettings.Update();

            // Header with logo and title
            DrawHeader();

            EditorGUILayout.Space(5);

            // Tab navigation
            DrawTabNavigation();

            EditorGUILayout.Space(5);

            // Content area with scroll
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

            // Apply changes at the end of OnGUI
            if (serializedSettings.hasModifiedProperties)
            {
                serializedSettings.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Title and language selector row
            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            GUILayout.Label(L10n.Get("header.title"), new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            });

            GUILayout.FlexibleSpace();

            // Language selector on the right
            EditorGUI.BeginChangeCheck();
            string currentLang = EditorLocalization.GetCurrentLanguage();
            int currentIndex = System.Array.IndexOf(EditorLocalization.SupportedLanguages.Keys.ToArray(), currentLang);
            if (currentIndex < 0) currentIndex = 0;

            string[] languageNames = EditorLocalization.SupportedLanguages.Values.ToArray();
            int newIndex = EditorGUILayout.Popup(currentIndex, languageNames, GUILayout.Width(100));

            if (EditorGUI.EndChangeCheck())
            {
                string newLang = EditorLocalization.SupportedLanguages.Keys.ToArray()[newIndex];
                EditorLocalization.SetLanguage(newLang);
                Repaint(); // Refresh UI immediately
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Label(L10n.Get("header.subtitle"), new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic
            });

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

            // Game Configuration
            DrawGameConfiguration();

            EditorGUILayout.Space(10);

            // Developer Token Configuration
            DrawDeveloperTokenConfiguration();

            EditorGUILayout.Space(10);

            // Validation Status
            DrawValidationStatus();

            EditorGUILayout.Space(10);

            // AI Model Defaults
            DrawModelDefaults();

            EditorGUILayout.Space(10);

            // Developer Tools
            DrawDeveloperTools();
        }

        private void DrawGameConfiguration()
        {
            GUILayout.Label(L10n.Get("config.game.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Game ID
            SerializedProperty gameIdProp = serializedSettings.FindProperty("gameId");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(gameIdProp, new GUIContent(
                L10n.Get("config.game.id.label"),
                L10n.Get("config.game.id.tooltip")
            ));

            // Auto-validate when Game ID changes
            if (EditorGUI.EndChangeCheck() && !string.IsNullOrWhiteSpace(gameIdProp.stringValue))
            {
                ValidateConfiguration();
            }

            if (string.IsNullOrWhiteSpace(gameIdProp.stringValue))
            {
                EditorGUILayout.HelpBox(L10n.Get("config.game.id.required"), MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDeveloperTokenConfiguration()
        {
            GUILayout.Label(L10n.Get("dev.token.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Storage Mode Toggle
            EditorGUILayout.LabelField(L10n.Get("dev.storage.title"), EditorStyles.miniBoldLabel);

            SerializedProperty useLocalProp = serializedSettings.FindProperty("useLocalDeveloperToken");
            EditorGUILayout.PropertyField(useLocalProp, new GUIContent(
                L10n.Get("dev.storage.use_local"),
                L10n.Get("dev.storage.use_local.tooltip")
            ));

            EditorGUILayout.Space(5);

            // Display appropriate help message based on storage mode
            if (useLocalProp.boolValue)
            {
                EditorGUILayout.HelpBox(L10n.Get("dev.storage.local_mode"), MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(L10n.Get("dev.storage.project_mode"), MessageType.Info);
            }

            EditorGUILayout.Space(8);

            // Developer Token Input
            if (useLocalProp.boolValue)
            {
                // Local storage mode - use EditorPrefs
                string localToken = PlayKitSettings.LocalDeveloperToken;

                EditorGUI.BeginChangeCheck();
                if (showDeveloperToken)
                {
                    string newToken = EditorGUILayout.TextField(L10n.Get("dev.token.label"), localToken);
                    if (newToken != localToken)
                    {
                        PlayKitSettings.LocalDeveloperToken = newToken;
                    }
                }

                // Auto-validate when token changes (including when cleared)
                if (EditorGUI.EndChangeCheck())
                {
                    ValidateConfiguration();
                }
                else
                {
                    string maskedToken = string.IsNullOrEmpty(localToken) ?
                        L10n.Get("dev.token.not_set") : new string('●', 20);
                    EditorGUILayout.LabelField(L10n.Get("dev.token.label"), maskedToken);
                }
            }
            else
            {
                // Project storage mode - use ScriptableObject
                SerializedProperty tokenProp = serializedSettings.FindProperty("developerToken");

                EditorGUI.BeginChangeCheck();
                if (showDeveloperToken)
                {
                    EditorGUILayout.PropertyField(tokenProp, new GUIContent(L10n.Get("dev.token.label")));
                }
                else
                {
                    string maskedToken = string.IsNullOrEmpty(tokenProp.stringValue) ?
                        L10n.Get("dev.token.not_set") : new string('●', 20);
                    EditorGUILayout.LabelField(L10n.Get("dev.token.label"), maskedToken);
                }

                // Auto-validate when token changes (including when cleared)
                if (EditorGUI.EndChangeCheck())
                {
                    ValidateConfiguration();
                }
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(
                showDeveloperToken ? L10n.Get("dev.token.hide") : L10n.Get("dev.token.show"),
                GUILayout.Height(25),
                GUILayout.Width(200)))
            {
                showDeveloperToken = !showDeveloperToken;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawValidationStatus()
        {
            GUILayout.Label(L10n.Get("config.validation.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (isValidating)
            {
                EditorGUILayout.HelpBox(L10n.Get("config.validation.validating"), MessageType.Info);
            }
            else if (validationResult != null)
            {
                DrawValidationResult();
            }
            else if (!string.IsNullOrWhiteSpace(settings.GameId))
            {
                EditorGUILayout.HelpBox(L10n.Get("config.validation.changed"), MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(L10n.Get("config.validation.need_gameid"), MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawValidationResult()
        {
            if (!validationResult.success)
            {
                // Game not found or API error
                EditorGUILayout.HelpBox(
                    $"{L10n.Get("config.validation.failed")}\n\n{validationResult.error}",
                    MessageType.Error
                );
                return;
            }

            // Game found
            if (validationResult.game != null)
            {
                string gameName = validationResult.game.name ?? "Unknown";
                string gameDesc = validationResult.game.description ?? "";

                EditorGUILayout.LabelField(L10n.Get("config.validation.game_info"), EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(L10n.Get("config.validation.name"), gameName);
                if (!string.IsNullOrEmpty(gameDesc))
                {
                    EditorGUILayout.LabelField(L10n.Get("config.validation.description"), gameDesc, EditorStyles.wordWrappedLabel);
                }

                // Game status warnings
                if (validationResult.game.is_suspended)
                {
                    EditorGUILayout.HelpBox(L10n.Get("config.validation.suspended"), MessageType.Warning);
                }

                EditorGUILayout.Space(5);
            }

            // Token validation - only show warnings if a token was actually provided
            if (validationResult.tokenWasProvided)
            {
                // User provided a token, show validation results
                if (validationResult.tokenValid && validationResult.token != null)
                {
                    EditorGUILayout.HelpBox(
                        L10n.GetFormat("config.validation.token_valid", validationResult.token.name, validationResult.token.created_at),
                        MessageType.Info
                    );
                }
                else if (!string.IsNullOrEmpty(validationResult.tokenError))
                {
                    EditorGUILayout.HelpBox(
                        L10n.GetFormat("config.validation.token_invalid", validationResult.tokenError),
                        MessageType.Warning
                    );
                }
                else
                {
                    EditorGUILayout.HelpBox(L10n.Get("config.validation.token_error"), MessageType.Warning);
                }
            }
            else
            {
                // No token provided - this is fine for production use
                EditorGUILayout.HelpBox(L10n.Get("config.validation.no_token"), MessageType.Info);
            }
        }

        private void DrawModelDefaults()
        {
            GUILayout.Label(L10n.Get("config.models.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.HelpBox(L10n.Get("config.models.info"), MessageType.Info);

            // Default Chat Model
            SerializedProperty chatModelProp = serializedSettings.FindProperty("defaultChatModel");
            EditorGUILayout.PropertyField(chatModelProp, new GUIContent(
                L10n.Get("config.models.chat.label"),
                L10n.Get("config.models.chat.tooltip")
            ));

            EditorGUILayout.Space(5);

            // Default Image Model
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

            // Ignore Developer Token option
            SerializedProperty ignoreProp = serializedSettings.FindProperty("ignoreDeveloperToken");
            EditorGUILayout.PropertyField(ignoreProp, new GUIContent(
                L10n.Get("dev.token.ignore"),
                L10n.Get("dev.token.ignore.tooltip")
            ));

            EditorGUILayout.Space(10);

            // Clear Player Token Button
            if (GUILayout.Button(L10n.Get("dev.player_token.clear"), GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog(
                    L10n.Get("dev.player_token.clear.title"),
                    L10n.Get("dev.player_token.clear.confirm"),
                    L10n.Get("common.yes"),
                    L10n.Get("common.cancel")))
                {
                    PlayKit_SDK.Auth.PlayKit_AuthManager.ClearPlayerToken();
                    EditorUtility.DisplayDialog(
                        L10n.Get("dev.player_token.clear.success.title"),
                        L10n.Get("dev.player_token.clear.success.message"),
                        L10n.Get("common.ok")
                    );
                }
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region About Tab

        private void DrawAboutTab()
        {
            EditorGUILayout.Space(10);

            // Version Info
            DrawVersionInfo();

            EditorGUILayout.Space(10);

            // Quick Links
            DrawQuickLinks();

            EditorGUILayout.Space(10);

            // Resources
            DrawResources();
        }

        private void DrawVersionInfo()
        {
            GUILayout.Label(L10n.Get("about.version.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField(L10n.Get("about.version.sdk"), PlayKit_SDK.PlayKit_SDK.VERSION);
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

            // if (GUILayout.Button("加入 Discord 社区 Join Discord Community", GUILayout.Height(30)))
            // {
            //     Application.OpenURL("https://discord.gg/playkit");
            // }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Helper Methods

        private async void ValidateConfiguration()
        {
            // Apply any pending changes BEFORE reading values
            if (serializedSettings.hasModifiedProperties)
            {
                serializedSettings.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            string currentGameId = settings.GameId;
            string currentToken = settings.DeveloperToken;

            // Skip if already validating same configuration
            if (isValidating ||
                (currentGameId == lastValidatedGameId && currentToken == lastValidatedToken))
            {
                return;
            }

            isValidating = true;
            validationResult = null;
            Repaint();

            try
            {
                string apiUrl = $"https://playkit.agentlandlab.com/api/external/validate-editor-config?gameId={UnityEngine.Networking.UnityWebRequest.EscapeURL(currentGameId)}";
                bool tokenProvided = !string.IsNullOrWhiteSpace(currentToken);

                using (var webRequest = UnityEngine.Networking.UnityWebRequest.Get(apiUrl))
                {
                    // Add developer token if provided
                    if (tokenProvided)
                    {
                        webRequest.SetRequestHeader("Authorization", $"Bearer {currentToken}");
                    }

                    var operation = webRequest.SendWebRequest();

                    // Wait for completion
                    while (!operation.isDone)
                    {
                        await System.Threading.Tasks.Task.Delay(100);
                    }

                    if (webRequest.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        string jsonResponse = webRequest.downloadHandler.text;
                        validationResult = JsonUtility.FromJson<ValidationResult>(jsonResponse);
                        if (validationResult != null)
                        {
                            validationResult.tokenWasProvided = tokenProvided;
                        }
                    }
                    else
                    {
                        validationResult = new ValidationResult
                        {
                            success = false,
                            error = $"API Error: {webRequest.error}"
                        };
                    }
                }
            }
            catch (System.Exception ex)
            {
                validationResult = new ValidationResult
                {
                    success = false,
                    error = $"Exception: {ex.Message}"
                };
            }
            finally
            {
                // Update the last validated values AFTER validation completes
                lastValidatedGameId = currentGameId;
                lastValidatedToken = currentToken;
                isValidating = false;
                Repaint();
            }
        }

        private void OpenExampleScenes()
        {
            // Find example scenes in the SDK
            string examplePath = "Assets/Developerworks_SDK/Example";
            Object exampleFolder = AssetDatabase.LoadAssetAtPath<Object>(examplePath);
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
