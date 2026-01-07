using Cysharp.Threading.Tasks;

namespace PlayKit_SDK
{
    /// <summary>
    /// Extended addon interface for platforms that provide runtime services
    /// (authentication, in-app purchases, etc.)
    ///
    /// Platforms like Steam, iOS, PlayStation should implement this interface
    /// to provide platform-specific authentication and monetization.
    /// </summary>
    public interface IPlayKitPlatformAddon : IPlayKitAddon
    {
        /// <summary>
        /// Get authentication provider for this platform.
        /// Returns null if platform doesn't provide auth.
        /// </summary>
        /// <returns>Platform-specific auth provider, or null</returns>
        Auth.IAuthProvider GetAuthProvider();

        /// <summary>
        /// Get recharge/IAP provider for this platform.
        /// Returns null if platform doesn't provide IAP.
        /// </summary>
        /// <returns>Platform-specific recharge provider, or null</returns>
        Recharge.IRechargeProvider GetRechargeProvider();

        /// <summary>
        /// Check if this addon can provide services for the given channel.
        /// Uses same wildcard matching as RequiredChannelTypes.
        /// </summary>
        /// <param name="channelType">Channel type from PlayKitSettings (e.g., "steam_release")</param>
        /// <returns>True if this addon can provide services for this channel</returns>
        bool CanProvideServicesForChannel(string channelType);

        /// <summary>
        /// Initialize platform services for developer mode.
        /// Called when a developer token is configured to prepare platform
        /// services (IAP, overlay, etc.) without performing user authentication.
        ///
        /// Platform addons that need initialization (like Steam needing Steamworks
        /// to get Steam ID for IAP) should implement this. Addons that don't need
        /// any initialization can return UniTask.FromResult(true).
        /// </summary>
        /// <returns>True if initialization succeeded or is not needed</returns>
        UniTask<bool> InitializeForDeveloperModeAsync();
    }
}
