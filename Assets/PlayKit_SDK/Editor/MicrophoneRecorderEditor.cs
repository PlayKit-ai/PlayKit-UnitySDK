using UnityEngine;
using UnityEditor;
using PlayKit_SDK;
using L = PlayKit.SDK.Editor.EditorLocalization;

namespace PlayKit_SDK.Editor
{
    /// <summary>
    /// Custom Editor for PlayKit_MicrophoneRecorder with i18n support.
    /// Provides a user-friendly interface for configuring microphone recording and VAD settings.
    /// </summary>
    [CustomEditor(typeof(PlayKit_MicrophoneRecorder))]
    public class MicrophoneRecorderEditor : UnityEditor.Editor
    {
        // Serialized Properties
        private SerializedProperty maxRecordingSecondsProp;
        private SerializedProperty sampleRateProp;
        private SerializedProperty microphoneDeviceProp;
        private SerializedProperty useVADProp;
        private SerializedProperty silenceThresholdProp;
        private SerializedProperty maxSilenceDurationProp;

        // Foldout states
        private bool showRecordingSettings = true;
        private bool showVADSettings = true;
        private bool showRuntimeStatus = true;

        // Styles
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private bool stylesInitialized = false;

        // Cached device list
        private string[] availableDevices;
        private int selectedDeviceIndex = 0;

        private void OnEnable()
        {
            maxRecordingSecondsProp = serializedObject.FindProperty("maxRecordingSeconds");
            sampleRateProp = serializedObject.FindProperty("sampleRate");
            microphoneDeviceProp = serializedObject.FindProperty("microphoneDevice");
            useVADProp = serializedObject.FindProperty("useVAD");
            silenceThresholdProp = serializedObject.FindProperty("silenceThreshold");
            maxSilenceDurationProp = serializedObject.FindProperty("maxSilenceDuration");

            RefreshDeviceList();
        }

