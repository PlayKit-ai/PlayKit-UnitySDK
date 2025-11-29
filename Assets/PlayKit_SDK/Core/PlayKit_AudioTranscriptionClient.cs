using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PlayKit_SDK
{
    /// <summary>
    /// Client for audio transcription functionality
    /// Converts audio (AudioClip or raw bytes) to text using AI models
    /// </summary>
    public class PlayKit_AudioTranscriptionClient
    {
        private readonly string _model;
        private readonly Services.TranscriptionService _service;

        internal PlayKit_AudioTranscriptionClient(string model, Services.TranscriptionService service)
        {
            _model = model;
            _service = service;
        }

        /// <summary>
        /// Get the transcription model name this client is using
        /// </summary>
        public string ModelName => _model;

        /// <summary>
        /// Transcribe audio data (raw bytes) to text
        /// </summary>
        /// <param name="audioData">Audio file bytes (WAV, MP3, etc.)</param>
        /// <param name="language">Optional language code (e.g., "en", "zh")</param>
        /// <param name="prompt">Optional prompt to guide transcription</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Transcription result with text and optional metadata</returns>
        public async UniTask<Public.DW_TranscriptionResult> TranscribeAsync(
            byte[] audioData,
            string language = null,
            string prompt = null,
            CancellationToken cancellationToken = default)
        {
            if (audioData == null || audioData.Length == 0)
            {
                Debug.LogError("[DW_AudioTranscriptionClient] Audio data cannot be null or empty");
                return new Public.DW_TranscriptionResult("Audio data cannot be null or empty");
            }

            return await _service.TranscribeAsync(
                _model,
                audioData,
                language,
                prompt,
                null,
                cancellationToken
            );
        }

        /// <summary>
        /// Transcribe Unity AudioClip to text
        /// </summary>
        /// <param name="audioClip">Unity AudioClip to transcribe</param>
        /// <param name="language">Optional language code (e.g., "en", "zh")</param>
        /// <param name="prompt">Optional prompt to guide transcription</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Transcription result with text and optional metadata</returns>
        public async UniTask<Public.DW_TranscriptionResult> TranscribeAudioClipAsync(
            AudioClip audioClip,
            string language = null,
            string prompt = null,
            CancellationToken cancellationToken = default)
        {
            if (audioClip == null)
            {
                Debug.LogError("[DW_AudioTranscriptionClient] AudioClip cannot be null");
                return new Public.DW_TranscriptionResult("AudioClip cannot be null");
            }

            return await _service.TranscribeAudioClipAsync(
                _model,
                audioClip,
                language,
                prompt,
                null,
                cancellationToken
            );
        }
    }
}
