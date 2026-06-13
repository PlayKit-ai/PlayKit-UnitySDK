using System.Threading;
using Cysharp.Threading.Tasks;
using PlayKit_SDK.Provider.AI;

namespace PlayKit_SDK.Provider
{
    /// <summary>
    /// Interface for text-to-speech (TTS) providers
    /// </summary>
    internal interface ISpeechProvider
    {
        /// <summary>
        /// Synthesize speech audio from text
        /// </summary>
        /// <param name="request">Speech request containing model and text</param>
        /// <param name="sampleRate">Requested PCM sample rate (stamped onto the response)</param>
        /// <param name="channels">Requested PCM channel count (stamped onto the response)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Speech audio response with raw PCM bytes and metadata, or null on failure</returns>
        UniTask<SpeechAudioResponse> SynthesizeAsync(
            SpeechRequest request,
            int sampleRate,
            int channels,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Synthesize speech AND return timestamp alignment. Hits the
        /// speech-with-timestamps variant (JSON envelope: base64 audio + alignment).
        /// </summary>
        UniTask<SpeechAudioResponse> SynthesizeWithTimestampsAsync(
            SpeechRequest request,
            int sampleRate,
            int channels,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// List the voices available for synthesis (GET /v2/audio/voices).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Voices response, or null on failure</returns>
        UniTask<SpeechVoicesResponse> ListVoicesAsync(
            CancellationToken cancellationToken = default);
    }
}
