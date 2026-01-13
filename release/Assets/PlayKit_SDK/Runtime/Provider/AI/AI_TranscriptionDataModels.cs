using Newtonsoft.Json;

namespace PlayKit_SDK.Provider.AI
{
    /// <summary>
    /// Data models for audio transcription API
    /// </summary>

    [System.Serializable]
    public class TranscriptionRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("audio")]
        public string Audio { get; set; } // base64 encoded audio data

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("temperature")]
        public float? Temperature { get; set; }
    }

    [System.Serializable]
    public class TranscriptionResponse
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("segments")]
        public TranscriptionSegment[] Segments { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("durationInSeconds")]
        public float? DurationInSeconds { get; set; }
    }

    [System.Serializable]
    public class TranscriptionSegment
    {
        [JsonProperty("start")]
        public float Start { get; set; }

        [JsonProperty("end")]
        public float End { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }
}
