using UnityEngine;
using UnityEditor;
using PlayKit_SDK;

namespace PlayKit_SDK.Editor
{
    /// <summary>
    /// Custom Editor for PlayKit_Chat with i18n support.
    /// Provides a user-friendly interface for configuring Chat settings.
    /// </summary>
    [CustomEditor(typeof(PlayKit_Chat))]
    public class ChatEditor : UnityEditor.Editor
    {
        // Serialized Properties
        private SerializedProperty chatModelProp;
        private SerializedProperty systemPromptProp;
        private SerializedProperty temperatureProp;
        private SerializedProperty maintainHistoryProp;
        private SerializedProperty logMessagesProp;

        // Events
        private SerializedProperty onResponseReceivedProp;
        private SerializedProperty onStreamChunkProp;
        private SerializedProperty onStreamCompleteProp;
        private SerializedProperty onErrorProp;
        private SerializedProperty onRequestStartedProp;
        private SerializedProperty onRequestEndedProp;

        // Foldout states
        private bool showConfiguration = true;
        private bool showEvents = false;
        private bool showDebug = true;
        private bool showRuntimeStatus = true;

        // Styles
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private bool stylesInitialized = false;

        private void OnEnable()
        {
            chatModelProp = serializedObject.FindProperty("chatModel");
            systemPromptProp = serializedObject.FindProperty("systemPrompt");
            temperatureProp = serializedObject.FindProperty("temperature");
            maintainHistoryProp = serializedObject.FindProperty("maintainHistory");
            logMessagesProp = serializedObject.FindProperty("logMessages");

            onResponseReceivedProp = serializedObject.FindProperty("OnResponseReceived");
            onStreamChunkProp = serializedObject.FindProperty("OnStreamChunk");
            onStreamCompleteProp = serializedObject.FindProperty("OnStreamComplete");
            onErrorProp = serializedObject.FindProperty("OnError");
            onRequestStartedProp = serializedObject.FindProperty("OnRequestStarted");
            onRequestEndedProp = serializedObject.FindProperty("OnRequestEnded");
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

            stylesInitialized = true;
        }

        public override void OnInspectorGUI()
        {
            InitStyles();
            serializedObject.Update();

            // Header
            DrawHeader();

            EditorGUILayout.Space(5);

            // Configuration Section
            DrawConfigurationSection();

            // Events Section
            DrawEventsSection();

            // Debug Section
            DrawDebugSection();

            // Runtime Status (Play Mode Only)
            if (Application.isPlaying)
            {
                DrawRuntimeStatus();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();

            // Icon
            var iconContent = EditorGUIUtility.IconContent("d_console.infoicon.sml");
            if (iconContent != null && iconContent.image != null)
            {
                GUILayout.Label(iconContent, GUILayout.Width(32), GUILayout.Height(32));
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("PlayKit Chat", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("AI Chat Component", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "MonoBehaviour wrapper for AI chat functionality. Configure settings below or use GetUnderlyingClient() for advanced usage.",
                MessageType.Info);
        }

        private void DrawConfigurationSection()
        {
            showConfiguration = EditorGUILayout.BeginFoldoutHeaderGroup(showConfiguration, "Chat Configuration");

            if (showConfiguration)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                // Chat Model
                EditorGUILayout.LabelField("Model Settings", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(chatModelProp, new GUIContent("Chat Model"));
                if (string.IsNullOrEmpty(chatModelProp.stringValue))
                {
                    EditorGUILayout.LabelField("(SDK Default)", EditorStyles.miniLabel, GUILayout.Width(80));
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox("Leave empty to use SDK default model.", MessageType.None);

                EditorGUILayout.Space(10);

                // System Prompt
                EditorGUILayout.LabelField("System Prompt", EditorStyles.boldLabel);
                systemPromptProp.stringValue = EditorGUILayout.TextArea(
                    systemPromptProp.stringValue,
                    GUILayout.MinHeight(60)
                );
                int charCount = systemPromptProp.stringValue?.Length ?? 0;
                EditorGUILayout.LabelField($"Characters: {charCount}", EditorStyles.miniLabel);

                EditorGUILayout.Space(10);

                // Temperature
                EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(temperatureProp, new GUIContent("Temperature", "0 = Deterministic, 2 = Creative"));

                // Maintain History
                EditorGUILayout.PropertyField(maintainHistoryProp, new GUIContent("Maintain History", "Automatically manage conversation history"));

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawEventsSection()
        {
            showEvents = EditorGUILayout.BeginFoldoutHeaderGroup(showEvents, "Events");

            if (showEvents)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                EditorGUILayout.PropertyField(onResponseReceivedProp, new GUIContent("On Response Received"));
                EditorGUILayout.PropertyField(onStreamChunkProp, new GUIContent("On Stream Chunk"));
                EditorGUILayout.PropertyField(onStreamCompleteProp, new GUIContent("On Stream Complete"));
                EditorGUILayout.PropertyField(onErrorProp, new GUIContent("On Error"));
                EditorGUILayout.PropertyField(onRequestStartedProp, new GUIContent("On Request Started"));
                EditorGUILayout.PropertyField(onRequestEndedProp, new GUIContent("On Request Ended"));

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawDebugSection()
        {
            showDebug = EditorGUILayout.BeginFoldoutHeaderGroup(showDebug, "Debug Options");

            if (showDebug)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                EditorGUILayout.PropertyField(logMessagesProp, new GUIContent("Log Messages", "Log chat messages to console"));
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawRuntimeStatus()
        {
            showRuntimeStatus = EditorGUILayout.BeginFoldoutHeaderGroup(showRuntimeStatus, "Runtime Status");

            if (showRuntimeStatus)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                var chat = (PlayKit_Chat)target;

                // Status indicators
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Ready:", EditorStyles.boldLabel, GUILayout.Width(80));
                DrawStatusIndicator(chat.IsReady);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Processing:", EditorStyles.boldLabel, GUILayout.Width(80));
                DrawStatusIndicator(chat.IsProcessing);
                EditorGUILayout.EndHorizontal();

                // History info
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"History: {chat.HistoryLength} messages", EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(chat.ModelName))
                {
                    EditorGUILayout.LabelField($"Model: {chat.ModelName}", EditorStyles.miniLabel);
                }

                // Quick actions
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Clear History"))
                {
                    if (EditorUtility.DisplayDialog(
                        "Clear History",
                        "Are you sure you want to clear the conversation history?",
                        "Yes", "No"))
                    {
                        chat.ClearHistory();
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
            EditorGUILayout.LabelField(status ? "Yes" : "No");
        }
    }
}
