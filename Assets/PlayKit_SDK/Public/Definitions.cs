using System;
using System.Collections.Generic;
using PlayKit_SDK.Provider.AI;
using UnityEngine;

namespace PlayKit_SDK.Public
{
    public class PlayKit_AIResult<T> { public bool Success { get; } public T Response { get; } public string ErrorMessage { get; } public PlayKit_AIResult(T data) { Success = true; Response = data; } public PlayKit_AIResult(string errorMessage) { Success = false; Response = default; ErrorMessage = errorMessage; } }

    #region Multimodal Image Content

    /// <summary>
    /// Image content for multimodal chat messages.
    /// Provide either Base64Data or Texture (Texture will be converted to base64 automatically).
    /// </summary>
    [System.Serializable]
    public class PlayKit_ImageContent
    {
        /// <summary>
        /// Raw base64 encoded image data (without data URL prefix)
        /// </summary>
        public string Base64Data;
        
        /// <summary>
        /// Unity Texture2D to use as image (will be converted to base64 PNG)
        /// </summary>
        public Texture2D Texture;
        
        /// <summary>
        /// Image detail level: "auto", "low", or "high"
        /// "auto" lets the model decide based on image size
        /// "low" is faster and uses fewer tokens
        /// "high" provides more detail for the model
        /// </summary>
        public string Detail = "auto";

        /// <summary>
        /// Create from base64 string
        /// </summary>
        public static PlayKit_ImageContent FromBase64(string base64Data, string detail = "auto")
        {
            return new PlayKit_ImageContent { Base64Data = base64Data, Detail = detail };
        }

        /// <summary>
        /// Create from Texture2D
        /// </summary>
        public static PlayKit_ImageContent FromTexture(Texture2D texture, string detail = "auto")
        {
            return new PlayKit_ImageContent { Texture = texture, Detail = detail };
        }

        /// <summary>
        /// Get base64 data (converting from Texture if needed)
        /// </summary>
        public string GetBase64Data()
        {
            if (!string.IsNullOrEmpty(Base64Data))
                return Base64Data;
            
            if (Texture != null)
                return PlayKit_ImageUtils.Texture2DToBase64(Texture);
            
            return null;
        }
    }

    /// <summary>
    /// Utility methods for image conversion
    /// </summary>
    public static class PlayKit_ImageUtils
    {
        /// <summary>
        /// Convert Texture2D to base64 PNG string
        /// </summary>
        public static string Texture2DToBase64(Texture2D texture)
        {
            if (texture == null) return null;
            
            try
            {
                byte[] pngData = texture.EncodeToPNG();
                return Convert.ToBase64String(pngData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_ImageUtils] Failed to convert texture to base64: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Convert Texture2D to data URL (data:image/png;base64,...)
        /// </summary>
        public static string Texture2DToDataUrl(Texture2D texture)
        {
            var base64 = Texture2DToBase64(texture);
            if (base64 == null) return null;
            return $"data:image/png;base64,{base64}";
        }
    }

    #endregion

    /// <summary>
    /// Chat message for conversations.
    /// Supports multimodal content with optional Images list.
    /// ToolCallId and ToolCalls are optional fields used for tool calling.
    /// </summary>
    public class PlayKit_ChatMessage
    {
        public string Role;
        /// <summary>
        /// Text content of the message
        /// </summary>
        public string Content;
        /// <summary>
        /// Optional images for multimodal messages (Vision API support)
        /// </summary>
        public List<PlayKit_ImageContent> Images;
        /// <summary>
        /// Tool call ID - used when Role is "tool" to identify which tool call this is responding to
        /// </summary>
        public string ToolCallId;
        /// <summary>
        /// Tool calls made by the assistant - populated when the model requests tool execution
        /// </summary>
        public List<ChatToolCall> ToolCalls;

        /// <summary>
        /// Check if this message has image content
        /// </summary>
        public bool HasImages => Images != null && Images.Count > 0;

        /// <summary>
        /// Add an image to this message
        /// </summary>
        public void AddImage(Texture2D texture, string detail = "auto")
        {
            if (Images == null) Images = new List<PlayKit_ImageContent>();
            Images.Add(PlayKit_ImageContent.FromTexture(texture, detail));
        }

        /// <summary>
        /// Add an image from base64 data
        /// </summary>
        public void AddImageBase64(string base64Data, string detail = "auto")
        {
            if (Images == null) Images = new List<PlayKit_ImageContent>();
            Images.Add(PlayKit_ImageContent.FromBase64(base64Data, detail));
        }
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
