using System;
using Cysharp.Threading.Tasks;
using PlayKit_SDK.Art;
using UnityEngine;

namespace PlayKit_SDK.Auth
{
    public class PlayKit_AuthManager : MonoBehaviour
    {
        // CHANGED: Keys are now more specific to "PlayerToken"
        private const string PlayerTokenKey = "DW_SDK_PlayerToken";
        private const string TokenExpiryKey = "DW_SDK_TokenExpiry";
        private string _gameId;
        public string gameId { get=>_gameId; }
        public string AuthToken { get; private set; }
        public bool IsDeveloperToken { get; private set; }

        [SerializeField]private PlayKit_PlayerClient _playerClient;
        public PlayKit_PlayerClient PlayerClient { get => _playerClient; }
        private LoadingSpinner standaloneLoadingObject;

        private void Awake()
        {
            // Create PlayerClient if it doesn't exist (for dynamically added components)
            if (_playerClient == null)
            {
                _playerClient = gameObject.AddComponent<PlayKit_PlayerClient>();
            }
        }

        public void Setup(string publishableKey, string developerToken = null)
        {
            _gameId = publishableKey;
            Debug.Log("[Developerworks SDK] Initializing authentication with the following game id: "+_gameId);
            if (!string.IsNullOrEmpty(developerToken))
            {
                AuthToken = developerToken;
                IsDeveloperToken = true;
            }
            if(standaloneLoadingObject == null)
            {
                var loginWebPrefab = Resources.Load<GameObject>("Loading");
                standaloneLoadingObject = Instantiate(loginWebPrefab).GetComponent<LoadingSpinner>();
            }

        }

        public async UniTask<bool> AuthenticateAsync()
        {
            standaloneLoadingObject.gameObject.SetActive(true);
            // If using a developer token, authentication is always considered successful.
            if (IsDeveloperToken)
            {
                Debug.Log("[Developerworks SDK] Using developer token. Authentication successful.");
                standaloneLoadingObject.gameObject.SetActive(false);

                return true;
            }
            // Step 1: Try loading Shared Token if Player Token not found

            LoadSharedToken();

            if (await IsTokenValidWithAPICheck())
            {
                Debug.Log("[Developerworks SDK] Valid shared token found, loaded and verified.");
                standaloneLoadingObject.gameObject.SetActive(false);

                // Also save it as Player Token for consistency
                // SavePlayerTokenFromShared();
                return true;
            }

            // Step 2: Try loading Player Token from PlayerPrefs
            LoadPlayerToken();

            if (await IsTokenValidWithAPICheck())
            {
                standaloneLoadingObject.gameObject.SetActive(false);

                Debug.Log("[Developerworks SDK] Existing valid player token found and verified.");
                return true;
            }

            // Step 3: No valid tokens found, initiate login process
            Debug.Log("[Developerworks SDK] No valid player or shared token found. Initiating login process.");
            standaloneLoadingObject.gameObject.SetActive(false);

            return await ShowLoginWebAsync();
        }

        private async UniTask<bool> ShowLoginWebAsync()
        {
            var loginWebPrefab = Resources.Load<GameObject>("LoginWeb");
            if (loginWebPrefab == null)
            {
                Debug.LogError("[Developerworks SDK] LoginWeb prefab not found in Resources folder!");
                return false;
            }

            var loginWebInstance = GameObject.Instantiate(loginWebPrefab);
            var authFlowManager = loginWebInstance.GetComponent<PlayKit_AuthFlowManager>();
            if (authFlowManager == null)
            {
                Debug.LogError("[Developerworks SDK] AuthFlowManager component not found on the LoginWeb prefab!");
                GameObject.Destroy(loginWebInstance);
                return false;
            }
            
            // ADDED: Pass the AuthManager reference so the flow can use our PlayerClient
            authFlowManager.AuthManager = this;

            // Wait until the AuthFlowManager reports success (i.e., it has acquired and saved a player token).
            await UniTask.WaitUntil(() => authFlowManager.IsSuccess, cancellationToken: loginWebInstance.GetCancellationTokenOnDestroy());
            
            bool success = authFlowManager.IsSuccess;

            // Clean up the login UI.
            Destroy(loginWebInstance);

            if (success)
            {
                // The flow was successful, so a new token should have been saved. Load it.
                LoadPlayerToken();
                return IsTokenValid();
            }

            Debug.LogError("[Developerworks SDK] Login flow did not complete successfully.");
            return false;
        }

        private void LoadPlayerToken()
        {
            // Do not overwrite a developer token.
            if (IsDeveloperToken) return;

            AuthToken = PlayerPrefs.GetString(PlayerTokenKey, null);
        }

        private void LoadSharedToken()
        {
            // Do not overwrite a developer token or existing valid token.
            if (IsDeveloperToken || !string.IsNullOrEmpty(AuthToken)) return;

            string sharedToken = PlayKit_LocalSharedToken.LoadToken();
            if (!string.IsNullOrEmpty(sharedToken))
            {
                AuthToken = sharedToken;
                Debug.Log("[Developerworks SDK] Loaded token from shared storage.");
            }
        }

        private void SavePlayerTokenFromShared()
        {
            // Save the shared token as Player Token with a far future expiry
            if (!string.IsNullOrEmpty(AuthToken))
            {
                PlayerPrefs.SetString(PlayerTokenKey, AuthToken);
                // Set a far future expiry since we don't know the actual expiry
                PlayerPrefs.SetString(TokenExpiryKey, DateTime.MaxValue.ToUniversalTime().Ticks.ToString());
                PlayerPrefs.Save();
                Debug.Log("[Developerworks SDK] Shared token saved as player token.");
            }
        }

