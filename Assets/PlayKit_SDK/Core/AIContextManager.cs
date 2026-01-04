using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayKit_SDK.Public;
using UnityEngine;

namespace PlayKit_SDK
{
    /// <summary>
    /// Global AI Context Manager for managing NPC conversations and player context.
    /// Automatically created with PlayKit_SDK instance.
    ///
    /// Features:
    /// - Player description management (WhoIsPlayer)
    /// - NPC conversation tracking
    /// - Automatic conversation compaction (AutoCompact)
    /// </summary>
    public class AIContextManager : MonoBehaviour
    {
        #region Singleton

        private static AIContextManager _instance;

        /// <summary>
        /// Gets the singleton instance of AIContextManager.
        /// </summary>
        public static AIContextManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.LogWarning("[AIContextManager] Instance not initialized. Make sure PlayKit_SDK is initialized.");
                }
                return _instance;
            }
        }

        #endregion

        #region Player Description (WhoIsPlayer)

        private string _playerDescription;

        /// <summary>
        /// Set the player's description for AI context.
        /// Used when generating reply predictions.
        /// </summary>
        /// <param name="description">Description of the player character</param>
        public void SetPlayerDescription(string description)
        {
            _playerDescription = description;
            Debug.Log($"[AIContextManager] Player description set: {(description?.Length > 50 ? description.Substring(0, 50) + "..." : description)}");
        }

        /// <summary>
        /// Get the current player description.
        /// </summary>
        /// <returns>The player description, or null if not set</returns>
        public string GetPlayerDescription()
        {
            return _playerDescription;
        }

        /// <summary>
        /// Clear the player description.
        /// </summary>
        public void ClearPlayerDescription()
        {
            _playerDescription = null;
            Debug.Log("[AIContextManager] Player description cleared");
        }

        #endregion

        #region NPC Tracking

        private Dictionary<PlayKit_NPCClient, NpcConversationState> _npcStates = new Dictionary<PlayKit_NPCClient, NpcConversationState>();
        private Coroutine _autoCompactCoroutine;

        /// <summary>
        /// Register an NPC for context management.
        /// Called automatically by NPCClient.
        /// </summary>
        internal void RegisterNpc(PlayKit_NPCClient npc)
        {
            if (npc == null) return;

            if (!_npcStates.ContainsKey(npc))
            {
                _npcStates[npc] = new NpcConversationState
                {
                    LastConversationTime = DateTime.UtcNow,
                    IsCompacted = false,
                    CompactionCount = 0
                };
                Debug.Log($"[AIContextManager] NPC registered: {npc.gameObject.name}");
            }
        }

        /// <summary>
        /// Unregister an NPC (called on destroy).
        /// </summary>
        internal void UnregisterNpc(PlayKit_NPCClient npc)
        {
            if (npc == null) return;

            if (_npcStates.ContainsKey(npc))
            {
                _npcStates.Remove(npc);
                Debug.Log($"[AIContextManager] NPC unregistered: {npc.gameObject.name}");
            }
        }

        /// <summary>
        /// Update last conversation time for an NPC.
        /// Called after each Talk() exchange.
        /// </summary>
        internal void RecordConversation(PlayKit_NPCClient npc)
        {
            if (npc == null) return;

            if (!_npcStates.ContainsKey(npc))
            {
                RegisterNpc(npc);
            }

            _npcStates[npc].LastConversationTime = DateTime.UtcNow;
            _npcStates[npc].IsCompacted = false; // Reset compaction flag on new conversation
        }

        #endregion

        #region AutoCompact

        /// <summary>
        /// Event fired when an NPC's conversation is compacted.
        /// </summary>
        public event Action<PlayKit_NPCClient> OnNpcCompacted;

        /// <summary>
        /// Event fired when compaction fails for an NPC.
        /// </summary>
        public event Action<PlayKit_NPCClient, string> OnCompactionFailed;

        /// <summary>
        /// Check if an NPC is eligible for compaction.
        /// </summary>
        /// <param name="npc">The NPC to check</param>
        /// <returns>True if eligible for compaction</returns>
        public bool IsEligibleForCompaction(PlayKit_NPCClient npc)
        {
            if (npc == null) return false;
            if (!_npcStates.TryGetValue(npc, out var state)) return false;

            var settings = PlayKitSettings.Instance;
            if (settings == null || !settings.EnableAutoCompact) return false;

            // Check if already compacted since last conversation
            if (state.IsCompacted) return false;

            // Check message count
            var history = npc.GetHistory();
            var nonSystemMessages = history.Count(m => m.Role != "system");
            if (nonSystemMessages < settings.AutoCompactMinMessages) return false;

            // Check time since last conversation
            var timeSinceLastConversation = (DateTime.UtcNow - state.LastConversationTime).TotalSeconds;
            if (timeSinceLastConversation < settings.AutoCompactTimeoutSeconds) return false;

            return true;
        }

        /// <summary>
        /// Manually trigger conversation compaction for a specific NPC.
        /// </summary>
        /// <param name="npc">The NPC to compact</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if compaction succeeded</returns>
        public async UniTask<bool> CompactConversationAsync(PlayKit_NPCClient npc, CancellationToken cancellationToken = default)
        {
            if (npc == null)
            {
                Debug.LogWarning("[AIContextManager] Cannot compact: NPC is null");
                return false;
            }

            var history = npc.GetHistory();
            var nonSystemMessages = history.Where(m => m.Role != "system").ToList();

            if (nonSystemMessages.Count < 2)
            {
                Debug.Log($"[AIContextManager] Skipping compaction for {npc.gameObject.name}: not enough messages");
                return false;
            }

            try
            {
                Debug.Log($"[AIContextManager] Starting compaction for {npc.gameObject.name} ({nonSystemMessages.Count} messages)");

                // Build conversation text for summarization
                var conversationText = string.Join("\n", nonSystemMessages.Select(m => $"{m.Role}: {m.Content}"));

                // Create summarization prompt
                var summaryPrompt = $@"Summarize the following conversation concisely. Focus on:
1. Key topics discussed
2. Important information exchanged
3. Any decisions or commitments made
4. The emotional tone

Keep the summary under 200 words. Write in third person.

Conversation:
{conversationText}";

                // Use fast model for summarization
                var settings = PlayKitSettings.Instance;
                var chatClient = PlayKit_SDK.Factory.CreateChatClient(settings?.FastModel ?? "default-chat-fast");

                var config = new PlayKit_ChatConfig(new List<PlayKit_ChatMessage>
                {
                    new PlayKit_ChatMessage { Role = "user", Content = summaryPrompt }
                });

                var result = await chatClient.TextGenerationAsync(config, cancellationToken);

                if (!result.Success || string.IsNullOrEmpty(result.Response))
                {
                    var error = result.ErrorMessage ?? "Unknown error";
                    Debug.LogError($"[AIContextManager] Compaction failed for {npc.gameObject.name}: {error}");
                    OnCompactionFailed?.Invoke(npc, error);
                    return false;
                }

                // Get the character design (will be preserved)
                var characterDesign = npc.CharacterDesign;

                // Clear history and rebuild with summary
                npc.ClearHistory();

                // Add summary as a memory
                npc.SetMemory("PreviousConversationSummary", result.Response);

                // Update state
                if (_npcStates.TryGetValue(npc, out var state))
                {
                    state.IsCompacted = true;
                    state.CompactionCount++;
                }

                Debug.Log($"[AIContextManager] Compaction completed for {npc.gameObject.name}. Summary: {result.Response.Substring(0, Math.Min(100, result.Response.Length))}...");
                OnNpcCompacted?.Invoke(npc);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIContextManager] Compaction error for {npc.gameObject.name}: {ex.Message}");
                OnCompactionFailed?.Invoke(npc, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Compact all registered NPCs that meet the eligibility criteria.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        public async UniTask CompactAllEligibleAsync(CancellationToken cancellationToken = default)
        {
            var eligibleNpcs = _npcStates.Keys.Where(IsEligibleForCompaction).ToList();

            if (eligibleNpcs.Count == 0)
            {
                Debug.Log("[AIContextManager] No NPCs eligible for compaction");
                return;
            }

            Debug.Log($"[AIContextManager] Compacting {eligibleNpcs.Count} eligible NPCs");

            foreach (var npc in eligibleNpcs)
            {
                if (cancellationToken.IsCancellationRequested) break;
                await CompactConversationAsync(npc, cancellationToken);
            }
        }

        #endregion

        #region Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // Silently destroy duplicate - this can happen during normal initialization
                Destroy(this);
                return;
            }

            _instance = this;
            // Don't log initialization message to reduce console noise
        }

        private void Start()
        {
            // Start auto-compact check coroutine
            if (PlayKitSettings.Instance?.EnableAutoCompact == true)
            {
                StartAutoCompactCheck();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            if (_autoCompactCoroutine != null)
            {
                StopCoroutine(_autoCompactCoroutine);
                _autoCompactCoroutine = null;
            }
        }

        private void StartAutoCompactCheck()
        {
            if (_autoCompactCoroutine != null)
            {
                StopCoroutine(_autoCompactCoroutine);
            }
            _autoCompactCoroutine = StartCoroutine(AutoCompactCheckRoutine());
        }

        private IEnumerator AutoCompactCheckRoutine()
        {
            const float checkIntervalSeconds = 60f;

            while (true)
            {
                yield return new WaitForSeconds(checkIntervalSeconds);

                if (!PlayKitSettings.Instance?.EnableAutoCompact == true)
                    continue;

                // Check for eligible NPCs
                var eligibleNpcs = _npcStates.Keys.Where(IsEligibleForCompaction).ToList();

                foreach (var npc in eligibleNpcs)
                {
                    // Fire and forget compaction
                    CompactConversationAsync(npc, this.GetCancellationTokenOnDestroy()).Forget();
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Internal state tracking for each NPC's conversation.
    /// </summary>
    internal class NpcConversationState
    {
        /// <summary>
        /// The last time a conversation exchange occurred with this NPC.
        /// </summary>
        public DateTime LastConversationTime { get; set; }

        /// <summary>
        /// Whether the conversation has been compacted since the last exchange.
        /// </summary>
        public bool IsCompacted { get; set; }

        /// <summary>
        /// Number of times this NPC's conversation has been compacted.
        /// </summary>
        public int CompactionCount { get; set; }
    }
}
