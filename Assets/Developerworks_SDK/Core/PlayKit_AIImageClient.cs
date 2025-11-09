using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using PlayKit_SDK.Provider;
using PlayKit_SDK.Provider.AI;

namespace PlayKit_SDK
{
    /// <summary>
    /// Client for AI image generation using platform-hosted models
    /// Provides simple interface for generating images from text prompts
    /// </summary>
    public class PlayKit_AIImageClient
    {
        private readonly string _modelName;
        private readonly IImageProvider _imageProvider;

        internal PlayKit_AIImageClient(string modelName, IImageProvider imageProvider)
        {
            _modelName = modelName;
            _imageProvider = imageProvider;
        }

        /// <summary>
        /// Generate a single image from a text prompt
        /// </summary>
        /// <param name="prompt">Text description of the desired image</param>
        /// <param name="size">Image size (e.g., "1024x1024", "1792x1024", "1024x1792")</param>
        /// <param name="seed">Optional seed for reproducible results</param>
        /// <returns>Generated image with metadata, or null if generation failed</returns>
        public async UniTask<DW_GeneratedImage> GenerateImageAsync(
            string prompt,
            string size = "1024x1024",
            int? seed = null,
            CancellationToken cancellationToken = default)
        {
            var results = await GenerateImagesAsync(prompt, 1, size, null, seed, cancellationToken);
            return results?.Count > 0 ? results[0] : null;
        }

        /// <summary>
        /// Generate a single image from a text prompt and return only the base64 string
        /// </summary>
        /// <param name="prompt">Text description of the desired image</param>
        /// <param name="size">Image size (e.g., "1024x1024", "1792x1024", "1024x1792")</param>
        /// <param name="seed">Optional seed for reproducible results</param>
        /// <returns>Generated image as base64 string, or null if generation failed</returns>
        [System.Obsolete("Use GenerateImageAsync() which returns DW_GeneratedImage with metadata. This method is kept for backward compatibility.")]
        public async UniTask<string> GenerateImageBase64Async(
            string prompt,
            string size = "1024x1024",
            int? seed = null,
            CancellationToken cancellationToken = default)
        {
            var result = await GenerateImageAsync(prompt, size, seed, cancellationToken);
            return result?.ImageBase64;
        }

        /// <summary>
        /// Generate multiple images from a text prompt
        /// </summary>
        /// <param name="prompt">Text description of the desired images</param>
        /// <param name="count">Number of images to generate (1-10)</param>
        /// <param name="size">Image size (e.g., "1024x1024", "1792x1024", "1024x1792")</param>
        /// <param name="aspectRatio">Aspect ratio (e.g., "16:9", "1:1", "9:16") - alternative to size</param>
        /// <param name="seed">Optional seed for reproducible results</param>
        /// <returns>List of generated images with metadata</returns>
        public async UniTask<List<DW_GeneratedImage>> GenerateImagesAsync(
            string prompt,
            int count = 1,
            string size = "1024x1024",
            string aspectRatio = null,
            int? seed = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(prompt))
            {
                Debug.LogError("[DW_AIImageClient] Prompt cannot be empty");
                return null;
            }

            if (count < 1 || count > 10)
            {
                Debug.LogError("[DW_AIImageClient] Count must be between 1 and 10");
                return null;
            }

            var request = new ImageGenerationRequest
            {
                Model = _modelName,
                Prompt = prompt,
                N = count,
                Size = size,
                Seed = seed
            };

