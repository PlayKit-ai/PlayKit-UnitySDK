using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PlayKit_SDK.Provider.AI
{
    /// <summary>
    /// Data models for the AI platform endpoint
    /// Currently uses OpenAI-compatible format but can be extended for platform-specific features
    /// </summary>

    // For now, we can use aliases to OpenAI compatible models
    // This allows us to extend in the future if AI endpoint adds platform-specific features

    #region Multimodal Content Types

    /// <summary>
    /// Content part for multimodal messages (text or image)
    /// </summary>
    [System.Serializable]
    public class ChatContentPart
    {
        [JsonProperty("type")]
        public string Type { get; set; } // "text" or "image_url"

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }

        [JsonProperty("image_url", NullValueHandling = NullValueHandling.Ignore)]
        public ChatImageUrl ImageUrl { get; set; }

        /// <summary>
        /// Create a text content part
        /// </summary>
        public static ChatContentPart CreateText(string text)
        {
            return new ChatContentPart { Type = "text", Text = text };
        }

        /// <summary>
        /// Create an image content part from base64 data
        /// </summary>
        public static ChatContentPart CreateImage(string base64Data, string detail = "auto")
        {
            return new ChatContentPart
            {
                Type = "image_url",
                ImageUrl = new ChatImageUrl
                {
                    Url = $"data:image/png;base64,{base64Data}",
                    Detail = detail
                }
            };
        }
    }

    /// <summary>
    /// Image URL for multimodal chat messages
    /// </summary>
    [System.Serializable]
    public class ChatImageUrl
    {
        [JsonProperty("url")]
        public string Url { get; set; } // data:image/png;base64,... or URL

        [JsonProperty("detail", NullValueHandling = NullValueHandling.Ignore)]
        public string Detail { get; set; } // "auto", "low", "high"
    }

    #endregion

    [System.Serializable]
    public class ChatMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        /// <summary>
        /// Message content - can be string for text-only or List of ChatContentPart for multimodal
        /// </summary>
        [JsonProperty("content")]
        public object Content { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("tool_calls", NullValueHandling = NullValueHandling.Ignore)]
        public List<ChatToolCall> ToolCalls { get; set; }

        [JsonProperty("tool_call_id", NullValueHandling = NullValueHandling.Ignore)]
        public string ToolCallId { get; set; }

        /// <summary>
        /// Get content as string (for text-only messages)
        /// </summary>
        public string GetTextContent()
        {
            if (Content is string textContent)
                return textContent;
            return null;
        }

        /// <summary>
        /// Set content as text string
        /// </summary>
        public void SetTextContent(string text)
        {
            Content = text;
        }

        /// <summary>
        /// Set content as multimodal (text + images)
        /// </summary>
        public void SetMultimodalContent(string text, List<string> imageBase64List, string detail = "auto")
        {
            var parts = new List<ChatContentPart>();
            
            // Add text part first
            if (!string.IsNullOrEmpty(text))
            {
                parts.Add(ChatContentPart.CreateText(text));
            }
            
            // Add image parts
            if (imageBase64List != null)
            {
                foreach (var base64 in imageBase64List)
                {
                    parts.Add(ChatContentPart.CreateImage(base64, detail));
                }
            }
            
            Content = parts;
        }
    }

    #region Tool Calling Types

    /// <summary>
    /// Tool definition for function calling
    /// </summary>
    [System.Serializable]
    public class ChatTool
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "function";

        [JsonProperty("function")]
        public ChatToolFunction Function { get; set; }
    }

    /// <summary>
    /// Function definition within a tool
    /// </summary>
    [System.Serializable]
    public class ChatToolFunction
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("parameters")]
        public JObject Parameters { get; set; }
    }

    /// <summary>
    /// Tool call returned by the model
    /// </summary>
    [System.Serializable]
    public class ChatToolCall
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("function")]
        public ChatToolCallFunction Function { get; set; }
    }

    /// <summary>
    /// Function call details within a tool call
    /// </summary>
    [System.Serializable]
    public class ChatToolCallFunction
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("arguments")]
        public string Arguments { get; set; }
    }

    /// <summary>
    /// Tool choice options for controlling tool usage
    /// </summary>
    [System.Serializable]
    public class ChatToolChoice
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "function";

        [JsonProperty("function")]
        public ChatToolChoiceFunction Function { get; set; }
    }

    [System.Serializable]
    public class ChatToolChoiceFunction
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    #endregion

    [System.Serializable]
    public class ChatCompletionRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("messages")]
        public List<ChatMessage> Messages { get; set; }

        [JsonProperty("temperature")]
        public float? Temperature { get; set; }

        [JsonProperty("stream")]
        public bool Stream { get; set; }

        [JsonProperty("max_tokens")]
        public int? MaxTokens { get; set; }

        [JsonProperty("top_p")]
        public float? TopP { get; set; }

        [JsonProperty("frequency_penalty")]
        public float? FrequencyPenalty { get; set; }

        [JsonProperty("presence_penalty")]
        public float? PresencePenalty { get; set; }

        [JsonProperty("stop")]
        public string[] Stop { get; set; }

        [JsonProperty("seed")]
        public int? Seed { get; set; }

        // Tool calling support
        [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)]
        public List<ChatTool> Tools { get; set; }

        /// <summary>
        /// Tool choice: "auto", "required", "none", or a specific tool choice object
        /// </summary>
        [JsonProperty("tool_choice", NullValueHandling = NullValueHandling.Ignore)]
        public object ToolChoice { get; set; }
    }

    [System.Serializable]
    public class ChatCompletionResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("object")]
        public string Object { get; set; }
        
        [JsonProperty("created")]
        public long Created { get; set; }
        
        [JsonProperty("model")]
        public string Model { get; set; }
        
        [JsonProperty("choices")]
        public List<Choice> Choices { get; set; }
        
        [JsonProperty("usage")]
        public Usage Usage { get; set; }
    }

    [System.Serializable]
    public class Choice
    {
        [JsonProperty("index")]
        public int Index { get; set; }
        
        [JsonProperty("message")]
        public ChatMessage Message { get; set; }
        
        [JsonProperty("finish_reason")]
        public string FinishReason { get; set; }
    }

    [System.Serializable]
    public class Usage
    {
        [JsonProperty("prompt_tokens")]
        public int PromptTokens { get; set; }
        
        [JsonProperty("completion_tokens")]
        public int CompletionTokens { get; set; }
        
        [JsonProperty("total_tokens")]
        public int TotalTokens { get; set; }
    }

    [System.Serializable]
    public class StreamCompletionResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("object")]
        public string Object { get; set; }
        
        [JsonProperty("created")]
        public long Created { get; set; }
        
        [JsonProperty("model")]
        public string Model { get; set; }
        
        [JsonProperty("choices")]
        public List<StreamChoice> Choices { get; set; }
    }

    [System.Serializable]
    public class StreamChoice
    {
        [JsonProperty("index")]
        public int Index { get; set; }
        
        [JsonProperty("delta")]
        public Delta Delta { get; set; }
        
        [JsonProperty("finish_reason")]
        public string FinishReason { get; set; }
    }

    [System.Serializable]
    public class Delta
    {
        [JsonProperty("role")]
        public string Role { get; set; }
        
        [JsonProperty("content")]
        public string Content { get; set; }
    }

    // New UI Message Stream format models
    [System.Serializable]
    public class UIMessageStreamResponse
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("delta")]
        public string Delta { get; set; }
    }
}