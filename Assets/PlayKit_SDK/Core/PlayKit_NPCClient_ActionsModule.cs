using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayKit_SDK.Public;
using UnityEngine;

namespace PlayKit_SDK
{
    /// <summary>
    /// Actions module for NPC Client
    /// Provides Inspector-friendly action configuration with UnityEvent callbacks
    /// Automatically integrates with PlayKit_NPCClient on the same GameObject
    /// </summary>
    public class PlayKit_NPCClient_ActionsModule : MonoBehaviour
    {
        [Header("Action Configuration")]
        [Tooltip("List of actions this NPC can perform")]
        [SerializeField] private List<NpcActionBinding> actionBindings = new List<NpcActionBinding>();

        [Header("Debug Options")]
        [Tooltip("Log action calls to console")]
        [SerializeField] private bool logActionCalls = true;

        [Tooltip("Auto-report success for actions that complete without error")]
        [SerializeField] private bool autoReportSuccess = true;

        private PlayKit_NPCClient _npcClient;
        private bool _isReady;

        /// <summary>
        /// Whether the actions module is ready to use
        /// </summary>
        public bool IsReady => _isReady;

        /// <summary>
        /// Whether the module is currently processing a request
        /// </summary>
        public bool IsProcessing { get; private set; }

        /// <summary>
        /// Get all action bindings (for runtime inspection/modification)
        /// </summary>
        public List<NpcActionBinding> ActionBindings => actionBindings;

        /// <summary>
        /// Get all enabled actions as NpcAction list
        /// </summary>
        public List<NpcAction> EnabledActions => actionBindings
            .Where(b => b != null && b.action != null && b.action.enabled)
            .Select(b => b.action)
            .ToList();

        private void Start()
        {
            Initialize().Forget();
        }

        private async UniTask Initialize()
        {
            // Wait for SDK to be ready
            await UniTask.WaitUntil(() => PlayKit_SDK.IsReady());

            // Auto-find NPCClient on the same GameObject
            _npcClient = GetComponent<PlayKit_NPCClient>();
            if (_npcClient == null)
            {
                Debug.LogError("[ActionsModule] No PlayKit_NPCClient found on this GameObject! Actions module requires PlayKit_NPCClient component.");
                return;
            }

            // Wait for NPCClient to be ready
            await UniTask.WaitUntil(() => _npcClient.IsReady);

            _isReady = true;
            Debug.Log($"[ActionsModule] Ready! {actionBindings.Count} action(s) configured for NPC '{gameObject.name}'");
        }

        #region Public API

        /// <summary>
        /// Talk to NPC with configured actions (non-streaming).
        /// When actions are triggered, the corresponding UnityEvent callbacks are invoked.
        /// </summary>
        /// <param name="message">The message to send to the NPC</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>NPC's text response, or null if failed</returns>
        public async UniTask<string> TalkWithActions(
            string message,
            CancellationToken? cancellationToken = null)
        {
            if (!ValidateReadyState()) return null;

            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            IsProcessing = true;

            try
            {
                var response = await _npcClient.TalkWithActions(message, EnabledActions, token);

                if (response == null)
                {
                    return null;
                }

                // Process action calls via UnityEvent bindings
                if (response.HasActions)
                {
                    ProcessActionCalls(response.ActionCalls);
                }

                return response.Text;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActionsModule] TalkWithActions failed: {ex.Message}");
                return null;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// Talk to NPC with configured actions (streaming).
        /// Text streams first, then action callbacks are invoked when complete.
        /// </summary>
        /// <param name="message">The message to send to the NPC</param>
        /// <param name="onChunk">Callback for each text chunk as it streams in</param>
        /// <param name="onComplete">Callback when complete (text response)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        public async UniTask TalkWithActionsStream(
            string message,
            Action<string> onChunk,
            Action<string> onComplete,
            CancellationToken? cancellationToken = null)
        {
            if (!ValidateReadyState())
            {
                onComplete?.Invoke(null);
                return;
            }

            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            IsProcessing = true;

            try
            {
                await _npcClient.TalkWithActionsStream(
                    message,
                    EnabledActions,
                    onChunk,
                    response =>
                    {
                        IsProcessing = false;

                        if (response != null)
                        {
                            // Process action calls via UnityEvent bindings
                            if (response.HasActions)
                            {
                                ProcessActionCalls(response.ActionCalls);
                            }

                            onComplete?.Invoke(response.Text);
                        }
                        else
                        {
                            onComplete?.Invoke(null);
                        }
                    },
                    token
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActionsModule] TalkWithActionsStream failed: {ex.Message}");
                IsProcessing = false;
                onComplete?.Invoke(null);
            }
        }

        /// <summary>
        /// Report action result back to NPC for continued conversation.
        /// Call this after your action handler completes.
        /// </summary>
        /// <param name="callId">The action call ID from NpcActionCallArgs</param>
        /// <param name="result">Result of the action execution</param>
        public void ReportActionResult(string callId, string result)
        {
            _npcClient?.ReportActionResult(callId, result);
        }

