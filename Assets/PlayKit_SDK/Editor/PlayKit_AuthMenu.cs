using UnityEditor;
using UnityEngine;

namespace PlayKit_SDK.Auth
{
    /// <summary>
    /// Adds menu items to the Unity Editor for PlayKit SDK tools and utilities.
    /// </summary>
    public static class PlayKit_AuthMenu
    {


        /// <summary>
        /// Opens the PlayKit documentation in the default browser.
        /// </summary>
        [MenuItem("PlayKit SDK/Documentation", priority = 52)]
        private static void OpenDocumentation()
        {
            Application.OpenURL("https://docs.playkit.agentlandlab.com");
        }

       

        /// <summary>
        /// Clears the locally stored Player Token using PlayerPrefs.
        /// </summary>
        [MenuItem("PlayKit SDK/Clear Local Player Token", priority = 100)]
        private static void ClearLocalPlayerToken()
        {
            // Call the static method from your existing AuthManager
            PlayKit_AuthManager.ClearPlayerToken();

            // Log a confirmation message to the Unity Console
            Debug.Log("[PlayKit SDK] Local player token and expiry have been cleared from PlayerPrefs.");
        }
    }
}