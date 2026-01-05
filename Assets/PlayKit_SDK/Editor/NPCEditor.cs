using UnityEngine;
using UnityEditor;
using PlayKit_SDK;
using L = PlayKit.SDK.Editor.EditorLocalization;

namespace PlayKit_SDK.Editor
{
    /// <summary>
    /// Custom Editor for PlayKit_NPC with i18n support.
    /// Provides a user-friendly interface for configuring NPC settings.
    /// </summary>
    [CustomEditor(typeof(PlayKit_NPC))]
    public class NPCEditor : UnityEditor.Editor
    {
        // Serialized Properties
        private SerializedProperty characterDesignProp;
        private SerializedProperty chatModelProp;
        private SerializedProperty generateReplyPredictionProp;
        private SerializedProperty predictionCountProp;

        // Foldout states
        private bool showCharacterDesign = true;
        private bool showChatSettings = true;
        private bool showPredictionSettings = true;
        private bool showRuntimeStatus = true;

        // Styles
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private GUIStyle helpBoxStyle;
        private bool stylesInitialized = false;

        private void OnEnable()
        {
            characterDesignProp = serializedObject.FindProperty("characterDesign");
            chatModelProp = serializedObject.FindProperty("chatModel");
            generateReplyPredictionProp = serializedObject.FindProperty("generateReplyPrediction");
            predictionCountProp = serializedObject.FindProperty("predictionCount");
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(0, 0, 8, 4)
            };

