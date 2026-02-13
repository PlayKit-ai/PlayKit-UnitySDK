using UnityEngine;
using UnityEditor;
using PlayKit_SDK;

namespace PlayKit_SDK.Editor
{
    /// <summary>
    /// Custom Editor for PlayKit_Image with i18n support.
    /// Provides a user-friendly interface for configuring Image generation settings.
    /// </summary>
    [CustomEditor(typeof(PlayKit_Image))]
    public class ImageEditor : UnityEditor.Editor
    {
        // Serialized Properties
        private SerializedProperty imageModelProp;
        private SerializedProperty defaultSizeProp;
        private SerializedProperty defaultCountProp;
        private SerializedProperty autoConvertToTextureProp;
        private SerializedProperty logGenerationProp;

        // Events
        private SerializedProperty onTextureGeneratedProp;
        private SerializedProperty onSpriteGeneratedProp;
        private SerializedProperty onImagesGeneratedProp;
        private SerializedProperty onGenerationStartedProp;
        private SerializedProperty onGenerationEndedProp;
        private SerializedProperty onErrorProp;

        // Foldout states
        private bool showConfiguration = true;
        private bool showEvents = false;
        private bool showDebug = true;
        private bool showRuntimeStatus = true;

        // Size presets
        private static readonly string[] sizePresets = new string[]
        {
            "1024x1024",
            "1792x1024",
            "1024x1792",
            "512x512",
            "256x256"
        };

        // Styles
        private GUIStyle boxStyle;
        private bool stylesInitialized = false;

        private void OnEnable()
        {
            imageModelProp = serializedObject.FindProperty("imageModel");
            defaultSizeProp = serializedObject.FindProperty("defaultSize");
            defaultCountProp = serializedObject.FindProperty("defaultCount");
            autoConvertToTextureProp = serializedObject.FindProperty("autoConvertToTexture");
            logGenerationProp = serializedObject.FindProperty("logGeneration");

            onTextureGeneratedProp = serializedObject.FindProperty("OnTextureGenerated");
            onSpriteGeneratedProp = serializedObject.FindProperty("OnSpriteGenerated");
            onImagesGeneratedProp = serializedObject.FindProperty("OnImagesGenerated");
            onGenerationStartedProp = serializedObject.FindProperty("OnGenerationStarted");
            onGenerationEndedProp = serializedObject.FindProperty("OnGenerationEnded");
            onErrorProp = serializedObject.FindProperty("OnError");
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
            var iconContent = EditorGUIUtility.IconContent("d_RawImage Icon");
            if (iconContent != null && iconContent.image != null)
            {
                GUILayout.Label(iconContent, GUILayout.Width(32), GUILayout.Height(32));
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("PlayKit Image", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("AI Image Generation Component", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "MonoBehaviour wrapper for AI image generation. Configure settings below or use GetUnderlyingClient() for advanced usage.",
                MessageType.Info);
        }

        private void DrawConfigurationSection()
        {
            showConfiguration = EditorGUILayout.BeginFoldoutHeaderGroup(showConfiguration, "Image Configuration");

            if (showConfiguration)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                // Image Model
                EditorGUILayout.LabelField("Model Settings", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(imageModelProp, new GUIContent("Image Model"));
                if (string.IsNullOrEmpty(imageModelProp.stringValue))
                {
                    EditorGUILayout.LabelField("(SDK Default)", EditorStyles.miniLabel, GUILayout.Width(80));
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox("Leave empty to use SDK default image model.", MessageType.None);

                EditorGUILayout.Space(10);

                // Size Settings
                EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);

                // Size dropdown with presets
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(defaultSizeProp, new GUIContent("Default Size"));

                // Preset dropdown
                int currentPreset = System.Array.IndexOf(sizePresets, defaultSizeProp.stringValue);
                int newPreset = EditorGUILayout.Popup(currentPreset, sizePresets, GUILayout.Width(100));
                if (newPreset >= 0 && newPreset != currentPreset)
                {
                    defaultSizeProp.stringValue = sizePresets[newPreset];
                }
                EditorGUILayout.EndHorizontal();

                // Count
                EditorGUILayout.PropertyField(defaultCountProp, new GUIContent("Default Count", "Number of images to generate (1-10)"));

                EditorGUILayout.Space(10);

                // Output Options
                EditorGUILayout.LabelField("Output Options", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(autoConvertToTextureProp, new GUIContent("Auto Convert to Texture", "Automatically convert generated images to Texture2D"));

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

                EditorGUILayout.PropertyField(onTextureGeneratedProp, new GUIContent("On Texture Generated"));
                EditorGUILayout.PropertyField(onSpriteGeneratedProp, new GUIContent("On Sprite Generated"));
                EditorGUILayout.PropertyField(onImagesGeneratedProp, new GUIContent("On Images Generated"));
                EditorGUILayout.PropertyField(onGenerationStartedProp, new GUIContent("On Generation Started"));
                EditorGUILayout.PropertyField(onGenerationEndedProp, new GUIContent("On Generation Ended"));
                EditorGUILayout.PropertyField(onErrorProp, new GUIContent("On Error"));

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
                EditorGUILayout.PropertyField(logGenerationProp, new GUIContent("Log Generation", "Log generation status to console"));
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

                var image = (PlayKit_Image)target;

                // Status indicators
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Ready:", EditorStyles.boldLabel, GUILayout.Width(80));
                DrawStatusIndicator(image.IsReady);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Generating:", EditorStyles.boldLabel, GUILayout.Width(80));
                DrawStatusIndicator(image.IsGenerating);
                EditorGUILayout.EndHorizontal();

                // Last generated texture preview
                if (image.LastGeneratedTexture != null)
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Last Generated:", EditorStyles.boldLabel);

                    var texture = image.LastGeneratedTexture;
                    float aspectRatio = (float)texture.width / texture.height;
                    float previewWidth = EditorGUIUtility.currentViewWidth - 40;
                    float previewHeight = previewWidth / aspectRatio;
                    previewHeight = Mathf.Min(previewHeight, 200);
                    previewWidth = previewHeight * aspectRatio;

                    var rect = GUILayoutUtility.GetRect(previewWidth, previewHeight);
                    GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
                }

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
