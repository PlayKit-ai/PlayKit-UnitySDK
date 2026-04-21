using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PlayKit_SDK
{
    /// <summary>
    /// Voice input module for NPC Client
    /// Provides speech-to-text capabilities for NPC conversations
    /// Automatically integrates with PlayKit_NPC on the same GameObject
    /// </summary>
    public class PlayKit_NPC_VoiceModule : MonoBehaviour
    {
        [Header("Voice Transcription Configuration 语音转录配置")]
        [Tooltip("Transcription model name. Leave empty to use default from PlayKitSettings. 转录模型名称。留空使用PlayKitSettings中的默认值。")]
        [SerializeField] private string transcriptionModel = "";
        [Tooltip("Default language code for transcription (e.g., 'zh', 'en') 默认转录语言代码（例如：'zh', 'en'）")]
        [SerializeField] private string defaultLanguage = "zh";

        [Header("Microphone Recording (Optional) 麦克风录制（可选）")]
        [Tooltip("Optional: Attach a PlayKit_MicrophoneRecorder for integrated recording functionality 可选：附加PlayKit_MicrophoneRecorder组件以集成录制功能")]
        [SerializeField] private PlayKit_MicrophoneRecorder microphoneRecorder;

        [Header("Audio Pass-Through 音频直通")]
        [Tooltip("When enabled, audio is sent directly to the chat model as a multimodal input_audio part instead of being transcribed first. This reduces latency by skipping the STT step, but requires a model that supports audio input. 启用后，音频将直接作为多模态音频部分发送给聊天模型，跳过语音转文字步骤以降低延迟，但需要支持音频输入的模型。")]
        [SerializeField] private bool passAudioDirectly = false;

        [Header("Always Listening Behavior 始终监听行为")]
        [Tooltip("Allow new transcriptions to interrupt an in-progress NPC stream 允许新的转录结果打断正在进行的NPC流")]
        [SerializeField] private bool interruptible = true;

        [Header("Debug Options 调试选项")]
        [Tooltip("Log transcription results to console 将转录结果输出到控制台")]
        [SerializeField] private bool logTranscription = true;

        private PlayKit_NPC _npcClient;
        private PlayKit_AudioTranscriptionClient _transcriptionClient;
        private bool _isReady;

        /// <summary>
        /// Whether the voice module is ready to use
        /// </summary>
        public bool IsReady => _isReady;

        /// <summary>
        /// Whether the voice module is currently processing audio or getting NPC response
        /// </summary>
        public bool IsProcessing { get; private set; }

        /// <summary>
        /// The transcription model being used
        /// </summary>
        public string TranscriptionModel => transcriptionModel;

        /// <summary>
        /// When true, audio is sent directly to the chat model as multimodal input
        /// instead of going through STT first. Reduces latency but requires a model
        /// that supports audio input (e.g. gpt-4o-audio).
        /// </summary>
        public bool PassAudioDirectly
        {
            get => passAudioDirectly;
            set => passAudioDirectly = value;
        }

        /// <summary>
        /// Whether new transcriptions can interrupt an in-progress NPC stream.
        /// When true: new voice input interrupts old NPC response, merges user messages, and starts a new request.
        /// When false: waits for current NPC response to finish before processing new voice input.
        /// </summary>
        public bool Interruptible
        {
            get => interruptible;
            set => interruptible = value;
        }

        private void Start()
        {
            Initialize().Forget();
        }

        private async UniTask Initialize()
        {
            // Wait for SDK to be ready
            await UniTask.WaitUntil(() => PlayKitSDK.IsReady());

            // Auto-find NPCClient on the same GameObject
            _npcClient = GetComponent<PlayKit_NPC>();
            if (_npcClient == null)
            {
                Debug.LogError("[VoiceModule] No PlayKit_NPC found on this GameObject! Voice module requires PlayKit_NPC component.");
                return;
            }

            // Wait for NPCClient to be ready
            await UniTask.WaitUntil(() => _npcClient.IsReady);

            // Create transcription client (use settings default if model not specified)
            try
            {
                var modelToUse = string.IsNullOrEmpty(transcriptionModel) 
                    ? PlayKitSettings.Instance?.DefaultTranscriptionModel ?? "default-transcription-model"
                    : transcriptionModel;
                _transcriptionClient = PlayKitSDK.CreateTranscriptionClient(modelToUse);
                _isReady = true;
                Debug.Log($"[VoiceModule] Ready! Using transcription model '{modelToUse}' with NPC '{gameObject.name}'");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceModule] Failed to initialize: {ex.Message}");
            }
        }

        /// <summary>
        /// Process voice input and get NPC text response (non-streaming)
        /// Workflow: Audio → Transcription → NPC Talk
        /// </summary>
        /// <param name="audioClip">Audio clip containing user's voice input</param>
        /// <param name="language">Optional language code (defaults to module's defaultLanguage)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>NPC's text response, or null if failed</returns>
        public async UniTask<string> ListenAndTalk(
            AudioClip audioClip,
            string language = null,
            CancellationToken? cancellationToken = null)
        {
            if (!ValidateReadyState(audioClip)) return null;

            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            IsProcessing = true;

            try
            {
                if (passAudioDirectly)
                {
                    var msg = new Public.PlayKit_ChatMessage { Role = "user", Content = "" };
                    msg.AddAudio(audioClip);
                    var response = await _npcClient.TalkWithMessage(msg, token);
                    return response;
                }
                else
                {
                    var transcription = await TranscribeAudio(audioClip, language, token);
                    if (transcription == null) return null;
                    var response = await _npcClient.Talk(transcription, token);
                    return response;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceModule] ListenAndTalk failed: {ex.Message}");
                return null;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// Process voice input and get NPC streaming response
        /// Workflow: Audio → Transcription → NPC TalkStream
        /// </summary>
        /// <param name="audioClip">Audio clip containing user's voice input</param>
        /// <param name="onChunk">Callback for each text chunk as it streams in</param>
        /// <param name="onComplete">Callback when complete response is ready</param>
        /// <param name="language">Optional language code (defaults to module's defaultLanguage)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        public async UniTask ListenAndTalkStream(
            AudioClip audioClip,
            Action<string> onChunk,
            Action<string> onComplete,
            string language = null,
            CancellationToken? cancellationToken = null)
        {
            if (!ValidateReadyState(audioClip))
            {
                onComplete?.Invoke(null);
                return;
            }

            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            IsProcessing = true;

            try
            {
                if (passAudioDirectly)
                {
                    var msg = new Public.PlayKit_ChatMessage { Role = "user", Content = "" };
                    msg.AddAudio(audioClip);
                    await _npcClient.TalkStreamWithMessage(
                        msg,
                        onChunk,
                        completeResponse =>
                        {
                            IsProcessing = false;
                            onComplete?.Invoke(completeResponse);
                        },
                        token
                    );
                }
                else
                {
                    var transcription = await TranscribeAudio(audioClip, language, token);
                    if (transcription == null)
                    {
                        IsProcessing = false;
                        onComplete?.Invoke(null);
                        return;
                    }

                    await _npcClient.TalkStream(
                        transcription,
                        onChunk,
                        completeResponse =>
                        {
                            IsProcessing = false;
                            onComplete?.Invoke(completeResponse);
                        },
                        token
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceModule] ListenAndTalkStream failed: {ex.Message}");
                IsProcessing = false;
                onComplete?.Invoke(null);
            }
        }

        /// <summary>
        /// Transcribe audio to text only, without calling NPC
        /// Useful for getting user input text without generating response
        /// </summary>
        /// <param name="audioClip">Audio clip to transcribe</param>
        /// <param name="language">Optional language code (defaults to module's defaultLanguage)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Transcribed text, or null if failed</returns>
        public async UniTask<string> ListenOnly(
            AudioClip audioClip,
            string language = null,
            CancellationToken? cancellationToken = null)
        {
            if (!ValidateReadyState(audioClip)) return null;

            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            IsProcessing = true;

            try
            {
                return await TranscribeAudio(audioClip, language, token);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// Get the full transcription result with metadata (segments, duration, etc.)
        /// </summary>
        /// <param name="audioClip">Audio clip to transcribe</param>
        /// <param name="language">Optional language code (defaults to module's defaultLanguage)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Full transcription result with metadata</returns>
        public async UniTask<Public.PlayKit_TranscriptionResult> GetFullTranscription(
            AudioClip audioClip,
            string language = null,
            CancellationToken? cancellationToken = null)
        {
            if (!ValidateReadyState(audioClip))
            {
                return new Public.PlayKit_TranscriptionResult("Voice module not ready or audio clip is null");
            }

            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            IsProcessing = true;

            try
            {
                var result = await _transcriptionClient.TranscribeAudioClipAsync(
                    audioClip,
                    language ?? defaultLanguage,
                    null,
                    token
                );

                if (result.Success && logTranscription)
                {
                    Debug.Log($"[VoiceModule] Full transcription result:\n" +
                             $"  Text: '{result.Text}'\n" +
                             $"  Language: {result.Language ?? "unknown"}\n" +
                             $"  Duration: {result.DurationInSeconds?.ToString("F2") ?? "unknown"}s\n" +
                             $"  Segments: {result.Segments?.Length ?? 0}");
                }

                return result;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// Internal helper to transcribe audio with logging
        /// </summary>
        private async UniTask<string> TranscribeAudio(AudioClip audioClip, string language, CancellationToken token)
        {
            var transcription = await _transcriptionClient.TranscribeAudioClipAsync(
                audioClip,
                language ?? defaultLanguage,
                null,
                token
            );

            if (!transcription.Success || string.IsNullOrEmpty(transcription.Text))
            {
                Debug.LogError($"[VoiceModule] Transcription failed: {transcription.Error}");
                return null;
            }

            if (logTranscription)
            {
                Debug.Log($"[VoiceModule] 🎤 Transcribed: '{transcription.Text}'" +
                         (transcription.Language != null ? $" (Language: {transcription.Language})" : ""));
            }

            return transcription.Text;
        }

        /// <summary>
        /// Validate that the module is ready and audio clip is valid
        /// </summary>
        private bool ValidateReadyState(AudioClip audioClip)
        {
            if (!_isReady)
            {
                Debug.LogError("[VoiceModule] Voice module is not ready yet. Wait for initialization.");
                return false;
            }

            if (audioClip == null)
            {
                Debug.LogError("[VoiceModule] AudioClip cannot be null");
                return false;
            }

            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError("[VoiceModule] GameObject is not active");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Change the default language at runtime
        /// </summary>
        public void SetDefaultLanguage(string language)
        {
            defaultLanguage = language;
            Debug.Log($"[VoiceModule] Default language changed to: {language}");
        }

        /// <summary>
        /// Get the associated NPCClient
        /// </summary>
        public PlayKit_NPC GetNPCClient()
        {
            return _npcClient;
        }

        /// <summary>
        /// Get the transcription client (for advanced usage)
        /// </summary>
        public PlayKit_AudioTranscriptionClient GetTranscriptionClient()
        {
            return _transcriptionClient;
        }

        /// <summary>
        /// Get or create a microphone recorder component
        /// </summary>
        /// <returns>The PlayKit_MicrophoneRecorder instance</returns>
        public PlayKit_MicrophoneRecorder GetOrCreateRecorder()
        {
            if (microphoneRecorder == null)
            {
                microphoneRecorder = GetComponent<PlayKit_MicrophoneRecorder>();
                if (microphoneRecorder == null)
                {
                    microphoneRecorder = gameObject.AddComponent<PlayKit_MicrophoneRecorder>();
                    Debug.Log("[VoiceModule] Created new PlayKit_MicrophoneRecorder component");
                }
            }
            return microphoneRecorder;
        }

        /// <summary>
        /// Record audio from microphone and process with NPC (non-streaming)
        /// Workflow: Start Recording → User Speaks → Stop Recording → Transcription → NPC Response
        ///
        /// Recording will auto-stop when:
        /// - User manually calls StopRecording() on the recorder
        /// - maxDuration is reached
        /// - Silence is detected (if VAD enabled on recorder)
        /// </summary>
        /// <param name="maxDuration">Maximum recording duration in seconds (default: 30)</param>
        /// <param name="language">Optional language code (defaults to module's defaultLanguage)</param>
        /// <param name="onRecordingStarted">Called when recording starts</param>
        /// <param name="onRecordingProgress">Called during recording with elapsed time</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>NPC's text response, or null if failed</returns>
        public async UniTask<string> RecordAndTalk(
            float maxDuration = 30f,
            string language = null,
            Action onRecordingStarted = null,
            Action<float> onRecordingProgress = null,
            CancellationToken? cancellationToken = null)
        {
            if (!ValidateRecorderReady()) return null;

            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            IsProcessing = true;

            try
            {
                // Start recording
                if (!microphoneRecorder.StartRecording())
                {
                    Debug.LogError("[VoiceModule] Failed to start recording");
                    return null;
                }

                onRecordingStarted?.Invoke();
                Debug.Log($"[VoiceModule] 🎤 Recording started (max: {maxDuration}s)... Speak now!");

                // Wait for recording to complete (manual stop, timeout, or VAD)
                float elapsed = 0f;
                while (microphoneRecorder.IsRecording && elapsed < maxDuration)
                {
                    await UniTask.Yield(token);
                    elapsed += Time.deltaTime;
                    onRecordingProgress?.Invoke(elapsed);
                }

                // If still recording after timeout, stop it
                AudioClip audioClip;
                if (microphoneRecorder.IsRecording)
                {
                    Debug.Log("[VoiceModule] Max duration reached, stopping recording");
                    audioClip = microphoneRecorder.StopRecording();
                }
                else
                {
                    audioClip = microphoneRecorder.LastRecording;
                }

                if (audioClip == null)
                {
                    Debug.LogError("[VoiceModule] No audio recorded");
                    return null;
                }

                Debug.Log($"[VoiceModule] Recording finished ({audioClip.length:F1}s)");

                // Use existing ListenAndTalk to process the audio
                return await ListenAndTalk(audioClip, language, token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceModule] RecordAndTalk failed: {ex.Message}");
                return null;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// Record audio from microphone and get NPC streaming response
        /// Workflow: Start Recording → User Speaks → Stop Recording → Transcription → NPC Stream
        /// </summary>
        /// <param name="onChunk">Callback for each text chunk as it streams in</param>
        /// <param name="onComplete">Callback when complete response is ready</param>
        /// <param name="maxDuration">Maximum recording duration in seconds (default: 30)</param>
        /// <param name="language">Optional language code (defaults to module's defaultLanguage)</param>
        /// <param name="onRecordingStarted">Called when recording starts</param>
        /// <param name="onRecordingProgress">Called during recording with elapsed time</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        public async UniTask RecordAndTalkStream(
            Action<string> onChunk,
            Action<string> onComplete,
            float maxDuration = 30f,
            string language = null,
            Action onRecordingStarted = null,
            Action<float> onRecordingProgress = null,
            CancellationToken? cancellationToken = null)
        {
            if (!ValidateRecorderReady())
            {
                onComplete?.Invoke(null);
                return;
            }

            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();
            IsProcessing = true;

            try
            {
                // Start recording
                if (!microphoneRecorder.StartRecording())
                {
                    Debug.LogError("[VoiceModule] Failed to start recording");
                    onComplete?.Invoke(null);
                    return;
                }

                onRecordingStarted?.Invoke();
                Debug.Log($"[VoiceModule] 🎤 Recording started (max: {maxDuration}s)... Speak now!");

                // Wait for recording to complete
                float elapsed = 0f;
                while (microphoneRecorder.IsRecording && elapsed < maxDuration)
                {
                    await UniTask.Yield(token);
                    elapsed += Time.deltaTime;
                    onRecordingProgress?.Invoke(elapsed);
                }

                // Get recorded audio
                AudioClip audioClip;
                if (microphoneRecorder.IsRecording)
                {
                    Debug.Log("[VoiceModule] Max duration reached, stopping recording");
                    audioClip = microphoneRecorder.StopRecording();
                }
                else
                {
                    audioClip = microphoneRecorder.LastRecording;
                }

                if (audioClip == null)
                {
                    Debug.LogError("[VoiceModule] No audio recorded");
                    onComplete?.Invoke(null);
                    return;
                }

                Debug.Log($"[VoiceModule] Recording finished ({audioClip.length:F1}s)");

                // Use existing ListenAndTalkStream to process the audio
                await ListenAndTalkStream(audioClip, onChunk, onComplete, language, token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceModule] RecordAndTalkStream failed: {ex.Message}");
                IsProcessing = false;
                onComplete?.Invoke(null);
            }
        }

        /// <summary>
        /// Validate that recorder is available and ready
        /// </summary>
        private bool ValidateRecorderReady()
        {
            if (!_isReady)
            {
                Debug.LogError("[VoiceModule] Voice module is not ready yet. Wait for initialization.");
                return false;
            }

            if (microphoneRecorder == null)
            {
                Debug.LogError("[VoiceModule] No microphone recorder configured! Please assign a PlayKit_MicrophoneRecorder component or call GetOrCreateRecorder().");
                return false;
            }

            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError("[VoiceModule] GameObject is not active");
                return false;
            }

            return true;
        }

        #region Always Listening Mode

        private Action<string> _alwaysListeningOnChunk;
        private Action<string> _alwaysListeningOnComplete;
        private string _alwaysListeningLanguage;

        // Parallel transcription & generation tracking
        private int _transcriptionGeneration;
        private int _lastForwardedGeneration;
        private int _pendingTranscriptions;
        private string _accumulatedUserText;

        /// <summary>
        /// Start always listening mode - automatically detects voice and processes with NPC.
        /// Supports parallel transcription: multiple recordings can be transcribed simultaneously.
        /// When interruptible=true, new transcriptions interrupt the current NPC stream and merge user messages.
        /// Workflow: Listen → Voice Detected → Auto Record → Parallel Transcription → NPC Stream → Restart Listening
        /// </summary>
        /// <param name="onChunk">Callback for each text chunk as NPC response streams in</param>
        /// <param name="onComplete">Callback when complete NPC response is ready</param>
        /// <param name="language">Optional language code for transcription (defaults to module's defaultLanguage)</param>
        public void StartAlwaysListening(
            Action<string> onChunk = null,
            Action<string> onComplete = null,
            string language = null)
        {
            if (!ValidateRecorderReady())
            {
                Debug.LogError("[VoiceModule] Cannot start always listening - recorder not ready");
                return;
            }

            // Store callbacks for use when recording completes
            _alwaysListeningOnChunk = onChunk;
            _alwaysListeningOnComplete = onComplete;
            _alwaysListeningLanguage = language;

            // Reset generation tracking
            _transcriptionGeneration = 0;
            _lastForwardedGeneration = 0;
            _pendingTranscriptions = 0;
            _accumulatedUserText = null;

            // Enable always listening mode on recorder
            microphoneRecorder.SetAlwaysListeningMode(true);

            // Subscribe to recording stopped event
            microphoneRecorder.OnRecordingStopped -= OnAlwaysListeningRecordingStopped;
            microphoneRecorder.OnRecordingStopped += OnAlwaysListeningRecordingStopped;

            microphoneRecorder.OnAutoRecordingStarted -= OnAlwaysListeningAutoRecordingStarted;
            microphoneRecorder.OnAutoRecordingStarted += OnAlwaysListeningAutoRecordingStarted;

            // Start listening
            if (!microphoneRecorder.StartListening())
            {
                // Provide specific reason for failure
                if (microphoneRecorder.IsListening)
                    Debug.LogError("[VoiceModule] Failed to start listening: recorder is already listening. Call StopAlwaysListening() first.");
                else if (microphoneRecorder.IsRecording)
                    Debug.LogError("[VoiceModule] Failed to start listening: recorder is currently recording.");
                else if (!microphoneRecorder.AlwaysListeningModeEnabled)
                    Debug.LogError("[VoiceModule] Failed to start listening: always listening mode not enabled.");
                else
                    Debug.LogError("[VoiceModule] Failed to start listening: check microphone availability.");
                return;
            }

            Debug.Log("[VoiceModule] Always listening mode started - speak to trigger recording");
        }

        /// <summary>
        /// Stop always listening mode.
        /// Only stops the recorder from accepting new audio.
        /// In-flight transcriptions and NPC streams continue until completion.
        /// </summary>
        public void StopAlwaysListening()
        {
            if (microphoneRecorder == null) return;

            // Unsubscribe from events (prevents new recordings from being processed)
            microphoneRecorder.OnRecordingStopped -= OnAlwaysListeningRecordingStopped;
            microphoneRecorder.OnAutoRecordingStarted -= OnAlwaysListeningAutoRecordingStarted;

            // Stop any ongoing recording
            if (microphoneRecorder.IsRecording)
            {
                microphoneRecorder.StopRecording(false); // Don't auto-restart
            }

            // Stop listening
            microphoneRecorder.StopListening();

            // Disable always listening mode
            microphoneRecorder.SetAlwaysListeningMode(false);

            Debug.Log("[VoiceModule] Always listening mode stopped (in-flight processing continues)");
        }

        private void OnAlwaysListeningAutoRecordingStarted()
        {
            Debug.Log("[VoiceModule] Voice detected - auto-recording started");
        }

        private void OnAlwaysListeningRecordingStopped(AudioClip audioClip)
        {
            if (audioClip == null)
            {
                Debug.LogWarning("[VoiceModule] No audio recorded in always listening mode");
                return;
            }

            // Assign generation based on recording order (not completion order)
            _transcriptionGeneration++;
            int generation = _transcriptionGeneration;
            _pendingTranscriptions++;

            Debug.Log($"[VoiceModule] Auto-recording completed ({audioClip.length:F1}s), transcribing (gen={generation})...");

            // Fire transcription in parallel — recorder auto-restarts via debounce
            TranscribeAndForwardToNpc(audioClip, generation).Forget();
        }

        /// <summary>
        /// Core always-listening pipeline: transcribe audio, check generation, handle interruption/merging, forward to NPC.
        /// Multiple instances may run in parallel (one per recording).
        /// </summary>
        private async UniTask TranscribeAndForwardToNpc(AudioClip audioClip, int generation)
        {
            var token = this.GetCancellationTokenOnDestroy();

            try
            {
                string transcription = null;

                if (passAudioDirectly)
                {
                    _pendingTranscriptions--;
                }
                else
                {
                    transcription = await TranscribeAudio(audioClip, _alwaysListeningLanguage, token);
                    _pendingTranscriptions--;

                    if (string.IsNullOrEmpty(transcription))
                    {
                        Debug.LogWarning($"[VoiceModule] Transcription returned empty (gen={generation}), skipping");
                        if (_pendingTranscriptions <= 0)
                        {
                            _pendingTranscriptions = 0;
                            if (!_npcClient.IsTalking) IsProcessing = false;
                        }
                        return;
                    }
                }

                if (generation < _lastForwardedGeneration)
                {
                    Debug.Log($"[VoiceModule] Stale result (gen={generation} < last={_lastForwardedGeneration}), discarding");
                    return;
                }

                _lastForwardedGeneration = generation;
                IsProcessing = true;

                if (_npcClient.IsTalking)
                {
                    if (interruptible)
                    {
                        _npcClient.InterruptCurrentTalk();

                        if (!passAudioDirectly && !string.IsNullOrEmpty(_accumulatedUserText))
                        {
                            _npcClient.RevertChatMessages(1);
                            transcription = _accumulatedUserText + " " + transcription;
                            Debug.Log($"[VoiceModule] Merged user text: '{transcription}'");
                        }
                    }
                    else
                    {
                        Debug.Log($"[VoiceModule] Waiting for current NPC talk to finish (non-interruptible mode)");
                        await UniTask.WaitUntil(() => !_npcClient.IsTalking, cancellationToken: token);
                    }
                }

                if (!passAudioDirectly)
                    _accumulatedUserText = transcription;

                Action<string> onDone = response =>
                {
                    if (generation >= _lastForwardedGeneration)
                    {
                        IsProcessing = false;
                        _accumulatedUserText = null;
                    }
                    _alwaysListeningOnComplete?.Invoke(response);
                };

                if (passAudioDirectly)
                {
                    var msg = new Public.PlayKit_ChatMessage { Role = "user", Content = "" };
                    msg.AddAudio(audioClip);
                    await _npcClient.TalkStreamWithMessage(msg, _alwaysListeningOnChunk, onDone, token);
                }
                else
                {
                    await _npcClient.TalkStream(transcription, _alwaysListeningOnChunk, onDone, token);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceModule] Error processing always listening audio (gen={generation}): {ex.Message}");
                _pendingTranscriptions = Mathf.Max(0, _pendingTranscriptions);
                if (_pendingTranscriptions <= 0 && !_npcClient.IsTalking)
                {
                    IsProcessing = false;
                }
                _alwaysListeningOnComplete?.Invoke(null);
            }
        }

        private void OnDestroy()
        {
            // Clean up event subscriptions
            if (microphoneRecorder != null)
            {
                microphoneRecorder.OnRecordingStopped -= OnAlwaysListeningRecordingStopped;
                microphoneRecorder.OnAutoRecordingStarted -= OnAlwaysListeningAutoRecordingStarted;
            }
        }

        #endregion
    }
}
