using System;
using System.Collections;
using UnityEngine;

namespace PlayKit_SDK
{
    /// <summary>
    /// Microphone recorder component that wraps Unity's Microphone API
    /// Provides simple recording, stopping, and AudioClip retrieval
    /// Supports Voice Activity Detection (VAD) for automatic silence detection
    /// Supports Always Listening Mode for automatic voice start detection
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

        [Header("Always Listening Mode 始终监听模式")]
        [Tooltip("Enable always listening mode for automatic voice detection 启用始终监听模式以自动检测语音开始")]
        [SerializeField] private bool alwaysListeningMode = false;

        [Tooltip("Volume threshold to detect voice start (0.0 - 1.0) 检测语音开始的音量阈值（0.0 - 1.0）")]
        [SerializeField] private float voiceStartThreshold = 0.02f;

        [Tooltip("Minimum voice duration before starting recording (seconds) 开始录制前的最小语音持续时间（秒）")]
        [SerializeField] private float minVoiceDuration = 0.1f;

        [Tooltip("Pre-buffer duration to capture audio before voice detection (seconds) 语音检测前的预缓冲时长（秒）")]
        [SerializeField] private float preBufferDuration = 0.5f;

        [Tooltip("Debounce delay before auto-restarting listening after recording stops (seconds) 录音结束后自动重新监听的防抖延迟（秒）")]
        [SerializeField] private float restartDebounceTime = 0.3f;

        [Header("Status (Read Only) 状态（只读）")]
        [SerializeField] private bool isRecording = false;
        [SerializeField] private float recordingTime = 0f;
        [SerializeField] private bool isListening = false;
        [SerializeField] private ListeningState listeningState = ListeningState.Idle;

        /// <summary>
        /// Listening state for Always Listening Mode
        /// </summary>
        public enum ListeningState
        {
            /// <summary>Not listening</summary>
            Idle,
            /// <summary>Listening for voice start</summary>
            Listening,
            /// <summary>Voice detected, waiting for minimum duration</summary>
            VoiceDetected,
            /// <summary>Recording in progress (triggered by voice detection)</summary>
            Recording
        }

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

        /// <summary>
        /// Whether the recorder is currently in listening mode (Always Listening Mode)
        /// </summary>
        public bool IsListening => isListening;

        /// <summary>
        /// Current listening state (Always Listening Mode)
        /// </summary>
        public ListeningState CurrentListeningState => listeningState;

        /// <summary>
        /// Whether Always Listening Mode is enabled
        /// </summary>
        public bool AlwaysListeningModeEnabled => alwaysListeningMode;

        private AudioClip _recordingClip;
        private float _silenceTimer = 0f;
        private Coroutine _restartDebounceCoroutine;

        // Always Listening Mode fields
        private AudioClip _listeningClip;
        private float[] _circularBuffer;
        private int _circularBufferWriteIndex;
        private int _circularBufferSize;
        private float _voiceDetectionTimer;
        private float[] _preRecordedSamples;
        private bool _autoRecordingTriggered;

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

        // Always Listening Mode events
        /// <summary>
        /// Invoked when listening mode starts
        /// </summary>
        public event Action OnListeningStarted;

        /// <summary>
        /// Invoked when listening mode stops
        /// </summary>
        public event Action OnListeningStopped;

        /// <summary>
        /// Invoked when voice is first detected (volume exceeds threshold)
        /// </summary>
        public event Action OnVoiceDetected;

        /// <summary>
        /// Invoked when voice detection is cancelled (voice too short)
        /// </summary>
        public event Action OnVoiceCancelled;

        /// <summary>
        /// Invoked when recording starts automatically from voice detection
        /// </summary>
        public event Action OnAutoRecordingStarted;

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
        /// <param name="autoRestartListening">Whether to auto-restart listening mode (default: false for manual stop)</param>
        /// <returns>Recorded AudioClip trimmed to actual recording duration</returns>
        public AudioClip StopRecording(bool autoRestartListening = false)
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

