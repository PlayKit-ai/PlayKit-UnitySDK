using System;
using System.Collections;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
namespace PlayKit_SDK.Auth
{
    /// <summary>
    /// Manages the headless authentication flow by directly calling the backend API.
    /// This component now also drives the UI flow based on serialized fields.
    /// </summary>
    public class PlayKit_AuthFlowManager : MonoBehaviour
    {
        // Public property to signal the final outcome of the entire flow.
        public bool IsSuccess { get; private set; } = false;

        // --- Serialized Fields for UI ---
        [Header("Core UI")]
        [Tooltip("The modal GameObject shown during loading.")]
        [SerializeField] private GameObject loadingModal;
        
        [Tooltip("A UI Text element to display error messages to the user.")]
        [SerializeField] private Text errorText; // Use TextMeshProUGUI if you prefer

        [Header("UI Panels")]
        [Tooltip("The panel containing the identifier input and send button.")]
        [SerializeField] private GameObject identifierPanel;
        [Tooltip("The panel containing the code input and verify button.")]
        [SerializeField] private GameObject verificationPanel;

        [SerializeField] private GameObject backBtn;

        [Header("UI Interactables")]
        [Tooltip("Input field for the user's email or phone number.")]
        [SerializeField] private InputField identifierInput; 
        [Tooltip("Input field for the 6-digit verification code.")]
        [SerializeField] private InputField codeInput; 
        [Tooltip("The button that triggers sending the verification code.")]
        [SerializeField] private Button sendCodeButton;
        [Tooltip("The button that submits the verification code.")]
        [SerializeField] private Button verifyButton;
        [Tooltip("Dropdown to select authentication type ('Email' or 'Phone').")]
        [SerializeField] private Toggle emailToggle,phoneToggle;
        [Tooltip("Icon that indicate identifier type")]
        [SerializeField] private Sprite emailIcon,phoneIcon;
        [SerializeField] private Image identifierIconDisplay;
        [SerializeField] private Text placeholderText;
        [SerializeField] private GameObject dialogue;

        [Header("API Configuration")] [Tooltip("The base URL of your backend authentication API.")]
        private string apiBaseUrl = "https://playkit.agentlandlab.com";

        // --- Public Properties ---
        public PlayKit_AuthManager AuthManager { get; set; }

        // --- Private State ---
        private string _currentSessionId;

        private async void Start()
        {
            // Ensure EventSystem exists for UI interaction
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

            // Setup the initial UI state
            identifierPanel.SetActive(true);
            verificationPanel.SetActive(false);
            backBtn.SetActive(false);
            if (errorText != null) errorText.text = "";
            emailToggle.onValueChanged.AddListener((s)=>OnDropDownChanged(s));

            OnDropDownChanged(emailToggle.isOn);
            // Add listeners to the buttons
            sendCodeButton.onClick.AddListener(OnSendCodeClicked);
            verifyButton.onClick.AddListener(OnVerifyClicked);

            dialogue.SetActive(false);
            
            // Set default auth type based on user's region
            await SetDefaultAuthTypeByRegion();
        }

        public void ShowIdentifierModal()
        {
            identifierPanel.SetActive(true);
            verificationPanel.SetActive(false);
            backBtn.SetActive(false);
        }
        private void OnDropDownChanged(bool useEmail)
        {
            identifierIconDisplay.sprite = useEmail ? emailIcon : phoneIcon;
            placeholderText.text = "Enter your " + (useEmail ? "email address" : "phone number (+86 Only)");
        }


        /// <summary>
        /// Method called when the 'Send Code' button is clicked.
        /// </summary>
        private async void OnSendCodeClicked()
        {
            if (errorText != null) errorText.text = ""; // Clear previous errors
            
            string identifier = identifierInput.text;
            // Get type from dropdown. Assumes options are "Email" and "Phone".
            string type = emailToggle.isOn ? "email" : "phone";
            sendCodeButton.interactable = false;

            ShowLoadingModal();
            bool requestSent = await SendVerificationCodeInternal(identifier, type);

            HideLoadingModal();
            if (requestSent)
            {
                // Switch to the verification panel
                identifierPanel.SetActive(false);
                verificationPanel.SetActive(true);
                backBtn.SetActive(true);
                sendCodeButton.interactable = true;
            }
        }

