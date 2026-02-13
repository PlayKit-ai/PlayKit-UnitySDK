using System;
using Cysharp.Threading.Tasks;

namespace PlayKit_SDK.Auth
{
    /// <summary>
    /// Interface for platform-specific authentication providers.
    /// Implement this interface in addons to provide custom authentication flows.
    /// </summary>
    public interface IAuthProvider
    {
        /// <summary>
        /// Unique identifier for this provider (e.g., "steam", "ios", "android")
        /// Should match the addon ID from IPlayKitAddon.
        /// </summary>
        string ProviderId { get; }

        /// <summary>
        /// Display name for logging/debugging (e.g., "Steam Authentication")
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Whether this provider is available and can authenticate on the current platform.
        /// Check runtime conditions (e.g., is Steam running? is device configured?)
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Authenticate the user using the platform-specific method.
        /// This is an async operation that may involve:
        /// - Opening Steam overlay
        /// - Triggering iOS/Android native login
        /// - Communicating with platform APIs
        /// </summary>
        /// <returns>AuthResult containing success status and authentication data</returns>
        UniTask<AuthResult> AuthenticateAsync();

        /// <summary>
        /// Optional: Clean up resources (cancel tickets, close connections, etc.)
        /// Called when authentication is cancelled or SDK is shutting down.
        /// </summary>
        void Cleanup();

        /// <summary>
        /// Event fired when authentication status changes (optional, for UI feedback)
        /// </summary>
        event Action<string> OnStatusChanged;
    }
}
