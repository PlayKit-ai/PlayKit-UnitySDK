using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using PlayKit_SDK;
using PlayKit_SDK.Public;
using L = PlayKit.SDK.Editor.EditorLocalization;
using System.Collections.Generic;

namespace PlayKit_SDK.Editor
{
    [CustomEditor(typeof(PlayKit_NPC_ActionsModule))]
    public class NpcActionsModuleEditor : UnityEditor.Editor
    {
        private ReorderableList actionsList;
        private SerializedProperty actionBindingsProperty;
        private SerializedProperty logActionCallsProperty;
        private SerializedProperty autoReportSuccessProperty;

        private Dictionary<int, bool> actionFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> paramsFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, ReorderableList> paramsLists = new Dictionary<int, ReorderableList>();

        private GUIStyle headerStyle;
        private GUIStyle boxStyle;

        private void OnEnable()
        {
            actionBindingsProperty = serializedObject.FindProperty("actionBindings");
            logActionCallsProperty = serializedObject.FindProperty("logActionCalls");
            autoReportSuccessProperty = serializedObject.FindProperty("autoReportSuccess");

            SetupActionsList();
        }

        private void SetupActionsList()
        {
            actionsList = new ReorderableList(serializedObject, actionBindingsProperty, true, true, true, true);

            actionsList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, L.Get("npc.actions.section.actions"), EditorStyles.boldLabel);
            };

            actionsList.elementHeightCallback = index =>
            {
                if (!actionFoldouts.ContainsKey(index))
                    actionFoldouts[index] = false;

                if (!actionFoldouts[index])
                    return EditorGUIUtility.singleLineHeight + 4;

                var element = actionBindingsProperty.GetArrayElementAtIndex(index);
                var actionProp = element.FindPropertyRelative("action");
                var paramsProp = actionProp.FindPropertyRelative("parameters");

                float height = EditorGUIUtility.singleLineHeight * 5 + 20; // Header + name + desc + enabled + callback
                height += EditorGUIUtility.singleLineHeight + 4; // Parameters header

                if (paramsFoldouts.ContainsKey(index) && paramsFoldouts[index])
                {
                    height += (paramsProp.arraySize + 1) * (EditorGUIUtility.singleLineHeight * 4 + 8);
                    height += EditorGUIUtility.singleLineHeight + 8; // Add button
                }

                return height + 10;
            };

            actionsList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = actionBindingsProperty.GetArrayElementAtIndex(index);
                var actionProp = element.FindPropertyRelative("action");
                var actionNameProp = actionProp.FindPropertyRelative("actionName");
                var enabledProp = actionProp.FindPropertyRelative("enabled");

                // Initialize foldout state
                if (!actionFoldouts.ContainsKey(index))
                    actionFoldouts[index] = false;

                // Draw foldout header
                var foldoutRect = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
                string displayName = string.IsNullOrEmpty(actionNameProp.stringValue) ? "(Unnamed Action)" : actionNameProp.stringValue;
                var foldoutContent = new GUIContent($"  {displayName}");
                actionFoldouts[index] = EditorGUI.Foldout(foldoutRect, actionFoldouts[index], foldoutContent, true, EditorStyles.foldoutHeader);

                // Draw status icon
                var iconRect = new Rect(rect.x + rect.width - 20, rect.y + 2, 18, EditorGUIUtility.singleLineHeight);
                string statusIconName = enabledProp.boolValue ? "d_winbtn_mac_max" : "d_winbtn_mac_min";
                string statusFallback = enabledProp.boolValue ? "●" : "○";
                GUI.Label(iconRect, SafeIconContent(statusIconName, statusFallback));

