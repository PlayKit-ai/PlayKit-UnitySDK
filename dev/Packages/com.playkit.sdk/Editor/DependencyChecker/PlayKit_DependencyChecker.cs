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

        private const string NEWTONSOFT_PACKAGE_ID = "com.unity.nuget.newtonsoft-json";
        private const string NEWTONSOFT_VERSION = "3.2.1";

        private const string SKIP_CHECK_KEY = "PlayKit_SDK_SkipDependencyCheck";
        private const string LAST_CHECK_KEY = "PlayKit_SDK_LastDependencyCheck";

        private static AddRequest _addRequest;
        private static bool _isInstalling;
        private static string _installingPackage;

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
                        if (IsUniTaskAvailable() && IsNewtonsoftAvailable())
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

            // Check if installed via Package Manager (required for versionDefines to work)
            if (IsUniTaskInstalledViaPackageManager())
            {
                EditorUtility.DisplayDialog(
                    "UniTask Already Installed",
                    "UniTask is already installed via Package Manager.",
                    "OK"
                );
                return;
            }

            // Check if UniTask exists but not via PM (manual install)
            if (IsUniTaskAssemblyAvailable())
            {
                int option = EditorUtility.DisplayDialogComplex(
                    "UniTask Installation Issue",
                    "UniTask is installed in your project, but NOT via Package Manager.\n\n" +
                    "PlayKit SDK requires UniTask to be installed via Package Manager " +
                    "for proper integration (scripting define symbols).\n\n" +
                    "Please remove the manually installed UniTask and install via Package Manager.",
                    "Install via PM",
                    "Cancel",
                    "Copy Git URL"
                );

                switch (option)
                {
                    case 0: // Install via PM
                        InstallUniTask();
                        break;
                    case 2: // Copy Git URL
                        GUIUtility.systemCopyBuffer = UNITASK_GIT_URL;
                        EditorUtility.DisplayDialog(
                            "Git URL Copied",
                            "Please remove the existing UniTask first, then:\n\n" +
                            "1. Window > Package Manager\n" +
                            "2. Click '+' > 'Add package from git URL...'\n" +
                            "3. Paste the URL and click 'Add'",
                            "OK"
                        );
                        break;
                }
                return;
            }

            InstallUniTask();
        }

        private static void CheckDependencies(bool isManual = false)
        {
            EditorPrefs.SetString(LAST_CHECK_KEY, DateTime.Now.ToString());

            bool hasUniTaskViaPM = IsUniTaskInstalledViaPackageManager();
            bool hasUniTaskAssembly = IsUniTaskAssemblyAvailable();
            bool hasNewtonsoft = IsNewtonsoftAvailable();

            // All dependencies properly installed via Package Manager
            if (hasUniTaskViaPM && hasNewtonsoft)
            {
                if (isManual)
                {
                    EditorUtility.DisplayDialog(
                        "PlayKit SDK - Dependencies",
                        "All required dependencies are installed.\n\n" +
                        "- UniTask: Installed (via Package Manager)\n" +
                        "- Newtonsoft.Json: Installed",
                        "OK"
                    );
                }
                return;
            }

            // Check for manual UniTask installation (not via PM)
            if (!hasUniTaskViaPM && hasUniTaskAssembly)
            {
                ShowUniTaskManualInstallWarning();
            }
            // UniTask not installed at all
            else if (!hasUniTaskViaPM)
            {
                ShowUniTaskInstallDialog();
            }
            // UniTask OK, check Newtonsoft
            else if (!hasNewtonsoft)
            {
                ShowNewtonsoftInstallDialog();
            }
        }

        /// <summary>
        /// Show warning when UniTask is manually installed but not via Package Manager
        /// </summary>
        private static void ShowUniTaskManualInstallWarning()
        {
            int option = EditorUtility.DisplayDialogComplex(
                "PlayKit SDK - UniTask Installation Issue",
                "UniTask is detected in your project, but it was NOT installed via Package Manager.\n\n" +
                "PlayKit SDK requires UniTask to be installed via Package Manager for proper integration.\n\n" +
                "Please:\n" +
                "1. Remove the manually installed UniTask from your Assets folder\n" +
                "2. Install UniTask via Package Manager (Git URL or OpenUPM)\n\n" +
                "This ensures scripting define symbols are set correctly.",
                "Install via Git URL",
                "Don't Show Again",
                "Manual Install Guide"
            );

            switch (option)
            {
                case 0: // Install via Git URL
                    InstallUniTask();
                    break;
                case 1: // Don't show again
                    EditorPrefs.SetBool(SKIP_CHECK_KEY, true);
                    Debug.LogWarning(
                        "[PlayKit SDK] Dependency check disabled. " +
                        "Re-enable via: PlayKit SDK > Reset Dependency Check"
                    );
                    break;
                case 2: // Manual Install Guide
                    ShowManualInstallOptions();
                    break;
            }
        }

        /// <summary>
        /// Check if UniTask is properly installed via Package Manager.
        /// This is required for versionDefines to work and define PLAYKIT_UNITASK_SUPPORT.
        /// </summary>
        private static bool IsUniTaskInstalledViaPackageManager()
        {
            var listRequest = Client.List(true);
            while (!listRequest.IsCompleted)
            {
                System.Threading.Thread.Sleep(10);
            }

            if (listRequest.Status == StatusCode.Success)
            {
                foreach (var package in listRequest.Result)
                {
                    if (package.name == "com.cysharp.unitask")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Check if UniTask assembly exists (may be manually installed)
        /// </summary>
        private static bool IsUniTaskAssemblyAvailable()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "UniTask")
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Quick check if UniTask is available (via PM or assembly)
        /// For dependency check, we need PM installation for proper integration.
        /// </summary>
        private static bool IsUniTaskAvailable()
        {
            // For SDK to work properly, UniTask must be installed via Package Manager
            // so that versionDefines can set PLAYKIT_UNITASK_SUPPORT
            return IsUniTaskInstalledViaPackageManager();
        }

        /// <summary>
        /// Quick check if Newtonsoft.Json is available
        /// </summary>
        private static bool IsNewtonsoftAvailable()
        {
            // Check if Newtonsoft.Json package is listed in Package Manager
            var listRequest = Client.List(true);
            while (!listRequest.IsCompleted)
            {
                System.Threading.Thread.Sleep(10);
            }

            if (listRequest.Status == StatusCode.Success)
            {
                foreach (var package in listRequest.Result)
                {
                    if (package.name == NEWTONSOFT_PACKAGE_ID)
                    {
                        return true;
                    }
                }
            }

            // Fallback: check assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "Newtonsoft.Json" ||
                    assembly.GetName().Name == "Unity.Newtonsoft.Json")
                {
                    return true;
                }
            }

            return false;
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
            _installingPackage = null;
        }

        #region Newtonsoft.Json Installation

        private static void ShowNewtonsoftInstallDialog()
        {
            int option = EditorUtility.DisplayDialogComplex(
                "PlayKit SDK - Missing Dependency",
                "PlayKit SDK requires Newtonsoft.Json for JSON serialization.\n\n" +
                "Newtonsoft.Json is not installed in your project.\n\n" +
                "Click 'Install Now' to automatically install from Unity Package Manager.",
                "Install Now",           // 0 - Returns 0
                "Don't Show Again",      // 1 - Returns 1
                "Cancel"                 // 2 - Returns 2
            );

            switch (option)
            {
                case 0: // Install Now
                    InstallNewtonsoft();
                    break;
                case 1: // Don't show again
                    EditorPrefs.SetBool(SKIP_CHECK_KEY, true);
                    Debug.LogWarning(
                        "[PlayKit SDK] Dependency check disabled. " +
                        "Re-enable via: PlayKit SDK > Reset Dependency Check"
                    );
                    break;
            }
        }

        /// <summary>
        /// Install Newtonsoft.Json via Package Manager API
        /// </summary>
        private static void InstallNewtonsoft()
        {
            if (_isInstalling)
            {
                Debug.LogWarning("[PlayKit SDK] Installation already in progress.");
                return;
            }

            _isInstalling = true;
            _installingPackage = "Newtonsoft.Json";
            Debug.Log("[PlayKit SDK] Installing Newtonsoft.Json...");

            // Show progress bar
            EditorUtility.DisplayProgressBar(
                "PlayKit SDK",
                "Installing Newtonsoft.Json...",
                0.3f
            );

            try
            {
                _addRequest = Client.Add($"{NEWTONSOFT_PACKAGE_ID}@{NEWTONSOFT_VERSION}");
                EditorApplication.update += OnNewtonsoftInstallProgress;
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                _isInstalling = false;
                _installingPackage = null;
                Debug.LogError($"[PlayKit SDK] Failed to start Newtonsoft.Json installation: {ex.Message}");
                EditorUtility.DisplayDialog(
                    "Installation Failed",
                    $"Failed to start Newtonsoft.Json installation:\n\n{ex.Message}\n\n" +
                    "Please try manual installation via Package Manager.",
                    "OK"
                );
            }
        }

        private static void OnNewtonsoftInstallProgress()
        {
            if (_addRequest == null || !_addRequest.IsCompleted)
            {
                // Still in progress, update progress bar
                EditorUtility.DisplayProgressBar(
                    "PlayKit SDK",
                    "Installing Newtonsoft.Json...",
                    0.5f
                );
                return;
            }

            // Completed, clean up
            EditorApplication.update -= OnNewtonsoftInstallProgress;
            EditorUtility.ClearProgressBar();
            _isInstalling = false;
            _installingPackage = null;

            if (_addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[PlayKit SDK] Newtonsoft.Json installed successfully: {_addRequest.Result.packageId}");
                EditorUtility.DisplayDialog(
                    "Installation Successful",
                    "Newtonsoft.Json has been installed successfully!\n\n" +
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
                Debug.LogError($"[PlayKit SDK] Failed to install Newtonsoft.Json: {errorMessage}");

                EditorUtility.DisplayDialog(
                    "Installation Failed",
                    $"Failed to install Newtonsoft.Json:\n\n{errorMessage}\n\n" +
                    "Please install manually via Package Manager:\n" +
                    "Window > Package Manager > + > Add package by name\n" +
                    $"Name: {NEWTONSOFT_PACKAGE_ID}\n" +
                    $"Version: {NEWTONSOFT_VERSION}",
                    "OK"
                );
            }

            _addRequest = null;
        }

        #endregion

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
