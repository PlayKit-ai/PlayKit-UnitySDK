using Newtonsoft.Json;

namespace PlayKit_SDK.Provider.AI
{
    /// <summary>
    /// Data models for text-to-speech (TTS) API
    /// </summary>

    [System.Serializable]
    public class SpeechRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("voice")]
        public string Voice { get; set; }

        [JsonProperty("speed")]
        public float? Speed { get; set; }

        [JsonProperty("vol")]
        public float? Vol { get; set; }

        [JsonProperty("pitch")]
        public float? Pitch { get; set; }

        [JsonProperty("emotion")]
        public string Emotion { get; set; }

        [JsonProperty("language_boost")]
        public string LanguageBoost { get; set; }

        [JsonProperty("voice_setting")]
        public object VoiceSetting { get; set; }

        [JsonProperty("audio_setting")]
        public object AudioSetting { get; set; }
    }

    /// <summary>
    /// Response from the TTS endpoint. The body is raw audio bytes (not JSON), so this
    /// is built by the provider from the response body plus response headers.
    /// </summary>
    public class SpeechAudioResponse
    {
        public byte[] Audio { get; set; }
        public string Format { get; set; }
        public int UsageCharacters { get; set; }
        public float? AudioLengthMs { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
    }
}
