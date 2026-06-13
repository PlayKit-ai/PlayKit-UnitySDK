using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Synthesize spoken audio AND timestamp alignment (word/sentence timings).
        /// The returned result carries both the audio (use <c>ToAudioClip()</c>) and
        /// <c>Alignment</c>.
        /// </summary>
        /// <param name="text">Text to speak (max 10000 characters)</param>
        /// <param name="voice">Optional voice id. Null uses the system default voice.</param>
        /// <param name="granularity">"word" (default) or "sentence".</param>
        /// <param name="speed">Optional playback speed multiplier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Speech result with audio bytes, metadata, and <c>Alignment</c></returns>
        public async UniTask<Public.PlayKit_SpeechResult> SynthesizeWithTimestampsAsync(
            string text,
            string voice = null,
            string granularity = "word",
            float? speed = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(text))
            {
                Debug.LogError("[PlayKit_TextToSpeechClient] Text cannot be null or empty");
                return new Public.PlayKit_SpeechResult("Text cannot be null or empty");
            }

            return await _service.SynthesizeWithTimestampsAsync(
                _model,
                text,
                voice,
                granularity,
                speed,
                cancellationToken: cancellationToken
            );
        }

        /// <summary>
        /// Synthesize spoken audio from text using full synthesis options
        /// (voice or voice mix, speed, volume, pitch, emotion, language, sample rate).
        /// </summary>
        /// <param name="text">Text to speak (max 10000 characters)</param>
        /// <param name="options">Synthesis options. Null uses all defaults.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Speech result containing audio bytes and metadata</returns>
        public async UniTask<Public.PlayKit_SpeechResult> SynthesizeAsync(
            string text,
            Public.PlayKit_SpeechOptions options,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(text))
            {
                Debug.LogError("[PlayKit_TextToSpeechClient] Text cannot be null or empty");
                return new Public.PlayKit_SpeechResult("Text cannot be null or empty");
            }

            var validationError = ValidateOptions(options);
            if (validationError != null)
            {
                Debug.LogError($"[PlayKit_TextToSpeechClient] {validationError}");
                return new Public.PlayKit_SpeechResult(validationError);
            }

            options = options ?? new Public.PlayKit_SpeechOptions();
            return await _service.SynthesizeAsync(
                _model,
                text,
                voice: options.Voice,
                speed: options.Speed,
                volume: options.Volume,
                pitch: options.Pitch,
                emotion: options.Emotion,
                language: options.Language,
                voiceMix: MapVoiceMix(options.VoiceMix),
                sampleRate: options.SampleRate,
                cancellationToken: cancellationToken
            );
        }

        /// <summary>
        /// Synthesize spoken audio from text using full synthesis options and decode it
        /// into a playable Unity AudioClip.
        /// </summary>
        /// <param name="text">Text to speak (max 10000 characters)</param>
        /// <param name="options">Synthesis options. Null uses all defaults.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>An AudioClip ready to assign to an AudioSource, or null on failure</returns>
        public async UniTask<AudioClip> SynthesizeToAudioClipAsync(
            string text,
            Public.PlayKit_SpeechOptions options,
            CancellationToken cancellationToken = default)
        {
            var result = await SynthesizeAsync(text, options, cancellationToken);
            if (!result.Success)
            {
                Debug.LogError($"[PlayKit_TextToSpeechClient] Synthesis failed: {result.Error}");
                return null;
            }

            return result.ToAudioClip();
        }

        /// <summary>
        /// Synthesize spoken audio AND timestamp alignment using full synthesis options.
        /// The returned result carries both the audio (use <c>ToAudioClip()</c>) and
        /// <c>Alignment</c>.
        /// </summary>
        /// <param name="text">Text to speak (max 10000 characters)</param>
        /// <param name="options">Synthesis options. Null uses all defaults.</param>
        /// <param name="granularity">"word" (default) or "sentence".</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Speech result with audio bytes, metadata, and <c>Alignment</c></returns>
        public async UniTask<Public.PlayKit_SpeechResult> SynthesizeWithTimestampsAsync(
            string text,
            Public.PlayKit_SpeechOptions options,
            string granularity = "word",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(text))
            {
                Debug.LogError("[PlayKit_TextToSpeechClient] Text cannot be null or empty");
                return new Public.PlayKit_SpeechResult("Text cannot be null or empty");
            }

            var validationError = ValidateOptions(options);
            if (validationError != null)
            {
                Debug.LogError($"[PlayKit_TextToSpeechClient] {validationError}");
                return new Public.PlayKit_SpeechResult(validationError);
            }

            options = options ?? new Public.PlayKit_SpeechOptions();
            return await _service.SynthesizeWithTimestampsAsync(
                _model,
                text,
                voice: options.Voice,
                granularity: granularity,
                speed: options.Speed,
                volume: options.Volume,
                pitch: options.Pitch,
                emotion: options.Emotion,
                language: options.Language,
                voiceMix: MapVoiceMix(options.VoiceMix),
                sampleRate: options.SampleRate,
                cancellationToken: cancellationToken
            );
        }

        /// <summary>
        /// List the voices available for synthesis.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Available voices, or null on failure</returns>
        public async UniTask<List<Public.PlayKit_VoiceInfo>> GetVoicesAsync(
            CancellationToken cancellationToken = default)
        {
            var voices = await _service.GetVoicesAsync(cancellationToken);
            if (voices == null)
            {
                Debug.LogError("[PlayKit_TextToSpeechClient] Failed to list voices");
                return null;
            }

            return voices;
        }

        /// <summary>Validate option combinations. Returns an error message, or null when valid.</summary>
        private static string ValidateOptions(Public.PlayKit_SpeechOptions options)
        {
            if (options == null) return null;

            bool hasVoice = !string.IsNullOrEmpty(options.Voice);
            bool hasMix = options.VoiceMix != null && options.VoiceMix.Count > 0;
            if (hasVoice && hasMix)
            {
                return "Voice and VoiceMix cannot both be set; choose one";
            }
            if (options.VoiceMix != null && (options.VoiceMix.Count < 1 || options.VoiceMix.Count > 4))
            {
                return "VoiceMix must contain between 1 and 4 entries";
            }
            return null;
        }

        /// <summary>Map public voice-mix entries to the provider DTO. Returns null when empty.</summary>
        private static List<Provider.AI.VoiceMixEntry> MapVoiceMix(List<Public.PlayKit_VoiceMixEntry> voiceMix)
        {
            if (voiceMix == null || voiceMix.Count == 0) return null;
            return voiceMix.Select(e => new Provider.AI.VoiceMixEntry
            {
                Voice = e.Voice,
                Weight = e.Weight
            }).ToList();
        }
    }
}
