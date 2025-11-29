using System.Collections.Generic;
using Newtonsoft.Json;

namespace PlayKit_SDK.Provider.AI
{
    /// <summary>
    /// Data models for the AI platform endpoint
    /// Currently uses OpenAI-compatible format but can be extended for platform-specific features
    /// </summary>
    
    // For now, we can use aliases to OpenAI compatible models
    // This allows us to extend in the future if AI endpoint adds platform-specific features
    
    [System.Serializable]
    public class ChatMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; }
        
        [JsonProperty("content")]
        public string Content { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
    }

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
        
        // Future: Platform-specific parameters can be added here
        // [JsonProperty("platform_model_config")]
        // public PlatformModelConfig PlatformConfig { get; set; }
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