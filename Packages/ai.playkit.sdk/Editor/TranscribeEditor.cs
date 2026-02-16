using UnityEngine;
using UnityEditor;
using PlayKit_SDK;

namespace PlayKit_SDK.Editor
{
    /// <summary>
    /// Custom Editor for PlayKit_Transcribe with i18n support.
    /// Provides a user-friendly interface for configuring Transcription settings.
    /// </summary>
    [CustomEditor(typeof(PlayKit_Transcribe))]
    public class TranscribeEditor : UnityEditor.Editor
    {
        // Serialized Properties
        private SerializedProperty transcriptionModelProp;
        private SerializedProperty defaultLanguageProp;
        private SerializedProperty transcriptionPromptProp;
        private SerializedProperty microphoneRecorderProp;
        private SerializedProperty autoCreateRecorderProp;
        private SerializedProperty logTranscriptionProp;

        // Events
        private SerializedProperty onTranscriptionCompleteProp;
        private SerializedProperty onFullTranscriptionCompleteProp;
        private SerializedProperty onTranscriptionStartedProp;
        private SerializedProperty onTranscriptionEndedProp;
        private SerializedProperty onErrorProp;
        private SerializedProperty onRecordingStartedProp;
        private SerializedProperty onRecordingStoppedProp;
        private SerializedProperty onRecordingVolumeProp;

        // Foldout states
        private bool showConfiguration = true;
        private bool showMicrophone = true;
        private bool showEvents = false;
        private bool showDebug = true;
        private bool showRuntimeStatus = true;

        // Language presets
        private static readonly string[] languagePresets = new string[]
        {
            "zh", "en", "ja", "ko", "es", "fr", "de", "it", "pt", "ru"
        };
        private static readonly string[] languageNames = new string[]
        {
            "Chinese", "English", "Japanese", "Korean", "Spanish", "French", "German", "Italian", "Portuguese", "Russian"
        };

        // Styles
        private GUIStyle boxStyle;
        private bool stylesInitialized = false;

