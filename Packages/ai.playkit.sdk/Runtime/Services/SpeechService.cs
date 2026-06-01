using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayKit_SDK.Provider;
using PlayKit_SDK.Provider.AI;
using UnityEngine;

namespace PlayKit_SDK.Services
{
    /// <summary>
    /// Service for text-to-speech synthesis. Builds the request, requests PCM audio so the
    /// result can be decoded into an AudioClip in-engine, and wraps the response.
    /// </summary>
    internal class SpeechService
    {
        // MiniMax accepts 8000/16000/22050/24000/32000/44100; 24000 is a good voice default.
        private const int DEFAULT_SAMPLE_RATE = 24000;
        private const int DEFAULT_CHANNELS = 1;

        private readonly ISpeechProvider _provider;

        public SpeechService(ISpeechProvider provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// Synthesize speech audio from text.
        /// </summary>
        public async UniTask<Public.PlayKit_SpeechResult> SynthesizeAsync(
            string model,
            string text,
            string voice = null,
            float? speed = null,
            float? vol = null,
            float? pitch = null,
            string emotion = null,
            string languageBoost = null,
            int sampleRate = DEFAULT_SAMPLE_RATE,
            int channels = DEFAULT_CHANNELS,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(model))
            {
                return new Public.PlayKit_SpeechResult("Model name cannot be empty");
            }

            if (string.IsNullOrEmpty(text))
            {
                return new Public.PlayKit_SpeechResult("Text to synthesize cannot be empty");
            }

            // Request raw PCM so PlayKit_SpeechResult.ToAudioClip() can decode it directly.
            var audioSetting = new Dictionary<string, object>
            {
                { "format", "pcm" },
                { "sample_rate", sampleRate },
                { "channel", channels }
            };

            var request = new SpeechRequest
            {
                Model = model,
                Text = text,
                Voice = voice,
                Speed = speed,
                Vol = vol,
                Pitch = pitch,
                Emotion = emotion,
                LanguageBoost = languageBoost,
                AudioSetting = audioSetting
            };

            try
            {
                var response = await _provider.SynthesizeAsync(request, sampleRate, channels, cancellationToken);

                if (response == null || response.Audio == null || response.Audio.Length == 0)
                {
                    return new Public.PlayKit_SpeechResult("Failed to get valid audio from API");
                }

                return new Public.PlayKit_SpeechResult(
                    audioData: response.Audio,
                    format: response.Format,
                    usageCharacters: response.UsageCharacters,
                    audioLengthMs: response.AudioLengthMs,
                    sampleRate: response.SampleRate,
                    channels: response.Channels
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SpeechService] Synthesis failed: {ex.Message}");
                return new Public.PlayKit_SpeechResult($"Synthesis failed: {ex.Message}");
            }
        }
    }
}
