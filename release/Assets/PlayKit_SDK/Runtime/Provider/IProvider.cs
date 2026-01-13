using System;
using Cysharp.Threading.Tasks;
using PlayKit_SDK.Provider.AI;

namespace PlayKit_SDK.Provider
{
    /// <summary>
    /// Interface for chat completion providers
    /// </summary>
    internal interface IChatProvider
    {
        /// <summary>
        /// Sends a chat completion request and returns the complete response
        /// </summary>
        UniTask<ChatCompletionResponse> ChatCompletionAsync(
            ChatCompletionRequest request, 
            System.Threading.CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a streaming chat completion request
        /// Supports both UI Message Stream format (onTextDelta) and legacy format (onLegacyResponse)
        /// </summary>
        UniTask ChatCompletionStreamAsync(
            ChatCompletionRequest request, 
            Action<string> onTextDelta,
            Action<StreamCompletionResponse> onLegacyResponse, 
            Action onFinally, 
            System.Threading.CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for image generation providers
    /// </summary>
    internal interface IImageProvider
    {
        /// <summary>
        /// Generates images based on a text prompt
        /// </summary>
        UniTask<ImageGenerationResponse> GenerateImageAsync(
            ImageGenerationRequest request, 
            System.Threading.CancellationToken cancellationToken = default);
    }
}