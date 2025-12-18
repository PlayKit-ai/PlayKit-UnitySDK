using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayKit_SDK.Provider;
using PlayKit_SDK.Provider.AI;
using UnityEngine;

namespace PlayKit_SDK.Services
{
    /// <summary>
    /// Service for audio transcription, handles audio format conversion and API communication
    /// </summary>
    internal class TranscriptionService
    {
        private readonly ITranscriptionProvider _provider;

        public TranscriptionService(ITranscriptionProvider provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// Transcribe audio data (raw bytes) to text
        /// </summary>
        public async UniTask<Public.PlayKit_TranscriptionResult> TranscribeAsync(
            string model,
            byte[] audioData,
            string language = null,
            string prompt = null,
            float? temperature = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(model))
            {
                return new Public.PlayKit_TranscriptionResult("Model name cannot be empty");
            }

            if (audioData == null || audioData.Length == 0)
            {
                return new Public.PlayKit_TranscriptionResult("Audio data cannot be empty");
            }

            // Convert audio bytes to base64
            string audioBase64 = Convert.ToBase64String(audioData);

            var request = new TranscriptionRequest
            {
                Model = model,
                Audio = audioBase64,
                Language = language,
                Prompt = prompt,
                Temperature = temperature
            };

            try
            {
                var response = await _provider.TranscribeAsync(request, cancellationToken);

                if (response == null || string.IsNullOrEmpty(response.Text))
                {
                    return new Public.PlayKit_TranscriptionResult("Failed to get valid transcription from API");
                }

                return new Public.PlayKit_TranscriptionResult(
                    text: response.Text,
                    language: response.Language,
                    durationInSeconds: response.DurationInSeconds,
                    segments: ConvertSegments(response.Segments)
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TranscriptionService] Transcription failed: {ex.Message}");
                return new Public.PlayKit_TranscriptionResult($"Transcription failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Transcribe Unity AudioClip to text
        /// </summary>
        public async UniTask<Public.PlayKit_TranscriptionResult> TranscribeAudioClipAsync(
            string model,
            AudioClip audioClip,
            string language = null,
            string prompt = null,
            float? temperature = null,
            CancellationToken cancellationToken = default)
        {
            if (audioClip == null)
            {
                return new Public.PlayKit_TranscriptionResult("AudioClip cannot be null");
            }

            // Convert AudioClip to WAV bytes
            byte[] wavData;
            try
            {
                wavData = AudioClipToWav(audioClip);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TranscriptionService] Failed to convert AudioClip to WAV: {ex.Message}");
                return new Public.PlayKit_TranscriptionResult($"Audio conversion failed: {ex.Message}");
            }

            return await TranscribeAsync(model, wavData, language, prompt, temperature, cancellationToken);
        }

        /// <summary>
        /// Convert AudioClip to WAV format bytes
        /// </summary>
        private byte[] AudioClipToWav(AudioClip clip)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                // WAV header
                int sampleCount = samples.Length;
                int byteRate = clip.frequency * clip.channels * 2; // 16-bit = 2 bytes per sample
                int blockAlign = clip.channels * 2;

                // RIFF header
                writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + sampleCount * 2); // File size - 8
                writer.Write(new char[4] { 'W', 'A', 'V', 'E' });

                // fmt chunk
                writer.Write(new char[4] { 'f', 'm', 't', ' ' });
                writer.Write(16); // Subchunk1 size (16 for PCM)
                writer.Write((ushort)1); // Audio format (1 = PCM)
                writer.Write((ushort)clip.channels); // Number of channels
                writer.Write(clip.frequency); // Sample rate
                writer.Write(byteRate); // Byte rate
                writer.Write((ushort)blockAlign); // Block align
                writer.Write((ushort)16); // Bits per sample

                // data chunk
                writer.Write(new char[4] { 'd', 'a', 't', 'a' });
                writer.Write(sampleCount * 2); // Subchunk2 size

                // Write sample data (convert float to 16-bit PCM)
                foreach (float sample in samples)
                {
                    short intSample = (short)(sample * short.MaxValue);
                    writer.Write(intSample);
                }

                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Convert internal segments to public API segments
        /// </summary>
        private Public.PlayKit_TranscriptionSegment[] ConvertSegments(TranscriptionSegment[] segments)
        {
            if (segments == null || segments.Length == 0)
            {
                return null;
            }

            var result = new Public.PlayKit_TranscriptionSegment[segments.Length];
            for (int i = 0; i < segments.Length; i++)
            {
                result[i] = new Public.PlayKit_TranscriptionSegment
                {
                    Start = segments[i].Start,
                    End = segments[i].End,
                    Text = segments[i].Text
                };
            }

            return result;
        }
    }
}
