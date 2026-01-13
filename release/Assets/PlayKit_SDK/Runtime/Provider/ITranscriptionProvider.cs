using System.Threading;
using Cysharp.Threading.Tasks;
using PlayKit_SDK.Provider.AI;

namespace PlayKit_SDK.Provider
{
    /// <summary>
    /// Interface for audio transcription providers
    /// </summary>
    internal interface ITranscriptionProvider
    {
        /// <summary>
        /// Transcribe audio to text
        /// </summary>
        /// <param name="request">Transcription request containing model and audio data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Transcription response with text and optional segments</returns>
        UniTask<TranscriptionResponse> TranscribeAsync(
            TranscriptionRequest request,
            CancellationToken cancellationToken = default);
    }
}
