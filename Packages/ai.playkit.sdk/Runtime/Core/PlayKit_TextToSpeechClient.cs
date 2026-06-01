using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PlayKit_SDK
{
    /// <summary>
    /// Client for text-to-speech (TTS) synthesis.
    /// Converts text into spoken audio that can be played back as a Unity AudioClip.
    /// </summary>
    public class PlayKit_TextToSpeechClient
    {
        private readonly string _model;
        private readonly Services.SpeechService _service;

        internal PlayKit_TextToSpeechClient(string model, Services.SpeechService service)
        {
            _model = model;
            _service = service;
        }

        /// <summary>
        /// Get the TTS model name this client is using
        /// </summary>
        public string ModelName => _model;

        /// <summary>
        /// Synthesize spoken audio from text and return the raw result (audio bytes + metadata).
        /// </summary>
        /// <param name="text">Text to speak (max 10000 characters)</param>
        /// <param name="voice">Optional voice id (e.g., "male-qn-qingse"). Null uses the system default voice.</param>
        /// <param name="speed">Optional playback speed multiplier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Speech result containing audio bytes and metadata</returns>
        public async UniTask<Public.PlayKit_SpeechResult> SynthesizeAsync(
            string text,
            string voice = null,
            float? speed = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(text))
            {
                Debug.LogError("[PlayKit_TextToSpeechClient] Text cannot be null or empty");
                return new Public.PlayKit_SpeechResult("Text cannot be null or empty");
            }

            return await _service.SynthesizeAsync(
                _model,
                text,
                voice,
                speed,
                cancellationToken: cancellationToken
            );
        }

        /// <summary>
        /// Synthesize spoken audio from text and decode it into a playable Unity AudioClip.
        /// </summary>
        /// <param name="text">Text to speak (max 10000 characters)</param>
        /// <param name="voice">Optional voice id (e.g., "male-qn-qingse"). Null uses the system default voice.</param>
        /// <param name="speed">Optional playback speed multiplier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>An AudioClip ready to assign to an AudioSource, or null on failure</returns>
        public async UniTask<AudioClip> SynthesizeToAudioClipAsync(
            string text,
            string voice = null,
            float? speed = null,
            CancellationToken cancellationToken = default)
        {
            var result = await SynthesizeAsync(text, voice, speed, cancellationToken);
            if (!result.Success)
            {
                Debug.LogError($"[PlayKit_TextToSpeechClient] Synthesis failed: {result.Error}");
                return null;
            }

            return result.ToAudioClip();
        }
    }
}
