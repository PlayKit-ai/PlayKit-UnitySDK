using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using PlayKit_SDK;

namespace PlayKit.SDK.Editor
{
    /// <summary>
    /// Build preprocessor that injects developer token into builds when enabled.
    /// The token is written to a temporary Resources file during build and cleaned up afterward.
    /// </summary>
    public class PlayKitBuildTokenInjector : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string TOKEN_FILE_PATH = "Assets/Resources/PlayKit_BuildToken.txt";
        private const string RESOURCES_DIR = "Assets/Resources";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var settings = PlayKitSettings.Instance;
            if (settings == null)
            {
                return;
            }

            // Check if developer token injection is enabled
            if (!settings.ForceDeveloperTokenInBuild)
            {
                return;
            }

            Debug.Log("[PlayKit Build] Developer token injection is enabled");

            // Get token from EditorPrefs
            string token = EditorPrefs.GetString("PlayKit_LocalDeveloperToken", "");

            if (string.IsNullOrEmpty(token))
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "⚠️ Build Error",
                    "Developer token injection is enabled, but no developer token found in EditorPrefs.\n\n" +
                    "Please configure a developer token in Tools > PlayKit SDK > Settings first, or disable " +
                    "'Force Developer Token Injection At Build'.",
                    "Cancel Build"
                );
                throw new BuildFailedException("Developer token not found in EditorPrefs");
            }

            // Show security warning
            bool confirmedRisk = EditorUtility.DisplayDialog(
                "⚠️ SECURITY WARNING - Developer Token Injection",
                "You are about to build with DEVELOPER TOKEN INJECTION enabled!\n\n" +
                "⚠️ RISKS:\n" +
                "• Your developer token will be embedded in the build\n" +
                "• Anyone who decompiles the game can extract the token\n" +
                "• The token provides API access with your credentials\n\n" +
                "This feature should ONLY be used for:\n" +
                "• Internal testing builds\n" +
                "• Development/staging environments\n\n" +
                "NEVER use this for production or public releases!\n\n" +
                "Token preview: " + MaskToken(token) + "\n\n" +
                "Do you understand the risks and want to proceed?",
                "Yes, I Understand the Risk",
                "Cancel Build"
            );

            if (!confirmedRisk)
            {
                throw new BuildFailedException("Build cancelled by user due to developer token injection warning");
            }

            try
            {
                // Ensure Resources directory exists
                if (!Directory.Exists(RESOURCES_DIR))
                {
                    Directory.CreateDirectory(RESOURCES_DIR);
                    Debug.Log($"[PlayKit Build] Created Resources directory: {RESOURCES_DIR}");
                }

                // Write token to temporary file
                File.WriteAllText(TOKEN_FILE_PATH, token);
                Debug.Log($"[PlayKit Build] ✓ Developer token injected to: {TOKEN_FILE_PATH}");

                // Refresh AssetDatabase to include the file in build
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlayKit Build] Failed to inject developer token: {ex.Message}");
                throw new BuildFailedException($"Failed to inject developer token: {ex.Message}");
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            // Clean up the temporary token file after build
            if (File.Exists(TOKEN_FILE_PATH))
            {
                try
                {
                    File.Delete(TOKEN_FILE_PATH);

                    // Also delete .meta file
                    string metaPath = TOKEN_FILE_PATH + ".meta";
                    if (File.Exists(metaPath))
                    {
                        File.Delete(metaPath);
                    }

                    Debug.Log("[PlayKit Build] ✓ Cleaned up temporary token file");

                    AssetDatabase.Refresh();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[PlayKit Build] Failed to clean up token file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Masks a token for display, showing only last 10 characters
        /// </summary>
        private string MaskToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return "EMPTY";
            }

            if (token.Length <= 10)
            {
                return "***" + token;
            }

            return "***" + token.Substring(token.Length - 10);
        }
    }
}