        private bool IsTokenValid()
        {
            if (string.IsNullOrEmpty(AuthToken))
            {
                return false;
            }

            // Developer tokens are always considered valid.
            if (IsDeveloperToken)
            {
                return true;
            }

            // Check expiry for Player Tokens.
            string expiryString = PlayerPrefs.GetString(TokenExpiryKey, "0");
            if (long.TryParse(expiryString, out long expiryTicks))
            {
                 if (DateTime.UtcNow.Ticks > expiryTicks)
                 {
                    Debug.Log("[Developerworks SDK] Player token has expired.");
                    ClearPlayerToken(); // Clean up expired token
                    return false;
                 }
            }
            else
            {
                // If expiry date is not a valid long, treat it as invalid.
                return false;
            }

            return true;
        }

        private async UniTask<bool> IsTokenValidWithAPICheck()
        {
            if (string.IsNullOrEmpty(AuthToken))
            {
                return false;
            }

            // Developer tokens are always considered valid.
            if (IsDeveloperToken)
            {
                return true;
            }

            // First check expiry for Player Tokens.
#if !UNITY_WEBGL

            string expiryString = PlayerPrefs.GetString(TokenExpiryKey, "0");
            if (long.TryParse(expiryString, out long expiryTicks))
            {
                if (DateTime.UtcNow.Ticks > expiryTicks)
                {
                    Debug.Log("[Developerworks SDK] Player token has expired based on stored expiry.");
                    ClearPlayerToken(); // Clean up expired token
                    return false;
                }
            }
            else
            {
                // If expiry date is not a valid long, treat it as invalid.
                Debug.Log("[Developerworks SDK] Invalid expiry date format.");
                ClearPlayerToken();
                return false;
            }
#endif

            // Token hasn't expired according to stored data, now verify with API
            if (PlayerClient != null)
            {
                // Set the token in the client if not already set
                if (!PlayerClient.HasValidPlayerToken())
                {
                    PlayerClient.SetPlayerToken(AuthToken);
                }

                try
                {
                    Debug.Log("[Developerworks SDK] Verifying token with player-info API...");
                    var result = await PlayerClient.GetPlayerInfoAsync();

                    if (!result.Success)
                    {
                        Debug.LogWarning($"[Developerworks SDK] Token verification failed: {result.Error}");
                        ClearPlayerToken(); // Clear invalid token
                        return false;
                    }

                    Debug.Log($"[Developerworks SDK] Token verified successfully. User ID: {result.Data.UserId}");
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Developerworks SDK] Error verifying token: {e.Message}");
                    // Don't clear token on network error - might be temporary
                    return false;
                }
            }
            else
            {
                Debug.LogWarning("[Developerworks SDK] PlayerClient not available for token verification.");
                // If we can't verify with API, trust the expiry check
                return true;
            }
        }
        
        // CHANGED: Renamed and updated to handle the Player Token and its specific expiry format.
        public static void SavePlayerToken(string token, string expiresAtString)
        {
            PlayerPrefs.SetString(PlayerTokenKey, token);

            // The API returns null for never-expiring tokens. We'll store a far-future date.
            // Otherwise, we parse the ISO 8601 date string.
            DateTime expiryDate = string.IsNullOrEmpty(expiresAtString) 
                ? DateTime.MaxValue 
                : DateTime.Parse(expiresAtString, null, System.Globalization.DateTimeStyles.RoundtripKind);
            
            PlayerPrefs.SetString(TokenExpiryKey, expiryDate.ToUniversalTime().Ticks.ToString());
            PlayerPrefs.Save();
            
            // Also save to shared token storage
            PlayKit_LocalSharedToken.SaveToken(token);
            
            Debug.Log("[Developerworks SDK] New player token saved successfully (both local and shared).");
        }
        
        public static void ClearPlayerToken()
        {
            PlayerPrefs.DeleteKey(PlayerTokenKey);
            PlayerPrefs.DeleteKey(TokenExpiryKey);
            PlayerPrefs.Save();
            
            // Also clear the shared token
            PlayKit_LocalSharedToken.EraseToken();
        }
        
        /// <summary>
        /// Get access to the PlayerClient for querying user information.
        /// This should be called after successful authentication.
        /// </summary>
        /// <returns>The PlayerClient instance, or null if not authenticated</returns>
        public PlayKit_PlayerClient GetPlayerClient()
        {
            // Only return the PlayerClient if we have a valid token
            if (IsTokenValid())
            {
                // If we have a saved player token but the PlayerClient doesn't have it, 
                // we should initialize it with the current token
                if (PlayerClient != null && !PlayerClient.HasValidPlayerToken() && !IsDeveloperToken)
                {
                    // Set the player token directly since we already have it
                    SetPlayerTokenInClient(AuthToken);
                }
                return PlayerClient;
            }
            return null;
        }
        
        /// <summary>
        /// Internal method to set the player token in the client when we load it from storage
        /// </summary>
        private void SetPlayerTokenInClient(string token)
        {
            if (PlayerClient != null && !string.IsNullOrEmpty(token))
            {
                PlayerClient.SetPlayerToken(token);
                Debug.Log($"[DW_AuthManager] Player token loaded from storage and set in PlayerClient");
            }
        }
    }
}