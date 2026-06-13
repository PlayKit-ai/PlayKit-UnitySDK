using System.Collections.Generic;
using Newtonsoft.Json;

namespace PlayKit_SDK.Provider.AI
{
    /// <summary>
    /// Data models for text-to-speech (TTS) API. The contract is the neutral,
    /// ElevenLabs-aligned shape (TTS is beta — no legacy aliases).
    /// </summary>

    /// <summary>One voice in a <see cref="SpeechRequest.VoiceMix"/> blend.</summary>
    [System.Serializable]
    public class VoiceMixEntry
    {
        [JsonProperty("voice")]
        public string Voice { get; set; }

        /// <summary>Relative weight, integer 1-100.</summary>
        [JsonProperty("weight")]
        public int Weight { get; set; }
    }

    /// <summary>Neutral voice tuning knobs.</summary>
    [System.Serializable]
    public class VoiceSettings
    {
        [JsonProperty("speed")]
        public float? Speed { get; set; }

        [JsonProperty("volume")]
        public float? Volume { get; set; }

        [JsonProperty("pitch")]
        public int? Pitch { get; set; }

        [JsonProperty("emotion")]
        public string Emotion { get; set; }
    }

    [System.Serializable]
    public class SpeechRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("voice")]
        public string Voice { get; set; }

        /// <summary>Blend voices (1-4). Mutually exclusive with <see cref="Voice"/>.</summary>
        [JsonProperty("voice_mix")]
        public List<VoiceMixEntry> VoiceMix { get; set; }

        [JsonProperty("voice_settings")]
        public VoiceSettings VoiceSettings { get; set; }

        /// <summary>e.g. "mp3_44100_128", "pcm_24000".</summary>
        [JsonProperty("output_format")]
        public string OutputFormat { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        /// <summary>Escape hatch for advanced provider-specific fields.</summary>
        [JsonProperty("provider_options")]
        public object ProviderOptions { get; set; }

        /// <summary>Subtitle granularity for the with-timestamps endpoint ("word"|"sentence").</summary>
        [JsonProperty("subtitle_type")]
        public string SubtitleType { get; set; }
    }

    /// <summary>
    /// Raw-audio response. The /v2/audio/speech body is raw bytes (not JSON), so this
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
        /// <summary>Word/sentence timings (with-timestamps endpoint only; null otherwise).</summary>
        public SpeechAlignment Alignment { get; set; }
    }

    // ----- with-timestamps JSON envelope --------------------------------------

    /// <summary>One timed unit (word/sentence) in an alignment.</summary>
    [System.Serializable]
    public class SpeechAlignmentItem
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("start_ms")]
        public float StartMs { get; set; }

        [JsonProperty("end_ms")]
        public float EndMs { get; set; }

        [JsonProperty("text_start")]
        public int? TextStart { get; set; }

        [JsonProperty("text_end")]
        public int? TextEnd { get; set; }
    }

    [System.Serializable]
    public class SpeechAlignment
    {
        [JsonProperty("granularity")]
        public string Granularity { get; set; }

        [JsonProperty("items")]
        public List<SpeechAlignmentItem> Items { get; set; }
    }

    // ----- voice listing -------------------------------------------------------

    /// <summary>One available voice from /v2/audio/voices.</summary>
    [System.Serializable]
    public class SpeechVoiceInfo
    {
        [JsonProperty("voice_id")]
        public string VoiceId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }
    }

    /// <summary>JSON envelope returned by GET /v2/audio/voices.</summary>
    [System.Serializable]
    public class SpeechVoicesResponse
    {
        [JsonProperty("voices")]
        public List<SpeechVoiceInfo> Voices { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }
    }

    /// <summary>
    /// JSON envelope returned by /v2/audio/speech-with-timestamps.
    /// </summary>
    [System.Serializable]
    public class SpeechTimestampsEnvelope
    {
        [JsonProperty("audio_base64")]
        public string AudioBase64 { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("usage_characters")]
        public int UsageCharacters { get; set; }

        [JsonProperty("audio_length_ms")]
        public float? AudioLengthMs { get; set; }

        [JsonProperty("alignment")]
        public SpeechAlignment Alignment { get; set; }
    }
}
