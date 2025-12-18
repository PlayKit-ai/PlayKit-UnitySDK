using System.Collections.Generic;
using PlayKit_SDK.Provider.AI;

namespace PlayKit_SDK.Public
{
    public class PlayKit_AIResult<T> { public bool Success { get; } public T Response { get; } public string ErrorMessage { get; } public PlayKit_AIResult(T data) { Success = true; Response = data; } public PlayKit_AIResult(string errorMessage) { Success = false; Response = default; ErrorMessage = errorMessage; } }

    /// <summary>
    /// Chat message for conversations.
    /// ToolCallId and ToolCalls are optional fields used for tool calling.
    /// </summary>
    public class PlayKit_ChatMessage
    {
        public string Role;
        public string Content;
        /// <summary>
        /// Tool call ID - used when Role is "tool" to identify which tool call this is responding to
        /// </summary>
        public string ToolCallId;
        /// <summary>
        /// Tool calls made by the assistant - populated when the model requests tool execution
        /// </summary>
        public List<ChatToolCall> ToolCalls;
    }

    public abstract class PlayKit_ChatConfigBase { public List<PlayKit_ChatMessage> Messages { get; set; } = new List<PlayKit_ChatMessage>(); public float Temperature { get; set; } = 0.7f; protected PlayKit_ChatConfigBase(List<PlayKit_ChatMessage> messages) { Messages = messages; } protected PlayKit_ChatConfigBase(string userMessage) { Messages.Add(new PlayKit_ChatMessage { Role = "user", Content = userMessage }); } }
    public class PlayKit_ChatConfig : PlayKit_ChatConfigBase { public PlayKit_ChatConfig(string userMessage) : base(userMessage) { } public PlayKit_ChatConfig(List<PlayKit_ChatMessage> messages) : base(messages) { } }
    public class PlayKit_ChatStreamConfig : PlayKit_ChatConfigBase { public PlayKit_ChatStreamConfig(string userMessage) : base(userMessage) { } public PlayKit_ChatStreamConfig(List<PlayKit_ChatMessage> messages) : base(messages) { } }

    // Audio Transcription
    [System.Serializable]
    public class PlayKit_TranscriptionResult
    {
        public bool Success { get; }
        public string Text { get; }
        public string Language { get; }
        public float? DurationInSeconds { get; }
        public PlayKit_TranscriptionSegment[] Segments { get; }
        public string Error { get; }

        public PlayKit_TranscriptionResult(string text, string language = null, float? durationInSeconds = null, PlayKit_TranscriptionSegment[] segments = null)
        {
            Success = true;
            Text = text;
            Language = language;
            DurationInSeconds = durationInSeconds;
            Segments = segments;
        }

        public PlayKit_TranscriptionResult(string errorMessage)
        {
            Success = false;
            Error = errorMessage;
        }
    }

    [System.Serializable]
    public class PlayKit_TranscriptionSegment
    {
        public float Start;
        public float End;
        public string Text;
    }
}
