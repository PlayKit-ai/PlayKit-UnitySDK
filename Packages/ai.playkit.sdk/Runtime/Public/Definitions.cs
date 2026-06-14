using System;
using System.Collections.Generic;
using PlayKit_SDK.Provider.AI;
using UnityEngine;

namespace PlayKit_SDK.Public
{
    public class PlayKit_AIResult<T> { public bool Success { get; } public T Response { get; } public string ErrorMessage { get; }
        /// <summary>Model reasoning / thinking trace, populated only when the model thought and the result type carries text. Null otherwise.</summary>
        public string ReasoningContent { get; }
        public PlayKit_AIResult(T data, string reasoningContent = null) { Success = true; Response = data; ReasoningContent = reasoningContent; } public PlayKit_AIResult(string errorMessage) { Success = false; Response = default; ErrorMessage = errorMessage; } }

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

    #region Multimodal Audio Content

    /// <summary>
    /// Audio content for multimodal chat messages.
    /// Provide either Base64Data or AudioClip (AudioClip will be converted to WAV base64 automatically).
    /// This allows sending audio directly to models that support audio input,
    /// bypassing the transcription step for lower latency.
    /// </summary>
    [System.Serializable]
    public class PlayKit_AudioContent
    {
        public string Base64Data;
        public AudioClip AudioClip;
        public string Format = "wav";

        public static PlayKit_AudioContent FromBase64(string base64Data, string format = "wav")
        {
            return new PlayKit_AudioContent { Base64Data = base64Data, Format = format };
        }

        public static PlayKit_AudioContent FromAudioClip(AudioClip clip, string format = "wav")
        {
            return new PlayKit_AudioContent { AudioClip = clip, Format = format };
        }

        public string GetBase64Data()
        {
            if (!string.IsNullOrEmpty(Base64Data))
                return Base64Data;

            if (AudioClip != null)
                return PlayKit_AudioUtils.AudioClipToBase64Wav(AudioClip);

            return null;
        }
    }

    public static class PlayKit_AudioUtils
    {
        public static string AudioClipToBase64Wav(AudioClip clip)
        {
            if (clip == null) return null;

            try
            {
                var samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);

                int sampleCount = samples.Length;
                int sampleRate = clip.frequency;
                int channels = clip.channels;
                int bitsPerSample = 16;
                int byteRate = sampleRate * channels * bitsPerSample / 8;
                int blockAlign = channels * bitsPerSample / 8;
                int dataSize = sampleCount * blockAlign;
                int fileSize = 44 + dataSize;

                var wav = new byte[fileSize];
                int pos = 0;

                // RIFF header
                wav[pos++] = (byte)'R'; wav[pos++] = (byte)'I'; wav[pos++] = (byte)'F'; wav[pos++] = (byte)'F';
                WriteInt32(wav, ref pos, fileSize - 8);
                wav[pos++] = (byte)'W'; wav[pos++] = (byte)'A'; wav[pos++] = (byte)'V'; wav[pos++] = (byte)'E';

                // fmt chunk
                wav[pos++] = (byte)'f'; wav[pos++] = (byte)'m'; wav[pos++] = (byte)'t'; wav[pos++] = (byte)' ';
                WriteInt32(wav, ref pos, 16);
                WriteInt16(wav, ref pos, 1); // PCM
                WriteInt16(wav, ref pos, (short)channels);
                WriteInt32(wav, ref pos, sampleRate);
                WriteInt32(wav, ref pos, byteRate);
                WriteInt16(wav, ref pos, (short)blockAlign);
                WriteInt16(wav, ref pos, (short)bitsPerSample);

                // data chunk
                wav[pos++] = (byte)'d'; wav[pos++] = (byte)'a'; wav[pos++] = (byte)'t'; wav[pos++] = (byte)'a';
                WriteInt32(wav, ref pos, dataSize);

                for (int i = 0; i < sampleCount; i++)
                {
                    short sample = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767);
                    wav[pos++] = (byte)(sample & 0xFF);
                    wav[pos++] = (byte)((sample >> 8) & 0xFF);
                }