        /// <summary>
        /// Continue the conversation after actions have been executed and results reported.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>NPC's follow-up response</returns>
        public async UniTask<string> ContinueAfterActions(CancellationToken? cancellationToken = null)
        {
            if (!ValidateReadyState()) return null;

            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            IsProcessing = true;

            try
            {
                // Continue with empty message - the tool results are already in history
                var response = await _npcClient.TalkWithActions("", EnabledActions, token);
                return response?.Text;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActionsModule] ContinueAfterActions failed: {ex.Message}");
                return null;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        #endregion

        #region Action Management

        /// <summary>
        /// Add a new action at runtime
        /// </summary>
        /// <param name="action">The action definition</param>
        /// <param name="callback">The callback to invoke when action is triggered</param>
        /// <returns>The created binding for further configuration</returns>
        public NpcActionBinding AddAction(NpcAction action, UnityEngine.Events.UnityAction<NpcActionCallArgs> callback = null)
        {
            var binding = new NpcActionBinding
            {
                action = action
            };

            if (callback != null)
            {
                binding.onTriggered.AddListener(callback);
            }

            actionBindings.Add(binding);
            Debug.Log($"[ActionsModule] Added action: {action.actionName}");

            return binding;
        }

        /// <summary>
        /// Remove an action by name
        /// </summary>
        /// <param name="actionName">Name of the action to remove</param>
        /// <returns>True if removed, false if not found</returns>
        public bool RemoveAction(string actionName)
        {
            var binding = actionBindings.FirstOrDefault(b => b.action?.actionName == actionName);
            if (binding != null)
            {
                actionBindings.Remove(binding);
                Debug.Log($"[ActionsModule] Removed action: {actionName}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get an action binding by name
        /// </summary>
        /// <param name="actionName">Name of the action</param>
        /// <returns>The binding, or null if not found</returns>
        public NpcActionBinding GetAction(string actionName)
        {
            return actionBindings.FirstOrDefault(b => b.action?.actionName == actionName);
        }

        /// <summary>
        /// Enable or disable an action
        /// </summary>
        /// <param name="actionName">Name of the action</param>
        /// <param name="enabled">Whether to enable or disable</param>
        public void SetActionEnabled(string actionName, bool enabled)
        {
            var binding = GetAction(actionName);
            if (binding?.action != null)
            {
                binding.action.enabled = enabled;
                Debug.Log($"[ActionsModule] Action '{actionName}' {(enabled ? "enabled" : "disabled")}");
            }
        }

        /// <summary>
        /// Clear all actions
        /// </summary>
        public void ClearActions()
        {
            actionBindings.Clear();
            Debug.Log("[ActionsModule] All actions cleared");
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Process action calls by invoking the corresponding UnityEvent callbacks
        /// </summary>
        private void ProcessActionCalls(List<NpcActionCall> actionCalls)
        {
            if (actionCalls == null) return;

            foreach (var call in actionCalls)
            {
                var binding = actionBindings.FirstOrDefault(b =>
                    b.action?.actionName == call.ActionName && b.action.enabled);

                if (binding != null)
                {
                    if (logActionCalls)
                    {
                        Debug.Log($"[ActionsModule] Invoking action: {call.ActionName} (ID: {call.Id})");
                    }

                    try
                    {
                        // Create NpcActionCallArgs from the action call
                        var args = CreateActionCallArgs(call);
                        binding.onTriggered?.Invoke(args);

                        // Auto-report success if configured
                        if (autoReportSuccess)
                        {
                            ReportActionResult(call.Id, "success");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ActionsModule] Error invoking action '{call.ActionName}': {ex.Message}");
                        ReportActionResult(call.Id, $"error: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[ActionsModule] No binding found for action: {call.ActionName}");
                }
            }
        }

        /// <summary>
        /// Create NpcActionCallArgs from NpcActionCall
        /// </summary>
        private NpcActionCallArgs CreateActionCallArgs(NpcActionCall call)
        {
            // Create a mock ChatToolCall to construct NpcActionCallArgs
            var toolCall = new Provider.AI.ChatToolCall
            {
                Id = call.Id,
                Type = "function",
                Function = new Provider.AI.ChatToolCallFunction
                {
                    Name = call.ActionName,
                    Arguments = call.Arguments?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}"
                }
            };

            return new NpcActionCallArgs(toolCall);
        }

        /// <summary>
        /// Validate that the module is ready
        /// </summary>
        private bool ValidateReadyState()
        {
            if (!_isReady)
            {
                Debug.LogError("[ActionsModule] Actions module is not ready yet. Wait for initialization.");
                return false;
            }

            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError("[ActionsModule] GameObject is not active");
                return false;
            }

            return true;
        }

        #endregion

        #region Accessors

        /// <summary>
        /// Get the associated NPCClient
        /// </summary>
        public PlayKit_NPCClient GetNPCClient()
        {
            return _npcClient;
        }

        #endregion
    }
}
