using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PlayKit_SDK
{
    /// <summary>
    /// Snapshot of a single NPC's debug state.
    /// Immutable — represents state at the moment of capture.
    /// </summary>
    public class NpcDebugSnapshot
    {
        public string GameObjectName { get; set; }
        public string CharacterDesign { get; set; }
        public bool IsReady { get; set; }
        public bool IsTalking { get; set; }
        public bool HasActions { get; set; }

        // Memory
        public Dictionary<string, string> Memories { get; set; } = new();

        // Conversation
        public int HistoryLength { get; set; }
        public Public.PlayKit_ChatMessage[] History { get; set; } = Array.Empty<Public.PlayKit_ChatMessage>();

        // Context manager state
        public bool IsEligibleForCompaction { get; set; }
        public int CompactionCount { get; set; }
        public DateTime? LastConversationTime { get; set; }
    }

    /// <summary>
    /// Programmatic debugger for PlayKit NPCs.
    /// Provides read-only access to all registered NPCs, their memories, conversation history,
    /// and internal state. Designed for both Editor tooling and runtime debug UIs.
    ///
    /// Usage:
    ///   var npcs = PlayKit_Debugger.GetAllNpcs();
    ///   var snapshot = PlayKit_Debugger.GetSnapshot(npc);
    ///   var allSnapshots = PlayKit_Debugger.GetAllSnapshots();
    /// </summary>
    public static class PlayKit_Debugger
    {
        /// <summary>
        /// Find all active PlayKit_NPC instances in the scene.
        /// </summary>
        public static PlayKit_NPC[] GetAllNpcs()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<PlayKit_NPC>(FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<PlayKit_NPC>();
#endif
        }

        /// <summary>
        /// Get a full debug snapshot of a single NPC.
        /// </summary>
        public static NpcDebugSnapshot GetSnapshot(PlayKit_NPC npc)
        {
            if (npc == null) return null;

            var snapshot = new NpcDebugSnapshot
            {
                GameObjectName = npc.gameObject.name,
                CharacterDesign = npc.CharacterDesign,
                IsReady = npc.IsReady,
                IsTalking = npc.IsTalking,
                HasActions = npc.HasEnabledActions,
                HistoryLength = npc.GetHistoryLength(),
                History = npc.GetHistory(),
            };

            // Collect memories
            var memoryNames = npc.GetMemoryNames();
            foreach (var name in memoryNames)
            {
                snapshot.Memories[name] = npc.GetMemory(name);
            }

            // Context manager state
            var ctx = AIContextManager.Instance;
            if (ctx != null)
            {
                snapshot.IsEligibleForCompaction = ctx.IsEligibleForCompaction(npc);
                var convState = ctx.GetNpcConversationState(npc);
                if (convState != null)
                {
                    snapshot.CompactionCount = convState.CompactionCount;
                    snapshot.LastConversationTime = convState.LastConversationTime;
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Get debug snapshots for all active NPCs.
        /// </summary>
        public static NpcDebugSnapshot[] GetAllSnapshots()
        {
            return GetAllNpcs().Select(GetSnapshot).Where(s => s != null).ToArray();
        }

        /// <summary>
        /// Get a formatted debug string for a single NPC.
        /// Useful for console logging or runtime debug UIs.
        /// </summary>
        public static string FormatSnapshot(NpcDebugSnapshot snapshot)
        {
            if (snapshot == null) return "(null)";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== {snapshot.GameObjectName} ===");
            sb.AppendLine($"Ready: {snapshot.IsReady} | Talking: {snapshot.IsTalking} | Actions: {snapshot.HasActions}");

            if (!string.IsNullOrEmpty(snapshot.CharacterDesign))
            {
                var preview = snapshot.CharacterDesign.Length > 100
                    ? snapshot.CharacterDesign.Substring(0, 100) + "..."
                    : snapshot.CharacterDesign;
                sb.AppendLine($"Character Design: {preview}");
            }

            sb.AppendLine($"--- Memories ({snapshot.Memories.Count}) ---");
            foreach (var kvp in snapshot.Memories)
            {
                sb.AppendLine($"  [{kvp.Key}]: {kvp.Value}");
            }

            sb.AppendLine($"--- Conversation ({snapshot.HistoryLength} messages) ---");
            foreach (var msg in snapshot.History)
            {
                var content = msg.Content ?? "";
                var preview = content.Length > 80 ? content.Substring(0, 80) + "..." : content;
                sb.AppendLine($"  [{msg.Role}]: {preview}");
            }

            if (snapshot.LastConversationTime.HasValue)
            {
                sb.AppendLine($"Last conversation: {snapshot.LastConversationTime.Value:HH:mm:ss} | Compactions: {snapshot.CompactionCount} | Eligible: {snapshot.IsEligibleForCompaction}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Dump all NPCs' debug info to Unity console.
        /// </summary>
        public static void LogAll()
        {
            var snapshots = GetAllSnapshots();
            if (snapshots.Length == 0)
            {
                Debug.Log("[PlayKit Debugger] No active NPCs found.");
                return;
            }

            foreach (var snapshot in snapshots)
            {
                Debug.Log(FormatSnapshot(snapshot));
            }
        }
    }
}
