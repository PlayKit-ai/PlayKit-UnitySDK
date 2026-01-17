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

            // Always ensure scripting define symbols are set for manual installs
            // This handles the case where user manually installed UniTask
            if (!IsUniTaskInstalledViaPackageManager() && IsUniTaskAssemblyAvailable())
            {
                EnsureScriptingDefineSymbols();
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

            // Check if already installed (via PM or manually)
            bool installedViaPM = IsUniTaskInstalledViaPackageManager();
            bool installedManually = IsUniTaskAssemblyAvailable();

            if (installedViaPM || installedManually)
            {
                // Ensure scripting define symbols are set for manual installs
                if (!installedViaPM && installedManually)
                {
                    EnsureScriptingDefineSymbols();
                }

                string status = installedViaPM ? "via Package Manager" : "manually (scripting symbols configured)";
                EditorUtility.DisplayDialog(
                    "UniTask Already Installed",
                    $"UniTask is already installed {status}.",
                    "OK"
                );
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

            // Check if UniTask is available (either via PM or manual install)
            bool hasUniTask = hasUniTaskViaPM || hasUniTaskAssembly;

            // If manually installed, ensure scripting define symbols are set
            if (!hasUniTaskViaPM && hasUniTaskAssembly)
            {
                EnsureScriptingDefineSymbols();
            }

            // All dependencies installed
            if (hasUniTask && hasNewtonsoft)
            {
                if (isManual)
                {
                    string uniTaskStatus = hasUniTaskViaPM ? "Installed (via Package Manager)" : "Installed (manual)";
                    EditorUtility.DisplayDialog(
                        "PlayKit SDK - Dependencies",
                        "All required dependencies are installed.\n\n" +
                        $"- UniTask: {uniTaskStatus}\n" +
                        "- Newtonsoft.Json: Installed",
                        "OK"
                    );
                }
                return;
            }

            // UniTask not installed at all
            if (!hasUniTask)
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
        /// Ensure scripting define symbols are set for manually installed packages.
        /// This is needed because versionDefines only work with Package Manager installations.
        /// </summary>
        private static void EnsureScriptingDefineSymbols()
        {
            var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (buildTargetGroup == BuildTargetGroup.Unknown)
                return;

            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
            var defineList = new System.Collections.Generic.List<string>(defines.Split(';'));
            bool changed = false;

            // Add PLAYKIT_UNITASK_SUPPORT if UniTask assembly is available
            if (IsUniTaskAssemblyAvailable() && !defineList.Contains("PLAYKIT_UNITASK_SUPPORT"))
            {
                defineList.Add("PLAYKIT_UNITASK_SUPPORT");
                changed = true;
                Debug.Log("[PlayKit SDK] Added PLAYKIT_UNITASK_SUPPORT scripting define symbol for manually installed UniTask.");
            }

            // Add PLAYKIT_NEWTONSOFT_SUPPORT if Newtonsoft assembly is available
            if (IsNewtonsoftAssemblyAvailable() && !defineList.Contains("PLAYKIT_NEWTONSOFT_SUPPORT"))
            {
                defineList.Add("PLAYKIT_NEWTONSOFT_SUPPORT");
                changed = true;
                Debug.Log("[PlayKit SDK] Added PLAYKIT_NEWTONSOFT_SUPPORT scripting define symbol.");
            }

            if (changed)
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, string.Join(";", defineList));
            }
        }

        /// <summary>
        /// Check if Newtonsoft.Json assembly exists
        /// </summary>
        private static bool IsNewtonsoftAssemblyAvailable()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (name == "Newtonsoft.Json" || name == "Unity.Newtonsoft.Json")
                {
                    return true;
                }
            }
            return false;
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
        /// Quick check if UniTask is available (via PM or manual install)
        /// </summary>
        private static bool IsUniTaskAvailable()
        {
            return IsUniTaskInstalledViaPackageManager() || IsUniTaskAssemblyAvailable();
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
