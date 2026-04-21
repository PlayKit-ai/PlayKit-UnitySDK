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
