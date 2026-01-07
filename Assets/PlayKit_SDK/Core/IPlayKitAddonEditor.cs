#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PlayKit_SDK
{
    /// <summary>
    /// Optional interface for addons that need to respond to editor events.
    /// Provides hooks for game selection changes and custom settings UI.
    /// </summary>
    public interface IPlayKitAddonEditor
    {
#if UNITY_EDITOR
        /// <summary>
        /// Called when the selected game changes in PlayKit Settings.
        /// Addons can use this to sync configuration files, update settings, etc.
        /// </summary>
        /// <param name="gameId">The newly selected game ID</param>
        /// <param name="channelType">The channel type of the game (e.g., "standalone", "steam_release")</param>
        void OnGameSelectionChanged(string gameId, string channelType);

        /// <summary>
        /// Draws additional settings UI in the Addons tab.
        /// Called after the standard addon card UI (toggle, description, etc.).
        /// </summary>
        /// <param name="currentChannelType">The current selected game's channel type</param>
        void DrawAddonSettings(string currentChannelType);
#endif
    }
}
