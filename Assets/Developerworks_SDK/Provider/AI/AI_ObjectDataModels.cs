using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using PlayKit_SDK.Public;

namespace PlayKit_SDK.Provider.AI
{
    /// <summary>
    /// Data models for AI structured object generation endpoint
    /// Compatible with OpenAI structured output format
    /// </summary>
    
    [System.Serializable]
    public class ObjectGenerationRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }
        
        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("messages")]
        public List<DW_ChatMessage> Messages { get; set; }
        
        [JsonProperty("schema")]
        public object Schema { get; set; }
        
        [JsonProperty("output")]
        public string Output { get; set; } = "object"; // Always object
        
        [JsonProperty("schemaName")]
        public string SchemaName { get; set; }
        
        [JsonProperty("schemaDescription")]
        public string SchemaDescription { get; set; }
        
        [JsonProperty("temperature")]
        public float? Temperature { get; set; }
        
        [JsonProperty("maxTokens")]
        public int? MaxTokens { get; set; }
        
        [JsonProperty("system")]
        public string System { get; set; }
    }


    [System.Serializable]
    public class ObjectGenerationResponse<T>
    {
        [JsonProperty("object")]
        public T Object { get; set; }
        
        [JsonProperty("finishReason")]
        public string FinishReason { get; set; }
        
        [JsonProperty("usage")]
        public ObjectUsage Usage { get; set; }
        
        [JsonProperty("model")]
        public string Model { get; set; }
        
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }
    }

    [System.Serializable]
    public class ObjectUsage
    {
        [JsonProperty("inputTokens")]
        public int InputTokens { get; set; }
        
        [JsonProperty("outputTokens")]
        public int OutputTokens { get; set; }
        
        [JsonProperty("totalTokens")]
        public int TotalTokens { get; set; }
        
        [JsonProperty("cost")]
        public float Cost { get; set; }
    }

    /// <summary>
    /// Base class for all structured schemas
    /// </summary>
    public abstract class StructuredSchema
    {
        /// <summary>
        /// Name of the schema for identification
        /// </summary>
        public abstract string SchemaName { get; }
        
        /// <summary>
        /// Description of what this schema represents
        /// </summary>
        public abstract string SchemaDescription { get; }
        
        /// <summary>
        /// JSON Schema definition
        /// </summary>
        public abstract object GetJsonSchema();
        
        /// <summary>
        /// Output type for this schema (object, array, enum)
        /// </summary>
        public virtual string OutputType => "object";
    }
}