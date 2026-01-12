using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayKit_SDK.Public;
using UnityEngine;
using UnityEngine.Events;

namespace PlayKit_SDK
{
    /// <summary>
    /// MonoBehaviour wrapper for PlayKit_AudioTranscriptionClient.
    /// Provides Inspector configuration, UnityEvent support, microphone integration, and automatic lifecycle management.
    ///
    /// For advanced usage, use PlayKit_AudioTranscriptionClient directly via GetUnderlyingClient().
    /// </summary>
    public class PlayKit_Transcribe : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Transcription Configuration")]
        [Tooltip("Transcription model name. Leave empty to use default from PlayKitSettings. 语音转文字模型名称。留空使用PlayKitSettings中的默认值。")]
        [SerializeField] private string transcriptionModel = "";

        [Tooltip("Default language code for transcription (e.g., 'zh', 'en') 默认语言代码")]
        [SerializeField] private string defaultLanguage = "zh";

        [Tooltip("Optional prompt to guide transcription 可选的提示词引导转录")]
        [SerializeField] private string transcriptionPrompt;

        [Header("Microphone Integration (Optional)")]
        [Tooltip("Optional: Attach a PlayKit_MicrophoneRecorder for integrated recording 可选：关联麦克风录制组件")]
        [SerializeField] private PlayKit_MicrophoneRecorder microphoneRecorder;

        [Tooltip("Auto-create microphone recorder if not assigned 自动创建麦克风录制组件")]
        [SerializeField] private bool autoCreateRecorder = true;

        [Header("Debug Options")]
        [Tooltip("Log transcription results to console 在控制台输出转录结果")]
        [SerializeField] private bool logTranscription = false;

        #endregion

        #region UnityEvents

        [Header("Events")]
        [Tooltip("Called when transcription completes with text result 转录完成时触发")]
        public UnityEvent<string> OnTranscriptionComplete;

        [Tooltip("Called when full transcription result with metadata is available 完整转录结果可用时触发")]
        public UnityEvent<PlayKit_TranscriptionResult> OnFullTranscriptionComplete;

        [Tooltip("Called when transcription starts 开始转录时触发")]
        public UnityEvent OnTranscriptionStarted;

        [Tooltip("Called when transcription ends (success or failure) 转录结束时触发")]
        public UnityEvent OnTranscriptionEnded;

        [Tooltip("Called when an error occurs 发生错误时触发")]
        public UnityEvent<string> OnError;

        [Tooltip("Called when recording starts (if using integrated microphone) 录制开始时触发")]
        public UnityEvent OnRecordingStarted;

        [Tooltip("Called when recording stops (if using integrated microphone) 录制停止时触发")]
        public UnityEvent OnRecordingStopped;

        [Tooltip("Called during recording with current volume level 录制过程中触发，提供当前音量")]
        public UnityEvent<float> OnRecordingVolume;

        #endregion

        #region Private Fields

        private PlayKit_AudioTranscriptionClient _transcriptionClient;
        private bool _isReady;
        private bool _isProcessing;
        private bool _isRecording;

        #endregion

        #region Public Properties

        /// <summary>
        /// Whether the transcription client is ready to use
        /// </summary>
        public bool IsReady => _isReady;

        /// <summary>
        /// Whether a transcription is currently being processed
        /// </summary>
        public bool IsProcessing => _isProcessing;

        /// <summary>
        /// Whether recording is currently active
        /// </summary>
        public bool IsRecording => _isRecording || (microphoneRecorder != null && microphoneRecorder.IsRecording);

        /// <summary>
        /// The model name being used
        /// </summary>
        public string ModelName => transcriptionModel;

        /// <summary>
        /// Default language for transcription
        /// </summary>
        public string DefaultLanguage
        {
            get => defaultLanguage;
            set => defaultLanguage = value;
        }

        /// <summary>
        /// The associated microphone recorder
        /// </summary>
        public PlayKit_MicrophoneRecorder MicrophoneRecorder => microphoneRecorder;

        #endregion

        #region Lifecycle

        private void Start()
        {
            Initialize().Forget();
        }

