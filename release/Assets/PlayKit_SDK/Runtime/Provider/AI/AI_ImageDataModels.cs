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
        
        /// <summary>
        /// If true, automatically remove background from generated images
        /// </summary>
        [JsonProperty("transparent")]
        public bool? Transparent { get; set; }
        
        /// <summary>
        /// Input images for img2img generation (base64 encoded)
        /// </summary>
        [JsonProperty("images", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Images { get; set; }
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
        
        /// <summary>
        /// Original image before background removal (only present when transparent=true)
        /// </summary>
        [JsonProperty("b64_json_original")]
        public string B64JsonOriginal { get; set; }
        
        /// <summary>
        /// Whether background removal was successful (only present when transparent=true)
        /// </summary>
        [JsonProperty("transparent_success")]
        public bool? TransparentSuccess { get; set; }
    }
}