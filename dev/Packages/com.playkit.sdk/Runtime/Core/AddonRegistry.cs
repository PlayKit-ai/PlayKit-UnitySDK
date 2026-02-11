using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PlayKit_SDK
{
    /// <summary>
    /// Central registry for all PlayKit SDK addons.
    /// Addons register themselves automatically using [InitializeOnLoad] in editor.
    /// </summary>
    public class AddonRegistry
    {
        private static AddonRegistry _instance;
        public static AddonRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AddonRegistry();
                }
                return _instance;
            }
        }

        private Dictionary<string, IPlayKitAddon> _addons = new Dictionary<string, IPlayKitAddon>();

        private AddonRegistry()
        {
            // Private constructor for singleton
        }

        /// <summary>
        /// Register an addon. Called automatically by addon descriptors.
        /// </summary>
        /// <param name="addon">The addon to register</param>
        public void RegisterAddon(IPlayKitAddon addon)
        {
            if (addon == null)
            {
                Debug.LogWarning("[AddonRegistry] Cannot register null addon");
                return;
            }

            if (string.IsNullOrEmpty(addon.AddonId))
            {
                Debug.LogWarning("[AddonRegistry] Cannot register addon with null/empty AddonId");
                return;
            }

            // if (_addons.ContainsKey(addon.AddonId))
            // {
            //     Debug.LogWarning($"[AddonRegistry] Addon '{addon.AddonId}' is already registered");
            //     return;
            // }

            _addons[addon.AddonId] = addon;
            Debug.Log($"[AddonRegistry] Registered addon: {addon.AddonId} ({addon.DisplayName} v{addon.Version})");
        }

        /// <summary>
        /// Get all registered addons
        /// </summary>
        /// <returns>Read-only dictionary of addon ID to addon instance</returns>
        public IReadOnlyDictionary<string, IPlayKitAddon> GetAllAddons()
        {
            return _addons;
        }

        /// <summary>
        /// Get all addons in a specific exclusion group
        /// </summary>
        /// <param name="exclusionGroup">The exclusion group name (e.g., "distribution-channel")</param>
        /// <returns>List of addons in the group</returns>
        public List<IPlayKitAddon> GetAddonsInExclusionGroup(string exclusionGroup)
        {
            if (string.IsNullOrEmpty(exclusionGroup))
            {
                return new List<IPlayKitAddon>();
            }

            return _addons.Values
                .Where(addon => addon.ExclusionGroup == exclusionGroup)
                .ToList();
        }

        /// <summary>
        /// Get addons that would conflict with the given addon if it were enabled.
        /// </summary>
        /// <param name="addonId">The addon ID to check</param>
        /// <param name="currentEnabledState">Current enabled state dictionary</param>
        /// <returns>List of conflicting addon IDs that are currently enabled</returns>
        public List<string> GetConflictingAddons(string addonId, IReadOnlyDictionary<string, bool> currentEnabledState)
        {
            if (!_addons.TryGetValue(addonId, out var addon))
            {
                return new List<string>();
            }

            // If no exclusion group, no conflicts
            if (string.IsNullOrEmpty(addon.ExclusionGroup))
            {
                return new List<string>();
            }

            // Find all other addons in the same exclusion group that are currently enabled
            var conflicts = new List<string>();
            foreach (var otherAddon in GetAddonsInExclusionGroup(addon.ExclusionGroup))
            {
                // Skip self
                if (otherAddon.AddonId == addonId)
                    continue;

                // Check if this addon is enabled
                if (currentEnabledState != null &&
                    currentEnabledState.TryGetValue(otherAddon.AddonId, out bool isEnabled) &&
                    isEnabled)
                {
                    conflicts.Add(otherAddon.AddonId);
                }
            }

            return conflicts;
        }

        /// <summary>
        /// Check if a channel type matches any of the required channel patterns.
        /// Supports wildcards (e.g., "steam_*" matches "steam_release").
        /// </summary>
        /// <param name="channelType">The actual channel type (e.g., "steam_release")</param>
        /// <param name="requiredChannelTypes">Array of required patterns (e.g., ["steam_*"])</param>
        /// <returns>True if channel matches any pattern, false otherwise</returns>
        public static bool CheckChannelMatch(string channelType, string[] requiredChannelTypes)
        {
            if (requiredChannelTypes == null || requiredChannelTypes.Length == 0)
            {
                return true; // No specific channel required
            }

            if (string.IsNullOrEmpty(channelType))
            {
                return false;
            }

            foreach (var pattern in requiredChannelTypes)
            {
                if (string.IsNullOrEmpty(pattern))
                    continue;

                // Exact match
                if (pattern == channelType)
                    return true;

                // Wildcard match (e.g., "steam_*" matches "steam_release")
                if (pattern.EndsWith("*"))
                {
                    string prefix = pattern.Substring(0, pattern.Length - 1);
                    if (channelType.StartsWith(prefix))
                        return true;
                }
            }

            return false;
        }
    }
}
