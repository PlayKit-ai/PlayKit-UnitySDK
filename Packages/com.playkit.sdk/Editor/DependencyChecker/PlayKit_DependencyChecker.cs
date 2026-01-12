using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace PlayKit_SDK.Editor
{
    /// <summary>
    /// Checks for required dependencies on Unity Editor startup
    /// and provides one-click installation for missing packages.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayKit_DependencyChecker
    {
        private const string UNITASK_GIT_URL =
            "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask";
        private const string UNITASK_ASSET_STORE_URL =
            "https://assetstore.unity.com/packages/tools/integration/unitask-async-await-integration-for-unity-206367";
        private const string OPENUPM_INSTALL_GUIDE =
            "https://github.com/Cysharp/UniTask#install-via-upm";

        private const string SKIP_CHECK_KEY = "PlayKit_SDK_SkipDependencyCheck";
        private const string LAST_CHECK_KEY = "PlayKit_SDK_LastDependencyCheck";

        private static AddRequest _addRequest;
        private static bool _isInstalling;

        static PlayKit_DependencyChecker()
        {
            // Delay check to avoid interfering with Unity startup
            EditorApplication.delayCall += CheckDependenciesDelayed;
        }

        private static void CheckDependenciesDelayed()
        {
            // Skip if user chose to skip
            if (EditorPrefs.GetBool(SKIP_CHECK_KEY, false))
            {
                return;
            }

            // Only check once per session (check timestamp)
            string lastCheckStr = EditorPrefs.GetString(LAST_CHECK_KEY, "");
            if (!string.IsNullOrEmpty(lastCheckStr))
            {
                if (DateTime.TryParse(lastCheckStr, out DateTime lastCheck))
                {
                    // Only show dialog once per Unity session (approximate by checking time difference)
                    if ((DateTime.Now - lastCheck).TotalMinutes < 1)
                    {
                        // Already checked this session, do quick type check
                        if (IsUniTaskAvailable())
                        {
                            return;
                        }
                    }
                }
            }

            CheckDependencies();
        }

        // [MenuItem("PlayKit SDK/Check Dependencies")]
        // public static void CheckDependenciesManual()
        // {
        //     CheckDependencies(isManual: true);
        // }

        [MenuItem("PlayKit SDK/Install UniTask")]
        public static void InstallUniTaskManual()
        {
            if (_isInstalling)
            {
                EditorUtility.DisplayDialog(
                    "Installation in Progress",
                    "UniTask installation is already in progress. Please wait...",
                    "OK"
                );
                return;
            }

            if (IsUniTaskAvailable())
            {
                EditorUtility.DisplayDialog(
                    "UniTask Already Installed",
                    "UniTask is already installed in your project.",
                    "OK"
                );
                return;
            }

            InstallUniTask();
        }

        private static void CheckDependencies(bool isManual = false)
        {
            EditorPrefs.SetString(LAST_CHECK_KEY, DateTime.Now.ToString());

            // Check if UniTask is available
            if (IsUniTaskAvailable())
            {
                if (isManual)
                {
                    EditorUtility.DisplayDialog(
                        "PlayKit SDK - Dependencies",
                        "All required dependencies are installed.\n\n" +
                        "- UniTask: Installed",
                        "OK"
                    );
                }
                return;
            }

            // UniTask not found, show installation dialog
            ShowUniTaskInstallDialog();
        }

        /// <summary>
        /// Quick check if UniTask types are available
        /// </summary>
        private static bool IsUniTaskAvailable()
        {
            // Try to find the UniTask type by checking multiple possible assembly names
            var unitaskType = Type.GetType("Cysharp.Threading.Tasks.UniTask, UniTask");
            if (unitaskType != null) return true;

            // Also check the full assembly qualified name pattern
            unitaskType = Type.GetType("Cysharp.Threading.Tasks.UniTask, UniTask, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            return unitaskType != null;
        }

        private static void ShowUniTaskInstallDialog()
        {
            int option = EditorUtility.DisplayDialogComplex(
                "PlayKit SDK - Missing Dependency",
                "PlayKit SDK requires UniTask for async/await support.\n\n" +
                "UniTask is not installed in your project.\n\n" +
                "Click 'Install Now' to automatically install UniTask via Git URL.\n" +
                "This will download and install UniTask from GitHub.",
                "Install Now",           // 0 - Returns 0
                "Don't Show Again",      // 1 - Returns 1
                "Manual Install..."      // 2 - Returns 2
            );

            switch (option)
            {
                case 0: // Install Now
                    InstallUniTask();
                    break;
                case 1: // Don't show again
                    EditorPrefs.SetBool(SKIP_CHECK_KEY, true);
                    Debug.LogWarning(
                        "[PlayKit SDK] Dependency check disabled. " +
                        "Re-enable via: PlayKit SDK > Reset Dependency Check"
                    );
                    break;
                case 2: // Manual Install
                    ShowManualInstallOptions();
                    break;
            }
        }

        private static void ShowManualInstallOptions()
        {
            int option = EditorUtility.DisplayDialogComplex(
                "UniTask Installation Options",
                "Choose an installation method:\n\n" +
                "Git URL (Recommended):\n" +
                "Window > Package Manager > + > Add package from git URL\n" +
                "Paste: " + UNITASK_GIT_URL + "\n\n" +
                "OpenUPM:\n" +
                "Add to manifest.json scopedRegistries and dependencies\n\n" +
                "Asset Store:\n" +
                "Download from Unity Asset Store",
                "Copy Git URL",         // 0
                "Close",                 // 1
                "Open Asset Store"       // 2
            );

            switch (option)
            {
                case 0: // Copy Git URL
                    GUIUtility.systemCopyBuffer = UNITASK_GIT_URL;
                    EditorUtility.DisplayDialog(
                        "Git URL Copied",
                        "Git URL has been copied to clipboard.\n\n" +
                        "Steps:\n" +
                        "1. Window > Package Manager\n" +
                        "2. Click '+' button\n" +
                        "3. Select 'Add package from git URL...'\n" +
                        "4. Paste the URL (Ctrl+V) and click 'Add'",
                        "Open Package Manager"
                    );
                    UnityEditor.PackageManager.UI.Window.Open("");
                    break;
                case 2: // Asset Store
                    Application.OpenURL(UNITASK_ASSET_STORE_URL);
                    break;
            }
        }

        /// <summary>
        /// Install UniTask via Package Manager API
        /// </summary>
        private static void InstallUniTask()
        {
            if (_isInstalling)
            {
                Debug.LogWarning("[PlayKit SDK] Installation already in progress.");
                return;
            }

            _isInstalling = true;
            Debug.Log("[PlayKit SDK] Installing UniTask from GitHub...");

            // Show progress bar
            EditorUtility.DisplayProgressBar(
                "PlayKit SDK",
                "Installing UniTask... This may take a moment.",
                0.3f
            );

            try
            {
                _addRequest = Client.Add(UNITASK_GIT_URL);
                EditorApplication.update += OnInstallProgress;
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                _isInstalling = false;
                Debug.LogError($"[PlayKit SDK] Failed to start UniTask installation: {ex.Message}");
                EditorUtility.DisplayDialog(
                    "Installation Failed",
                    $"Failed to start UniTask installation:\n\n{ex.Message}\n\n" +
                    "Please try manual installation via Package Manager.",
                    "OK"
                );
            }
        }

        private static void OnInstallProgress()
        {
            if (_addRequest == null || !_addRequest.IsCompleted)
            {
                // Still in progress, update progress bar
                EditorUtility.DisplayProgressBar(
                    "PlayKit SDK",
                    "Installing UniTask... This may take a moment.",
                    0.5f
                );
                return;
            }

            // Completed, clean up
            EditorApplication.update -= OnInstallProgress;
            EditorUtility.ClearProgressBar();
            _isInstalling = false;

            if (_addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[PlayKit SDK] UniTask installed successfully: {_addRequest.Result.packageId}");
                EditorUtility.DisplayDialog(
                    "Installation Successful",
                    "UniTask has been installed successfully!\n\n" +
                    "Unity will now recompile scripts. " +
                    "PlayKit SDK is ready to use after recompilation.",
                    "OK"
                );

                // Force script recompilation
                AssetDatabase.Refresh();
            }
            else
            {
                string errorMessage = _addRequest.Error?.message ?? "Unknown error";
                Debug.LogError($"[PlayKit SDK] Failed to install UniTask: {errorMessage}");

                int option = EditorUtility.DisplayDialogComplex(
                    "Installation Failed",
                    $"Failed to install UniTask:\n\n{errorMessage}\n\n" +
                    "This might be due to network issues or firewall restrictions.\n" +
                    "Would you like to try manual installation?",
                    "Copy Git URL",
                    "Cancel",
                    "Open Asset Store"
                );

                switch (option)
                {
                    case 0:
                        GUIUtility.systemCopyBuffer = UNITASK_GIT_URL;
                        UnityEditor.PackageManager.UI.Window.Open("");
                        break;
                    case 2:
                        Application.OpenURL(UNITASK_ASSET_STORE_URL);
                        break;
                }
            }

            _addRequest = null;
        }

        /// <summary>
        /// Reset the skip preference (useful for testing or re-enabling check)
        /// </summary>
        // [MenuItem("PlayKit SDK/Reset Dependency Check")]
        // public static void ResetDependencyCheck()
        // {
        //     EditorPrefs.DeleteKey(SKIP_CHECK_KEY);
        //     EditorPrefs.DeleteKey(LAST_CHECK_KEY);
        //     Debug.Log("[PlayKit SDK] Dependency check preferences reset. Check will run on next Editor startup.");
        // }
    }
}