            StopRecordingInternal(autoRestartListening);
            return LastRecording;
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

        /// <summary>
        /// Set Voice Activity Detection parameters for recording auto-stop
        /// </summary>
        /// <param name="silenceThreshold">Volume below which is silence (0.0-1.0, default 0.01)</param>
        /// <param name="maxSilenceDuration">Seconds of silence before auto-stop (default 2.0)</param>
        public void SetVADSettings(float silenceThreshold, float maxSilenceDuration)
        {
            this.silenceThreshold = Mathf.Clamp01(silenceThreshold);
            this.maxSilenceDuration = Mathf.Max(0f, maxSilenceDuration);
            Debug.Log($"[MicrophoneRecorder] VAD settings updated: silenceThreshold={this.silenceThreshold}, maxSilenceDuration={this.maxSilenceDuration}s");
        }

        /// <summary>
        /// Set Always Listening Mode voice detection parameters
        /// Cannot be changed while listening is active
        /// </summary>
        /// <param name="voiceStartThreshold">Volume to trigger voice detection (0.0-1.0, default 0.02)</param>
        /// <param name="minVoiceDuration">Minimum seconds of voice before recording starts (default 0.1)</param>
        /// <param name="preBufferDuration">Seconds of audio to keep before voice detected (default 0.5)</param>
        public void SetListeningSettings(float voiceStartThreshold, float minVoiceDuration, float preBufferDuration)
        {
            if (isListening)
            {
                Debug.LogWarning("[MicrophoneRecorder] Cannot change listening settings while listening! Stop listening first.");
                return;
            }
            this.voiceStartThreshold = Mathf.Clamp01(voiceStartThreshold);
            this.minVoiceDuration = Mathf.Max(0f, minVoiceDuration);
            this.preBufferDuration = Mathf.Max(0f, preBufferDuration);
            Debug.Log($"[MicrophoneRecorder] Listening settings updated: voiceStartThreshold={this.voiceStartThreshold}, minVoiceDuration={this.minVoiceDuration}s, preBufferDuration={this.preBufferDuration}s");
        }

        /// <summary>
        /// Set the debounce delay before auto-restarting listening after recording stops
        /// </summary>
        /// <param name="seconds">Debounce delay in seconds (default 0.3)</param>
        public void SetRestartDebounce(float seconds)
        {
            restartDebounceTime = Mathf.Max(0f, seconds);
            Debug.Log($"[MicrophoneRecorder] Restart debounce set to: {restartDebounceTime}s");
        }

        #region Always Listening Mode API

        /// <summary>
        /// Enable or disable Always Listening Mode
        /// </summary>
        /// <param name="enabled">True to enable, false to disable</param>
        public void SetAlwaysListeningMode(bool enabled)
        {
            if (alwaysListeningMode == enabled) return;

            alwaysListeningMode = enabled;
            Debug.Log($"[MicrophoneRecorder] Always Listening Mode {(enabled ? "enabled" : "disabled")}");

            // If disabling while listening, stop listening
            if (!enabled && isListening)
            {
                StopListening();
            }
        }

        /// <summary>
        /// Start listening for voice activity (Always Listening Mode)
        /// Will automatically start recording when voice is detected
        /// </summary>
        /// <param name="deviceName">Optional specific device name to use</param>
        /// <returns>True if listening started successfully</returns>
        public bool StartListening(string deviceName = null)
        {
#if UNITY_WEBGL
            Debug.LogError("[MicrophoneRecorder] Microphone recording is not supported in WebGL builds!");
            return false;
#else
            if (!alwaysListeningMode)
            {
                Debug.LogWarning("[MicrophoneRecorder] Always Listening Mode is not enabled! Call SetAlwaysListeningMode(true) first or enable it in Inspector.");
                return false;
            }

            if (isListening)
            {
                Debug.LogWarning("[MicrophoneRecorder] Already listening!");
                return false;
            }

            if (isRecording)
            {
                Debug.LogWarning("[MicrophoneRecorder] Cannot start listening while recording!");
                return false;
            }

            // Check if microphone devices are available
            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("[MicrophoneRecorder] No microphone devices found!");
                return false;
            }

