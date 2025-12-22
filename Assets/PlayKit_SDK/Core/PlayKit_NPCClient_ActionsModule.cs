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
    /// Also supports interface-based action handlers (INpcActionHandler / INpcActionHandlerAsync).
    /// Automatically integrates with PlayKit_NPCClient on the same GameObject.
    ///
    /// Usage:
    /// 1. Add this component to same GameObject as PlayKit_NPCClient
    /// 2. Configure actions in Inspector with UnityEvent callbacks
    ///    OR implement INpcActionHandler/INpcActionHandlerAsync on child components
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

        // Interface-based handlers (auto-discovered or manually registered)
        // Maps actionName -> handler for quick lookup
        private readonly Dictionary<string, INpcActionHandler> _syncHandlerMap = new Dictionary<string, INpcActionHandler>();
        private readonly Dictionary<string, INpcActionHandlerAsync> _asyncHandlerMap = new Dictionary<string, INpcActionHandlerAsync>();
        // Keep references to registered handlers for cleanup
        private readonly List<INpcActionHandler> _syncHandlers = new List<INpcActionHandler>();
        private readonly List<INpcActionHandlerAsync> _asyncHandlers = new List<INpcActionHandlerAsync>();

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
        /// Includes both Inspector-configured actions and interface-based handlers.
        /// Returns empty list if no actions are enabled.
        /// </summary>
        public List<NpcAction> EnabledActions
        {
            get
            {
                var actions = new List<NpcAction>();

                // Add Inspector-configured actions
                actions.AddRange(actionBindings
                    .Where(b => b != null && b.action != null && b.action.enabled)
                    .Select(b => b.action));

                // Add sync handler actions
                foreach (var handler in _syncHandlers)
                {
                    var defs = handler?.ActionDefinitions;
                    if (defs == null) continue;
                    foreach (var def in defs)
                    {
                        if (def != null && def.enabled)
                        {
                            actions.Add(def);
                        }
                    }
                }

                // Add async handler actions
                foreach (var handler in _asyncHandlers)
                {
                    var defs = handler?.ActionDefinitions;
                    if (defs == null) continue;
                    foreach (var def in defs)
                    {
                        if (def != null && def.enabled)
                        {
                            actions.Add(def);
                        }
                    }
                }

                return actions;
            }
        }

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

            // Auto-discover interface-based handlers on this GameObject and children
            DiscoverHandlers();

            _isReady = true;

            var totalActions = EnabledActions.Count;
            Debug.Log($"[ActionsModule] Ready! {totalActions} action(s) configured for NPC '{gameObject.name}' " +
                      $"(Inspector: {actionBindings.Count}, Sync handlers: {_syncHandlers.Count}, Async handlers: {_asyncHandlers.Count})");
        }

        /// <summary>
        /// Discover and register INpcActionHandler/INpcActionHandlerAsync implementations
        /// on this GameObject and its children.
        /// </summary>
        private void DiscoverHandlers()
        {
            // Discover sync handlers
            var syncHandlers = GetComponentsInChildren<INpcActionHandler>();
            if (syncHandlers != null)
            {
                foreach (var handler in syncHandlers)
                {
                    if (handler == null) continue;

                    try
                    {
                        var defs = handler.ActionDefinitions;
                        if (defs == null || defs.Count == 0) continue;

                        // Register handler reference
                        _syncHandlers.Add(handler);

                        // Map each action to this handler
                        foreach (var def in defs)
                        {
                            if (def == null || string.IsNullOrEmpty(def.actionName)) continue;

                            if (_syncHandlerMap.ContainsKey(def.actionName) || _asyncHandlerMap.ContainsKey(def.actionName))
                            {
                                Debug.LogWarning($"[ActionsModule] Duplicate action '{def.actionName}', skipping.");
                                continue;
                            }
                            _syncHandlerMap[def.actionName] = handler;
                            if (logActionCalls)
                            {
                                Debug.Log($"[ActionsModule] Discovered sync action: {def.actionName}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ActionsModule] Error discovering sync handler: {ex.Message}");
                    }
                }
            }

            // Discover async handlers
            var asyncHandlers = GetComponentsInChildren<INpcActionHandlerAsync>();
            if (asyncHandlers != null)
            {
                foreach (var handler in asyncHandlers)
                {
                    if (handler == null) continue;

                    try
                    {
                        var defs = handler.ActionDefinitions;
                        if (defs == null || defs.Count == 0) continue;

                        // Register handler reference
                        _asyncHandlers.Add(handler);

                        // Map each action to this handler
                        foreach (var def in defs)
                        {
                            if (def == null || string.IsNullOrEmpty(def.actionName)) continue;

                            if (_syncHandlerMap.ContainsKey(def.actionName) || _asyncHandlerMap.ContainsKey(def.actionName))
                            {
                                Debug.LogWarning($"[ActionsModule] Duplicate action '{def.actionName}', skipping.");
                                continue;
                            }
                            _asyncHandlerMap[def.actionName] = handler;
                            if (logActionCalls)
                            {
                                Debug.Log($"[ActionsModule] Discovered async action: {def.actionName}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ActionsModule] Error discovering async handler: {ex.Message}");
                    }
                }
            }
        }

        #region Internal - Called by NPCClient

        /// <summary>
        /// Handle an action call from NPCClient (sync version).
        /// Priority: Async handler → Sync handler → UnityEvent binding.
        /// </summary>
        internal void HandleActionCall(NpcActionCallArgs args)
        {
            HandleActionCallAsync(args).Forget();
        }

        /// <summary>
        /// Handle an action call from NPCClient (async version).
        /// Priority: Async handler → Sync handler → UnityEvent binding.
        /// </summary>
        internal async UniTask HandleActionCallAsync(NpcActionCallArgs args)
        {
            if (args == null) return;

            var actionName = args.ActionName;

            if (logActionCalls)
            {
                Debug.Log($"[ActionsModule] Invoking action: {actionName} (ID: {args.CallId})");
            }

            try
            {
                string result = null;
                bool handled = false;

                // Priority 1: Check async handlers
                if (_asyncHandlerMap.TryGetValue(actionName, out var asyncHandler))
                {
                    result = await asyncHandler.ExecuteAsync(args);
                    handled = true;
                }
                // Priority 2: Check sync handlers
                else if (_syncHandlerMap.TryGetValue(actionName, out var syncHandler))
                {
                    result = syncHandler.Execute(args);
                    handled = true;
                }
                // Priority 3: Check UnityEvent bindings
                else
                {
                    var binding = actionBindings.FirstOrDefault(b =>
                        b.action?.actionName == actionName && b.action.enabled);

                    if (binding != null)
                    {
                        binding.onTriggered?.Invoke(args);
                        handled = true;
                        // UnityEvent doesn't return result, use auto-report if enabled
                        if (autoReportSuccess)
                        {
                            _npcClient?.ReportActionResult(args.CallId, "success");
                        }
                        return;
                    }
                }

                if (handled)
                {
                    // Report result from interface handler
                    if (autoReportSuccess)
                    {
                        _npcClient?.ReportActionResult(args.CallId, result ?? "success");
                    }
                }
                else
                {
                    Debug.LogWarning($"[ActionsModule] No handler found for action: {actionName}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActionsModule] Error invoking action '{actionName}': {ex.Message}");
                _npcClient?.ReportActionResult(args.CallId, $"error: {ex.Message}");
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

        #region Handler Registration

        /// <summary>
        /// Register a sync action handler at runtime.
        /// </summary>
        /// <param name="handler">The handler to register</param>
        /// <returns>Number of actions registered, 0 if failed</returns>
        public int RegisterHandler(INpcActionHandler handler)
        {
            if (handler == null) return 0;

            var defs = handler.ActionDefinitions;
            if (defs == null || defs.Count == 0)
            {
                Debug.LogWarning("[ActionsModule] Cannot register handler with no action definitions.");
                return 0;
            }

            int registered = 0;
            foreach (var def in defs)
            {
                if (def == null || string.IsNullOrEmpty(def.actionName)) continue;

                if (_syncHandlerMap.ContainsKey(def.actionName) || _asyncHandlerMap.ContainsKey(def.actionName))
                {
                    Debug.LogWarning($"[ActionsModule] Action '{def.actionName}' already exists, skipping.");
                    continue;
                }

                _syncHandlerMap[def.actionName] = handler;
                registered++;
                Debug.Log($"[ActionsModule] Registered sync action: {def.actionName}");
            }

            if (registered > 0 && !_syncHandlers.Contains(handler))
            {
                _syncHandlers.Add(handler);
            }

            return registered;
        }

        /// <summary>
        /// Register an async action handler at runtime.
        /// </summary>
        /// <param name="handler">The handler to register</param>
        /// <returns>Number of actions registered, 0 if failed</returns>
        public int RegisterHandler(INpcActionHandlerAsync handler)
        {
            if (handler == null) return 0;

            var defs = handler.ActionDefinitions;
            if (defs == null || defs.Count == 0)
            {
                Debug.LogWarning("[ActionsModule] Cannot register handler with no action definitions.");
                return 0;
            }

            int registered = 0;
            foreach (var def in defs)
            {
                if (def == null || string.IsNullOrEmpty(def.actionName)) continue;

                if (_syncHandlerMap.ContainsKey(def.actionName) || _asyncHandlerMap.ContainsKey(def.actionName))
                {
                    Debug.LogWarning($"[ActionsModule] Action '{def.actionName}' already exists, skipping.");
                    continue;
                }

                _asyncHandlerMap[def.actionName] = handler;
                registered++;
                Debug.Log($"[ActionsModule] Registered async action: {def.actionName}");
            }

            if (registered > 0 && !_asyncHandlers.Contains(handler))
            {
                _asyncHandlers.Add(handler);
            }

            return registered;
        }

        /// <summary>
        /// Unregister an action by action name.
        /// </summary>
        /// <param name="actionName">The action name to unregister</param>
        /// <returns>True if unregistered, false if not found</returns>
        public bool UnregisterAction(string actionName)
        {
            if (string.IsNullOrEmpty(actionName)) return false;

            if (_syncHandlerMap.Remove(actionName))
            {
                Debug.Log($"[ActionsModule] Unregistered sync action: {actionName}");
                return true;
            }

            if (_asyncHandlerMap.Remove(actionName))
            {
                Debug.Log($"[ActionsModule] Unregistered async action: {actionName}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Unregister all actions from a sync handler.
        /// </summary>
        /// <param name="handler">The handler to unregister</param>
        /// <returns>Number of actions unregistered</returns>
        public int UnregisterHandler(INpcActionHandler handler)
        {
            if (handler == null) return 0;

            var defs = handler.ActionDefinitions;
            if (defs == null) return 0;

            int unregistered = 0;
            foreach (var def in defs)
            {
                if (def != null && !string.IsNullOrEmpty(def.actionName))
                {
                    if (_syncHandlerMap.Remove(def.actionName))
                    {
                        unregistered++;
                    }
                }
            }

            _syncHandlers.Remove(handler);
            if (unregistered > 0)
            {
                Debug.Log($"[ActionsModule] Unregistered {unregistered} sync action(s)");
            }
            return unregistered;
        }

        /// <summary>
        /// Unregister all actions from an async handler.
        /// </summary>
        /// <param name="handler">The handler to unregister</param>
        /// <returns>Number of actions unregistered</returns>
        public int UnregisterHandler(INpcActionHandlerAsync handler)
        {
            if (handler == null) return 0;

            var defs = handler.ActionDefinitions;
            if (defs == null) return 0;

            int unregistered = 0;
            foreach (var def in defs)
            {
                if (def != null && !string.IsNullOrEmpty(def.actionName))
                {
                    if (_asyncHandlerMap.Remove(def.actionName))
                    {
                        unregistered++;
                    }
                }
            }

            _asyncHandlers.Remove(handler);
            if (unregistered > 0)
            {
                Debug.Log($"[ActionsModule] Unregistered {unregistered} async action(s)");
            }
            return unregistered;
        }

        /// <summary>
        /// Re-discover handlers on this GameObject and children.
        /// Useful after dynamically adding new handler components.
        /// </summary>
        public void RefreshHandlers()
        {
            _syncHandlers.Clear();
            _asyncHandlers.Clear();
            _syncHandlerMap.Clear();
            _asyncHandlerMap.Clear();
            DiscoverHandlers();
            Debug.Log($"[ActionsModule] Refreshed handlers. Sync: {_syncHandlers.Count}, Async: {_asyncHandlers.Count}");
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
