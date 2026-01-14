using UnityEngine;

namespace PlayKit_SDK.Steam
{
    /// <summary>
    /// Descriptor for the Steam addon.
    /// Automatically registers with AddonRegistry on Unity startup.
    /// Provides both authentication and in-app purchase capabilities.
    /// </summary>
    public class SteamAddonDescriptor : IPlayKitPlatformAddon
    {
        public string AddonId => "steam";
        public string DisplayName => "Steam Integration";
        public string Description => "Provides Steam platform integration including authentication and in-app purchases via Steam overlay.";
        public string Version => "0.2.3.12";
        public string ExclusionGroup => "distribution-channel";
        public string[] RequiredChannelTypes => new[] { "steam_*" };

        // Cached instances for consistent state
        private static SteamRechargeProvider _cachedRechargeProvider;
        private static PlayKit_SteamMicroTxnManager _microTxnManager;

        public bool IsInstalled
        {
            get
            {
                // Check if Steam addon assembly and types are properly loaded
                var authManagerType = System.Type.GetType("PlayKit_SDK.Steam.PlayKit_SteamAuthManager, PlayKit.Steam");
                return authManagerType != null;
            }
        }

        /// <summary>
        /// Get Steam authentication provider
        /// </summary>
        public Auth.IAuthProvider GetAuthProvider()
        {
            if (!IsInstalled)
            {
                Debug.LogWarning("[SteamAddon] Cannot create auth provider - addon not installed");
                return null;
            }

            return new SteamAuthProvider();
        }

        /// <summary>
        /// Get Steam recharge/IAP provider
        /// </summary>
        public Recharge.IRechargeProvider GetRechargeProvider()
        {
            if (!IsInstalled)
            {
                Debug.LogWarning("[SteamAddon] Cannot create recharge provider - addon not installed");
                return null;
            }

            // Return cached instance or create new one
            if (_cachedRechargeProvider == null)
            {
                _cachedRechargeProvider = new SteamRechargeProvider();
                Debug.Log("[SteamAddon] Created SteamRechargeProvider instance");

                // Create MicroTxnManager to handle Steam callbacks
                EnsureMicroTxnManagerExists();
            }

            return _cachedRechargeProvider;
        }

        /// <summary>
        /// Ensure the MicroTxnManager exists and is initialized
        /// </summary>
        private static void EnsureMicroTxnManagerExists()
        {
            if (_microTxnManager != null) return;

            // Find existing manager or create new one
            _microTxnManager = Object.FindObjectOfType<PlayKit_SteamMicroTxnManager>();

            if (_microTxnManager == null)
            {
                var managerObj = new GameObject("[PlayKit_SteamMicroTxnManager]");
                Object.DontDestroyOnLoad(managerObj);
                _microTxnManager = managerObj.AddComponent<PlayKit_SteamMicroTxnManager>();
                Debug.Log("[SteamAddon] Created PlayKit_SteamMicroTxnManager");
            }

            // Initialize with the cached provider
            if (_cachedRechargeProvider != null && !_microTxnManager.IsInitialized)
            {
                _microTxnManager.Initialize(_cachedRechargeProvider);
                Debug.Log("[SteamAddon] Initialized PlayKit_SteamMicroTxnManager with SteamRechargeProvider");
            }
        }

        /// <summary>
        /// Check if this addon can provide services for the given channel
        /// </summary>
        public bool CanProvideServicesForChannel(string channelType)
        {
            // Use the existing channel matching logic
            return AddonRegistry.CheckChannelMatch(channelType, RequiredChannelTypes);
        }

        /// <summary>
        /// Initialize Steam for developer mode (without authentication).
        /// This initializes Steamworks to enable IAP functionality while preserving
        /// the developer token for Sandbox API usage.
        /// </summary>
        public async Cysharp.Threading.Tasks.UniTask<bool> InitializeForDeveloperModeAsync()
        {
            if (!IsInstalled)
            {
                Debug.LogWarning("[SteamAddon] Cannot initialize - addon not installed");
                return false;
            }

            // Create auth provider and call its initialize-only method
            var authProvider = GetAuthProvider() as SteamAuthProvider;
            if (authProvider == null)
            {
                Debug.LogError("[SteamAddon] Failed to create Steam auth provider");
                return false;
            }

            // Initialize Steamworks without authentication
            return await authProvider.InitializeWithoutAuthAsync();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Auto-register this addon when Unity loads in the editor.
        /// </summary>
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterAddon()
        {
            AddonRegistry.Instance.RegisterAddon(new SteamAddonDescriptor());
        }
#endif

        /// <summary>
        /// Auto-register this addon at runtime.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterAddonRuntime()
        {
            AddonRegistry.Instance.RegisterAddon(new SteamAddonDescriptor());
        }
    }
}