            try
            {
                var response = await _imageProvider.GenerateImageAsync(request, cancellationToken);
                
                if (response?.Data == null)
                {
                    Debug.LogError("[DW_AIImageClient] Image generation failed - no response data");
                    return null;
                }

                var results = new List<DW_GeneratedImage>();
                foreach (var imageData in response.Data)
                {
                    results.Add(new DW_GeneratedImage
                    {
                        ImageBase64 = imageData.B64Json,
                        RevisedPrompt = imageData.RevisedPrompt,
                        OriginalPrompt = prompt,
                        GeneratedAt = DateTimeOffset.FromUnixTimeSeconds(response.Created).DateTime
                    });
                }

                Debug.Log($"[DW_AIImageClient] Successfully generated {results.Count} images");
                return results;
            }
            catch (PlayKitImageSizeValidationException ex)
            {
                // Log a concise error message for size validation
                // Debug.LogError($"[DW_AIImageClient] Size validation failed ({ex.ErrorCode}): {ex.Message}");
                throw; // Re-throw for caller to handle
            }
            catch (PlayKitApiErrorException ex)
            {
                // Log API errors concisely
                // Debug.LogError($"[DW_AIImageClient] API error ({ex.ErrorCode}): {ex.Message}");
                throw; // Re-throw for caller to handle
            }
            catch (PlayKitException)
            {
                // Don't log here as it's already logged in AIImageProvider
                throw; // Re-throw for caller to handle
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DW_AIImageClient] Unexpected error: {ex.Message}");
                throw new PlayKitException("Unexpected error during image generation", ex);
            }
        }

        /// <summary>
        /// Generate images with advanced provider-specific options
        /// </summary>
        /// <param name="prompt">Text description of the desired images</param>
        /// <param name="options">Advanced generation options</param>
        /// <returns>List of generated images with metadata</returns>
        public async UniTask<List<DW_GeneratedImage>> GenerateImagesAsync(string prompt, DW_ImageGenerationOptions options, CancellationToken cancellationToken = default)
        {
            if (options == null)
            {
                return await GenerateImagesAsync(prompt, cancellationToken: cancellationToken);
            }

            var request = new ImageGenerationRequest
            {
                Model = _modelName,
                Prompt = prompt,
                N = options.Count,
                Size = options.Size,
                Seed = options.Seed,
                ProviderOptions = options.ProviderOptions
            };

            try
            {
                var response = await _imageProvider.GenerateImageAsync(request, cancellationToken);

                if (response?.Data == null)
                {
                    Debug.LogError("[DW_AIImageClient] Image generation failed - no response data");
                    return null;
                }

                var results = new List<DW_GeneratedImage>();
                foreach (var imageData in response.Data)
                {
                    results.Add(new DW_GeneratedImage
                    {
                        ImageBase64 = imageData.B64Json,
                        RevisedPrompt = imageData.RevisedPrompt,
                        OriginalPrompt = prompt,
                        GeneratedAt = DateTimeOffset.FromUnixTimeSeconds(response.Created).DateTime
                    });
                }

                return results;
            }
            catch (PlayKitImageSizeValidationException ex)
            {
                // Log a concise error message for size validation
                Debug.LogError($"[DW_AIImageClient] Size validation failed ({ex.ErrorCode}): {ex.Message}");
                throw; // Re-throw for caller to handle
            }
            catch (PlayKitApiErrorException ex)
            {
                // Log API errors concisely
                Debug.LogError($"[DW_AIImageClient] API error ({ex.ErrorCode}): {ex.Message}");
                throw; // Re-throw for caller to handle
            }
            catch (PlayKitException)
            {
                // Don't log here as it's already logged in AIImageProvider
                throw; // Re-throw for caller to handle
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DW_AIImageClient] Unexpected error: {ex.Message}");
                throw new PlayKitException("Unexpected error during image generation", ex);
            }
        }

        /// <summary>
        /// Convert base64 image data to Unity Texture2D
        /// </summary>
        /// <param name="base64Data">Base64 encoded image data</param>
        /// <returns>Unity Texture2D, or null if conversion failed</returns>
        public static Texture2D Base64ToTexture2D(string base64Data)
        {
            if (string.IsNullOrEmpty(base64Data))
            {
                Debug.LogError("[DW_AIImageClient] Base64 data is null or empty");
                return null;
            }

            try
            {
                byte[] imageData = Convert.FromBase64String(base64Data);
                Texture2D texture = new Texture2D(2, 2);
                
                if (texture.LoadImage(imageData))
                {
                    return texture;
                }
                else
                {
                    Debug.LogError("[DW_AIImageClient] Failed to load image data into texture");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DW_AIImageClient] Failed to convert base64 to texture: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Convert a Unity Texture2D to a Sprite.
        /// </summary>
        /// <param name="texture">The Texture2D to convert.</param>
        /// <returns>A Unity Sprite, or null if conversion failed.</returns>
        public static Sprite Texture2DToSprite(Texture2D texture)
        {
            if (texture == null)
            {
                Debug.LogError("[DW_AIImageClient] Input Texture2D is null.");
                return null;
            }
    
            try
            {
                Rect rect = new Rect(0, 0, texture.width, texture.height);
                Vector2 pivot = new Vector2(0.5f, 0.5f); // Center pivot
                return Sprite.Create(texture, rect, pivot);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DW_AIImageClient] Failed to convert Texture2D to Sprite: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Represents a generated image with metadata
    /// </summary>
    [System.Serializable]
    public class DW_GeneratedImage
    {
        /// <summary>
        /// Base64 encoded image data
        /// </summary>
        public string ImageBase64 { get; set; }

        /// <summary>
        /// The original prompt used for generation
        /// </summary>
        public string OriginalPrompt { get; set; }

        /// <summary>
        /// The revised prompt (if modified by the AI provider)
        /// </summary>
        public string RevisedPrompt { get; set; }

        /// <summary>
        /// When the image was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Convert to Unity Texture2D
        /// </summary>
        public Texture2D ToTexture2D()
        {
            return PlayKit_AIImageClient.Base64ToTexture2D(ImageBase64);
        }
        
        /// <summary>
        /// Converts the image data to a Unity Sprite.
        /// </summary>
        /// <returns>A Sprite representation of the image.</returns>
        public Sprite ToSprite()
        {
            Texture2D texture = ToTexture2D();
            return PlayKit_AIImageClient.Texture2DToSprite(texture);
        }
    }

    /// <summary>
    /// Advanced options for image generation
    /// </summary>
    [System.Serializable]
    public class DW_ImageGenerationOptions
    {
        /// <summary>
        /// Number of images to generate (1-10)
        /// </summary>
        public int Count { get; set; } = 1;

        /// <summary>
        /// Image size (e.g., "1024x1024", "1792x1024", "1024x1792")
        /// </summary>
        public string Size { get; set; } = "1024x1024";
        
        /// <summary>
        /// Seed for reproducible results
        /// </summary>
        public int? Seed { get; set; }

        /// <summary>
        /// Provider-specific options (e.g., for OpenAI: {"openai": {"style": "vivid", "quality": "hd"}})
        /// </summary>
        public Dictionary<string, object> ProviderOptions { get; set; }
    }
}