        /// <summary>
        /// Method called when the 'Verify' button is clicked.
        /// </summary>
        private async void OnVerifyClicked()
        {
            if (errorText != null) errorText.text = ""; // Clear previous errors

            string code = codeInput.text;
            await SubmitVerificationCodeInternal(code);
            // After this, the parent AuthManager will check IsSuccess and handle the result.
        }

        #region Internal API Logic
        
        private async UniTask<bool> SendVerificationCodeInternal(string identifier, string type)
        {
            if (string.IsNullOrEmpty(identifier) || (type != "email" && type != "phone"))
            {
                Debug.LogError("[DW Auth] Invalid identifier or type provided.");
                if (errorText != null) errorText.text = "Please enter a valid email or phone number.";
                return false;
            }

            ShowLoadingModal();

            var requestPayload = new SendCodeRequest { identifier = identifier, type = type };
            string jsonPayload = JsonConvert.SerializeObject(requestPayload);
            string endpoint = $"{apiBaseUrl}/api/auth/send-code";

            using (var webRequest = new UnityWebRequest(endpoint, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");

                try
                {
                    await webRequest.SendWebRequest().ToUniTask(cancellationToken: this.destroyCancellationToken);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    sendCodeButton.interactable = true;
                    Debug.LogError($"[DW Auth] API request to send code failed: {ex.Message}");
                    if (errorText != null) errorText.text = "Network error. Please try again.";
                    HideLoadingModal();
                    return false;
                }

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    sendCodeButton.interactable = true;

                    Debug.LogError($"[DW Auth] Send Code API Error: {webRequest.responseCode} - {webRequest.error}\n{webRequest.downloadHandler.text}");
                    if (errorText != null) errorText.text = "Failed to send code. Please check your input and try again.";
                    HideLoadingModal();
                    return false;
                }

                var response = JsonConvert.DeserializeObject<SendCodeResponse>(webRequest.downloadHandler.text);
                if (response == null || !response.success || string.IsNullOrEmpty(response.sessionId))
                {
                    sendCodeButton.interactable = true;
                    Debug.LogError("[DW Auth] Failed to get a valid session ID from the server.");
                    if (errorText != null) errorText.text = "An unexpected error occurred. Please try again later.";
                    HideLoadingModal();
                    return false;
                }

                _currentSessionId = response.sessionId;
                Debug.Log($"[DW Auth] Verification code sent. Session ID: {_currentSessionId}");
                HideLoadingModal();
                return true;
            }
        }
        
        private async UniTask SubmitVerificationCodeInternal(string code)
        {
            if (string.IsNullOrEmpty(_currentSessionId))
            {
                Debug.LogError("[DW Auth] Cannot verify code, no session ID is available. Please request a code first.");
                if (errorText != null) errorText.text = "Session expired. Please request a new code.";
                IsSuccess = false;
                return;
            }
             if (string.IsNullOrEmpty(code) || code.Length < 6)
            {
                if (errorText != null) errorText.text = "Please enter a valid 6-digit code.";
                IsSuccess = false;
                return;
            }

            ShowLoadingModal();

            var requestPayload = new VerifyCodeRequest { sessionId = _currentSessionId, code = code };
            string jsonPayload = JsonConvert.SerializeObject(requestPayload);
            string endpoint = $"{apiBaseUrl}/api/auth/verify-code";
            
            string clerkJwt = null;

            using (var webRequest = new UnityWebRequest(endpoint, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");

                try
                {
                    await webRequest.SendWebRequest().ToUniTask(cancellationToken: this.destroyCancellationToken);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    Debug.LogError($"[DW Auth] API request to verify code failed: {ex.Message}");
                    if (errorText != null) errorText.text = "Network error. Please try again.";
                    IsSuccess = false;
                    HideLoadingModal();
                    return;
                }

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[DW Auth] Verify Code API Error: {webRequest.responseCode} - {webRequest.error}\n{webRequest.downloadHandler.text}");
                    if (errorText != null) errorText.text = "Invalid verification code.";
                    IsSuccess = false;
                    HideLoadingModal();
                    return;
                }
                
                var response = JsonConvert.DeserializeObject<VerifyCodeResponse>(webRequest.downloadHandler.text);
                if (response == null || !response.success || string.IsNullOrEmpty(response.globalToken))
                {
                    Debug.LogError("[DW Auth] Verification failed or did not return a valid token.");
                    if (errorText != null) errorText.text = "Verification failed. Please try again.";
                    IsSuccess = false;
                    HideLoadingModal();
                    return;
                }

                clerkJwt = response.globalToken;
            }

            if (!string.IsNullOrEmpty(clerkJwt))
            {
                IsSuccess = await ExchangeClerkTokenForPlayerToken(clerkJwt);
            }
            else
            {
                IsSuccess = false;
            }
            
            HideLoadingModal();
        }

