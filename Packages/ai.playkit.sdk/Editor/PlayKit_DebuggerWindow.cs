using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PlayKit_SDK.Editor
{
    /// <summary>
    /// Editor window for inspecting all PlayKit NPCs at runtime.
    /// Shows memories, conversation history, state, and actions for each NPC.
    /// Open via menu: PlayKit > Debugger
    /// </summary>
    public class PlayKit_DebuggerWindow : EditorWindow
    {
        private Vector2 _npcListScroll;
        private Vector2 _detailScroll;
        private int _selectedNpcIndex = -1;
        private NpcDebugSnapshot _selectedSnapshot;
        private NpcDebugSnapshot[] _snapshots = System.Array.Empty<NpcDebugSnapshot>();
        private bool _autoRefresh = true;
        private double _lastRefreshTime;
        private const float RefreshInterval = 1f;

        // Foldout states
        private bool _showMemories = true;
        private bool _showHistory = true;
        private bool _showCharacterDesign;
        private bool _showCompaction;

        [MenuItem("PlayKit/Debugger")]
        public static void ShowWindow()
        {
            var window = GetWindow<PlayKit_DebuggerWindow>("PlayKit Debugger");
            window.minSize = new Vector2(600, 400);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            _snapshots = System.Array.Empty<NpcDebugSnapshot>();
            _selectedNpcIndex = -1;
            _selectedSnapshot = null;
            Repaint();
        }

        private void Update()
        {
            if (!Application.isPlaying || !_autoRefresh) return;

            if (EditorApplication.timeSinceStartup - _lastRefreshTime > RefreshInterval)
            {
                Refresh();
                Repaint();
            }
        }

        private void Refresh()
        {
            _lastRefreshTime = EditorApplication.timeSinceStartup;
            _snapshots = PlayKit_Debugger.GetAllSnapshots();

            if (_selectedNpcIndex >= 0 && _selectedNpcIndex < _snapshots.Length)
                _selectedSnapshot = _snapshots[_selectedNpcIndex];
            else
                _selectedSnapshot = null;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect NPCs.", MessageType.Info);
                return;
            }

            DrawToolbar();

            EditorGUILayout.BeginHorizontal();

            // Left panel: NPC list
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            DrawNpcList();
            EditorGUILayout.EndVertical();

            // Divider
            GUILayout.Box("", GUILayout.Width(1), GUILayout.ExpandHeight(true));

            // Right panel: NPC detail
            EditorGUILayout.BeginVertical();
            DrawNpcDetail();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                Refresh();

            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto", EditorStyles.toolbarButton, GUILayout.Width(40));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Log All", EditorStyles.toolbarButton, GUILayout.Width(60)))
                PlayKit_Debugger.LogAll();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawNpcList()
        {
            EditorGUILayout.LabelField("NPCs", EditorStyles.boldLabel);

            if (_snapshots.Length == 0)
            {
                EditorGUILayout.HelpBox("No NPCs found.", MessageType.None);
                return;
            }

            _npcListScroll = EditorGUILayout.BeginScrollView(_npcListScroll);

            for (int i = 0; i < _snapshots.Length; i++)
            {
                var snapshot = _snapshots[i];
                var isSelected = i == _selectedNpcIndex;

                // Status indicator
                var statusColor = snapshot.IsReady ? (snapshot.IsTalking ? Color.yellow : Color.green) : Color.gray;
                var label = snapshot.GameObjectName;
                if (snapshot.Memories.Count > 0)
                    label += $" ({snapshot.Memories.Count})";

                var style = isSelected ? new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold } : EditorStyles.label;

                EditorGUILayout.BeginHorizontal();

                // Status dot
                var rect = GUILayoutUtility.GetRect(8, 8, GUILayout.Width(8));
                rect.y += 4;
                EditorGUI.DrawRect(rect, statusColor);

                if (GUILayout.Button(label, style))
                {
                    _selectedNpcIndex = i;
                    _selectedSnapshot = snapshot;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawNpcDetail()
        {
            if (_selectedSnapshot == null)
            {
                EditorGUILayout.HelpBox("Select an NPC to inspect.", MessageType.None);
                return;
            }

            var s = _selectedSnapshot;

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);

            // Header
            EditorGUILayout.LabelField(s.GameObjectName, EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            DrawStatusBadge("Ready", s.IsReady);
            DrawStatusBadge("Talking", s.IsTalking);
            DrawStatusBadge("Actions", s.HasActions);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // Character Design
            _showCharacterDesign = EditorGUILayout.Foldout(_showCharacterDesign, $"Character Design", true);
            if (_showCharacterDesign)
            {
                EditorGUI.indentLevel++;
                var design = string.IsNullOrEmpty(s.CharacterDesign) ? "(empty)" : s.CharacterDesign;
                EditorGUILayout.TextArea(design, EditorStyles.wordWrappedLabel);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // Memories
            _showMemories = EditorGUILayout.Foldout(_showMemories, $"Memories ({s.Memories.Count})", true);
            if (_showMemories)
            {
                EditorGUI.indentLevel++;
                if (s.Memories.Count == 0)
                {
                    EditorGUILayout.LabelField("(no memories)", EditorStyles.miniLabel);
                }
                else
                {
                    foreach (var kvp in s.Memories)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(kvp.Key, EditorStyles.miniLabel, GUILayout.Width(150));
                        EditorGUILayout.SelectableLabel(kvp.Value, EditorStyles.miniLabel, GUILayout.Height(16));
                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // Conversation History
            _showHistory = EditorGUILayout.Foldout(_showHistory, $"Conversation ({s.HistoryLength} messages)", true);
            if (_showHistory)
            {
                EditorGUI.indentLevel++;
                if (s.History.Length == 0)
                {
                    EditorGUILayout.LabelField("(no messages)", EditorStyles.miniLabel);
                }
                else
                {
                    foreach (var msg in s.History)
                    {
                        var roleColor = msg.Role switch
                        {
                            "system" => "#888888",
                            "user" => "#4488ff",
                            "assistant" => "#44cc44",
                            "tool" => "#cc8844",
                            _ => "#cccccc"
                        };

                        var content = msg.Content ?? "(null)";
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"<color={roleColor}>[{msg.Role}]</color>",
                            new GUIStyle(EditorStyles.miniLabel) { richText = true }, GUILayout.Width(80));
                        EditorGUILayout.SelectableLabel(content, EditorStyles.wordWrappedMiniLabel,
                            GUILayout.MinHeight(16));
                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // Compaction
            _showCompaction = EditorGUILayout.Foldout(_showCompaction, "Compaction", true);
            if (_showCompaction)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Eligible", s.IsEligibleForCompaction.ToString());
                EditorGUILayout.LabelField("Compaction Count", s.CompactionCount.ToString());
                if (s.LastConversationTime.HasValue)
                    EditorGUILayout.LabelField("Last Conversation", s.LastConversationTime.Value.ToLocalTime().ToString("HH:mm:ss"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawStatusBadge(string label, bool active)
        {
            var color = active ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.5f, 0.5f, 0.5f);
            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label(label, EditorStyles.miniButton, GUILayout.Width(60));
            GUI.backgroundColor = prevColor;
        }
    }
}