        private void OnEnable()
        {
            transcriptionModelProp = serializedObject.FindProperty("transcriptionModel");
            defaultLanguageProp = serializedObject.FindProperty("defaultLanguage");
            transcriptionPromptProp = serializedObject.FindProperty("transcriptionPrompt");
            microphoneRecorderProp = serializedObject.FindProperty("microphoneRecorder");
            autoCreateRecorderProp = serializedObject.FindProperty("autoCreateRecorder");
            logTranscriptionProp = serializedObject.FindProperty("logTranscription");

            onTranscriptionCompleteProp = serializedObject.FindProperty("OnTranscriptionComplete");
            onFullTranscriptionCompleteProp = serializedObject.FindProperty("OnFullTranscriptionComplete");
            onTranscriptionStartedProp = serializedObject.FindProperty("OnTranscriptionStarted");
            onTranscriptionEndedProp = serializedObject.FindProperty("OnTranscriptionEnded");
            onErrorProp = serializedObject.FindProperty("OnError");
            onRecordingStartedProp = serializedObject.FindProperty("OnRecordingStarted");
            onRecordingStoppedProp = serializedObject.FindProperty("OnRecordingStopped");
            onRecordingVolumeProp = serializedObject.FindProperty("OnRecordingVolume");
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

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

            // Microphone Section
            DrawMicrophoneSection();

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
            var iconContent = EditorGUIUtility.IconContent("d_Microphone Icon");
            if (iconContent != null && iconContent.image != null)
            {
                GUILayout.Label(iconContent, GUILayout.Width(32), GUILayout.Height(32));
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("PlayKit Transcribe", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Speech-to-Text Component", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "MonoBehaviour wrapper for audio transcription. Configure settings below or use GetUnderlyingClient() for advanced usage.",
                MessageType.Info);
        }

        private void DrawConfigurationSection()
        {
            showConfiguration = EditorGUILayout.BeginFoldoutHeaderGroup(showConfiguration, "Transcription Configuration");

            if (showConfiguration)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                // Model
                EditorGUILayout.LabelField("Model Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(transcriptionModelProp, new GUIContent("Transcription Model"));
                EditorGUILayout.HelpBox("Common models: whisper-large, whisper-medium, whisper-small", MessageType.None);

                EditorGUILayout.Space(10);

                // Language
                EditorGUILayout.LabelField("Language Settings", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(defaultLanguageProp, new GUIContent("Default Language"));

                // Language dropdown
                int currentLang = System.Array.IndexOf(languagePresets, defaultLanguageProp.stringValue);
                string[] displayOptions = new string[languagePresets.Length];
                for (int i = 0; i < languagePresets.Length; i++)
                {
                    displayOptions[i] = $"{languagePresets[i]} ({languageNames[i]})";
                }
                int newLang = EditorGUILayout.Popup(currentLang, displayOptions, GUILayout.Width(150));
                if (newLang >= 0 && newLang != currentLang)
                {
                    defaultLanguageProp.stringValue = languagePresets[newLang];
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(10);

                // Prompt
                EditorGUILayout.LabelField("Transcription Prompt (Optional)", EditorStyles.boldLabel);
                transcriptionPromptProp.stringValue = EditorGUILayout.TextArea(
                    transcriptionPromptProp.stringValue,
                    GUILayout.MinHeight(40)
                );
                EditorGUILayout.HelpBox("Optional prompt to guide the transcription. Useful for domain-specific vocabulary.", MessageType.None);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawMicrophoneSection()
        {
            showMicrophone = EditorGUILayout.BeginFoldoutHeaderGroup(showMicrophone, "Microphone Integration");

            if (showMicrophone)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                EditorGUILayout.LabelField("Microphone Recorder", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(microphoneRecorderProp, new GUIContent("Recorder Reference"));

                EditorGUILayout.PropertyField(autoCreateRecorderProp, new GUIContent("Auto Create Recorder", "Automatically create a MicrophoneRecorder if not assigned"));

                // Show recorder status
                var transcribe = (PlayKit_Transcribe)target;
                if (transcribe.MicrophoneRecorder != null)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.BeginHorizontal();
                    DrawStatusIndicator(true);
                    EditorGUILayout.LabelField("Microphone recorder attached");
                    EditorGUILayout.EndHorizontal();
                }
                else if (autoCreateRecorderProp.boolValue)
                {
                    EditorGUILayout.HelpBox("A MicrophoneRecorder will be auto-created at runtime.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("No microphone recorder assigned. RecordAndTranscribeAsync() will not work.", MessageType.Warning);
                }

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

                EditorGUILayout.LabelField("Transcription Events", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(onTranscriptionCompleteProp, new GUIContent("On Transcription Complete"));
                EditorGUILayout.PropertyField(onFullTranscriptionCompleteProp, new GUIContent("On Full Result"));
                EditorGUILayout.PropertyField(onTranscriptionStartedProp, new GUIContent("On Transcription Started"));
                EditorGUILayout.PropertyField(onTranscriptionEndedProp, new GUIContent("On Transcription Ended"));
                EditorGUILayout.PropertyField(onErrorProp, new GUIContent("On Error"));

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Recording Events", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(onRecordingStartedProp, new GUIContent("On Recording Started"));
                EditorGUILayout.PropertyField(onRecordingStoppedProp, new GUIContent("On Recording Stopped"));
                EditorGUILayout.PropertyField(onRecordingVolumeProp, new GUIContent("On Recording Volume"));

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
                EditorGUILayout.PropertyField(logTranscriptionProp, new GUIContent("Log Transcription", "Log transcription results to console"));
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

                var transcribe = (PlayKit_Transcribe)target;

                // Status indicators
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Ready:", EditorStyles.boldLabel, GUILayout.Width(80));
                DrawStatusIndicator(transcribe.IsReady);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Processing:", EditorStyles.boldLabel, GUILayout.Width(80));
                DrawStatusIndicator(transcribe.IsProcessing);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Recording:", EditorStyles.boldLabel, GUILayout.Width(80));
                DrawStatusIndicator(transcribe.IsRecording);
                EditorGUILayout.EndHorizontal();

                // Model info
                if (!string.IsNullOrEmpty(transcribe.ModelName))
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField($"Model: {transcribe.ModelName}", EditorStyles.miniLabel);
                }

                // Quick actions
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();

                using (new EditorGUI.DisabledGroupScope(!transcribe.IsReady || transcribe.IsRecording))
                {
                    if (GUILayout.Button("Start Recording"))
                    {
                        transcribe.StartRecording();
                    }
                }

                using (new EditorGUI.DisabledGroupScope(!transcribe.IsRecording))
                {
                    if (GUILayout.Button("Stop Recording"))
                    {
                        transcribe.StopRecording();
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
