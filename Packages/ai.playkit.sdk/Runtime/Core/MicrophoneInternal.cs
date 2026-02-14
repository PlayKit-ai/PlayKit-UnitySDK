using System.Collections.Generic;
using UnityEngine;

namespace PlayKit_SDK
{
    /// <summary>
    /// Centralized microphone hardware access with device-level mutual exclusion.
    /// Only PlayKit_MicrophoneRecorder should use this class directly.
    /// All Unity Microphone.* calls are funneled through here to prevent
    /// multiple recorders from conflicting on the same device.
    /// </summary>
    internal static class MicrophoneInternal
    {
        /// <summary>
        /// Tracks which recorder owns each device.
        /// Key = normalized device name ("" for default device).
        /// </summary>
        private static readonly Dictionary<string, PlayKit_MicrophoneRecorder> _deviceOwners = new();

        /// <summary>
        /// Clear static state on domain reload / Enter Play Mode.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _deviceOwners.Clear();
        }

        #region Platform Check

        internal static bool IsWebGL =>
#if UNITY_WEBGL
            true;
#else
            false;
#endif

        #endregion

        #region Device Lease API

        /// <summary>
        /// Try to acquire exclusive access to a microphone device.
        /// Returns true if this recorder now owns the device.
        /// If the same recorder already owns it, returns true (idempotent).
        /// If another recorder owns it, returns false with a warning.
        /// </summary>
        internal static bool TryAcquireDevice(string device, PlayKit_MicrophoneRecorder owner)
        {
            var key = NormalizeDevice(device);

            if (_deviceOwners.TryGetValue(key, out var currentOwner))
            {
                // Same owner — idempotent
                if (ReferenceEquals(currentOwner, owner))
                    return true;

                // Unity overloads == to return true for destroyed MonoBehaviours
                if (currentOwner == null)
                {
                    // Stale entry from a destroyed recorder — reclaim
                    _deviceOwners[key] = owner;
                    Debug.Log($"[MicrophoneInternal] Reclaimed stale device '{DeviceDisplayName(device)}' for '{owner.name}'");
                    return true;
                }

                // Another live recorder owns the device
                Debug.LogWarning(
                    $"[MicrophoneInternal] Device '{DeviceDisplayName(device)}' is already in use by '{currentOwner.name}'. " +
                    $"'{owner.name}' cannot acquire it.");
                return false;
            }

            _deviceOwners[key] = owner;
            return true;
        }

        /// <summary>
        /// Release a device. Only the owning recorder can release it.
        /// Safe to call even if not owned (no-op).
        /// </summary>
        internal static void ReleaseDevice(string device, PlayKit_MicrophoneRecorder owner)
        {
            var key = NormalizeDevice(device);
            if (_deviceOwners.TryGetValue(key, out var currentOwner))
            {
                if (ReferenceEquals(currentOwner, owner) || currentOwner == null)
                {
                    _deviceOwners.Remove(key);
                }
            }
        }

        /// <summary>
        /// Check if a specific recorder owns a device.
        /// </summary>
        internal static bool IsDeviceOwnedBy(string device, PlayKit_MicrophoneRecorder owner)
        {
            var key = NormalizeDevice(device);
            return _deviceOwners.TryGetValue(key, out var currentOwner)
                   && ReferenceEquals(currentOwner, owner);
        }

        /// <summary>
        /// Get the current owner of a device, or null if unowned.
        /// </summary>
        internal static PlayKit_MicrophoneRecorder GetDeviceOwner(string device)
        {
            var key = NormalizeDevice(device);
            _deviceOwners.TryGetValue(key, out var owner);
            return owner;
        }

        #endregion

        #region Microphone Hardware API

        /// <summary>
        /// Start microphone recording on a device.
        /// Caller must already own the device via TryAcquireDevice.
        /// Returns the AudioClip, or null on failure / WebGL.
        /// </summary>
        internal static AudioClip StartMicrophone(string device, bool loop, int lengthSec, int frequency)
        {
#if UNITY_WEBGL
            return null;
#else
            return Microphone.Start(device, loop, lengthSec, frequency);
#endif
        }

        /// <summary>
        /// Stop microphone hardware on a device.
        /// Does NOT release the device lease — call ReleaseDevice separately.
        /// </summary>
        internal static void StopMicrophone(string device, PlayKit_MicrophoneRecorder caller)
        {
#if UNITY_WEBGL
            return;
#else
            if (!IsDeviceOwnedBy(device, caller))
            {
                Debug.LogWarning(
                    $"[MicrophoneInternal] '{caller.name}' tried to stop device '{DeviceDisplayName(device)}' " +
                    "it doesn't own. Ignored.");
                return;
            }
            Microphone.End(device);
#endif
        }

        /// <summary>
        /// Get current microphone recording position.
        /// Returns -1 on WebGL or error.
        /// </summary>
        internal static int GetMicrophonePosition(string device)
        {
#if UNITY_WEBGL
            return -1;
#else
            return Microphone.GetPosition(device);
#endif
        }

        /// <summary>
        /// Get available microphone devices.
        /// Returns empty array on WebGL.
        /// </summary>
        internal static string[] GetDevices()
        {
#if UNITY_WEBGL
            return System.Array.Empty<string>();
#else
            return Microphone.devices;
#endif
        }

        #endregion

        #region Utility

        private static string NormalizeDevice(string device)
        {
            return device ?? "";
        }

        private static string DeviceDisplayName(string device)
        {
            return string.IsNullOrEmpty(device) ? "(default)" : device;
        }

        #endregion
    }
}
