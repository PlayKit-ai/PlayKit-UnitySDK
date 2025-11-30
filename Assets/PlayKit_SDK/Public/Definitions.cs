using System.Collections.Generic;
using PlayKit_SDK.Provider.AI;

namespace PlayKit_SDK.Public
{
    public class DW_AIResult<T> { public bool Success { get; } public T Response { get; } public string ErrorMessage { get; } public DW_AIResult(T data) { Success = true; Response = data; } public DW_AIResult(string errorMessage) { Success = false; Response = default; ErrorMessage = errorMessage; } }

    /// <summary>
    /// Chat message for conversations.
    /// ToolCallId and ToolCalls are optional fields used for tool calling.
    /// </summary>
    public class DW_ChatMessage
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

    public abstract class DW_ChatConfigBase { public List<DW_ChatMessage> Messages { get; set; } = new List<DW_ChatMessage>(); public float Temperature { get; set; } = 0.7f; protected DW_ChatConfigBase(List<DW_ChatMessage> messages) { Messages = messages; } protected DW_ChatConfigBase(string userMessage) { Messages.Add(new DW_ChatMessage { Role = "user", Content = userMessage }); } }
    public class DW_ChatConfig : DW_ChatConfigBase { public DW_ChatConfig(string userMessage) : base(userMessage) { } public DW_ChatConfig(List<DW_ChatMessage> messages) : base(messages) { } }
    public class DW_ChatStreamConfig : DW_ChatConfigBase { public DW_ChatStreamConfig(string userMessage) : base(userMessage) { } public DW_ChatStreamConfig(List<DW_ChatMessage> messages) : base(messages) { } }

    // Audio Transcription
    [System.Serializable]
    public class DW_TranscriptionResult
    {
        public bool Success { get; }
        public string Text { get; }
        public string Language { get; }
        public float? DurationInSeconds { get; }
        public DW_TranscriptionSegment[] Segments { get; }
        public string Error { get; }

        public DW_TranscriptionResult(string text, string language = null, float? durationInSeconds = null, DW_TranscriptionSegment[] segments = null)
        {
            Success = true;
            Text = text;
            Language = language;
            DurationInSeconds = durationInSeconds;
            Segments = segments;
        }

        public DW_TranscriptionResult(string errorMessage)
        {
            Success = false;
            Error = errorMessage;
        }
    }

    [System.Serializable]
    public class DW_TranscriptionSegment
    {
        public float Start;
        public float End;
        public string Text;
    }
}