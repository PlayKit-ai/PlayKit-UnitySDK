using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace PlayKit_SDK.Editor
{
    /// <summary>
    /// Repository-pinned PlayKit game identity, stored in a git-trackable
    /// "playkit.json" at the Unity project root (next to Assets/).
    ///
    /// Written when a developer key is authorized and bound to a game, and read
    /// when starting editor device-auth: a pinned game id is sent with the
    /// initiate request so the OAuth page asks for that exact game — every
    /// developer cloning the repo authorizes against the same game instead of
    /// picking one ad hoc. Use Clear() (Settings window → Unpin) to release the
    /// repository pin.
    /// </summary>
    public static class PlayKitProjectPin
    {
        [Serializable]
        private class PinFile
        {
            [JsonProperty("game_id")] public string GameId;
            [JsonProperty("channel", NullValueHandling = NullValueHandling.Ignore)] public string Channel;
        }

        public static string FilePath =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "playkit.json");

        public static bool Exists => File.Exists(FilePath);

        /// <summary>Pinned game id, or null when the repo has no pin.</summary>
        public static string ReadGameId()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var pin = JsonConvert.DeserializeObject<PinFile>(File.ReadAllText(FilePath));
                return string.IsNullOrEmpty(pin?.GameId) ? null : pin.GameId;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayKit SDK] Failed to read playkit.json: {ex.Message}");
                return null;
            }
        }

        /// <summary>Pin the given game to the repository (idempotent).</summary>
        public static void Write(string gameId, string channel)
        {
            if (string.IsNullOrEmpty(gameId)) return;
            try
            {
                if (ReadGameId() == gameId) return;
                var json = JsonConvert.SerializeObject(
                    new PinFile { GameId = gameId, Channel = string.IsNullOrEmpty(channel) ? null : channel },
                    Formatting.Indented);
                File.WriteAllText(FilePath, json + "\n");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayKit SDK] Failed to write playkit.json: {ex.Message}");
            }
        }

        /// <summary>Remove the repository pin.</summary>
        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayKit SDK] Failed to delete playkit.json: {ex.Message}");
            }
        }
    }
}
