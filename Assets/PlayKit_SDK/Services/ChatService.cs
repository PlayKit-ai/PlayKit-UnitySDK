// Developerworks_SDK/Services/ChatService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text; // MODIFIED: Added for StringBuilder
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayKit_SDK.Provider.AI;

namespace PlayKit_SDK.Services
{
    internal class ChatService
    {
        private readonly Provider.IChatProvider _chatProvider;

        public ChatService(Provider.IChatProvider chatProvider)
        {
            _chatProvider = chatProvider;
        }
        public async UniTask<Public.DW_AIResult<string>> RequestAsync(string model, Public.DW_ChatConfig config, CancellationToken cancellationToken = default)
        {
            var internalMessages = config.Messages.Select(m => new ChatMessage { Role = m.Role, Content = m.Content }).ToList();
            var request = new ChatCompletionRequest { Model = model, Messages = internalMessages, Temperature = config.Temperature, Stream = false };
            var response = await _chatProvider.ChatCompletionAsync(request, cancellationToken);
            if (response == null || response.Choices == null || response.Choices.Count == 0) return new Public.DW_AIResult<string>("Failed to get a valid response from AI.");
            return new Public.DW_AIResult<string>(data: response.Choices[0].Message.Content);
        }

        // MODIFIED: Method signature changed to accept Action<string> for onConcluded.
        public async UniTask RequestStreamAsync(string model, Public.DW_ChatStreamConfig config, Action<string> onNewChunk, Action<string> onConcluded, CancellationToken cancellationToken = default)
        {
            var internalMessages = config.Messages.Select(m => new ChatMessage { Role = m.Role, Content = m.Content }).ToList();
            var request = new ChatCompletionRequest { Model = model, Messages = internalMessages, Temperature = config.Temperature, Stream = true };

            // MODIFIED: StringBuilder to accumulate the full response.
            var fullResponseBuilder = new StringBuilder();
            
            bool concludedFired = false; 
            
            // MODIFIED: The safeOnConcluded action now invokes the callback with the accumulated string.
            Action safeOnConcluded = () => 
            { 
                if (!concludedFired) 
                { 
                    onConcluded?.Invoke(fullResponseBuilder.ToString()); 
                    concludedFired = true; 
                } 
            };

            await _chatProvider.ChatCompletionStreamAsync(
                request,
                // UI Message Stream format callback (preferred)
                textDelta =>
                {
                    if (!string.IsNullOrEmpty(textDelta))
                    {
                        fullResponseBuilder.Append(textDelta);
                        onNewChunk?.Invoke(textDelta);
                    }
                },
                // Legacy format fallback callback
                streamResponse =>
                {
                    if (streamResponse == null) return;

                    var content = streamResponse.Choices?.FirstOrDefault()?.Delta?.Content;

                    if (!string.IsNullOrEmpty(content))
                    {
                        // MODIFIED: Append the new chunk to the builder and invoke the chunk callback.
                        fullResponseBuilder.Append(content);
                        onNewChunk?.Invoke(content);
                    }

                    if (streamResponse.Choices?.FirstOrDefault()?.FinishReason != null)
                    {
                        safeOnConcluded();
                    }
                },
                safeOnConcluded,
                cancellationToken
            );
        }

        /// <summary>
        /// Request with tool calling support (non-streaming)
        /// </summary>
        public async UniTask<ChatCompletionResponse> RequestWithToolsAsync(
            string model,
            Public.DW_ChatConfig config,
            List<ChatTool> tools,
            object toolChoice,
            CancellationToken cancellationToken = default)
        {
            var internalMessages = config.Messages.Select(m => new ChatMessage
            {
                Role = m.Role,
                Content = m.Content,
                ToolCallId = m.ToolCallId,
                ToolCalls = m.ToolCalls
            }).ToList();

            var request = new ChatCompletionRequest
            {
                Model = model,
                Messages = internalMessages,
                Temperature = config.Temperature,
                Stream = false,
                Tools = tools,
                ToolChoice = toolChoice
            };

            return await _chatProvider.ChatCompletionAsync(request, cancellationToken);
        }

        /// <summary>
        /// Request with tool calling support (streaming)
        /// Text chunks are streamed, tool calls are returned in onComplete callback
        /// </summary>
        public async UniTask RequestWithToolsStreamAsync(
            string model,
            Public.DW_ChatStreamConfig config,
            List<ChatTool> tools,
            object toolChoice,
            Action<string> onTextChunk,
            Action<ChatCompletionResponse> onComplete,
            CancellationToken cancellationToken = default)
        {
            var internalMessages = config.Messages.Select(m => new ChatMessage
            {
                Role = m.Role,
                Content = m.Content,
                ToolCallId = m.ToolCallId,
                ToolCalls = m.ToolCalls
            }).ToList();

            var request = new ChatCompletionRequest
            {
                Model = model,
                Messages = internalMessages,
                Temperature = config.Temperature,
                Stream = true,
                Tools = tools,
                ToolChoice = toolChoice
            };

            var fullResponseBuilder = new StringBuilder();
            var accumulatedToolCalls = new List<ChatToolCall>();
            bool completed = false;

            Action safeOnComplete = () =>
            {
                if (!completed)
                {
                    completed = true;
                    // Build a ChatCompletionResponse with accumulated data
                    var response = new ChatCompletionResponse
                    {
                        Choices = new List<Choice>
                        {
                            new Choice
                            {
                                Index = 0,
                                Message = new ChatMessage
                                {
                                    Role = "assistant",
                                    Content = fullResponseBuilder.ToString(),
                                    ToolCalls = accumulatedToolCalls.Count > 0 ? accumulatedToolCalls : null
                                },
                                FinishReason = accumulatedToolCalls.Count > 0 ? "tool_calls" : "stop"
                            }
                        }
                    };
                    onComplete?.Invoke(response);
                }
            };

            await _chatProvider.ChatCompletionStreamAsync(
                request,
                // Text delta callback
                textDelta =>
                {
                    if (!string.IsNullOrEmpty(textDelta))
                    {
                        fullResponseBuilder.Append(textDelta);
                        onTextChunk?.Invoke(textDelta);
                    }
                },
                // Legacy format callback - may contain tool calls
                streamResponse =>
                {
                    if (streamResponse == null) return;

                    var delta = streamResponse.Choices?.FirstOrDefault()?.Delta;
                    if (delta != null && !string.IsNullOrEmpty(delta.Content))
                    {
                        fullResponseBuilder.Append(delta.Content);
                        onTextChunk?.Invoke(delta.Content);
                    }

                    if (streamResponse.Choices?.FirstOrDefault()?.FinishReason != null)
                    {
                        safeOnComplete();
                    }
                },
                safeOnComplete,
                cancellationToken
            );
        }
    }
}