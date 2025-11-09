using System.Collections.Generic;
using Newtonsoft.Json;

namespace PlayKit_SDK.Provider.AI
{
    /// <summary>
    /// Data models for AI image generation endpoint
    /// Compatible with OpenAI image generation API format
    /// </summary>
    
    [System.Serializable]
    public class ImageGenerationRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }
        
        [JsonProperty("prompt")]
        public string Prompt { get; set; }
        
        [JsonProperty("n")]
        public int? N { get; set; } = 1;
        
        [JsonProperty("size")]
        public string Size { get; set; }
        
        [JsonProperty("seed")]
        public int? Seed { get; set; }
        
        [JsonProperty("provider_options")]
        public Dictionary<string, object> ProviderOptions { get; set; }
    }

    [System.Serializable]
    public class ImageGenerationResponse
    {
        [JsonProperty("created")]
        public long Created { get; set; }
        
        [JsonProperty("data")]
        public List<ImageData> Data { get; set; }
    }

    [System.Serializable]
    public class ImageData
    {
        [JsonProperty("b64_json")]
        public string B64Json { get; set; }
        
        [JsonProperty("revised_prompt")]
        public string RevisedPrompt { get; set; }
    }
}