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
        private const string UNITASK_PACKAGE_FILENAME = "UniTask.unitypackage";

        private const string NEWTONSOFT_PACKAGE_ID = "com.unity.nuget.newtonsoft-json";
        private const string NEWTONSOFT_VERSION = "3.2.1";

        private const string SKIP_CHECK_KEY = "PlayKit_SDK_SkipDependencyCheck";
        private const string LAST_CHECK_KEY = "PlayKit_SDK_LastDependencyCheck";

        private static AddRequest _addRequest;
        private static bool _isInstalling;

        static PlayKit_DependencyChecker()
        {
            EditorApplication.delayCall += CheckDependenciesDelayed;
        }

        private static void CheckDependenciesDelayed()
        {
            if (EditorPrefs.GetBool(SKIP_CHECK_KEY, false))
            {
                return;
            }

            string lastCheckStr = EditorPrefs.GetString(LAST_CHECK_KEY, "");
            if (!string.IsNullOrEmpty(lastCheckStr))
            {
                if (DateTime.TryParse(lastCheckStr, out DateTime lastCheck))
                {
                    if ((DateTime.Now - lastCheck).TotalMinutes < 1)
                    {
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

            if (hasUniTask && hasNewtonsoft)
            {
                if (isManual)
                {
                    EditorUtility.DisplayDialog(
                        "PlayKit SDK - Dependencies",
                        "All required dependencies are installed.\n\n" +
                        "✓ UniTask: Installed\n" +
                        "✓ Newtonsoft.Json: Installed",
                        "OK"
                    );
                }
                return;
            }

            if (!hasUniTask)
            {
                ShowUniTaskInstallDialog();
            }
            else if (!hasNewtonsoft)
            {
                ShowNewtonsoftInstallDialog();
            }
        }

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
        /// Get the absolute path to the embedded UniTask.unitypackage
        /// </summary>
        private static string GetEmbeddedUniTaskPackagePath()
        {
            // Method 1: Find via script asset path (works for both Packages/ and Assets/ locations)
            var scriptGuids = AssetDatabase.FindAssets("PlayKit_DependencyChecker t:MonoScript");
            foreach (string guid in scriptGuids)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                if (scriptPath.EndsWith("PlayKit_DependencyChecker.cs"))
                {
                    // Get the Editor folder (parent of DependencyChecker folder)
                    string dependencyCheckerFolder = Path.GetDirectoryName(scriptPath);
                    string editorFolder = Path.GetDirectoryName(dependencyCheckerFolder);

                    // Build the relative path to Dependencies folder
                    string dependenciesRelativePath = Path.Combine(editorFolder, "Dependencies", UNITASK_PACKAGE_FILENAME);
                    dependenciesRelativePath = dependenciesRelativePath.Replace("\\", "/");

                    // Convert Unity relative path to absolute path
                    // For Assets/... paths: combine with Application.dataPath
                    // For Packages/... paths: use Path.GetFullPath
                    string absolutePath;
                    if (dependenciesRelativePath.StartsWith("Assets/"))
                    {
                        // Application.dataPath returns ".../Assets", so we need to go up one level
                        string projectRoot = Path.GetDirectoryName(Application.dataPath);
                        absolutePath = Path.Combine(projectRoot, dependenciesRelativePath);
                    }
                    else
                    {
                        absolutePath = Path.GetFullPath(dependenciesRelativePath);
                    }

                    absolutePath = absolutePath.Replace("\\", "/");
                    Debug.Log($"[PlayKit SDK] Checking UniTask package at: {absolutePath}");

                    if (File.Exists(absolutePath))
                    {
                        return absolutePath;
                    }

                    // Also check in same folder as this script
                    string sameFolderRelativePath = Path.Combine(dependencyCheckerFolder, UNITASK_PACKAGE_FILENAME);
                    sameFolderRelativePath = sameFolderRelativePath.Replace("\\", "/");

                    if (sameFolderRelativePath.StartsWith("Assets/"))
                    {
                        string projectRoot = Path.GetDirectoryName(Application.dataPath);
                        absolutePath = Path.Combine(projectRoot, sameFolderRelativePath);
                    }
                    else
                    {
                        absolutePath = Path.GetFullPath(sameFolderRelativePath);
                    }

                    absolutePath = absolutePath.Replace("\\", "/");
                    if (File.Exists(absolutePath))
                    {
                        return absolutePath;
                    }
                }
            }

            Debug.LogWarning("[PlayKit SDK] UniTask.unitypackage not found in SDK package.");
            return null;
        }

        private static void ShowUniTaskInstallDialog()
        {
            string embeddedPackagePath = GetEmbeddedUniTaskPackagePath();
            bool hasEmbeddedPackage = !string.IsNullOrEmpty(embeddedPackagePath);

            if (hasEmbeddedPackage)
            {
                int option = EditorUtility.DisplayDialogComplex(
                    "PlayKit SDK - Missing Dependency",
                    "PlayKit SDK requires UniTask for async/await support.\n\n" +
                    "UniTask is not installed in your project.\n\n" +
                    "Click 'Import UniTask' to import the embedded UniTask package.",
                    "Import UniTask",        // 0
                    "Don't Show Again",      // 1
                    "Cancel"                 // 2
                );

                switch (option)
                {
                    case 0:
                        ImportUniTaskPackage(embeddedPackagePath);
                        break;
                    case 1:
                        EditorPrefs.SetBool(SKIP_CHECK_KEY, true);
                        Debug.LogWarning("[PlayKit SDK] Dependency check disabled.");
                        break;
                }
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "PlayKit SDK - Error",
                    "PlayKit SDK requires UniTask, but the embedded UniTask.unitypackage was not found.\n\n" +
                    "Please ensure the SDK package is complete.\n\n" +
                    "Expected location:\n" +
                    "Packages/ai.playkit.sdk/Editor/Dependencies/UniTask.unitypackage",
                    "OK"
                );
            }
        }

        private static void ImportUniTaskPackage(string packagePath)
        {
            Debug.Log($"[PlayKit SDK] Importing UniTask from: {packagePath}");

            try
            {
                AssetDatabase.ImportPackage(packagePath, true);
                Debug.Log("[PlayKit SDK] UniTask import dialog opened. Please click 'Import' to complete.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit SDK] Failed to import UniTask package: {ex.Message}");
                EditorUtility.DisplayDialog(
                    "Import Failed",
                    $"Failed to import UniTask package:\n\n{ex.Message}",
                    "OK"
                );
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
                EditorUtility.DisplayDialog(
                    "UniTask Package Not Found",
                    "The embedded UniTask.unitypackage was not found.\n\n" +
                    "Please ensure the SDK package is complete.\n\n" +
                    "Expected location:\n" +
                    "Packages/ai.playkit.sdk/Editor/Dependencies/UniTask.unitypackage",
                    "OK"
                );
            }
        }

        #region Newtonsoft.Json Installation

        private static void ShowNewtonsoftInstallDialog()
        {
            int option = EditorUtility.DisplayDialogComplex(
                "PlayKit SDK - Missing Dependency",
                "PlayKit SDK requires Newtonsoft.Json for JSON serialization.\n\n" +
                "Newtonsoft.Json is not installed in your project.\n\n" +
                "Click 'Install Now' to install from Unity Package Manager.",
                "Install Now",
                "Don't Show Again",
                "Cancel"
            );

            switch (option)
            {
                case 0:
                    InstallNewtonsoft();
                    break;
                case 1:
                    EditorPrefs.SetBool(SKIP_CHECK_KEY, true);
                    Debug.LogWarning("[PlayKit SDK] Dependency check disabled.");
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

            EditorUtility.DisplayProgressBar("PlayKit SDK", "Installing Newtonsoft.Json...", 0.3f);

            try
            {
                _addRequest = Client.Add($"{NEWTONSOFT_PACKAGE_ID}@{NEWTONSOFT_VERSION}");
                EditorApplication.update += OnNewtonsoftInstallProgress;
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                _isInstalling = false;
                Debug.LogError($"[PlayKit SDK] Failed to install Newtonsoft.Json: {ex.Message}");
                EditorUtility.DisplayDialog(
                    "Installation Failed",
                    $"Failed to install Newtonsoft.Json:\n\n{ex.Message}",
                    "OK"
                );
            }
        }

        private static void OnNewtonsoftInstallProgress()
        {
            if (_addRequest == null || !_addRequest.IsCompleted)
            {
                EditorUtility.DisplayProgressBar("PlayKit SDK", "Installing Newtonsoft.Json...", 0.5f);
                return;
            }

            EditorApplication.update -= OnNewtonsoftInstallProgress;
            EditorUtility.ClearProgressBar();
            _isInstalling = false;

            if (_addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[PlayKit SDK] Newtonsoft.Json installed: {_addRequest.Result.packageId}");
                EditorUtility.DisplayDialog(
                    "Installation Successful",
                    "Newtonsoft.Json has been installed!\n\nUnity will now recompile scripts.",
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
                    "Please install manually via Package Manager.",
                    "OK"
                );
            }

            _addRequest = null;
        }

        #endregion

        [MenuItem("PlayKit SDK/Reset Dependency Check")]
        public static void ResetDependencyCheck()
        {
            EditorPrefs.DeleteKey(SKIP_CHECK_KEY);
            EditorPrefs.DeleteKey(LAST_CHECK_KEY);
            Debug.Log("[PlayKit SDK] Dependency check reset.");

            // Trigger script define symbols update
            PlayKit_ScriptDefineManager.UpdateScriptDefines();

            CheckDependencies(isManual: true);
        }
    }
}
