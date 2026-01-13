using System;
using System.Collections.Generic;

namespace PlayKit_SDK.Auth
{
    /// <summary>
    /// Standardized result from any authentication provider.
    /// Contains all data needed to establish a player session.
    /// </summary>
    [Serializable]
    public class AuthResult
    {
        /// <summary>
        /// Whether authentication succeeded
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The player token to use for API calls.
        /// This is the primary authentication credential.
        /// </summary>
        public string PlayerToken { get; set; }

        /// <summary>
        /// Optional: Refresh token for token renewal
        /// </summary>
        public string RefreshToken { get; set; }

        /// <summary>
        /// Token expiry time (in seconds from now).
        /// 0 or negative means no expiry.
        /// </summary>
        public int ExpiresIn { get; set; }

        /// <summary>
        /// When the token expires (absolute UTC time).
        /// Use this or ExpiresIn, whichever is more convenient.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// User ID from the platform (e.g., PlayKit userId, Steam ID, iOS Game Center ID)
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Platform-specific user identifier (e.g., Steam ID for Steam, Apple ID for iOS)
        /// </summary>
        public string PlatformUserId { get; set; }

        /// <summary>
        /// Error message if authentication failed
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Additional metadata from the authentication process
        /// (e.g., "userCreated": true, "steamId": "76561198XXXXXX")
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// The provider that generated this result (for logging/debugging)
        /// </summary>
        public string ProviderId { get; set; }

        public AuthResult()
        {
            Metadata = new Dictionary<string, object>();
        }
    }
}
