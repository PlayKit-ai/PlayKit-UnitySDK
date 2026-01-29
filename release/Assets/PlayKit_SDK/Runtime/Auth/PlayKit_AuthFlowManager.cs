using System;
using Cysharp.Threading.Tasks;
using PlayKit_SDK;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlayKit_SDK.Auth
{
    /// <summary>
    /// Manages the authentication flow using Device Authorization.
    /// Replaces the previous OTP (send-code/verify-code) authentication method.
    /// Reuses the existing LoginWeb.prefab UI structure.
    /// </summary>
    public class PlayKit_AuthFlowManager : MonoBehaviour
    {
        // Public property to signal the final outcome of the entire flow.
        public bool IsSuccess { get; private set; } = false;

        // --- Serialized Fields for UI (matching LoginWeb.prefab structure) ---
        [Header("Core UI")]
        [Tooltip("The modal GameObject shown during loading/waiting.")]
        [SerializeField] private GameObject loadingModal;

        [Tooltip("A UI Text element to display status and error messages.")]
        [SerializeField] private Text errorText;

        [Header("Legacy OTP Panels (will be hidden)")]
        [Tooltip("The identifier input panel (email/phone) - will be hidden.")]
        [SerializeField] private GameObject identifierPanel;

        [Tooltip("The verification code panel - will be hidden.")]
        [SerializeField] private GameObject verificationPanel;

        [Header("Buttons")]
        [Tooltip("Back button - will be hidden.")]
        [SerializeField] private Button backBtn;

        [Tooltip("Legacy identifier input - not used.")]
        [SerializeField] private InputField identifierInput;

        [Tooltip("Legacy code input - not used.")]
        [SerializeField] private InputField codeInput;

        [Tooltip("Send code button - used as retry/start button.")]
        [SerializeField] private Button sendCodeButton;

        [Tooltip("Verify button - will be hidden.")]
        [SerializeField] private Button verifyButton;

        [Header("Legacy Toggles (will be hidden)")]
        [SerializeField] private Toggle emailToggle;
        [SerializeField] private Toggle phoneToggle;

        [Header("Legacy Icons")]
        [SerializeField] private Sprite emailIcon;
        [SerializeField] private Sprite phoneIcon;
        [SerializeField] private Image identifierIconDisplay;
        [SerializeField] private Text placeholderText;

        [Tooltip("Main dialogue container.")]
        [SerializeField] private GameObject dialogue;

        // BaseUrl is now retrieved from PlayKitSettings
        private string ApiBaseUrl => PlayKitSettings.Instance?.BaseUrl ?? "https://playkit.ai";

        // --- Public Properties ---
        public PlayKit_AuthManager AuthManager { get; set; }

        // --- Private State ---
        private PlayKit_DeviceAuthFlow _deviceAuthFlow;
        private bool _isAuthInProgress = false;
        private bool _isWaitingForLoginClick = false;

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

            // Setup UI for Device Auth flow
            SetupDeviceAuthUI();

            // Auto-start the login flow
            await StartLoginFlow();
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

        private void SetupDeviceAuthUI()
        {
            // Show dialogue
            if (dialogue != null) dialogue.SetActive(true);

            // Hide legacy OTP panels
            if (identifierPanel != null) identifierPanel.SetActive(false);
            if (verificationPanel != null) verificationPanel.SetActive(false);

            // Hide unused buttons
            if (backBtn != null) backBtn.gameObject.SetActive(false);
            if (verifyButton != null) verifyButton.gameObject.SetActive(false);

            // Hide toggles
            if (emailToggle != null) emailToggle.gameObject.SetActive(false);
            if (phoneToggle != null) phoneToggle.gameObject.SetActive(false);

            // Setup sendCodeButton - used as login button on WebGL, retry button on other platforms
            if (sendCodeButton != null)
            {
                sendCodeButton.onClick.RemoveAllListeners();
                sendCodeButton.onClick.AddListener(OnLoginButtonClicked);
                sendCodeButton.gameObject.SetActive(false);
            }

            // Show loading modal
            ShowLoadingModal();
            UpdateStatus("正在连接...\nConnecting...");
        }

        private async UniTask StartLoginFlow()
        {
            if (_isAuthInProgress) return;

            _isAuthInProgress = true;
            _isWaitingForLoginClick = false;

            // Hide login/retry button during auth initialization
            if (sendCodeButton != null) sendCodeButton.gameObject.SetActive(false);

            ShowLoadingModal();
            UpdateStatus("正在启动登录...\nStarting authentication...");

            try
            {
                var gameId = PlayKitSettings.Instance?.GameId;
                if (string.IsNullOrEmpty(gameId))
                {
                    OnDeviceAuthError("Game ID 未配置\nGame ID not configured");
                    return;
                }

                // On WebGL platform, don't auto-open browser to avoid popup blocking
                // Instead, show a login button that user must click
                bool isWebGL = Application.platform == RuntimePlatform.WebGLPlayer;

                var result = await _deviceAuthFlow.StartAuthFlowAsync(
                    gameId,
                    "player:play",
                    this.GetCancellationTokenOnDestroy(),
                    autoOpenBrowser: !isWebGL
                );

                // On WebGL, result will be null and we need to wait for user to click login button
                if (isWebGL && result == null && _deviceAuthFlow.Status == DeviceAuthStatus.WaitingForBrowser)
                {
                    _isWaitingForLoginClick = true;
                    _isAuthInProgress = false;
                    HideLoadingModal();
                    ShowLoginButton();
                    UpdateStatus("点击下方按钮登录\nClick the button below to login");
                }
                // Result is handled in OnDeviceAuthSuccess or OnDeviceAuthError for non-WebGL
            }
            catch (OperationCanceledException)
            {
                OnDeviceAuthCancelled();
            }
            catch (Exception ex)
            {
                OnDeviceAuthError($"登录失败: {ex.Message}\nAuthentication failed: {ex.Message}");
            }
        }

        private async void OnLoginButtonClicked()
        {
            // If waiting for login click on WebGL, open browser and start polling
            if (_isWaitingForLoginClick)
            {
                _isWaitingForLoginClick = false;
                _isAuthInProgress = true;

                // Hide login button and show loading
                if (sendCodeButton != null) sendCodeButton.gameObject.SetActive(false);
                ShowLoadingModal();
                UpdateStatus("正在打开登录页面...\nOpening login page...");

                try
                {
                    // This call must be in the click event handler to avoid popup blocking
                    var result = await _deviceAuthFlow.OpenBrowserAndPollAsync(this.GetCancellationTokenOnDestroy());
                    // Result is handled in OnDeviceAuthSuccess or OnDeviceAuthError via events
                }
                catch (OperationCanceledException)
                {
                    OnDeviceAuthCancelled();
                }
                catch (Exception ex)
                {
                    OnDeviceAuthError($"登录失败: {ex.Message}\nAuthentication failed: {ex.Message}");
                }
            }
            else
            {
                // Normal retry flow
                await StartLoginFlow();
            }
        }

        #region Device Auth Event Handlers

        private void OnDeviceAuthStatusChanged(DeviceAuthStatus status)
        {
            switch (status)
            {
                case DeviceAuthStatus.Initiating:
                    UpdateStatus("正在初始化...\nInitializing...");
                    break;
                case DeviceAuthStatus.WaitingForBrowser:
                    UpdateStatus("请在浏览器中完成授权...\nPlease complete authorization in your browser...");
                    break;
                case DeviceAuthStatus.Polling:
                    UpdateStatus("等待授权中...\nWaiting for authorization...");
                    break;
                case DeviceAuthStatus.Authorized:
                    UpdateStatus("授权成功！\nAuthorization successful!");
                    break;
                case DeviceAuthStatus.Denied:
                    UpdateStatus("授权被拒绝\nAuthorization denied.");
                    break;
                case DeviceAuthStatus.Expired:
                    UpdateStatus("会话已过期，请重试\nSession expired. Please try again.");
                    break;
                case DeviceAuthStatus.Error:
                    UpdateStatus("发生错误\nAn error occurred.");
                    break;
            }
        }

        private async void OnDeviceAuthSuccess(DeviceAuthResult result)
        {
            Debug.Log("[PlayKit Auth] Device auth successful, saving tokens...");
            UpdateStatus("正在保存凭证...\nSaving credentials...");

            try
            {
                // Save access token, refresh token, and expiry
                PlayKit_AuthManager.SavePlayerToken(
                    result.AccessToken,
                    result.RefreshToken,
                    result.ExpiresIn
                );

                Debug.Log($"[PlayKit Auth] Tokens saved. Access token expires in {result.ExpiresIn}s, Refresh token: {(!string.IsNullOrEmpty(result.RefreshToken) ? "present" : "none")}");
                UpdateStatus("登录成功！\nLogin successful!");

                IsSuccess = true;
                
                // Hide loading and dialogue on success
                HideLoadingModal();
                if (dialogue != null) dialogue.SetActive(false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit Auth] Failed to save token: {ex.Message}");
                UpdateStatus($"保存凭证失败\nFailed to save credentials.");
                IsSuccess = false;
                ShowRetryButton();
            }
            finally
            {
                _isAuthInProgress = false;
            }
        }

        private void OnDeviceAuthError(string error)
        {
            Debug.LogError($"[PlayKit Auth] Device auth error: {error}");
            UpdateStatus($"错误: {error}\nError: {error}");

            _isAuthInProgress = false;
            IsSuccess = false;
            
            HideLoadingModal();
            ShowRetryButton();
        }

        private void OnDeviceAuthCancelled()
        {
            Debug.Log("[PlayKit Auth] Device auth cancelled by user.");
            UpdateStatus("登录已取消\nAuthentication cancelled.");

            _isAuthInProgress = false;
            IsSuccess = false;
            
            HideLoadingModal();
            ShowRetryButton();
        }

        #endregion

        #region UI Helpers

        private void UpdateStatus(string message)
        {
            if (errorText != null)
            {
                errorText.text = message;
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

        private void ShowRetryButton()
        {
            _isWaitingForLoginClick = false;
            if (sendCodeButton != null)
            {
                // Update button text for retry
                var buttonText = sendCodeButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    buttonText.text = "重试 / Retry";
                }
                sendCodeButton.gameObject.SetActive(true);
            }
        }

        private void ShowLoginButton()
        {
            if (sendCodeButton != null)
            {
                // Update button text for login
                var buttonText = sendCodeButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    buttonText.text = "登录 / Login";
                }
                sendCodeButton.gameObject.SetActive(true);
            }
        }

        #endregion

        #region Legacy API compatibility (not used, but keeps prefab references valid)

        public void ShowIdentifierModal()
        {
            // Legacy method - now triggers DeviceAuth retry
            OnLoginButtonClicked();
        }

        #endregion
    }
}