        private void OnDestroy()
        {
            // Unsubscribe from microphone events
            if (microphoneRecorder != null)
            {
                microphoneRecorder.OnRecordingStarted -= HandleRecordingStarted;
                microphoneRecorder.OnRecordingStopped -= HandleRecordingStopped;
                microphoneRecorder.OnVolumeChanged -= HandleVolumeChanged;
            }
        }

        private async UniTask Initialize()
        {
            await UniTask.WaitUntil(() => PlayKit_SDK.IsReady());

            // Use settings default if model not specified
            var modelToUse = string.IsNullOrEmpty(transcriptionModel) 
                ? PlayKitSettings.Instance?.DefaultTranscriptionModel ?? "default-transcription-model"
                : transcriptionModel;

            _transcriptionClient = PlayKit_SDK.Factory.CreateTranscriptionClient(modelToUse);

            if (_transcriptionClient == null)
            {
                Debug.LogError("[PlayKit_Transcribe] Failed to create transcription client");
                return;
            }

            // Setup microphone recorder if available or auto-create
            if (microphoneRecorder == null && autoCreateRecorder)
            {
                microphoneRecorder = GetComponent<PlayKit_MicrophoneRecorder>();
                if (microphoneRecorder == null)
                {
                    microphoneRecorder = gameObject.AddComponent<PlayKit_MicrophoneRecorder>();

                    if (logTranscription)
                    {
                        Debug.Log("[PlayKit_Transcribe] Auto-created MicrophoneRecorder component");
                    }
                }
            }

            // Subscribe to microphone events
            if (microphoneRecorder != null)
            {
                microphoneRecorder.OnRecordingStarted += HandleRecordingStarted;
                microphoneRecorder.OnRecordingStopped += HandleRecordingStopped;
                microphoneRecorder.OnVolumeChanged += HandleVolumeChanged;
            }

            _isReady = true;

            if (logTranscription)
            {
                Debug.Log($"[PlayKit_Transcribe] Ready with model '{transcriptionModel}'");
            }
        }

        #endregion

        #region Public API - Transcription Methods