            boxStyle = new GUIStyle("HelpBox")
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 5, 5)
            };

            helpBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                wordWrap = true
            };

            stylesInitialized = true;
        }

        public override void OnInspectorGUI()
        {
            InitStyles();
            serializedObject.Update();

            // Header with icon
            DrawHeader();

            EditorGUILayout.Space(5);

            // Character Design Section
            DrawCharacterDesignSection();

            // Chat Settings Section
            DrawChatSettingsSection();

            // Reply Prediction Section
            DrawPredictionSection();

            // Runtime Status (Play Mode Only)
            if (Application.isPlaying)
            {
                DrawRuntimeStatus();
            }

            // Modules Detection
            DrawModulesInfo();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();

            // Icon
            var iconContent = EditorGUIUtility.IconContent("d_UnityEditor.AnimationWindow");
            if (iconContent != null && iconContent.image != null)
            {
                GUILayout.Label(iconContent, GUILayout.Width(32), GUILayout.Height(32));
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(L.Get("npc.editor.title"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(L.Get("npc.editor.subtitle"), EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Info box
            EditorGUILayout.HelpBox(L.Get("npc.editor.info"), MessageType.Info);
        }

        private void DrawCharacterDesignSection()
        {
            showCharacterDesign = EditorGUILayout.BeginFoldoutHeaderGroup(showCharacterDesign,
                L.Get("npc.section.character"));

            if (showCharacterDesign)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                EditorGUILayout.LabelField(L.Get("npc.character.design.label"), EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(L.Get("npc.character.design.help"), MessageType.None);

                EditorGUILayout.Space(5);

                // Character Design TextArea
                EditorGUILayout.LabelField(L.Get("npc.character.design.prompt"));
                characterDesignProp.stringValue = EditorGUILayout.TextArea(
                    characterDesignProp.stringValue,
                    GUILayout.MinHeight(80)
                );

                // Character count
                int charCount = characterDesignProp.stringValue?.Length ?? 0;
                EditorGUILayout.LabelField(
                    L.GetFormat("npc.character.design.charcount", charCount),
                    EditorStyles.miniLabel
                );

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawChatSettingsSection()
        {
            showChatSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showChatSettings,
                L.Get("npc.section.chat"));

            if (showChatSettings)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                EditorGUILayout.LabelField(L.Get("npc.chat.model.label"), EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(chatModelProp, new GUIContent(L.Get("npc.chat.model.field")));

                // Show default model hint if empty
                if (string.IsNullOrEmpty(chatModelProp.stringValue))
                {
                    EditorGUILayout.LabelField(L.Get("npc.chat.model.default"), EditorStyles.miniLabel, GUILayout.Width(100));
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.HelpBox(L.Get("npc.chat.model.help"), MessageType.None);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawPredictionSection()
        {
            showPredictionSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showPredictionSettings,
                L.Get("npc.section.prediction"));

            if (showPredictionSettings)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                EditorGUILayout.LabelField(L.Get("npc.prediction.title"), EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(L.Get("npc.prediction.help"), MessageType.None);

                EditorGUILayout.Space(5);

                // Enable toggle with icon
                EditorGUILayout.BeginHorizontal();
                var toggleContent = new GUIContent(
                    L.Get("npc.prediction.enable"),
                    L.Get("npc.prediction.enable.tooltip")
                );
                EditorGUILayout.PropertyField(generateReplyPredictionProp, toggleContent);

                // Status indicator
                if (generateReplyPredictionProp.boolValue)
                {
                    GUILayout.Label(EditorGUIUtility.IconContent("d_winbtn_mac_max"), GUILayout.Width(20));
                }
                else
                {
                    GUILayout.Label(EditorGUIUtility.IconContent("d_winbtn_mac_min"), GUILayout.Width(20));
                }
                EditorGUILayout.EndHorizontal();

                // Prediction count (only if enabled)
                if (generateReplyPredictionProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(predictionCountProp,
                        new GUIContent(L.Get("npc.prediction.count"), L.Get("npc.prediction.count.tooltip")));
                    EditorGUI.indentLevel--;

                    // Event subscription hint
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox(L.Get("npc.prediction.event.hint"), MessageType.Info);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawRuntimeStatus()
        {
            showRuntimeStatus = EditorGUILayout.BeginFoldoutHeaderGroup(showRuntimeStatus,
                L.Get("npc.section.runtime"));

            if (showRuntimeStatus)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                var npc = (PlayKit_NPC)target;

                // Status row
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(L.Get("npc.runtime.ready"), EditorStyles.boldLabel, GUILayout.Width(80));
                DrawStatusIndicator(npc.IsReady);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(L.Get("npc.runtime.talking"), EditorStyles.boldLabel, GUILayout.Width(80));
                DrawStatusIndicator(npc.IsTalking);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(L.Get("npc.runtime.actions"), EditorStyles.boldLabel, GUILayout.Width(80));
                DrawStatusIndicator(npc.HasEnabledActions);
                EditorGUILayout.EndHorizontal();

                // History info
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField(
                    L.GetFormat("npc.runtime.history", npc.GetHistoryLength()),
                    EditorStyles.miniLabel
                );

                // Quick actions
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(L.Get("npc.runtime.print_history")))
                {
                    npc.PrintPrettyChatMessages();
                }

                if (GUILayout.Button(L.Get("npc.runtime.clear_history")))
                {
                    if (EditorUtility.DisplayDialog(
                        L.Get("npc.runtime.clear_confirm.title"),
                        L.Get("npc.runtime.clear_confirm.message"),
                        L.Get("common.yes"),
                        L.Get("common.no")))
                    {
                        npc.ClearHistory();
                    }
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawStatusIndicator(bool status)
        {
            var color = status ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.5f, 0.5f, 0.5f);
            var oldColor = GUI.color;
            GUI.color = color;
            GUILayout.Label(status ? "●" : "○", GUILayout.Width(20));
            GUI.color = oldColor;
            EditorGUILayout.LabelField(status ? L.Get("common.enabled") : L.Get("common.disabled"));
        }

        private void DrawModulesInfo()
        {
            EditorGUILayout.Space(10);

            var npc = (PlayKit_NPC)target;
            var actionsModule = npc.GetComponent<PlayKit_NPC_ActionsModule>();
            var voiceModule = npc.GetComponent<PlayKit_NPC_VoiceModule>();

            EditorGUILayout.LabelField(L.Get("npc.modules.title"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(boxStyle);

            // Actions Module
            EditorGUILayout.BeginHorizontal();
            DrawStatusIndicator(actionsModule != null);
            EditorGUILayout.LabelField(L.Get("npc.modules.actions"));
            EditorGUILayout.EndHorizontal();

            // Voice Module
            EditorGUILayout.BeginHorizontal();
            DrawStatusIndicator(voiceModule != null);
            EditorGUILayout.LabelField(L.Get("npc.modules.voice"));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
    }
}
