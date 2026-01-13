namespace PlayKit_SDK
{
    /// <summary>
    /// Interface for PlayKit SDK addons.
    /// Addons can extend SDK functionality with platform-specific features.
    /// </summary>
    public interface IPlayKitAddon
    {
        /// <summary>
        /// Unique identifier for the addon (e.g., "steam", "ios", "android")
        /// </summary>
        string AddonId { get; }

        /// <summary>
        /// Display name shown in the Settings UI (e.g., "Steam Integration")
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Description of what the addon provides
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Addon version (e.g., "1.0.0")
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Exclusion group name. Addons in the same group are mutually exclusive.
        /// Use "distribution-channel" for platform addons like Steam, iOS, Android.
        /// Use null if the addon can coexist with others.
        /// </summary>
        string ExclusionGroup { get; }

        /// <summary>
        /// Array of channel types this addon requires.
        /// Supports wildcards (e.g., "steam_*" matches "steam_release", "steam_demo", "steam_playtest").
        /// Return null or empty array if no specific channel is required.
        /// </summary>
        string[] RequiredChannelTypes { get; }

        /// <summary>
        /// Returns true if the addon assembly is present in the project.
        /// Typically checks if addon-specific types exist using Type.GetType().
        /// </summary>
        bool IsInstalled { get; }
    }
}