            // Use provided device name or fall back to configured/default
            string device = deviceName ?? microphoneDevice;

            // Initialize circular buffer for pre-buffering
            _circularBufferSize = Mathf.CeilToInt(preBufferDuration * sampleRate);
            _circularBuffer = new float[_circularBufferSize];
            _circularBufferWriteIndex = 0;

            // Start loop recording for listening (1 second loop is enough for monitoring)
            int listeningLoopSeconds = Mathf.Max(1, Mathf.CeilToInt(preBufferDuration));
            _listeningClip = Microphone.Start(device, true, listeningLoopSeconds, sampleRate);

            if (_listeningClip == null)
            {
                Debug.LogError($"[MicrophoneRecorder] Failed to start listening on device '{device}'");
                return false;
            }

            isListening = true;
            listeningState = ListeningState.Listening;
            _voiceDetectionTimer = 0f;
            _autoRecordingTriggered = false;

            Debug.Log($"[MicrophoneRecorder] Listening started on device '{device}' @ {sampleRate}Hz (pre-buffer: {preBufferDuration}s)");
            OnListeningStarted?.Invoke();

            return true;
#endif
        }

        /// <summary>
        /// Stop listening for voice activity
        /// </summary>
        public void StopListening()
        {
#if UNITY_WEBGL
            Debug.LogError("[MicrophoneRecorder] Microphone recording is not supported in WebGL builds!");
#else
            // Cancel any pending debounce restart
            if (_restartDebounceCoroutine != null)
            {
                StopCoroutine(_restartDebounceCoroutine);
                _restartDebounceCoroutine = null;
            }

            if (!isListening)
            {
                return;
            }

            Microphone.End(microphoneDevice);
            isListening = false;
            listeningState = ListeningState.Idle;
            _listeningClip = null;
            _circularBuffer = null;
            _voiceDetectionTimer = 0f;

            Debug.Log("[MicrophoneRecorder] Listening stopped");
            OnListeningStopped?.Invoke();
#endif
        }

        /// <summary>
        /// Get current volume level during listening mode
        /// </summary>
        /// <returns>RMS volume level (0.0 - 1.0)</returns>
        public float GetListeningVolume()
        {
#if UNITY_WEBGL
            return 0f;
#else
            if (!isListening || _listeningClip == null) return 0f;

            int sampleWindow = 128;
            float[] samples = new float[sampleWindow];
            int micPosition = Microphone.GetPosition(microphoneDevice);

            if (micPosition < sampleWindow) return 0f;

            _listeningClip.GetData(samples, micPosition - sampleWindow);

            float sum = 0f;
            for (int i = 0; i < sampleWindow; i++)
            {
                sum += samples[i] * samples[i];
            }

            return Mathf.Sqrt(sum / sampleWindow);
#endif
        }

        #endregion

        private void Update()
        {
            // Handle Always Listening Mode
            if (isListening && !isRecording)
            {
                UpdateListeningMode();
            }

            // Handle normal recording
            if (!isRecording) return;

            recordingTime += Time.deltaTime;

            // Auto-stop when max duration reached
            if (recordingTime >= maxRecordingSeconds)
            {
                Debug.Log("[MicrophoneRecorder] Max recording duration reached, auto-stopping");
                StopRecordingInternal(true);
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
                        StopRecordingInternal(true);
                    }
                }
                else
                {
                    // Reset silence timer when sound detected
                    _silenceTimer = 0f;
                }
            }
        }

        #region Always Listening Mode Internal

        private void UpdateListeningMode()
        {
#if !UNITY_WEBGL
            if (_listeningClip == null) return;

            // Get current microphone position
            int micPosition = Microphone.GetPosition(microphoneDevice);
            if (micPosition < 128) return;

            // Update circular buffer for pre-buffering
            UpdateCircularBuffer(micPosition);

            // Get current volume
            float volume = GetListeningVolume();
            OnVolumeChanged?.Invoke(volume);

            switch (listeningState)
            {
                case ListeningState.Listening:
                    // Check if voice detected
                    if (volume >= voiceStartThreshold)
                    {
                        listeningState = ListeningState.VoiceDetected;
                        _voiceDetectionTimer = 0f;
                        Debug.Log($"[MicrophoneRecorder] Voice detected (volume: {volume:F3})");
                        OnVoiceDetected?.Invoke();
                    }
                    break;

                case ListeningState.VoiceDetected:
                    _voiceDetectionTimer += Time.deltaTime;

                    // Check if voice continues
                    if (volume >= voiceStartThreshold)
                    {
                        // Check if minimum duration reached
                        if (_voiceDetectionTimer >= minVoiceDuration)
                        {
                            // Start actual recording
                            StartRecordingFromVoiceDetection();
                        }
                    }
                    else
                    {
                        // Voice stopped too soon, cancel and go back to listening
                        Debug.Log($"[MicrophoneRecorder] Voice too short ({_voiceDetectionTimer:F2}s < {minVoiceDuration}s), cancelled");
                        listeningState = ListeningState.Listening;
                        _voiceDetectionTimer = 0f;
                        OnVoiceCancelled?.Invoke();
                    }
                    break;

                case ListeningState.Recording:
                    // This state is handled by normal recording Update logic
                    break;
            }
#endif
        }

        private void UpdateCircularBuffer(int micPosition)
        {
#if !UNITY_WEBGL
            if (_circularBuffer == null || _listeningClip == null) return;

            // Calculate how many new samples to read
            int clipSamples = _listeningClip.samples;
            int samplesToRead = 256; // Read in chunks

            // Read samples from the listening clip
            float[] tempSamples = new float[samplesToRead];
            int readPosition = (micPosition - samplesToRead + clipSamples) % clipSamples;

            _listeningClip.GetData(tempSamples, readPosition);

            // Write to circular buffer
            for (int i = 0; i < samplesToRead; i++)
            {
                _circularBuffer[_circularBufferWriteIndex] = tempSamples[i];
                _circularBufferWriteIndex = (_circularBufferWriteIndex + 1) % _circularBufferSize;
            }
#endif
        }

        private void StartRecordingFromVoiceDetection()
        {
#if !UNITY_WEBGL
            Debug.Log("[MicrophoneRecorder] Starting recording from voice detection");

            // Save pre-recorded samples from circular buffer
            _preRecordedSamples = new float[_circularBufferSize];
            for (int i = 0; i < _circularBufferSize; i++)
            {
                int readIndex = (_circularBufferWriteIndex + i) % _circularBufferSize;
                _preRecordedSamples[i] = _circularBuffer[readIndex];
            }

            // Stop listening microphone
            Microphone.End(microphoneDevice);
            isListening = false;
            _listeningClip = null;

            // Start actual recording
            _recordingClip = Microphone.Start(microphoneDevice, false, maxRecordingSeconds, sampleRate);

            if (_recordingClip == null)
            {
                Debug.LogError("[MicrophoneRecorder] Failed to start recording after voice detection");
                // Restart listening
                StartListening();
                return;
            }

            isRecording = true;
            recordingTime = 0f;
            _silenceTimer = 0f;
            LastRecording = null;
            listeningState = ListeningState.Recording;
            _autoRecordingTriggered = true;

            Debug.Log("[MicrophoneRecorder] Auto-recording started from voice detection");
            OnAutoRecordingStarted?.Invoke();
            OnRecordingStarted?.Invoke();
#endif
        }

        private void StopRecordingInternal(bool autoRestart)
        {
#if UNITY_WEBGL
            Debug.LogError("[MicrophoneRecorder] Microphone recording is not supported in WebGL builds!");
#else
            if (!isRecording)
            {
                return;
            }

            // Get current microphone position before stopping
            int micPosition = Microphone.GetPosition(microphoneDevice);

            // Stop the microphone
            Microphone.End(microphoneDevice);
            isRecording = false;

            // Combine pre-buffer with recorded audio if this was auto-triggered
            AudioClip trimmedClip;
            if (_autoRecordingTriggered && _preRecordedSamples != null && _preRecordedSamples.Length > 0)
            {
                trimmedClip = CombineWithPreBuffer(_recordingClip, micPosition);
            }
            else
            {
                trimmedClip = TrimAudioClip(_recordingClip, micPosition);
            }

            LastRecording = trimmedClip;
            _autoRecordingTriggered = false;
            _preRecordedSamples = null;

            Debug.Log($"[MicrophoneRecorder] Recording stopped. Duration: {trimmedClip.length:F2}s");
            OnRecordingStopped?.Invoke(trimmedClip);

            listeningState = ListeningState.Idle;

            // Auto-restart listening if in Always Listening Mode (with debounce)
            if (autoRestart && alwaysListeningMode)
            {
                if (_restartDebounceCoroutine != null)
                    StopCoroutine(_restartDebounceCoroutine);
                _restartDebounceCoroutine = StartCoroutine(RestartListeningDebounced());
            }
#endif
        }

        private IEnumerator RestartListeningDebounced()
        {
            Debug.Log($"[MicrophoneRecorder] Debounce: restarting listening in {restartDebounceTime}s");
            yield return new WaitForSeconds(restartDebounceTime);
            _restartDebounceCoroutine = null;
            StartListening();
        }

        private AudioClip CombineWithPreBuffer(AudioClip recordedClip, int recordedSamples)
        {
            if (recordedClip == null) return null;

            // Ensure samples doesn't exceed clip length
            recordedSamples = Mathf.Min(recordedSamples, recordedClip.samples);

            int preBufferSamples = _preRecordedSamples?.Length ?? 0;
            int totalSamples = preBufferSamples + recordedSamples;

            if (totalSamples <= 0) return null;

            // Create combined AudioClip
            AudioClip combinedClip = AudioClip.Create(
                "Recording",
                totalSamples,
                recordedClip.channels,
                recordedClip.frequency,
                false
            );

            float[] combinedData = new float[totalSamples * recordedClip.channels];

            // Copy pre-buffer samples
            if (_preRecordedSamples != null)
            {
                for (int i = 0; i < preBufferSamples; i++)
                {
                    combinedData[i] = _preRecordedSamples[i];
                }
            }

            // Copy recorded samples
            float[] recordedData = new float[recordedSamples * recordedClip.channels];
            recordedClip.GetData(recordedData, 0);
            for (int i = 0; i < recordedData.Length; i++)
            {
                combinedData[preBufferSamples + i] = recordedData[i];
            }

            combinedClip.SetData(combinedData, 0);

            Debug.Log($"[MicrophoneRecorder] Combined pre-buffer ({preBufferSamples} samples) with recording ({recordedSamples} samples)");

            return combinedClip;
        }

        #endregion

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
            if (_restartDebounceCoroutine != null)
            {
                StopCoroutine(_restartDebounceCoroutine);
                _restartDebounceCoroutine = null;
            }
#if !UNITY_WEBGL
            if (isRecording)
            {
                Microphone.End(microphoneDevice);
            }
            if (isListening)
            {
                Microphone.End(microphoneDevice);
            }
#endif
        }

        private void OnApplicationPause(bool pauseStatus)
        {
#if !UNITY_WEBGL
            // Stop recording/listening when application is paused (e.g., on mobile)
            if (pauseStatus)
            {
                if (isRecording)
                {
                    Debug.Log("[MicrophoneRecorder] Application paused, stopping recording");
                    StopRecording();
                }
                if (isListening)
                {
                    Debug.Log("[MicrophoneRecorder] Application paused, stopping listening");
                    StopListening();
                }
            }
#endif
        }
    }
}
