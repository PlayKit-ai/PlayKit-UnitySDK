using System;
using Cysharp.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PlayKit_SDK.Steam
{
    /// <summary>
    /// Wrapper for Steamworks SDK operations.
    /// Handles Steam client initialization, session ticket retrieval, and user info.
    /// </summary>
    public class PlayKit_SteamService
    {
        public bool IsInitialized { get; private set; }
        public string SteamId => SteamClient.IsValid ? SteamClient.SteamId.ToString() : null;
        public string SteamName => SteamClient.IsValid ? SteamClient.Name : null;

        private AuthTicket _currentTicket;

        /// <summary>
        /// Initialize the Steam client.
        /// </summary>
        /// <param name="appId">Your Steam App ID</param>
        /// <param name="skipRestartCheck">Skip Steam restart check (for developer token mode)</param>
        /// <returns>True if initialization succeeded</returns>
        public async UniTask<bool> InitializeAsync(uint appId, bool skipRestartCheck = false)
        {
            if (IsInitialized)
            {
                Debug.Log("[PlayKit Steam] Already initialized");
                return true;
            }

            try
            {
                // Skip restart check in developer token mode (testing/development)
                if (!skipRestartCheck)
                {
                    // Check if restart through Steam is needed
                    if (SteamClient.RestartAppIfNecessary(appId))
                    {
                        Debug.Log("[PlayKit Steam] Restarting through Steam...");
                        Application.Quit();
                        return false;
                    }
                }
                else
                {
                    Debug.Log("[PlayKit Steam] Skipping restart check (developer token mode)");
                }

                // Initialize on thread pool to avoid blocking
                await UniTask.RunOnThreadPool(() => SteamClient.Init(appId));

                IsInitialized = true;
                Debug.Log($"[PlayKit Steam] Initialized successfully. Steam ID: {SteamId}, Name: {SteamName}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayKit Steam] Init failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get a session ticket for authentication with PlayKit backend.
        /// </summary>
        /// <returns>Hex-encoded session ticket string</returns>
        public async UniTask<string> GetSessionTicketAsync()
        {
            if (!SteamClient.IsValid)
            {
                Debug.LogError("[PlayKit Steam] Steam client not initialized");
                return null;
            }

            try
            {
                // Cancel any existing ticket
                _currentTicket?.Cancel();

                // Get new auth session ticket
                NetIdentity identity = SteamClient.SteamId;
                _currentTicket = await SteamUser.GetAuthSessionTicketAsync(identity);

                if (_currentTicket == null || _currentTicket.Data == null)
                {
                    Debug.LogError($"[PlayKit Steam] Failed to get session ticket for App ID {SteamClient.AppId}. " +
                                   "This usually means the current Steam user does not have access to this game. " +
                                   "For testing, use Spacewar (App ID 480) which is accessible to all Steam users.");
                    return null;
                }

                // Convert to hex string
                string ticketHex = BitConverter.ToString(_currentTicket.Data, 0, _currentTicket.Data.Length)
                    .Replace("-", "")
                    .ToLowerInvariant();

                Debug.Log($"[PlayKit Steam] Got session ticket ({ticketHex.Length / 2} bytes)");
                return ticketHex;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayKit Steam] GetSessionTicket failed: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Cancel the current auth ticket.
        /// Call this when done with authentication.
        /// </summary>
        public void CancelTicket()
        {
            _currentTicket?.Cancel();
            _currentTicket = null;
        }

        /// <summary>
        /// Shutdown the Steam client.
        /// </summary>
        public void Shutdown()
        {
            if (IsInitialized)
            {
                CancelTicket();
                SteamClient.Shutdown();
                IsInitialized = false;
                Debug.Log("[PlayKit Steam] Shutdown complete");
            }
        }

        /// <summary>
        /// Run Steam callbacks. Call this in Update() if needed.
        /// </summary>
        public void RunCallbacks()
        {
            if (IsInitialized)
            {
                SteamClient.RunCallbacks();
            }
        }
    }
}
