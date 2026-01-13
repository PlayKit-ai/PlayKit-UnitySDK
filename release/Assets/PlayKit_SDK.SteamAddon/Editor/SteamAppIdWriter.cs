using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace PlayKit_SDK.Steam.Editor
{
    /// <summary>
    /// Utility for managing steam_appid.txt in Unity project root.
    /// Required by Facepunch.Steamworks for Editor testing.
    ///
    /// Note: steam_appid.txt is only needed in the Unity Editor for testing.
    /// Production builds do not require this file as Steam handles it automatically.
    /// </summary>
    public static class SteamAppIdWriter
    {
        private const string FILENAME = "steam_appid.txt";

        /// <summary>
        /// Get the full path to steam_appid.txt (project root)
        /// </summary>
        private static string GetFilePath()
        {
            // Application.dataPath points to "Assets/"
            // We need to go up one level to project root
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, FILENAME);
        }

        /// <summary>
        /// Write Steam App ID to steam_appid.txt
        /// </summary>
        /// <param name="appId">Steam App ID (numeric string)</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool WriteSteamAppId(string appId)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(appId))
            {
                Debug.LogError($"[SteamAppIdWriter] Cannot write steam_appid.txt: App ID is empty");
                return false;
            }

            // Validate numeric
            if (!uint.TryParse(appId, out _))
            {
                Debug.LogError($"[SteamAppIdWriter] Invalid Steam App ID: '{appId}' (must be numeric)");
                return false;
            }

            try
            {
                string filePath = GetFilePath();

                // Write App ID to file
                File.WriteAllText(filePath, appId.Trim());

                Debug.Log($"[SteamAppIdWriter] ✓ Updated {FILENAME} with App ID: {appId}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamAppIdWriter] Failed to write {FILENAME}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Read current steam_appid.txt if it exists
        /// </summary>
        /// <returns>App ID string, or null if file doesn't exist</returns>
        public static string ReadCurrentAppId()
        {
            try
            {
                string filePath = GetFilePath();

                if (!File.Exists(filePath))
                {
                    return null;
                }

                string content = File.ReadAllText(filePath).Trim();
                return string.IsNullOrWhiteSpace(content) ? null : content;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SteamAppIdWriter] Failed to read {FILENAME}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if steam_appid.txt exists and matches expected App ID
        /// </summary>
        /// <param name="expectedAppId">Expected Steam App ID</param>
        /// <returns>True if file exists and matches</returns>
        public static bool IsSynced(string expectedAppId)
        {
            if (string.IsNullOrWhiteSpace(expectedAppId))
            {
                return false;
            }

            string currentAppId = ReadCurrentAppId();
            return currentAppId == expectedAppId.Trim();
        }

        /// <summary>
        /// Delete steam_appid.txt if it exists
        /// </summary>
        public static bool DeleteFile()
        {
            try
            {
                string filePath = GetFilePath();

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Debug.Log($"[SteamAppIdWriter] Deleted {FILENAME}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamAppIdWriter] Failed to delete {FILENAME}: {ex.Message}");
                return false;
            }
        }
    }
}
