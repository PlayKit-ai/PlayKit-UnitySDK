using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using PlayKit_SDK;

namespace PlayKit_SDK.Steam.Editor
{
    /// <summary>
    /// Editor extension for Steam addon.
    /// Implements IPlayKitPlatformAddon and IPlayKitAddonEditor to handle game selection changes and custom UI.
    /// Auto-registers as a wrapper around SteamAddonDescriptor.
    /// </summary>
    public class SteamAddonEditorExtension : IPlayKitPlatformAddon, IPlayKitAddonEditor
    {
        private readonly SteamAddonDescriptor _descriptor;

        public SteamAddonEditorExtension()
        {
            _descriptor = new SteamAddonDescriptor();
        }

        // Delegate IPlayKitAddon properties to the runtime descriptor
        public string AddonId => _descriptor.AddonId;
        public string DisplayName => _descriptor.DisplayName;
        public string Description => _descriptor.Description;
        public string Version => _descriptor.Version;
        public string ExclusionGroup => _descriptor.ExclusionGroup;
        public string[] RequiredChannelTypes => _descriptor.RequiredChannelTypes;
        public bool IsInstalled => _descriptor.IsInstalled;

        // Delegate IPlayKitPlatformAddon methods to the runtime descriptor
        public Auth.IAuthProvider GetAuthProvider() => _descriptor.GetAuthProvider();
        public Recharge.IRechargeProvider GetRechargeProvider() => _descriptor.GetRechargeProvider();
        public bool CanProvideServicesForChannel(string channelType) => _descriptor.CanProvideServicesForChannel(channelType);
        public Cysharp.Threading.Tasks.UniTask<bool> InitializeForDeveloperModeAsync() => _descriptor.InitializeForDeveloperModeAsync();

        /// <summary>
        /// Auto-register this editor extension when Unity loads.
        /// This replaces the runtime descriptor in the editor.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void RegisterEditorExtension()
        {
            // Register the editor-enhanced version of the addon
            AddonRegistry.Instance.RegisterAddon(new SteamAddonEditorExtension());
        }

        /// <summary>
        /// Called when game selection changes. Automatically syncs steam_appid.txt for Steam channels.
        /// </summary>
        public void OnGameSelectionChanged(string gameId, string channelType)
        {
            // Only sync for Steam channels
            if (channelType != null && channelType.StartsWith("steam_"))
            {
                SyncSteamAppIdAsync(gameId);
            }
        }

        /// <summary>
        /// Draws custom settings UI in the Addons tab.
        /// Shows steam_appid.txt sync status and sync button.
        /// </summary>
        public void DrawAddonSettings(string currentChannelType)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();

            // Show current sync status
            string currentAppId = SteamAppIdWriter.ReadCurrentAppId();

            EditorGUILayout.LabelField(
                "steam_appid.txt:",
                string.IsNullOrEmpty(currentAppId) ? "Not created" : currentAppId,
                EditorStyles.miniLabel,
                GUILayout.Width(200)
            );

            GUILayout.FlexibleSpace();

            // Sync button
            if (GUILayout.Button("Sync steam_appid.txt", GUILayout.Width(150)))
            {
                var settings = PlayKitSettings.Instance;
                if (string.IsNullOrEmpty(settings.GameId))
                {
                    EditorUtility.DisplayDialog(
                        "Error",
                        "Please select a game first",
                        "OK"
                    );
                }
                else if (!currentChannelType.StartsWith("steam_"))
                {
                    EditorUtility.DisplayDialog(
                        "Error",
                        "Selected game is not a Steam channel",
                        "OK"
                    );
                }
                else
                {
                    SyncSteamAppIdWithDialogAsync(settings.GameId);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private async void SyncSteamAppIdAsync(string gameId)
        {
            try
            {
                var settings = PlayKitSettings.Instance;
                string baseUrl = settings.BaseUrl;
                string developerToken = PlayKitSettings.LocalDeveloperToken;

                if (string.IsNullOrEmpty(developerToken))
                {
                    Debug.LogError("[PlayKit Steam] Cannot sync steam_appid.txt: No developer token");
                    return;
                }

                string endpoint = $"{baseUrl}/api/external/steam-config/{gameId}";

                using (var request = UnityWebRequest.Get(endpoint))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {developerToken}");
                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        await Task.Delay(100);
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.downloadHandler.text);
                        if (response?.ContainsKey("steamConfig") == true)
                        {
                            var config = JsonConvert.DeserializeObject<Dictionary<string, object>>(response["steamConfig"].ToString());
                            string appId = config?.ContainsKey("releaseAppId") == true
                                ? config["releaseAppId"]?.ToString()
                                : null;

                            if (!string.IsNullOrEmpty(appId))
                            {
                                bool success = SteamAppIdWriter.WriteSteamAppId(appId);
                                if (success)
                                {
                                    Debug.Log($"[PlayKit Steam] ✓ Synced steam_appid.txt with App ID: {appId}");
                                }
                            }
                            else
                            {
                                Debug.LogWarning("[PlayKit Steam] Steam App ID not configured on server for this game");
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[PlayKit Steam] Failed to fetch Steam config: {request.error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit Steam] Exception syncing steam_appid.txt: {ex.Message}");
            }
        }

        private async void SyncSteamAppIdWithDialogAsync(string gameId)
        {
            try
            {
                var settings = PlayKitSettings.Instance;
                string baseUrl = settings.BaseUrl;
                string developerToken = PlayKitSettings.LocalDeveloperToken;

                if (string.IsNullOrEmpty(developerToken))
                {
                    EditorUtility.DisplayDialog(
                        "Error",
                        "No developer token available",
                        "OK"
                    );
                    return;
                }

                string endpoint = $"{baseUrl}/api/external/steam-config/{gameId}";

                using (var request = UnityWebRequest.Get(endpoint))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {developerToken}");
                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        await Task.Delay(100);
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.downloadHandler.text);
                        if (response?.ContainsKey("steamConfig") == true)
                        {
                            var config = JsonConvert.DeserializeObject<Dictionary<string, object>>(response["steamConfig"].ToString());
                            string appId = config?.ContainsKey("releaseAppId") == true
                                ? config["releaseAppId"]?.ToString()
                                : null;

                            if (!string.IsNullOrEmpty(appId))
                            {
                                bool success = SteamAppIdWriter.WriteSteamAppId(appId);
                                if (success)
                                {
                                    Debug.Log($"[PlayKit Steam] ✓ Synced steam_appid.txt with App ID: {appId}");
                                    EditorUtility.DisplayDialog(
                                        "Success",
                                        $"steam_appid.txt updated with App ID: {appId}",
                                        "OK"
                                    );
                                }
                                else
                                {
                                    EditorUtility.DisplayDialog(
                                        "Error",
                                        "Failed to write steam_appid.txt. Check console for details.",
                                        "OK"
                                    );
                                }
                            }
                            else
                            {
                                EditorUtility.DisplayDialog(
                                    "Warning",
                                    "Steam App ID not configured on server for this game",
                                    "OK"
                                );
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError($"[PlayKit Steam] Failed to fetch Steam config: {request.error}");
                        EditorUtility.DisplayDialog(
                            "Error",
                            $"Failed to fetch Steam config: {request.error}",
                            "OK"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit Steam] Exception syncing steam_appid.txt: {ex.Message}");
                EditorUtility.DisplayDialog(
                    "Error",
                    $"Exception: {ex.Message}",
                    "OK"
                );
            }
        }
    }
}
