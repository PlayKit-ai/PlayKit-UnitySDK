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

        private Dictionary<PlayKit_NPC, NpcConversationState> _npcStates = new Dictionary<PlayKit_NPC, NpcConversationState>();
        private Coroutine _autoCompactCoroutine;

        // Track which NPCs are currently being compacted to prevent concurrent compaction
        private readonly HashSet<PlayKit_NPC> _compactingNpcs = new HashSet<PlayKit_NPC>();

        /// <summary>
        /// Event fired when an NPC is registered for context management.
        /// </summary>
        public event Action<PlayKit_NPC> OnNpcRegistered;

        /// <summary>
        /// Event fired when an NPC is unregistered (destroyed).
        /// </summary>
        public event Action<PlayKit_NPC> OnNpcUnregistered;

        /// <summary>
        /// Register an NPC for context management.
        /// Called automatically by NPCClient.
        /// </summary>
        internal void RegisterNpc(PlayKit_NPC npc)
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
                OnNpcRegistered?.Invoke(npc);
            }
        }

        /// <summary>
        /// Get the conversation state for a specific NPC (for debugging/tooling).
        /// Returns null if the NPC is not registered.
        /// </summary>
        public NpcConversationState GetNpcConversationState(PlayKit_NPC npc)
        {
            if (npc == null) return null;
            return _npcStates.TryGetValue(npc, out var state) ? state : null;
        }

        /// <summary>
        /// Unregister an NPC (called on destroy).
        /// </summary>
        internal void UnregisterNpc(PlayKit_NPC npc)
        {
            if (npc == null) return;

            if (_npcStates.ContainsKey(npc))
            {
                _npcStates.Remove(npc);
                _compactingNpcs.Remove(npc);
                Debug.Log($"[AIContextManager] NPC unregistered: {npc.gameObject.name}");
                OnNpcUnregistered?.Invoke(npc);
            }
        }

        /// <summary>
        /// Update last conversation time for an NPC.
        /// Called after each Talk() exchange.
        /// </summary>
        internal void RecordConversation(PlayKit_NPC npc)
        {
            if (npc == null) return;

            if (!_npcStates.ContainsKey(npc))
            {
                RegisterNpc(npc);
            }

            _npcStates[npc].LastConversationTime = DateTime.UtcNow;
            _npcStates[npc].IsCompacted = false; // Reset compaction flag on new conversation
        }

        /// <summary>
        /// Remove any NPC entries whose GameObjects have been destroyed.
        /// Called automatically before iteration to prevent MissingReferenceException.
        /// </summary>
        private void PurgeDestroyedNpcs()
        {
            // Collect destroyed keys first to avoid modifying dictionary during iteration
            List<PlayKit_NPC> destroyed = null;
            foreach (var npc in _npcStates.Keys)
            {
                if (npc == null)
                {
                    destroyed ??= new List<PlayKit_NPC>();
                    destroyed.Add(npc);
                }
            }

            if (destroyed == null) return;

            foreach (var npc in destroyed)
            {
                _npcStates.Remove(npc);
                _compactingNpcs.Remove(npc);
            }

            Debug.Log($"[AIContextManager] Purged {destroyed.Count} destroyed NPC(s)");
        }

        #endregion

        #region AutoCompact

        /// <summary>
        /// Event fired when an NPC's conversation is compacted.
        /// </summary>
        public event Action<PlayKit_NPC> OnNpcCompacted;

        /// <summary>
        /// Event fired when compaction fails for an NPC.
        /// </summary>
        public event Action<PlayKit_NPC, string> OnCompactionFailed;

        /// <summary>
        /// Check if an NPC is eligible for compaction.
        /// </summary>
        /// <param name="npc">The NPC to check</param>
        /// <returns>True if eligible for compaction</returns>
        public bool IsEligibleForCompaction(PlayKit_NPC npc)
        {
            if (npc == null) return false;
            if (!_npcStates.TryGetValue(npc, out var state)) return false;

            var settings = PlayKitSettings.Instance;
            if (settings == null || !settings.EnableAutoCompact) return false;

            // Check if already compacted since last conversation
            if (state.IsCompacted) return false;

            // Check if currently being compacted
            if (_compactingNpcs.Contains(npc)) return false;

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
        public async UniTask<bool> CompactConversationAsync(PlayKit_NPC npc, CancellationToken cancellationToken = default)
        {
            if (npc == null)
            {
                Debug.LogWarning("[AIContextManager] Cannot compact: NPC is null");
                return false;
            }

            // Prevent concurrent compaction on the same NPC
            if (!_compactingNpcs.Add(npc))
            {
                Debug.Log($"[AIContextManager] Skipping compaction for {npc.gameObject.name}: already in progress");
                return false;
            }

            try
            {
                var history = npc.GetHistory();
                var nonSystemMessages = history.Where(m => m.Role != "system").ToList();

                if (nonSystemMessages.Count < 2)
                {
                    Debug.Log($"[AIContextManager] Skipping compaction for {npc.gameObject.name}: not enough messages");
                    return false;
                }

                Debug.Log($"[AIContextManager] Starting compaction for {npc.gameObject.name} ({nonSystemMessages.Count} messages)");

                // Build conversation text for summarization
                var conversationText = string.Join("\n", nonSystemMessages.Select(m => $"{m.Role}: {m.Content}"));

                // Include character context and previous summary for better quality
                var contextPrefix = "";
                var characterDesign = npc.CharacterDesign;
                if (!string.IsNullOrEmpty(characterDesign))
                {
                    var designPreview = characterDesign.Length > 200
                        ? characterDesign.Substring(0, 200) + "..."
                        : characterDesign;
                    contextPrefix += $"Character context: {designPreview}\n\n";
                }

                // Preserve previous summaries to avoid losing long-term context
                var previousSummary = npc.GetMemory("PreviousConversationSummary");
                if (!string.IsNullOrEmpty(previousSummary))
                {
                    contextPrefix += $"Previous conversation summary: {previousSummary}\n\n";
                }

                // Create summarization prompt
                var summaryPrompt = $@"{contextPrefix}Summarize the following conversation concisely. Focus on:
1. Key topics discussed
2. Important information exchanged
3. Any decisions or commitments made
4. The emotional tone
5. Any state changes or persuasion progress

If there is a previous conversation summary above, incorporate its key points into your new summary so no important context is lost.

Keep the summary under 300 words. Write in third person.

Conversation:
{conversationText}";

                // Use fast model for summarization
                var settings = PlayKitSettings.Instance;
                var chatClient = PlayKitSDK.Factory.CreateChatClient(settings?.FastModel ?? "default-chat-fast");

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

                // Clear history and rebuild with summary
                npc.ClearHistory();

                // Add summary as a memory (replaces previous summary with accumulated context)
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
            finally
            {
                _compactingNpcs.Remove(npc);
            }
        }

        /// <summary>
        /// Compact all registered NPCs that meet the eligibility criteria.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        public async UniTask CompactAllEligibleAsync(CancellationToken cancellationToken = default)
        {
            PurgeDestroyedNpcs();

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

                // Fix: correct nullable bool check (was: !x == true which has wrong precedence)
                if (PlayKitSettings.Instance?.EnableAutoCompact != true)
                    continue;

                // Purge any destroyed NPCs before iterating
                PurgeDestroyedNpcs();

                // Check for eligible NPCs
                var eligibleNpcs = _npcStates.Keys.Where(IsEligibleForCompaction).ToList();

                foreach (var npc in eligibleNpcs)
                {
                    // Fire and forget compaction (concurrent guard is inside CompactConversationAsync)
                    CompactConversationAsync(npc, this.GetCancellationTokenOnDestroy()).Forget();
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// State tracking for each NPC's conversation.
    /// </summary>
    public class NpcConversationState
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
