using System;
using Cysharp.Threading.Tasks;
using Developerworks.SDK;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlayKit_SDK.Auth
{
    /// <summary>
    /// Manages the authentication flow using Device Authorization.
    /// Replaces the previous OTP (send-code/verify-code) authentication method.
    /// </summary>
    public class PlayKit_AuthFlowManager : MonoBehaviour
    {
        // Public property to signal the final outcome of the entire flow.
        public bool IsSuccess { get; private set; } = false;

        // --- Serialized Fields for UI ---
        [Header("Core UI")]
        [Tooltip("The modal GameObject shown during loading/waiting.")]
        [SerializeField] private GameObject loadingModal;

        [Tooltip("A UI Text element to display status and error messages.")]
        [SerializeField] private Text statusText;

        [Header("Device Auth UI")]
        [Tooltip("The main panel containing the login button.")]
        [SerializeField] private GameObject loginPanel;

        [Tooltip("Button to start the Device Auth flow.")]
        [SerializeField] private Button loginButton;

        [Tooltip("Button to cancel the auth flow.")]
        [SerializeField] private Button cancelButton;

        [Tooltip("Main dialogue container.")]
        [SerializeField] private GameObject dialogue;

        // BaseUrl is now retrieved from PlayKitSettings
        private string ApiBaseUrl => PlayKitSettings.Instance?.BaseUrl ?? "https://playkit.ai";

        // --- Public Properties ---
        public PlayKit_AuthManager AuthManager { get; set; }

        // --- Private State ---
        private PlayKit_DeviceAuthFlow _deviceAuthFlow;
        private bool _isAuthInProgress = false;

        private async void Start()
        {
            // Ensure EventSystem exists for UI interaction
            EnsureEventSystem();

            // Get or create Device Auth Flow component
            _deviceAuthFlow = GetComponent<PlayKit_DeviceAuthFlow>();
            if (_deviceAuthFlow == null)
            {
                _deviceAuthFlow = gameObject.AddComponent<PlayKit_DeviceAuthFlow>();
            }

            // Setup event handlers
            _deviceAuthFlow.OnStatusChanged += OnDeviceAuthStatusChanged;
            _deviceAuthFlow.OnAuthSuccess += OnDeviceAuthSuccess;
            _deviceAuthFlow.OnAuthError += OnDeviceAuthError;
            _deviceAuthFlow.OnCancelled += OnDeviceAuthCancelled;

            // Setup UI
            SetupUI();

            // Show login panel
            if (dialogue != null) dialogue.SetActive(true);
            ShowLoginPanel();
        }

        private void OnDestroy()
        {
            if (_deviceAuthFlow != null)
            {
                _deviceAuthFlow.OnStatusChanged -= OnDeviceAuthStatusChanged;
                _deviceAuthFlow.OnAuthSuccess -= OnDeviceAuthSuccess;
                _deviceAuthFlow.OnAuthError -= OnDeviceAuthError;
                _deviceAuthFlow.OnCancelled -= OnDeviceAuthCancelled;
            }
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                // New Input System only
                var inputModule = eventSystem.AddComponent(System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem"));
#elif ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
                // Legacy Input Manager only
                eventSystem.AddComponent<StandaloneInputModule>();
#else
                // Both enabled - try new Input System first, fallback to legacy
                var inputSystemType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputSystemType != null)
                {
                    eventSystem.AddComponent(inputSystemType);
                }
                else
                {
                    eventSystem.AddComponent<StandaloneInputModule>();
                }
#endif
            }
        }

        private void SetupUI()
        {
            if (loginButton != null)
            {
                loginButton.onClick.AddListener(OnLoginButtonClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancelButtonClicked);
                cancelButton.gameObject.SetActive(false);
            }

            if (statusText != null)
            {
                statusText.text = "Click 'Login' to authenticate";
            }
        }

        private void ShowLoginPanel()
        {
            if (loginPanel != null) loginPanel.SetActive(true);
            if (loginButton != null) loginButton.gameObject.SetActive(true);
            if (cancelButton != null) cancelButton.gameObject.SetActive(false);
            HideLoadingModal();
        }

        private void ShowWaitingState()
        {
            if (loginButton != null) loginButton.gameObject.SetActive(false);
            if (cancelButton != null) cancelButton.gameObject.SetActive(true);
            ShowLoadingModal();
        }

        #region Button Handlers

        private async void OnLoginButtonClicked()
        {
            if (_isAuthInProgress) return;

            _isAuthInProgress = true;
            ShowWaitingState();
            UpdateStatus("Starting authentication...");

            try
            {
                var gameId = PlayKitSettings.Instance?.GameId;
                if (string.IsNullOrEmpty(gameId))
                {
                    OnDeviceAuthError("Game ID not configured");
                    return;
                }

                var result = await _deviceAuthFlow.StartAuthFlowAsync(
                    gameId,
                    "player:play",
                    this.GetCancellationTokenOnDestroy()
                );

                // Result is handled in OnDeviceAuthSuccess or OnDeviceAuthError
            }
            catch (OperationCanceledException)
            {
                OnDeviceAuthCancelled();
            }
            catch (Exception ex)
            {
                OnDeviceAuthError($"Authentication failed: {ex.Message}");
            }
        }

        private void OnCancelButtonClicked()
        {
            _deviceAuthFlow?.CancelFlow();
        }

        #endregion

        #region Device Auth Event Handlers

        private void OnDeviceAuthStatusChanged(DeviceAuthStatus status)
        {
            switch (status)
            {
                case DeviceAuthStatus.Initiating:
                    UpdateStatus("Initializing...");
                    break;
                case DeviceAuthStatus.WaitingForBrowser:
                    UpdateStatus("Please complete authorization in your browser...");
                    break;
                case DeviceAuthStatus.Polling:
                    UpdateStatus("Waiting for authorization...");
                    break;
                case DeviceAuthStatus.Authorized:
                    UpdateStatus("Authorization successful!");
                    break;
                case DeviceAuthStatus.Denied:
                    UpdateStatus("Authorization denied.");
                    break;
                case DeviceAuthStatus.Expired:
                    UpdateStatus("Session expired. Please try again.");
                    break;
                case DeviceAuthStatus.Error:
                    UpdateStatus("An error occurred.");
                    break;
            }
        }

        private async void OnDeviceAuthSuccess(DeviceAuthResult result)
        {
            Debug.Log("[PlayKit Auth] Device auth successful, saving token...");
            UpdateStatus("Saving credentials...");

            try
            {
                // Save the access token as player token
                PlayKit_AuthManager.SavePlayerToken(result.AccessToken, null);

                // Also save to shared token storage
                PlayKit_LocalSharedToken.SaveToken(result.AccessToken);

                Debug.Log("[PlayKit Auth] Token saved successfully.");
                UpdateStatus("Login successful!");

                IsSuccess = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit Auth] Failed to save token: {ex.Message}");
                UpdateStatus("Failed to save credentials.");
                IsSuccess = false;
            }
            finally
            {
                _isAuthInProgress = false;
                HideLoadingModal();
            }
        }

        private void OnDeviceAuthError(string error)
        {
            Debug.LogError($"[PlayKit Auth] Device auth error: {error}");
            UpdateStatus($"Error: {error}");

            _isAuthInProgress = false;
            IsSuccess = false;
            ShowLoginPanel();
        }

        private void OnDeviceAuthCancelled()
        {
            Debug.Log("[PlayKit Auth] Device auth cancelled by user.");
            UpdateStatus("Authentication cancelled.");

            _isAuthInProgress = false;
            IsSuccess = false;
            ShowLoginPanel();
        }

        #endregion

        #region UI Helpers

        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        private void ShowLoadingModal()
        {
            if (loadingModal != null) loadingModal.SetActive(true);
        }

        private void HideLoadingModal()
        {
            if (loadingModal != null) loadingModal.SetActive(false);
        }

        #endregion
    }
}
