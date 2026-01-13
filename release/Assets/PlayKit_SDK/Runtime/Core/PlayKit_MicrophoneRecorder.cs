using System;
using UnityEngine;

namespace PlayKit_SDK
{
    /// <summary>
    /// Microphone recorder component that wraps Unity's Microphone API
    /// Provides simple recording, stopping, and AudioClip retrieval
    /// Supports Voice Activity Detection (VAD) for automatic silence detection
    /// </summary>
    public class PlayKit_MicrophoneRecorder : MonoBehaviour
    {
        [Header("Recording Configuration 录制配置")]
        [Tooltip("Maximum recording duration in seconds 最大录制时长（秒）")]
        [SerializeField] private int maxRecordingSeconds = 60;

        [Tooltip("Audio sample rate (Hz). 16000 is recommended for Whisper 音频采样率（Hz），Whisper推荐使用16000")]
        [SerializeField] private int sampleRate = 16000;

        [Tooltip("Microphone device name (null = default device) 麦克风设备名称（null = 默认设备）")]
        [SerializeField] private string microphoneDevice = null;

        [Header("Voice Activity Detection 语音活动检测")]
        [Tooltip("Enable automatic stop on silence 启用静音自动停止")]
        [SerializeField] private bool useVAD = true;

        [Tooltip("Volume threshold below which audio is considered silence (0.0 - 1.0) 音量阈值，低于此值视为静音（0.0 - 1.0）")]
        [SerializeField] private float silenceThreshold = 0.01f;

        [Tooltip("Duration of continuous silence before auto-stopping (seconds) 连续静音多久后自动停止（秒）")]
        [SerializeField] private float maxSilenceDuration = 2f;

        [Header("Status (Read Only) 状态（只读）")]
        [SerializeField] private bool isRecording = false;
        [SerializeField] private float recordingTime = 0f;

        /// <summary>
        /// Whether recording is currently active
        /// </summary>
        public bool IsRecording => isRecording;

        /// <summary>
        /// Current recording duration in seconds
        /// </summary>
        public float RecordingTime => recordingTime;

        /// <summary>
        /// The microphone device currently being used
        /// </summary>
        public string CurrentDevice => microphoneDevice ??
            #if !UNITY_WEBGL
            (Microphone.devices.Length > 0 ? Microphone.devices[0] : "None")
            #else
            "WebGL Not Supported"
            #endif
            ;

        /// <summary>
        /// Last recorded AudioClip (available after StopRecording)
        /// </summary>
        public AudioClip LastRecording { get; private set; }

        private AudioClip _recordingClip;
        private float _silenceTimer = 0f;

        // Events
        /// <summary>
        /// Invoked when recording starts
        /// </summary>
        public event Action OnRecordingStarted;

        /// <summary>
        /// Invoked when recording stops, provides the recorded AudioClip
        /// </summary>
        public event Action<AudioClip> OnRecordingStopped;

        /// <summary>
        /// Invoked during recording with current volume level (0.0 - 1.0)
        /// </summary>
        public event Action<float> OnVolumeChanged;

        /// <summary>
        /// Start recording from the microphone
        /// </summary>
        /// <param name="deviceName">Optional specific device name to use</param>
        /// <returns>True if recording started successfully, false otherwise</returns>
        public bool StartRecording(string deviceName = null)
        {
#if UNITY_WEBGL
            Debug.LogError("[MicrophoneRecorder] Microphone recording is not supported in WebGL builds!");
            return false;
#else
            if (isRecording)
            {
                Debug.LogWarning("[MicrophoneRecorder] Already recording!");
                return false;
            }

            // Check if microphone devices are available
            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("[MicrophoneRecorder] No microphone devices found! Please check your system's audio input settings.");
                return false;
            }

            // Use provided device name or fall back to configured/default
            string device = deviceName ?? microphoneDevice;

            // Start recording
            _recordingClip = Microphone.Start(device, false, maxRecordingSeconds, sampleRate);

            if (_recordingClip == null)
            {
                Debug.LogError($"[MicrophoneRecorder] Failed to start recording on device '{device}'");
                return false;
            }

            isRecording = true;
            recordingTime = 0f;
            _silenceTimer = 0f;
            LastRecording = null;

            Debug.Log($"[MicrophoneRecorder] Recording started on device '{device}' @ {sampleRate}Hz");
            OnRecordingStarted?.Invoke();

            return true;
#endif
        }

        /// <summary>
        /// Stop recording and return the recorded AudioClip
        /// </summary>
        /// <returns>Recorded AudioClip trimmed to actual recording duration</returns>
        public AudioClip StopRecording()
        {
#if UNITY_WEBGL
            Debug.LogError("[MicrophoneRecorder] Microphone recording is not supported in WebGL builds!");
            return null;
#else
            if (!isRecording)
            {
                Debug.LogWarning("[MicrophoneRecorder] Not currently recording!");
                return null;
            }

            // Get current microphone position before stopping
            int micPosition = Microphone.GetPosition(microphoneDevice);

            // Stop the microphone
            Microphone.End(microphoneDevice);
            isRecording = false;

            // Trim the AudioClip to actual recorded length
            AudioClip trimmedClip = TrimAudioClip(_recordingClip, micPosition);

            LastRecording = trimmedClip;

            Debug.Log($"[MicrophoneRecorder] Recording stopped. Duration: {trimmedClip.length:F2}s");
            OnRecordingStopped?.Invoke(trimmedClip);

            return trimmedClip;
#endif
        }

