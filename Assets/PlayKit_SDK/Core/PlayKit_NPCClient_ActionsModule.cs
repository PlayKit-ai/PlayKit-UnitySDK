using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using PlayKit_SDK.Public;
using UnityEngine;

namespace PlayKit_SDK
{
    /// <summary>
    /// Actions module for NPC Client.
    /// Provides Inspector-friendly action configuration with UnityEvent callbacks.
    /// Automatically integrates with PlayKit_NPCClient on the same GameObject.
    ///
    /// Usage:
    /// 1. Add this component to same GameObject as PlayKit_NPCClient
    /// 2. Configure actions in Inspector with UnityEvent callbacks
    /// 3. Call npcClient.Talk() - actions are used automatically when enabled
    /// 4. Or subscribe to npcClient.OnActionTriggered event for code-based handling
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
        /// Get all action bindings (for runtime inspection/modification)
        /// </summary>
        public List<NpcActionBinding> ActionBindings => actionBindings;

        /// <summary>
        /// Get all enabled actions as NpcAction list.
        /// Returns empty list if no actions are enabled.
        /// </summary>
        public List<NpcAction> EnabledActions => actionBindings
            .Where(b => b != null && b.action != null && b.action.enabled)
            .Select(b => b.action)
            .ToList();

        /// <summary>
        /// Check if any actions are currently enabled
        /// </summary>
        public bool HasEnabledActions => EnabledActions.Count > 0;

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

        #region Internal - Called by NPCClient

        /// <summary>
        /// Handle an action call from NPCClient.
        /// This invokes the corresponding UnityEvent callback.
        /// </summary>
        internal void HandleActionCall(NpcActionCallArgs args)
        {
            if (args == null) return;

            var binding = actionBindings.FirstOrDefault(b =>
                b.action?.actionName == args.ActionName && b.action.enabled);

            if (binding != null)
            {
                if (logActionCalls)
                {
                    Debug.Log($"[ActionsModule] Invoking action: {args.ActionName} (ID: {args.CallId})");
                }

                try
                {
                    binding.onTriggered?.Invoke(args);

                    // Auto-report success if configured
                    if (autoReportSuccess)
                    {
                        _npcClient?.ReportActionResult(args.CallId, "success");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ActionsModule] Error invoking action '{args.ActionName}': {ex.Message}");
                    _npcClient?.ReportActionResult(args.CallId, $"error: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[ActionsModule] No binding found for action: {args.ActionName}");
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
        /// Enable all actions
        /// </summary>
        public void EnableAllActions()
        {
            foreach (var binding in actionBindings)
            {
                if (binding?.action != null)
                {
                    binding.action.enabled = true;
                }
            }
            Debug.Log($"[ActionsModule] All actions enabled");
        }

        /// <summary>
        /// Disable all actions
        /// </summary>
        public void DisableAllActions()
        {
            foreach (var binding in actionBindings)
            {
                if (binding?.action != null)
                {
                    binding.action.enabled = false;
                }
            }
            Debug.Log($"[ActionsModule] All actions disabled");
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

        #region Accessors

        /// <summary>
        /// Get the associated NPCClient
        /// </summary>
        public PlayKit_NPCClient GetNPCClient()
        {
            return _npcClient;
        }

        /// <summary>
        /// Report action result back to NPC for continued conversation.
        /// Use this if autoReportSuccess is disabled and you need manual control.
        /// </summary>
        /// <param name="callId">The action call ID from NpcActionCallArgs</param>
        /// <param name="result">Result of the action execution</param>
        public void ReportActionResult(string callId, string result)
        {
            _npcClient?.ReportActionResult(callId, result);
        }

        #endregion
    }
}
