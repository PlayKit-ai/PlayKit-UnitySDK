using System;
using Cysharp.Threading.Tasks;

namespace PlayKit_SDK.Recharge
{
    /// <summary>
    /// Result of a recharge operation
    /// </summary>
    public class RechargeResult
    {
        /// <summary>
        /// Whether the recharge was initiated successfully
        /// </summary>
        public bool Initiated { get; set; }

        /// <summary>
        /// Error message if failed
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Additional data (e.g., Steam orderId)
        /// </summary>
        public string Data { get; set; }
    }

    /// <summary>
    /// Interface for platform-specific recharge implementations.
    /// Each platform (Browser, Steam, iOS, Android) has its own provider.
    /// </summary>
    public interface IRechargeProvider
    {
        /// <summary>
        /// The recharge method identifier (e.g., "browser", "steam", "ios", "android")
        /// </summary>
        string RechargeMethod { get; }

        /// <summary>
        /// Whether this provider is available on the current platform
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Initialize the provider
        /// </summary>
        /// <param name="baseUrl">API base URL</param>
        /// <param name="gameId">Game ID</param>
        /// <param name="getPlayerToken">Function to get player token</param>
        void Initialize(string baseUrl, string gameId, Func<string> getPlayerToken);

        /// <summary>
        /// Initiate a recharge operation
        /// </summary>
        /// <param name="sku">Product SKU (optional for browser, required for Steam IAP)</param>
        /// <returns>Result of the recharge initiation</returns>
        UniTask<RechargeResult> RechargeAsync(string sku = null);

        /// <summary>
        /// Event fired when recharge is initiated
        /// </summary>
        event Action OnRechargeInitiated;

        /// <summary>
        /// Event fired when recharge is completed (for async platforms like Steam)
        /// </summary>
        event Action<RechargeResult> OnRechargeCompleted;

        /// <summary>
        /// Event fired when recharge is cancelled
        /// </summary>
        event Action OnRechargeCancelled;
    }
}