                if (actionFoldouts[index])
                {
                    DrawActionDetails(rect, index, element);
                }
            };

            actionsList.onAddCallback = list =>
            {
                var index = list.serializedProperty.arraySize;
                list.serializedProperty.InsertArrayElementAtIndex(index);
                var element = list.serializedProperty.GetArrayElementAtIndex(index);

                // Initialize the new action
                var actionProp = element.FindPropertyRelative("action");
                actionProp.FindPropertyRelative("actionName").stringValue = "newAction";
                actionProp.FindPropertyRelative("description").stringValue = "Description of what this action does";
                actionProp.FindPropertyRelative("enabled").boolValue = true;
                actionProp.FindPropertyRelative("parameters").ClearArray();

                actionFoldouts[index] = true;
            };

            actionsList.onRemoveCallback = list =>
            {
                int index = list.index;
                actionFoldouts.Remove(index);
                paramsFoldouts.Remove(index);
                paramsLists.Remove(index);
                ReorderableList.defaultBehaviours.DoRemoveButton(list);
            };
        }

        private void DrawActionDetails(Rect containerRect, int index, SerializedProperty element)
        {
            var actionProp = element.FindPropertyRelative("action");
            var onTriggeredProp = element.FindPropertyRelative("onTriggered");

            float y = containerRect.y + EditorGUIUtility.singleLineHeight + 6;
            float indent = 20;
            float width = containerRect.width - indent;

            // Action Name
            var nameRect = new Rect(containerRect.x + indent, y, width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(nameRect, actionProp.FindPropertyRelative("actionName"), new GUIContent(L.Get("npc.actions.action.name")));
            y += EditorGUIUtility.singleLineHeight + 2;

            // Description (TextArea)
            var descLabel = new Rect(containerRect.x + indent, y, width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(descLabel, L.Get("npc.actions.action.description"));
            y += EditorGUIUtility.singleLineHeight;

            var descRect = new Rect(containerRect.x + indent, y, width, EditorGUIUtility.singleLineHeight * 2);
            var descProp = actionProp.FindPropertyRelative("description");
            descProp.stringValue = EditorGUI.TextArea(descRect, descProp.stringValue);
            y += EditorGUIUtility.singleLineHeight * 2 + 4;

            // Enabled toggle
            var enabledRect = new Rect(containerRect.x + indent, y, width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(enabledRect, actionProp.FindPropertyRelative("enabled"));
            y += EditorGUIUtility.singleLineHeight + 4;

            // Parameters section
            if (!paramsFoldouts.ContainsKey(index))
                paramsFoldouts[index] = false;

            var paramsFoldoutRect = new Rect(containerRect.x + indent, y, width, EditorGUIUtility.singleLineHeight);
            var paramsProp = actionProp.FindPropertyRelative("parameters");
            paramsFoldouts[index] = EditorGUI.Foldout(paramsFoldoutRect, paramsFoldouts[index],
                $"{L.Get("npc.actions.action.params")} ({paramsProp.arraySize})", true);
            y += EditorGUIUtility.singleLineHeight + 2;

            if (paramsFoldouts[index])
            {
                y = DrawParameters(containerRect.x + indent, y, width, paramsProp);
            }

            // Callback event
            var callbackRect = new Rect(containerRect.x + indent, y, width, EditorGUI.GetPropertyHeight(onTriggeredProp));
            EditorGUI.PropertyField(callbackRect, onTriggeredProp, new GUIContent(L.Get("npc.actions.action.callback")));
        }

        private float DrawParameters(float x, float y, float width, SerializedProperty paramsProp)
        {
            float paramIndent = 10;

            for (int i = 0; i < paramsProp.arraySize; i++)
            {
                var param = paramsProp.GetArrayElementAtIndex(i);

                // Parameter box background
                var boxRect = new Rect(x + paramIndent, y, width - paramIndent - 25, EditorGUIUtility.singleLineHeight * 4 + 6);
                EditorGUI.DrawRect(boxRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));

                float paramY = y + 2;
                float paramWidth = width - paramIndent - 30;

                // Name
                var nameRect = new Rect(x + paramIndent + 5, paramY, paramWidth * 0.5f - 5, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(nameRect, param.FindPropertyRelative("name"), GUIContent.none);

                // Type
                var typeRect = new Rect(x + paramIndent + 5 + paramWidth * 0.5f, paramY, paramWidth * 0.3f, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(typeRect, param.FindPropertyRelative("type"), GUIContent.none);

                // Required toggle
                var reqRect = new Rect(x + paramIndent + 5 + paramWidth * 0.8f, paramY, paramWidth * 0.2f, EditorGUIUtility.singleLineHeight);
                var reqProp = param.FindPropertyRelative("required");
                reqProp.boolValue = EditorGUI.ToggleLeft(reqRect, "Req", reqProp.boolValue);

                paramY += EditorGUIUtility.singleLineHeight + 2;

                // Description
                var descRect = new Rect(x + paramIndent + 5, paramY, paramWidth, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(descRect, param.FindPropertyRelative("description"), new GUIContent(L.Get("npc.actions.param.description")));
                paramY += EditorGUIUtility.singleLineHeight + 2;

                // Enum options (only for StringEnum type)
                var typeProp = param.FindPropertyRelative("type");
                if (typeProp.enumValueIndex == (int)NpcActionParamType.StringEnum)
                {
                    var enumRect = new Rect(x + paramIndent + 5, paramY, paramWidth, EditorGUIUtility.singleLineHeight);
                    var enumProp = param.FindPropertyRelative("enumOptions");

                    // Display as comma-separated string for easier editing
                    string enumStr = string.Join(", ", GetStringArray(enumProp));
                    var newEnumStr = EditorGUI.TextField(enumRect, L.Get("npc.actions.param.options"), enumStr);
                    if (newEnumStr != enumStr)
                    {
                        SetStringArray(enumProp, newEnumStr.Split(new[] { ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries));
                    }
                }

                // Remove button
                var removeRect = new Rect(x + width - 20, y + 2, 18, 18);
                if (GUI.Button(removeRect, EditorGUIUtility.IconContent("d_TreeEditor.Trash"), GUIStyle.none))
                {
                    paramsProp.DeleteArrayElementAtIndex(i);
                    break;
                }

                y += EditorGUIUtility.singleLineHeight * 4 + 8;
            }

            // Add parameter button
            var addRect = new Rect(x + paramIndent, y, width - paramIndent, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(addRect, L.Get("npc.actions.param.add")))
            {
                paramsProp.InsertArrayElementAtIndex(paramsProp.arraySize);
                var newParam = paramsProp.GetArrayElementAtIndex(paramsProp.arraySize - 1);
                newParam.FindPropertyRelative("name").stringValue = "paramName";
                newParam.FindPropertyRelative("description").stringValue = "";
                newParam.FindPropertyRelative("type").enumValueIndex = 0;
                newParam.FindPropertyRelative("required").boolValue = true;
            }

            return y + EditorGUIUtility.singleLineHeight + 4;
        }

        private string[] GetStringArray(SerializedProperty prop)
        {
            var result = new string[prop.arraySize];
            for (int i = 0; i < prop.arraySize; i++)
            {
                result[i] = prop.GetArrayElementAtIndex(i).stringValue.Trim();
            }
            return result;
        }

        private void SetStringArray(SerializedProperty prop, string[] values)
        {
            prop.ClearArray();
            for (int i = 0; i < values.Length; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).stringValue = values[i].Trim();
            }
        }

        private static GUIContent SafeIconContent(string iconName, string fallbackText)
        {
            var content = EditorGUIUtility.IconContent(iconName);
            if (content != null && content.image != null)
                return content;
            return new GUIContent(fallbackText);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Header
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L.Get("npc.actions.editor.title"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(L.Get("npc.actions.editor.info"), MessageType.Info);
            EditorGUILayout.Space(5);

            // Debug options
            EditorGUILayout.LabelField(L.Get("npc.actions.section.debug"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(logActionCallsProperty, new GUIContent(L.Get("npc.actions.log_calls")));
            EditorGUILayout.PropertyField(autoReportSuccessProperty, new GUIContent(L.Get("npc.actions.auto_report")));
            EditorGUILayout.Space(10);

            // Actions list
            actionsList.DoLayoutList();

            // Runtime info (only during play mode)
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField(L.Get("npc.section.runtime"), EditorStyles.boldLabel);

                var module = (PlayKit_NPC_ActionsModule)target;
                EditorGUILayout.LabelField(L.Get("npc.actions.runtime.ready"), module.IsReady ? L.Get("common.yes") : L.Get("common.no"));
                EditorGUILayout.LabelField(L.Get("npc.actions.runtime.enabled_count"), module.EnabledActions.Count.ToString());
            }

            serializedObject.ApplyModifiedProperties();
        }
    }

    /// <summary>
    /// Property drawer for NpcActionParameter to make it more compact in Inspector
    /// </summary>
    [CustomPropertyDrawer(typeof(NpcActionParameter))]
    public class NpcActionParameterDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var nameRect = new Rect(position.x, position.y, position.width * 0.3f - 2, EditorGUIUtility.singleLineHeight);
            var typeRect = new Rect(position.x + position.width * 0.3f, position.y, position.width * 0.25f - 2, EditorGUIUtility.singleLineHeight);
            var reqRect = new Rect(position.x + position.width * 0.55f, position.y, position.width * 0.15f, EditorGUIUtility.singleLineHeight);
            var descRect = new Rect(position.x + position.width * 0.7f, position.y, position.width * 0.3f, EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(nameRect, property.FindPropertyRelative("name"), GUIContent.none);
            EditorGUI.PropertyField(typeRect, property.FindPropertyRelative("type"), GUIContent.none);
            EditorGUI.PropertyField(reqRect, property.FindPropertyRelative("required"), GUIContent.none);
            EditorGUI.PropertyField(descRect, property.FindPropertyRelative("description"), GUIContent.none);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight + 2;
        }
    }
}