        #endregion

        #region Private Helper Methods
        
        private async UniTask SetDefaultAuthTypeByRegion()
        {
            ShowLoadingModal();
            Reachability reachability = null;
            using (var webRequest = new UnityWebRequest($"{apiBaseUrl}/api/reachability", "GET"))
            {
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
        
                try 
                { 
                    await webRequest.SendWebRequest().ToUniTask(cancellationToken: destroyCancellationToken); 
                }
                catch (Exception ex) when (!(ex is OperationCanceledException)) 
                { 
                    Debug.LogError($"[DW Auth] Reachability API request failed: {ex.Message}"); 
                }
        
                if (webRequest.result == UnityWebRequest.Result.Success) 
                { 
                    reachability = JsonConvert.DeserializeObject<Reachability>(webRequest.downloadHandler.text);
                }
                else
                {
                    Debug.LogError($"[DW Auth] Reachability API Error: {webRequest.responseCode} - {webRequest.error}\n{webRequest.downloadHandler.text}"); 
                }
            }

            if (reachability != null && reachability.Region == "CN")
            {
                phoneToggle.isOn = true;
            }
            dialogue.SetActive(true);

            HideLoadingModal();
        }
        
        private async UniTask<bool> ExchangeClerkTokenForPlayerToken(string clerkJwt)
        {
            if (AuthManager == null)
            {
                Debug.LogError("[DW Auth] AuthManager reference not set!");
                return false;
            }
            
            var playerClient = AuthManager.PlayerClient;
            if (playerClient == null)
            {
                Debug.LogError("[DW Auth] PlayerClient not available from AuthManager!");
                return false;
            }

            (bool success, string error) exchangeResult = await playerClient.InitializeAsync(clerkJwt, this.GetCancellationTokenOnDestroy());

            if (exchangeResult.success)
            {
                string playerToken = playerClient.GetPlayerToken();
                string expiresAt = playerClient.LastExchangeResponse.ExpiresAt;

                PlayKit_AuthManager.SavePlayerToken(playerToken, expiresAt);
                // Also save to shared token storage for cross-app usage
                PlayKit_LocalSharedToken.SaveToken(playerToken);
                Debug.Log("[DW Auth] JWT exchanged for Player Token and saved successfully (both local and shared).");
                return true;
            }
            else
            {
                Debug.LogError($"[DW Auth] Failed to exchange JWT for a Player Token. Reason: {exchangeResult.error}");
                if (errorText != null) errorText.text = "Final authentication step failed.";
                return false;
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

        #region JSON Data Structures

        [System.Serializable]
        private class Reachability
        {
            [JsonProperty("country")] // Match the JSON property name
            public string Country;
            [JsonProperty("region")]
            public string Region;
            [JsonProperty("city")]
            public string City;
        }

        [System.Serializable]
        private class SendCodeRequest
        {
            public string identifier;
            public string type;
        }

        [System.Serializable]
        private class SendCodeResponse
        {
            public bool success;
            public string sessionId;
        }

        [System.Serializable]
        private class VerifyCodeRequest
        {
            public string sessionId;
            public string code;
        }

        [System.Serializable]
        private class VerifyCodeResponse
        {
            public bool success;
            public string userId;
            public string globalToken;
        }

        #endregion
    }
}
