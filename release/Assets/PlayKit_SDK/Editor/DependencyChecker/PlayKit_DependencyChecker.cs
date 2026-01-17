using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace PlayKit_SDK.Editor
{
    /// <summary>
    /// Checks for required dependencies on Unity Editor startup
    /// and provides one-click installation for missing packages.
    ///
    /// UniTask: Uses embedded .unitypackage for offline installation
    /// Newtonsoft.Json: Installed via Unity Package Manager
    /// </summary>
    [InitializeOnLoad]
    public static class PlayKit_DependencyChecker
    {
        // Embedded UniTask package path (relative to this script)
        private const string UNITASK_PACKAGE_FILENAME = "UniTask.unitypackage";
        private const string UNITASK_ASSET_STORE_URL =
            "https://assetstore.unity.com/packages/tools/integration/unitask-async-await-integration-for-unity-206367";

        private const string NEWTONSOFT_PACKAGE_ID = "com.unity.nuget.newtonsoft-json";
        private const string NEWTONSOFT_VERSION = "3.2.1";

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
                        if (IsUniTaskAvailable() && IsNewtonsoftAvailable())
                        {
                            return;
                        }
                    }
                }
            }

            CheckDependencies();
        }

        private static void CheckDependencies(bool isManual = false)
        {
            EditorPrefs.SetString(LAST_CHECK_KEY, DateTime.Now.ToString());

            bool hasUniTask = IsUniTaskAvailable();
            bool hasNewtonsoft = IsNewtonsoftAvailable();

            // All dependencies installed
            if (hasUniTask && hasNewtonsoft)
            {
                if (isManual)
                {
                    EditorUtility.DisplayDialog(
                        "PlayKit SDK - Dependencies",
                        "All required dependencies are installed.\n\n" +
                        "- UniTask: Installed\n" +
                        "- Newtonsoft.Json: Installed",
                        "OK"
                    );
                }
                return;
            }

            // Check which dependencies are missing
            if (!hasUniTask)
            {
                ShowUniTaskInstallDialog();
            }
            else if (!hasNewtonsoft)
            {
                ShowNewtonsoftInstallDialog();
            }
        }

        /// <summary>
        /// Check if UniTask is available by checking loaded assemblies.
        /// This works regardless of how UniTask was installed (PM, Asset Store, or manual).
        /// </summary>
        private static bool IsUniTaskAvailable()
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
        /// Check if Newtonsoft.Json is available
        /// </summary>
        private static bool IsNewtonsoftAvailable()
        {
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

        /// <summary>
        /// Get the path to the embedded UniTask.unitypackage
        /// </summary>
        private static string GetEmbeddedUniTaskPackagePath()
        {
            // Search for UniTask.unitypackage using AssetDatabase
            string[] guids = AssetDatabase.FindAssets("UniTask");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".unitypackage") && path.Contains("com.playkit.sdk"))
                {
                    return path;
                }
            }

            // Fallback: check known locations with absolute path
            string[] possiblePaths = new[]
            {
                "Packages/com.playkit.sdk/Editor/Dependencies/" + UNITASK_PACKAGE_FILENAME,
                "Packages/com.playkit.sdk/Editor/DependencyChecker/" + UNITASK_PACKAGE_FILENAME,
            };

            foreach (var relativePath in possiblePaths)
            {
                // Convert to absolute path
                string absolutePath = Path.GetFullPath(relativePath);
                if (File.Exists(absolutePath))
                {
                    return relativePath; // Return relative path for AssetDatabase.ImportPackage
                }
            }

            return null;
        }

        private static void ShowUniTaskInstallDialog()
        {
            string embeddedPackagePath = GetEmbeddedUniTaskPackagePath();
            bool hasEmbeddedPackage = !string.IsNullOrEmpty(embeddedPackagePath);

            string message;
            string button0;

            if (hasEmbeddedPackage)
            {
                message = "PlayKit SDK requires UniTask for async/await support.\n\n" +
                          "UniTask is not installed in your project.\n\n" +
                          "Click 'Import UniTask' to import the embedded UniTask package.";
                button0 = "Import UniTask";
            }
            else
            {
                message = "PlayKit SDK requires UniTask for async/await support.\n\n" +
                          "UniTask is not installed in your project.\n\n" +
                          "Please download UniTask from the Asset Store (free) and import it.";
                button0 = "Open Asset Store";
            }

            int option = EditorUtility.DisplayDialogComplex(
                "PlayKit SDK - Missing Dependency",
                message,
                button0,                 // 0
                "Don't Show Again",      // 1
                "Cancel"                 // 2
            );

            switch (option)
            {
                case 0: // Import or Open Asset Store
                    if (hasEmbeddedPackage)
                    {
                        ImportUniTaskPackage(embeddedPackagePath);
                    }
                    else
                    {
                        Application.OpenURL(UNITASK_ASSET_STORE_URL);
                    }
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
        /// Import the embedded UniTask.unitypackage
        /// </summary>
        private static void ImportUniTaskPackage(string packagePath)
        {
            Debug.Log($"[PlayKit SDK] Importing UniTask from: {packagePath}");

            try
            {
                // ImportPackage will show Unity's import dialog
                AssetDatabase.ImportPackage(packagePath, true);

                Debug.Log("[PlayKit SDK] UniTask import dialog opened. Please click 'Import' to complete installation.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit SDK] Failed to import UniTask package: {ex.Message}");
                EditorUtility.DisplayDialog(
                    "Import Failed",
                    $"Failed to import UniTask package:\n\n{ex.Message}\n\n" +
                    "Please download UniTask from the Asset Store instead.",
                    "OK"
                );
                Application.OpenURL(UNITASK_ASSET_STORE_URL);
            }
        }

        [MenuItem("PlayKit SDK/Install UniTask")]
        public static void InstallUniTaskManual()
        {
            if (IsUniTaskAvailable())
            {
                EditorUtility.DisplayDialog(
                    "UniTask Already Installed",
                    "UniTask is already installed in your project.",
                    "OK"
                );
                return;
            }

            string embeddedPackagePath = GetEmbeddedUniTaskPackagePath();

            if (!string.IsNullOrEmpty(embeddedPackagePath))
            {
                ImportUniTaskPackage(embeddedPackagePath);
            }
            else
            {
                int option = EditorUtility.DisplayDialogComplex(
                    "UniTask Not Found",
                    "UniTask is not installed and the embedded package was not found.\n\n" +
                    "Please download UniTask from the Unity Asset Store (it's free).",
                    "Open Asset Store",
                    "Cancel",
                    ""
                );

                if (option == 0)
                {
                    Application.OpenURL(UNITASK_ASSET_STORE_URL);
                }
            }
        }

        #region Newtonsoft.Json Installation

        private static void ShowNewtonsoftInstallDialog()
        {
            int option = EditorUtility.DisplayDialogComplex(
                "PlayKit SDK - Missing Dependency",
                "PlayKit SDK requires Newtonsoft.Json for JSON serialization.\n\n" +
                "Newtonsoft.Json is not installed in your project.\n\n" +
                "Click 'Install Now' to automatically install from Unity Package Manager.",
                "Install Now",           // 0
                "Don't Show Again",      // 1
                "Cancel"                 // 2
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

        private static void InstallNewtonsoft()
        {
            if (_isInstalling)
            {
                Debug.LogWarning("[PlayKit SDK] Installation already in progress.");
                return;
            }

            _isInstalling = true;
            Debug.Log("[PlayKit SDK] Installing Newtonsoft.Json...");

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
                EditorUtility.DisplayProgressBar(
                    "PlayKit SDK",
                    "Installing Newtonsoft.Json...",
                    0.5f
                );
                return;
            }

            EditorApplication.update -= OnNewtonsoftInstallProgress;
            EditorUtility.ClearProgressBar();
            _isInstalling = false;

            if (_addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[PlayKit SDK] Newtonsoft.Json installed successfully: {_addRequest.Result.packageId}");
                EditorUtility.DisplayDialog(
                    "Installation Successful",
                    "Newtonsoft.Json has been installed successfully!\n\n" +
                    "Unity will now recompile scripts.",
                    "OK"
                );
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

        #region Utility Methods

        /// <summary>
        /// Reset the skip preference
        /// </summary>
        [MenuItem("PlayKit SDK/Reset Dependency Check")]
        public static void ResetDependencyCheck()
        {
            EditorPrefs.DeleteKey(SKIP_CHECK_KEY);
            EditorPrefs.DeleteKey(LAST_CHECK_KEY);
            Debug.Log("[PlayKit SDK] Dependency check preferences reset. Check will run on next Editor startup.");

            // Run check immediately
            CheckDependencies(isManual: true);
        }

        #endregion
    }
}