        /// <summary>
        /// Cancel the current recording without returning AudioClip
        /// </summary>
        public void CancelRecording()
        {
#if UNITY_WEBGL
            Debug.LogError("[MicrophoneRecorder] Microphone recording is not supported in WebGL builds!");
#else
            if (!isRecording) return;

            Microphone.End(microphoneDevice);
            isRecording = false;
            recordingTime = 0f;
            LastRecording = null;

            Debug.Log("[MicrophoneRecorder] Recording cancelled");
#endif
        }

        /// <summary>
        /// Get the current audio volume level (0.0 - 1.0)
        /// Useful for visual feedback and voice activity detection
        /// </summary>
        /// <returns>RMS (Root Mean Square) volume level</returns>
        public float GetCurrentVolume()
        {
#if UNITY_WEBGL
            Debug.LogError("[MicrophoneRecorder] Microphone recording is not supported in WebGL builds!");
            return 0f;
#else
            if (!isRecording || _recordingClip == null) return 0f;

            // Sample window for volume calculation
            int sampleWindow = 128;
            float[] samples = new float[sampleWindow];
            int micPosition = Microphone.GetPosition(microphoneDevice);

            // Need enough data to calculate volume
            if (micPosition < sampleWindow) return 0f;

            // Get recent audio data
            _recordingClip.GetData(samples, micPosition - sampleWindow);

            // Calculate RMS (Root Mean Square) volume
            float sum = 0f;
            for (int i = 0; i < sampleWindow; i++)
            {
                sum += samples[i] * samples[i];
            }

            return Mathf.Sqrt(sum / sampleWindow);
#endif
        }

        /// <summary>
        /// Get list of available microphone devices
        /// </summary>
        /// <returns>Array of device names</returns>
        public static string[] GetAvailableDevices()
        {
#if UNITY_WEBGL
            Debug.LogError("[MicrophoneRecorder] Microphone recording is not supported in WebGL builds!");
            return new string[] { "WebGL Not Supported" };
#else
            return Microphone.devices;
#endif
        }

        /// <summary>
        /// Set the microphone device to use for recording
        /// </summary>
        /// <param name="deviceName">Device name from GetAvailableDevices()</param>
        public void SetMicrophoneDevice(string deviceName)
        {
            if (isRecording)
            {
                Debug.LogWarning("[MicrophoneRecorder] Cannot change device while recording!");
                return;
            }

            microphoneDevice = deviceName;
            Debug.Log($"[MicrophoneRecorder] Microphone device set to: {deviceName}");
        }

        private void Update()
        {
            if (!isRecording) return;

            recordingTime += Time.deltaTime;

            // Auto-stop when max duration reached
            if (recordingTime >= maxRecordingSeconds)
            {
                Debug.Log("[MicrophoneRecorder] Max recording duration reached, auto-stopping");
                StopRecording();
                return;
            }

            // Voice Activity Detection (VAD)
            if (useVAD)
            {
                float volume = GetCurrentVolume();
                OnVolumeChanged?.Invoke(volume);

                if (volume < silenceThreshold)
                {
                    _silenceTimer += Time.deltaTime;

                    if (_silenceTimer >= maxSilenceDuration)
                    {
                        Debug.Log("[MicrophoneRecorder] Silence detected for " + maxSilenceDuration + "s, auto-stopping");
                        StopRecording();
                    }
                }
                else
                {
                    // Reset silence timer when sound detected
                    _silenceTimer = 0f;
                }
            }
        }

        /// <summary>
        /// Trim AudioClip to actual recorded samples
        /// </summary>
        private AudioClip TrimAudioClip(AudioClip clip, int samples)
        {
            if (clip == null) return null;

            // Ensure samples doesn't exceed clip length
            samples = Mathf.Min(samples, clip.samples);

            // Create new AudioClip with trimmed length
            AudioClip trimmedClip = AudioClip.Create(
                "Recording",
                samples,
                clip.channels,
                clip.frequency,
                false
            );

            // Copy data from original clip
            float[] data = new float[samples * clip.channels];
            clip.GetData(data, 0);
            trimmedClip.SetData(data, 0);

            return trimmedClip;
        }

        private void OnDestroy()
        {
#if !UNITY_WEBGL
            // Ensure microphone is stopped when component is destroyed
            if (isRecording)
            {
                Microphone.End(microphoneDevice);
            }
#endif
        }

        private void OnApplicationPause(bool pauseStatus)
        {
#if !UNITY_WEBGL
            // Stop recording when application is paused (e.g., on mobile)
            if (pauseStatus && isRecording)
            {
                Debug.Log("[MicrophoneRecorder] Application paused, stopping recording");
                StopRecording();
            }
#endif
        }
    }
}