        private void RefreshDeviceList()
        {
#if !UNITY_WEBGL
            availableDevices = Microphone.devices;
            if (availableDevices.Length > 0)
            {
                string currentDevice = microphoneDeviceProp.stringValue;
                selectedDeviceIndex = 0;
                for (int i = 0; i < availableDevices.Length; i++)
                {
                    if (availableDevices[i] == currentDevice)
                    {
                        selectedDeviceIndex = i;
                        break;
                    }
                }
            }
#else
            availableDevices = new string[] { "WebGL Not Supported" };
#endif
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

            // Recording Settings Section
            DrawRecordingSection();

            // VAD Settings Section
            DrawVADSection();

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
            EditorGUILayout.LabelField(L.Get("mic.editor.title"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(L.Get("mic.editor.subtitle"), EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Info box
            EditorGUILayout.HelpBox(L.Get("mic.editor.info"), MessageType.Info);
        }

        private void DrawRecordingSection()
        {
            showRecordingSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showRecordingSettings,
                L.Get("mic.section.recording"));

            if (showRecordingSettings)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                // Max Duration
                EditorGUILayout.PropertyField(maxRecordingSecondsProp,
                    new GUIContent(L.Get("mic.recording.max_duration"), L.Get("mic.recording.max_duration.tooltip")));

                EditorGUILayout.Space(5);

                // Sample Rate
                EditorGUILayout.PropertyField(sampleRateProp,
                    new GUIContent(L.Get("mic.recording.sample_rate")));
                EditorGUILayout.HelpBox(L.Get("mic.recording.sample_rate.help"), MessageType.None);

                EditorGUILayout.Space(5);

                // Microphone Device
                EditorGUILayout.LabelField(L.Get("mic.recording.device"), EditorStyles.boldLabel);

                if (availableDevices != null && availableDevices.Length > 0)
                {
                    EditorGUILayout.BeginHorizontal();

                    // Device dropdown
                    int newIndex = EditorGUILayout.Popup(selectedDeviceIndex, availableDevices);
                    if (newIndex != selectedDeviceIndex)
                    {
                        selectedDeviceIndex = newIndex;
                        if (selectedDeviceIndex == 0)
                        {
                            microphoneDeviceProp.stringValue = null; // Default device
                        }
                        else
                        {
                            microphoneDeviceProp.stringValue = availableDevices[selectedDeviceIndex];
                        }
                    }

                    // Refresh button
                    if (GUILayout.Button(L.Get("mic.recording.refresh"), GUILayout.Width(100)))
                    {
                        RefreshDeviceList();
                    }

                    EditorGUILayout.EndHorizontal();

                    // Show default device hint
                    if (string.IsNullOrEmpty(microphoneDeviceProp.stringValue))
                    {
                        EditorGUILayout.LabelField(L.Get("mic.recording.device.default"), EditorStyles.miniLabel);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No microphone devices found", MessageType.Warning);
                    if (GUILayout.Button(L.Get("mic.recording.refresh")))
                    {
                        RefreshDeviceList();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawVADSection()
        {
            showVADSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showVADSettings,
                L.Get("mic.section.vad"));

            if (showVADSettings)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                // Enable VAD toggle
                EditorGUILayout.BeginHorizontal();
                var toggleContent = new GUIContent(
                    L.Get("mic.vad.enable"),
                    L.Get("mic.vad.enable.tooltip")
                );
                EditorGUILayout.PropertyField(useVADProp, toggleContent);

                // Status indicator
                if (useVADProp.boolValue)
                {
                    GUILayout.Label(EditorGUIUtility.IconContent("d_winbtn_mac_max"), GUILayout.Width(20));
                }
                else
                {
                    GUILayout.Label(EditorGUIUtility.IconContent("d_winbtn_mac_min"), GUILayout.Width(20));
                }
                EditorGUILayout.EndHorizontal();

                // VAD settings (only if enabled)
                if (useVADProp.boolValue)
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.Space(5);

                    // Silence Threshold
                    EditorGUILayout.PropertyField(silenceThresholdProp,
                        new GUIContent(L.Get("mic.vad.threshold"), L.Get("mic.vad.threshold.tooltip")));

                    // Clamp the value between 0 and 1
                    silenceThresholdProp.floatValue = Mathf.Clamp01(silenceThresholdProp.floatValue);

                    EditorGUILayout.Space(5);

                    // Max Silence Duration
                    EditorGUILayout.PropertyField(maxSilenceDurationProp,
                        new GUIContent(L.Get("mic.vad.max_silence"), L.Get("mic.vad.max_silence.tooltip")));

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawRuntimeStatus()
        {
            showRuntimeStatus = EditorGUILayout.BeginFoldoutHeaderGroup(showRuntimeStatus,
                L.Get("mic.section.runtime"));

            if (showRuntimeStatus)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                var recorder = (PlayKit_MicrophoneRecorder)target;

                // Recording status
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(L.Get("mic.runtime.recording"), EditorStyles.boldLabel, GUILayout.Width(100));
                DrawStatusIndicator(recorder.IsRecording);
                EditorGUILayout.EndHorizontal();

                // Recording time
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(L.Get("mic.runtime.time"), EditorStyles.boldLabel, GUILayout.Width(100));
                EditorGUILayout.LabelField($"{recorder.RecordingTime:F1}s");
                EditorGUILayout.EndHorizontal();

                // Current device
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(L.Get("mic.runtime.device"), EditorStyles.boldLabel, GUILayout.Width(100));
                EditorGUILayout.LabelField(recorder.CurrentDevice ?? "(Default)");
                EditorGUILayout.EndHorizontal();

                // Last recording info
                if (recorder.LastRecording != null)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField(
                        $"{L.Get("mic.runtime.last_recording")} {recorder.LastRecording.length:F1}s",
                        EditorStyles.miniLabel
                    );
                }

                // Quick action buttons
                EditorGUILayout.Space(10);
                EditorGUILayout.BeginHorizontal();

                GUI.enabled = !recorder.IsRecording;
                if (GUILayout.Button(L.Get("mic.action.start")))
                {
                    recorder.StartRecording();
                }

                GUI.enabled = recorder.IsRecording;
                if (GUILayout.Button(L.Get("mic.action.stop")))
                {
                    recorder.StopRecording();
                }

                if (GUILayout.Button(L.Get("mic.action.cancel")))
                {
                    recorder.CancelRecording();
                }

                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                // Force repaint during recording to update the UI
                if (recorder.IsRecording)
                {
                    Repaint();
                }
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