                return Convert.ToBase64String(wav);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_AudioUtils] Failed to convert AudioClip to WAV base64: {ex.Message}");
                return null;
            }
        }

        private static void WriteInt32(byte[] buf, ref int pos, int value)
        {
            buf[pos++] = (byte)(value & 0xFF);
            buf[pos++] = (byte)((value >> 8) & 0xFF);
            buf[pos++] = (byte)((value >> 16) & 0xFF);
            buf[pos++] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteInt16(byte[] buf, ref int pos, short value)
        {
            buf[pos++] = (byte)(value & 0xFF);
            buf[pos++] = (byte)((value >> 8) & 0xFF);
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
        /// Optional audio clips for multimodal messages (direct audio input).
        /// When provided, audio is sent directly to the model without transcription,
        /// reducing latency compared to the STT-then-chat pipeline.
        /// </summary>
        public List<PlayKit_AudioContent> Audios;
        /// <summary>
        /// Tool call ID - used when Role is "tool" to identify which tool call this is responding to
        /// </summary>
        public string ToolCallId;
        /// <summary>
        /// Tool name for canonical tool-result content parts.
        /// Optional for legacy histories; the server can infer it when the matching
        /// assistant tool call is present.
        /// </summary>
        public string ToolName;
        /// <summary>
        /// Tool calls made by the assistant - populated when the model requests tool execution
        /// </summary>
        public List<ChatToolCall> ToolCalls;

        /// <summary>
        /// Check if this message has image content
        /// </summary>
        public bool HasImages => Images != null && Images.Count > 0;

        /// <summary>
        /// Check if this message has audio content
        /// </summary>
        public bool HasAudios => Audios != null && Audios.Count > 0;

        /// <summary>
        /// Check if this message has any multimodal content (images or audio)
        /// </summary>
        public bool IsMultimodal => HasImages || HasAudios;

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

        /// <summary>
        /// Add audio directly to this message (bypasses STT for lower latency)
        /// </summary>
        public void AddAudio(AudioClip clip, string format = "wav")
        {
            if (Audios == null) Audios = new List<PlayKit_AudioContent>();
            Audios.Add(PlayKit_AudioContent.FromAudioClip(clip, format));
        }

        /// <summary>
        /// Add audio from base64 data
        /// </summary>
        public void AddAudioBase64(string base64Data, string format = "wav")
        {
            if (Audios == null) Audios = new List<PlayKit_AudioContent>();
            Audios.Add(PlayKit_AudioContent.FromBase64(base64Data, format));
        }
    }

    /// <summary>
    /// Reasoning effort for thinking-capable chat models. When set, the model is
    /// asked to "think" before answering; higher effort spends more reasoning budget.
    /// Use <see cref="Off"/> to explicitly disable reasoning. Leave the config field
    /// null (and no SDK default) to omit thinking entirely (the server then defaults off).
    /// </summary>
    public enum PlayKit_ThinkingEffort { Off, Minimal, Low, Medium, High, Max }

    public abstract class PlayKit_ChatConfigBase
    {
        public List<PlayKit_ChatMessage> Messages { get; set; } = new List<PlayKit_ChatMessage>();
        public float Temperature { get; set; } = 0.7f;

        /// <summary>
        /// Optional reasoning effort. When set, enables thinking on supported models.
        /// Leave null to omit (model default).
        /// </summary>
        public PlayKit_ThinkingEffort? ThinkingEffort { get; set; }

        protected PlayKit_ChatConfigBase(List<PlayKit_ChatMessage> messages) { Messages = messages; }
        protected PlayKit_ChatConfigBase(string userMessage) { Messages.Add(new PlayKit_ChatMessage { Role = "user", Content = userMessage }); }

        /// <summary>
        /// Build the request-level Thinking object by resolving the effort the same way
        /// model selection resolves: per-request <see cref="ThinkingEffort"/> first, then the
        /// SDK-wide <see cref="PlayKitSettings.DefaultThinkingEffort"/>. Returns null when
        /// neither is set, so the field is omitted from the payload (the server then defaults off).
        /// The wire value is the lowercase effort string, e.g. "off" | "minimal" | ... | "max".
        /// </summary>
        internal PlayKit_SDK.Provider.AI.Thinking BuildThinking()
        {
            PlayKit_ThinkingEffort? effort = ThinkingEffort ?? PlayKitSettings.Instance?.DefaultThinkingEffort;
            if (effort == null) return null;
            return new PlayKit_SDK.Provider.AI.Thinking
            {
                Effort = effort.Value.ToString().ToLowerInvariant()
            };
        }
    }
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

    // Text-to-Speech
    /// <summary>One voice in a <see cref="PlayKit_SpeechOptions.VoiceMix"/> blend.</summary>
    [System.Serializable]
    public class PlayKit_VoiceMixEntry
    {
        /// <summary>Voice id to blend (e.g., "male-qn-qingse").</summary>
        public string Voice;
        /// <summary>Relative weight, integer 1-100.</summary>
        public int Weight;
    }

    /// <summary>
    /// Optional synthesis settings for text-to-speech.
    /// Set either <see cref="Voice"/> or <see cref="VoiceMix"/> (1-4 entries), not both.
    /// </summary>
    [System.Serializable]
    public class PlayKit_SpeechOptions
    {
        /// <summary>Voice id (e.g., "male-qn-qingse"). Null uses the system default voice. Mutually exclusive with <see cref="VoiceMix"/>.</summary>
        public string Voice;
        /// <summary>Blend of 1-4 voices. Mutually exclusive with <see cref="Voice"/>.</summary>
        public List<PlayKit_VoiceMixEntry> VoiceMix;
        /// <summary>Playback speed multiplier.</summary>
        public float? Speed;
        /// <summary>Volume multiplier.</summary>
        public float? Volume;
        /// <summary>Pitch shift.</summary>
        public int? Pitch;
        /// <summary>Emotion hint (e.g., "happy").</summary>
        public string Emotion;
        /// <summary>Language hint (e.g., "Chinese").</summary>
        public string Language;
        /// <summary>PCM sample rate to request. Defaults to 24000.</summary>
        public int SampleRate = 24000;
    }

    /// <summary>A voice available for text-to-speech synthesis.</summary>
    [System.Serializable]
    public class PlayKit_VoiceInfo
    {
        /// <summary>Voice id to pass as the synthesis voice.</summary>
        public string VoiceId;
        /// <summary>Human-readable voice name, when reported.</summary>
        public string Name;
        /// <summary>Voice description, when reported.</summary>
        public string Description;
        /// <summary>Primary language of the voice, when reported.</summary>
        public string Language;
        /// <summary>Voice kind (e.g., system vs. cloned).</summary>
        public string Kind;
    }

    [System.Serializable]
    /// <summary>One timed unit (word or sentence) in a <see cref="PlayKit_SpeechAlignment"/>.</summary>
    public class PlayKit_SpeechAlignmentItem
    {
        public string Text { get; set; }
        public float StartMs { get; set; }
        public float EndMs { get; set; }
        public int? TextStart { get; set; }
        public int? TextEnd { get; set; }
    }

    /// <summary>Word/sentence timestamp alignment for synthesized speech.</summary>
    public class PlayKit_SpeechAlignment
    {
        public string Granularity { get; set; }
        public System.Collections.Generic.List<PlayKit_SpeechAlignmentItem> Items { get; set; }
    }

    public class PlayKit_SpeechResult
    {
        public bool Success { get; }
        /// <summary>Raw audio bytes returned by the API (16-bit signed little-endian PCM).</summary>
        public byte[] AudioData { get; }
        /// <summary>Reported audio format / content type.</summary>
        public string Format { get; }
        /// <summary>Number of characters billed for this request.</summary>
        public int UsageCharacters { get; }
        /// <summary>Length of the generated audio in milliseconds, when reported.</summary>
        public float? AudioLengthMs { get; }
        /// <summary>PCM sample rate (the rate requested; the endpoint does not echo it back).</summary>
        public int SampleRate { get; }
        /// <summary>PCM channel count.</summary>
        public int Channels { get; }
        /// <summary>Timestamp alignment, present only from SynthesizeWithTimestampsAsync; null otherwise.</summary>
        public PlayKit_SpeechAlignment Alignment { get; }
        public string Error { get; }

        public PlayKit_SpeechResult(byte[] audioData, string format, int usageCharacters, float? audioLengthMs, int sampleRate, int channels, PlayKit_SpeechAlignment alignment = null)
        {
            Success = true;
            AudioData = audioData;
            Format = format;
            UsageCharacters = usageCharacters;
            AudioLengthMs = audioLengthMs;
            SampleRate = sampleRate;
            Channels = channels;
            Alignment = alignment;
        }

        public PlayKit_SpeechResult(string errorMessage)
        {
            Success = false;
            Error = errorMessage;
        }

        /// <summary>
        /// Decode the PCM audio into a playable AudioClip. Assumes 16-bit signed
        /// little-endian samples at <see cref="SampleRate"/> with <see cref="Channels"/>
        /// channels (the format the SDK requests). Returns null when there is no audio.
        /// </summary>
        public AudioClip ToAudioClip()
        {
            if (!Success || AudioData == null || AudioData.Length < 2)
            {
                return null;
            }

            int channels = Channels > 0 ? Channels : 1;
            int sampleRate = SampleRate > 0 ? SampleRate : 24000;

            int totalSamples = AudioData.Length / 2; // 16-bit samples, interleaved across channels
            var floats = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                short sample = (short)(AudioData[i * 2] | (AudioData[i * 2 + 1] << 8));
                floats[i] = sample / 32768f;
            }

            int lengthSamples = totalSamples / channels; // frames per channel
            if (lengthSamples <= 0)
            {
                return null;
            }

            var clip = AudioClip.Create("PlayKitTTS", lengthSamples, channels, sampleRate, false);
            clip.SetData(floats, 0);
            return clip;
        }
    }
}
