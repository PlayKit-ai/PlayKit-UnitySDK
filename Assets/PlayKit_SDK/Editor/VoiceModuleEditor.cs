using UnityEngine;
using UnityEditor;
using PlayKit_SDK;
using L = PlayKit.SDK.Editor.EditorLocalization;

namespace PlayKit_SDK.Editor
{
    /// <summary>
    /// Custom Editor for PlayKit_NPCClient_VoiceModule with i18n support.
    /// Provides a user-friendly interface for configuring voice transcription settings.
    /// </summary>
    [CustomEditor(typeof(PlayKit_NPCClient_VoiceModule))]
    public class VoiceModuleEditor : UnityEditor.Editor
    {
        // Serialized Properties
        private SerializedProperty transcriptionModelProp;
        private SerializedProperty defaultLanguageProp;
        private SerializedProperty microphoneRecorderProp;
        private SerializedProperty logTranscriptionProp;

        // Foldout states
        private bool showTranscriptionSettings = true;
        private bool showMicrophoneSettings = true;
        private bool showDebugSettings = true;
        private bool showRuntimeStatus = true;

        // Styles
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private bool stylesInitialized = false;

        private void OnEnable()
        {
            transcriptionModelProp = serializedObject.FindProperty("transcriptionModel");
            defaultLanguageProp = serializedObject.FindProperty("defaultLanguage");
            microphoneRecorderProp = serializedObject.FindProperty("microphoneRecorder");
            logTranscriptionProp = serializedObject.FindProperty("logTranscription");
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

            // Header with icon
            DrawHeader();

            EditorGUILayout.Space(5);

            // Transcription Settings Section
            DrawTranscriptionSection();

            // Microphone Settings Section
            DrawMicrophoneSection();

            // Debug Settings Section
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
            var iconContent = EditorGUIUtility.IconContent("d_Microphone Icon");
            if (iconContent != null && iconContent.image != null)
            {
                GUILayout.Label(iconContent, GUILayout.Width(32), GUILayout.Height(32));
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(L.Get("voice.editor.title"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(L.Get("voice.editor.subtitle"), EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Info box
            EditorGUILayout.HelpBox(L.Get("voice.editor.info"), MessageType.Info);
        }

        private void DrawTranscriptionSection()
        {
            showTranscriptionSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showTranscriptionSettings,
                L.Get("voice.section.transcription"));

            if (showTranscriptionSettings)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                // Transcription Model
                EditorGUILayout.PropertyField(transcriptionModelProp,
                    new GUIContent(L.Get("voice.transcription.model")));
                EditorGUILayout.HelpBox(L.Get("voice.transcription.model.help"), MessageType.None);

                EditorGUILayout.Space(5);

                // Default Language
                EditorGUILayout.PropertyField(defaultLanguageProp,
                    new GUIContent(L.Get("voice.transcription.language")));
                EditorGUILayout.HelpBox(L.Get("voice.transcription.language.help"), MessageType.None);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawMicrophoneSection()
        {
            showMicrophoneSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showMicrophoneSettings,
                L.Get("voice.section.microphone"));

            if (showMicrophoneSettings)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                // Microphone Recorder reference
                EditorGUILayout.PropertyField(microphoneRecorderProp,
                    new GUIContent(L.Get("voice.microphone.recorder")));
                EditorGUILayout.HelpBox(L.Get("voice.microphone.recorder.help"), MessageType.None);

                // Create Recorder button if none assigned
                if (microphoneRecorderProp.objectReferenceValue == null)
                {
                    EditorGUILayout.Space(5);
                    if (GUILayout.Button(L.Get("voice.microphone.create")))
                    {
                        var voiceModule = (PlayKit_NPCClient_VoiceModule)target;
                        var recorder = voiceModule.gameObject.GetComponent<PlayKit_MicrophoneRecorder>();
                        if (recorder == null)
                        {
                            recorder = voiceModule.gameObject.AddComponent<PlayKit_MicrophoneRecorder>();
                        }
                        microphoneRecorderProp.objectReferenceValue = recorder;
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawDebugSection()
        {
            showDebugSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showDebugSettings,
                L.Get("voice.section.debug"));

            if (showDebugSettings)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                // Log Transcription toggle
                var toggleContent = new GUIContent(
                    L.Get("voice.debug.log"),
                    L.Get("voice.debug.log.tooltip")
                );
                EditorGUILayout.PropertyField(logTranscriptionProp, toggleContent);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawRuntimeStatus()
        {
            showRuntimeStatus = EditorGUILayout.BeginFoldoutHeaderGroup(showRuntimeStatus,
                L.Get("voice.section.runtime"));

            if (showRuntimeStatus)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                var voiceModule = (PlayKit_NPCClient_VoiceModule)target;

                // Ready status
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(L.Get("voice.runtime.ready"), EditorStyles.boldLabel, GUILayout.Width(80));
                DrawStatusIndicator(voiceModule.IsReady);
                EditorGUILayout.EndHorizontal();

                // Processing status
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(L.Get("voice.runtime.processing"), EditorStyles.boldLabel, GUILayout.Width(80));
                DrawStatusIndicator(voiceModule.IsProcessing);
                EditorGUILayout.EndHorizontal();

                // Model info
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField(
                    L.Get("voice.runtime.model") + " " + voiceModule.TranscriptionModel,
                    EditorStyles.miniLabel
                );

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
    }
}