        /// <summary>
        /// Transcribe an AudioClip to text
        /// </summary>
        /// <param name="audioClip">The AudioClip to transcribe</param>
        /// <param name="language">Language code (optional, uses defaultLanguage if null)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Transcribed text</returns>
        public async UniTask<string> TranscribeAsync(
            AudioClip audioClip,
            string language = null,
            CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();

            if (!ValidateState())
            {
                return null;
            }

            if (audioClip == null)
            {
                OnError?.Invoke("AudioClip cannot be null");
                return null;
            }

            _isProcessing = true;
            OnTranscriptionStarted?.Invoke();

            try
            {
                var result = await _transcriptionClient.TranscribeAudioClipAsync(
                    audioClip,
                    language ?? defaultLanguage,
                    transcriptionPrompt,
                    token
                );

                if (result.Success)
                {
                    if (logTranscription)
                    {
                        Debug.Log($"[PlayKit_Transcribe] Result: {result.Text}");
                    }

                    OnTranscriptionComplete?.Invoke(result.Text);
                    OnFullTranscriptionComplete?.Invoke(result);
                    return result.Text;
                }
                else
                {
                    OnError?.Invoke(result.Error ?? "Transcription failed");
                    return null;
                }
            }
            catch (OperationCanceledException)
            {
                if (logTranscription)
                {
                    Debug.Log("[PlayKit_Transcribe] Transcription cancelled");
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_Transcribe] Error: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
            finally
            {
                _isProcessing = false;
                OnTranscriptionEnded?.Invoke();
            }
        }

        /// <summary>
        /// Transcribe raw audio bytes to text
        /// </summary>
        /// <param name="audioData">Audio file bytes (WAV, MP3, etc.)</param>
        /// <param name="language">Language code (optional, uses defaultLanguage if null)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Transcribed text</returns>
        public async UniTask<string> TranscribeAsync(
            byte[] audioData,
            string language = null,
            CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();

            if (!ValidateState())
            {
                return null;
            }

            if (audioData == null || audioData.Length == 0)
            {
                OnError?.Invoke("Audio data cannot be null or empty");
                return null;
            }

            _isProcessing = true;
            OnTranscriptionStarted?.Invoke();

            try
            {
                var result = await _transcriptionClient.TranscribeAsync(
                    audioData,
                    language ?? defaultLanguage,
                    transcriptionPrompt,
                    token
                );

                if (result.Success)
                {
                    if (logTranscription)
                    {
                        Debug.Log($"[PlayKit_Transcribe] Result: {result.Text}");
                    }

                    OnTranscriptionComplete?.Invoke(result.Text);
                    OnFullTranscriptionComplete?.Invoke(result);
                    return result.Text;
                }
                else
                {
                    OnError?.Invoke(result.Error ?? "Transcription failed");
                    return null;
                }
            }
            catch (OperationCanceledException)
            {
                if (logTranscription)
                {
                    Debug.Log("[PlayKit_Transcribe] Transcription cancelled");
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_Transcribe] Error: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
            finally
            {
                _isProcessing = false;
                OnTranscriptionEnded?.Invoke();
            }
        }

        /// <summary>
        /// Get full transcription result with metadata
        /// </summary>
        /// <param name="audioClip">The AudioClip to transcribe</param>
        /// <param name="language">Language code (optional)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Full transcription result with metadata</returns>
        public async UniTask<PlayKit_TranscriptionResult> TranscribeFullAsync(
            AudioClip audioClip,
            string language = null,
            CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();

            if (!ValidateState())
            {
                return new PlayKit_TranscriptionResult("Client not ready");
            }

            if (audioClip == null)
            {
                return new PlayKit_TranscriptionResult("AudioClip cannot be null");
            }

            _isProcessing = true;
            OnTranscriptionStarted?.Invoke();

            try
            {
                var result = await _transcriptionClient.TranscribeAudioClipAsync(
                    audioClip,
                    language ?? defaultLanguage,
                    transcriptionPrompt,
                    token
                );

                if (result.Success)
                {
                    if (logTranscription)
                    {
                        Debug.Log($"[PlayKit_Transcribe] Result: {result.Text}");
                    }

                    OnTranscriptionComplete?.Invoke(result.Text);
                    OnFullTranscriptionComplete?.Invoke(result);
                }
                else
                {
                    OnError?.Invoke(result.Error ?? "Transcription failed");
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_Transcribe] Error: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return new PlayKit_TranscriptionResult(ex.Message);
            }
            finally
            {
                _isProcessing = false;
                OnTranscriptionEnded?.Invoke();
            }
        }

        #endregion

        #region Microphone Integration

        /// <summary>
        /// Get or create a microphone recorder component
        /// </summary>
        /// <returns>The microphone recorder</returns>
        public PlayKit_MicrophoneRecorder GetOrCreateRecorder()
        {
            if (microphoneRecorder == null)
            {
                microphoneRecorder = GetComponent<PlayKit_MicrophoneRecorder>();
                if (microphoneRecorder == null)
                {
                    microphoneRecorder = gameObject.AddComponent<PlayKit_MicrophoneRecorder>();
                }

                // Subscribe to events
                microphoneRecorder.OnRecordingStarted += HandleRecordingStarted;
                microphoneRecorder.OnRecordingStopped += HandleRecordingStopped;
                microphoneRecorder.OnVolumeChanged += HandleVolumeChanged;
            }

            return microphoneRecorder;
        }

        /// <summary>
        /// Start recording from microphone
        /// </summary>
        /// <returns>True if recording started successfully</returns>
        public bool StartRecording()
        {
            var recorder = GetOrCreateRecorder();
            return recorder.StartRecording();
        }

        /// <summary>
        /// Stop recording and get the AudioClip
        /// </summary>
        /// <returns>The recorded AudioClip</returns>
        public AudioClip StopRecording()
        {
            if (microphoneRecorder == null)
            {
                Debug.LogWarning("[PlayKit_Transcribe] No microphone recorder available");
                return null;
            }

            return microphoneRecorder.StopRecording();
        }

        /// <summary>
        /// Record from microphone and transcribe the result
        /// </summary>
        /// <param name="maxDuration">Maximum recording duration in seconds</param>
        /// <param name="language">Language code (optional)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Transcribed text</returns>
        public async UniTask<string> RecordAndTranscribeAsync(
            float maxDuration = 30f,
            string language = null,
            CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();

            if (!ValidateState())
            {
                return null;
            }

            var recorder = GetOrCreateRecorder();

            // Start recording
            if (!recorder.StartRecording())
            {
                OnError?.Invoke("Failed to start recording");
                return null;
            }

            _isRecording = true;

            try
            {
                // Wait for recording to complete (either by silence detection or max duration)
                float elapsed = 0f;
                while (recorder.IsRecording && elapsed < maxDuration)
                {
                    await UniTask.Delay(100, cancellationToken: token);
                    elapsed += 0.1f;
                }

                // Stop recording if still active
                AudioClip audioClip = null;
                if (recorder.IsRecording)
                {
                    audioClip = recorder.StopRecording();
                }
                else
                {
                    audioClip = recorder.LastRecording;
                }

                _isRecording = false;

                if (audioClip == null)
                {
                    OnError?.Invoke("No audio recorded");
                    return null;
                }

                // Transcribe the recording
                return await TranscribeAsync(audioClip, language, token);
            }
            catch (OperationCanceledException)
            {
                if (recorder.IsRecording)
                {
                    recorder.StopRecording();
                }
                _isRecording = false;

                if (logTranscription)
                {
                    Debug.Log("[PlayKit_Transcribe] Recording cancelled");
                }
                return null;
            }
            catch (Exception ex)
            {
                if (recorder.IsRecording)
                {
                    recorder.StopRecording();
                }
                _isRecording = false;

                Debug.LogError($"[PlayKit_Transcribe] Error: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Set the default language for transcription
        /// </summary>
        /// <param name="language">Language code (e.g., "en", "zh")</param>
        public void SetDefaultLanguage(string language)
        {
            defaultLanguage = language;
        }

        /// <summary>
        /// Set the transcription prompt
        /// </summary>
        /// <param name="prompt">Prompt to guide transcription</param>
        public void SetTranscriptionPrompt(string prompt)
        {
            transcriptionPrompt = prompt;
        }

        #endregion

        #region Advanced Access

        /// <summary>
        /// Get the underlying PlayKit_AudioTranscriptionClient for advanced operations
        /// </summary>
        /// <returns>The underlying transcription client</returns>
        public PlayKit_AudioTranscriptionClient GetUnderlyingClient()
        {
            return _transcriptionClient;
        }

        /// <summary>
        /// Setup with an existing transcription client (for advanced scenarios)
        /// </summary>
        /// <param name="client">The transcription client to use</param>
        public void Setup(PlayKit_AudioTranscriptionClient client)
        {
            _transcriptionClient = client;
            _isReady = true;

            if (logTranscription)
            {
                Debug.Log($"[PlayKit_Transcribe] Setup with model '{client.ModelName}'");
            }
        }

        #endregion

        #region Private Helpers

        private bool ValidateState()
        {
            if (!_isReady)
            {
                Debug.LogWarning("[PlayKit_Transcribe] Client not ready. Please wait for initialization.");
                OnError?.Invoke("Client not ready");
                return false;
            }

            if (_transcriptionClient == null)
            {
                Debug.LogError("[PlayKit_Transcribe] Transcription client not initialized.");
                OnError?.Invoke("Client not initialized");
                return false;
            }

            if (_isProcessing)
            {
                Debug.LogWarning("[PlayKit_Transcribe] A transcription is already in progress.");
                OnError?.Invoke("Transcription already in progress");
                return false;
            }

            return true;
        }

        private void HandleRecordingStarted()
        {
            _isRecording = true;
            OnRecordingStarted?.Invoke();

            if (logTranscription)
            {
                Debug.Log("[PlayKit_Transcribe] Recording started");
            }
        }

        private void HandleRecordingStopped(AudioClip clip)
        {
            _isRecording = false;
            OnRecordingStopped?.Invoke();

            if (logTranscription)
            {
                Debug.Log($"[PlayKit_Transcribe] Recording stopped, duration: {clip?.length ?? 0}s");
            }
        }

        private void HandleVolumeChanged(float volume)
        {
            OnRecordingVolume?.Invoke(volume);
        }

        #endregion
    }
}
