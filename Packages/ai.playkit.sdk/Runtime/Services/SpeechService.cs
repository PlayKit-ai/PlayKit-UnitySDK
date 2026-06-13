using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayKit_SDK.Provider;
using PlayKit_SDK.Provider.AI;
using UnityEngine;

namespace PlayKit_SDK.Services
{
    /// <summary>
    /// Service for text-to-speech synthesis. Builds the neutral request, requests PCM
    /// (via output_format "pcm_{rate}") so the result can be decoded into an AudioClip
    /// in-engine, and wraps the response.
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

        /// <summary>Build a neutral SpeechRequest. Audio is requested as PCM for AudioClip decoding.</summary>
        private static SpeechRequest BuildRequest(
            string model, string text, string voice,
            float? speed, float? volume, int? pitch, string emotion,
            string language, List<VoiceMixEntry> voiceMix, int sampleRate)
        {
            VoiceSettings vs = null;
            if (speed.HasValue || volume.HasValue || pitch.HasValue || !string.IsNullOrEmpty(emotion))
            {
                vs = new VoiceSettings
                {
                    Speed = speed,
                    Volume = volume,
                    Pitch = pitch,
                    Emotion = emotion
                };
            }

            return new SpeechRequest
            {
                Model = model,
                Text = text,
                Voice = (voiceMix != null && voiceMix.Count > 0) ? null : voice,
                VoiceMix = (voiceMix != null && voiceMix.Count > 0) ? voiceMix : null,
                VoiceSettings = vs,
                OutputFormat = $"pcm_{sampleRate}",
                Language = language
            };
        }

        private static Public.PlayKit_SpeechAlignment MapAlignment(SpeechAlignment a)
        {
            if (a == null) return null;
            return new Public.PlayKit_SpeechAlignment
            {
                Granularity = a.Granularity,
                Items = a.Items?.Select(it => new Public.PlayKit_SpeechAlignmentItem
                {
                    Text = it.Text,
                    StartMs = it.StartMs,
                    EndMs = it.EndMs,
                    TextStart = it.TextStart,
                    TextEnd = it.TextEnd
                }).ToList() ?? new List<Public.PlayKit_SpeechAlignmentItem>()
            };
        }

        private static Public.PlayKit_SpeechResult Wrap(SpeechAudioResponse response)
        {
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
                channels: response.Channels,
                alignment: MapAlignment(response.Alignment)
            );
        }

        /// <summary>Synthesize speech audio from text (raw PCM).</summary>
        public async UniTask<Public.PlayKit_SpeechResult> SynthesizeAsync(
            string model,
            string text,
            string voice = null,
            float? speed = null,
            float? volume = null,
            int? pitch = null,
            string emotion = null,
            string language = null,
            List<VoiceMixEntry> voiceMix = null,
            int sampleRate = DEFAULT_SAMPLE_RATE,
            int channels = DEFAULT_CHANNELS,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(model))
                return new Public.PlayKit_SpeechResult("Model name cannot be empty");
            if (string.IsNullOrEmpty(text))
                return new Public.PlayKit_SpeechResult("Text to synthesize cannot be empty");

            var request = BuildRequest(model, text, voice, speed, volume, pitch, emotion, language, voiceMix, sampleRate);
            try
            {
                var response = await _provider.SynthesizeAsync(request, sampleRate, channels, cancellationToken);
                return Wrap(response);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SpeechService] Synthesis failed: {ex.Message}");
                return new Public.PlayKit_SpeechResult($"Synthesis failed: {ex.Message}");
            }
        }

        /// <summary>Synthesize speech AND return timestamp alignment (word/sentence).</summary>
        public async UniTask<Public.PlayKit_SpeechResult> SynthesizeWithTimestampsAsync(
            string model,
            string text,
            string voice = null,
            string granularity = "word",
            float? speed = null,
            float? volume = null,
            int? pitch = null,
            string emotion = null,
            string language = null,
            List<VoiceMixEntry> voiceMix = null,
            int sampleRate = DEFAULT_SAMPLE_RATE,
            int channels = DEFAULT_CHANNELS,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(model))
                return new Public.PlayKit_SpeechResult("Model name cannot be empty");
            if (string.IsNullOrEmpty(text))
                return new Public.PlayKit_SpeechResult("Text to synthesize cannot be empty");

            var request = BuildRequest(model, text, voice, speed, volume, pitch, emotion, language, voiceMix, sampleRate);
            request.SubtitleType = granularity;
            try
            {
                var response = await _provider.SynthesizeWithTimestampsAsync(request, sampleRate, channels, cancellationToken);
                return Wrap(response);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SpeechService] Synthesis failed: {ex.Message}");
                return new Public.PlayKit_SpeechResult($"Synthesis failed: {ex.Message}");
            }
        }

        /// <summary>List the voices available for synthesis. Returns null on failure.</summary>
        public async UniTask<List<Public.PlayKit_VoiceInfo>> GetVoicesAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _provider.ListVoicesAsync(cancellationToken);
                if (response?.Voices == null)
                {
                    return null;
                }

                return response.Voices.Select(v => new Public.PlayKit_VoiceInfo
                {
                    VoiceId = v.VoiceId,
                    Name = v.Name,
                    Description = v.Description,
                    Language = v.Language,
                    Kind = v.Kind
                }).ToList();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SpeechService] Failed to list voices: {ex.Message}");
                return null;
            }
        }
    }
}
