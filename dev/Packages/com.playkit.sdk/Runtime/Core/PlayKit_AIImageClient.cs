using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using PlayKit_SDK.Provider;
using PlayKit_SDK.Provider.AI;
using PlayKit_SDK.Public;

namespace PlayKit_SDK
{
    /// <summary>
    /// Client for AI image generation using platform-hosted models
    /// Provides simple interface for generating images from text prompts
    /// Supports both text-to-image and image-to-image (img2img) generation
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

        #region Text-to-Image Generation

        /// <summary>
        /// Generate a single image from a text prompt
        /// </summary>
        /// <param name="prompt">Text description of the desired image</param>
        /// <param name="size">Image size (e.g., "1024x1024", "1792x1024", "1024x1792")</param>
        /// <param name="seed">Optional seed for reproducible results</param>
        /// <returns>Generated image with metadata, or null if generation failed</returns>
        public async UniTask<PlayKit_GeneratedImage> GenerateImageAsync(
            string prompt,
            string size = "1024x1024",
            int? seed = null,
            CancellationToken cancellationToken = default)
        {
            var results = await GenerateImagesAsync(prompt, 1, size, seed, cancellationToken);
            return results?.Count > 0 ? results[0] : null;
        }

        /// <summary>
        /// Generate a single image from a text prompt and return only the base64 string
        /// </summary>
        /// <param name="prompt">Text description of the desired image</param>
        /// <param name="size">Image size (e.g., "1024x1024", "1792x1024", "1024x1792")</param>
        /// <param name="seed">Optional seed for reproducible results</param>
        /// <returns>Generated image as base64 string, or null if generation failed</returns>
        [System.Obsolete("Use GenerateImageAsync() which returns PlayKit_GeneratedImage with metadata. This method is kept for backward compatibility.")]
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
        /// <param name="seed">Optional seed for reproducible results</param>
        /// <returns>List of generated images with metadata</returns>
        public async UniTask<List<PlayKit_GeneratedImage>> GenerateImagesAsync(
            string prompt,
            int count = 1,
            string size = "1024x1024",
            int? seed = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(prompt))
            {
                throw new ArgumentException("Prompt cannot be null or empty", nameof(prompt));
            }

            if (count < 1 || count > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be between 1 and 10");
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

                // Response validation is now handled in AIImageProvider
                // If we reach here, response and response.Data are guaranteed to be valid

                var results = new List<PlayKit_GeneratedImage>();
                foreach (var imageData in response.Data)
                {
                    results.Add(new PlayKit_GeneratedImage
                    {
                        ImageBase64 = imageData.B64Json,
                        RevisedPrompt = imageData.RevisedPrompt,
                        OriginalPrompt = prompt,
                        GeneratedAt = DateTimeOffset.FromUnixTimeSeconds(response.Created).DateTime,
                        OriginalImageBase64 = imageData.B64JsonOriginal,
                        TransparentSuccess = imageData.TransparentSuccess
                    });
                }

                Debug.Log($"[PlayKit_AIImageClient] Successfully generated {results.Count} images");
                return results;
            }
            catch (PlayKitImageSizeValidationException)
            {
                throw; // Re-throw for caller to handle
            }
            catch (PlayKitApiErrorException)
            {
                throw; // Re-throw for caller to handle
            }
            catch (PlayKitException)
            {
                throw; // Re-throw for caller to handle
            }
            catch (ArgumentException)
            {
                throw; // Re-throw argument validation exceptions
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_AIImageClient] Unexpected error: {ex.Message}");
                throw new PlayKitException("Unexpected error during image generation", ex);
            }
        }

        /// <summary>
        /// Generate images with advanced provider-specific options
        /// </summary>
        /// <param name="prompt">Text description of the desired images</param>
        /// <param name="options">Advanced generation options (can include input images for img2img)</param>
        /// <returns>List of generated images with metadata</returns>
        public async UniTask<List<PlayKit_GeneratedImage>> GenerateImagesAsync(string prompt, PlayKit_ImageGenerationOptions options, CancellationToken cancellationToken = default)
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
                ProviderOptions = options.ProviderOptions,
                Transparent = options.Transparent ? true : null,
                Images = options.GetInputImagesBase64()
            };

            try
            {
                var response = await _imageProvider.GenerateImageAsync(request, cancellationToken);

                // Response validation is now handled in AIImageProvider
                // If we reach here, response and response.Data are guaranteed to be valid

                var results = new List<PlayKit_GeneratedImage>();
                foreach (var imageData in response.Data)
                {
                    results.Add(new PlayKit_GeneratedImage
                    {
                        ImageBase64 = imageData.B64Json,
                        RevisedPrompt = imageData.RevisedPrompt,
                        OriginalPrompt = prompt,
                        GeneratedAt = DateTimeOffset.FromUnixTimeSeconds(response.Created).DateTime,
                        OriginalImageBase64 = imageData.B64JsonOriginal,
                        TransparentSuccess = imageData.TransparentSuccess
                    });
                }

                return results;
            }
            catch (PlayKitImageSizeValidationException)
            {
                throw; // Re-throw for caller to handle
            }
            catch (PlayKitApiErrorException)
            {
                throw; // Re-throw for caller to handle
            }
            catch (PlayKitException)
            {
                throw; // Re-throw for caller to handle
            }
            catch (ArgumentException)
            {
                throw; // Re-throw argument validation exceptions
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_AIImageClient] Unexpected error: {ex.Message}");
                throw new PlayKitException("Unexpected error during image generation", ex);
            }
        }

        #endregion

        #region Image-to-Image Generation (img2img)

        /// <summary>
        /// Generate a single image using input reference images (img2img)
        /// </summary>
        /// <param name="prompt">Text description of the desired image</param>
        /// <param name="inputImages">One or more input reference images</param>
        /// <param name="size">Output image size (e.g., "1024x1024")</param>
        /// <param name="seed">Optional seed for reproducible results</param>
        /// <returns>Generated image with metadata, or null if generation failed</returns>
        public async UniTask<PlayKit_GeneratedImage> GenerateImageAsync(
            string prompt,
            List<Texture2D> inputImages,
            string size = "1024x1024",
            int? seed = null,
            CancellationToken cancellationToken = default)
        {
            var results = await GenerateImagesAsync(prompt, inputImages, 1, size, seed, cancellationToken);
            return results?.Count > 0 ? results[0] : null;
        }

        /// <summary>
        /// Generate a single image using a single input reference image (img2img)
        /// Convenience overload for single input image
        /// </summary>
        public async UniTask<PlayKit_GeneratedImage> GenerateImageAsync(
            string prompt,
            Texture2D inputImage,
            string size = "1024x1024",
            int? seed = null,
            CancellationToken cancellationToken = default)
        {
            return await GenerateImageAsync(prompt, new List<Texture2D> { inputImage }, size, seed, cancellationToken);
        }

        /// <summary>
        /// Generate multiple images using input reference images (img2img)
        /// Input images and output count are independent parameters.
        /// </summary>
        /// <param name="prompt">Text description of the desired images</param>
        /// <param name="inputImages">One or more input reference images</param>
        /// <param name="count">Number of output images to generate (1-10)</param>
        /// <param name="size">Output image size (e.g., "1024x1024")</param>
        /// <param name="seed">Optional seed for reproducible results</param>
        /// <returns>List of generated images with metadata</returns>
        public async UniTask<List<PlayKit_GeneratedImage>> GenerateImagesAsync(
            string prompt,
            List<Texture2D> inputImages,
            int count = 1,
            string size = "1024x1024",
            int? seed = null,
            CancellationToken cancellationToken = default)
        {
            if (inputImages == null || inputImages.Count == 0)
            {
                throw new ArgumentException("At least one input image is required for img2img", nameof(inputImages));
            }

            var options = new PlayKit_ImageGenerationOptions
            {
                Count = count,
                Size = size,
                Seed = seed,
                InputImages = inputImages
            };

            return await GenerateImagesAsync(prompt, options, cancellationToken);
        }

        /// <summary>
        /// Generate multiple images using a single input reference image (img2img)
        /// Convenience overload for single input image
        /// </summary>
        public async UniTask<List<PlayKit_GeneratedImage>> GenerateImagesAsync(
            string prompt,
            Texture2D inputImage,
            int count = 1,
            string size = "1024x1024",
            int? seed = null,
            CancellationToken cancellationToken = default)
        {
            return await GenerateImagesAsync(prompt, new List<Texture2D> { inputImage }, count, size, seed, cancellationToken);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Convert base64 image data to Unity Texture2D
        /// </summary>
        /// <param name="base64Data">Base64 encoded image data</param>
        /// <returns>Unity Texture2D, or null if conversion failed</returns>
        public static Texture2D Base64ToTexture2D(string base64Data)
        {
            if (string.IsNullOrEmpty(base64Data))
            {
                Debug.LogError("[PlayKit_AIImageClient] Base64 data is null or empty");
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
                    Debug.LogError("[PlayKit_AIImageClient] Failed to load image data into texture");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_AIImageClient] Failed to convert base64 to texture: {ex.Message}");
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
                Debug.LogError("[PlayKit_AIImageClient] Input Texture2D is null.");
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
                Debug.LogError($"[PlayKit_AIImageClient] Failed to convert Texture2D to Sprite: {ex.Message}");
                return null;
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents a generated image with metadata
    /// </summary>
    [System.Serializable]
    public class PlayKit_GeneratedImage
    {
        /// <summary>
        /// Base64 encoded image data (background removed if transparent=true and successful)
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
        /// Original image before background removal (only present when transparent=true)
        /// </summary>
        public string OriginalImageBase64 { get; set; }
        
        /// <summary>
        /// Whether background removal was successful (only present when transparent=true)
        /// </summary>
        public bool? TransparentSuccess { get; set; }

        /// <summary>
        /// Convert to Unity Texture2D
        /// </summary>
        public Texture2D ToTexture2D()
        {
            return PlayKit_AIImageClient.Base64ToTexture2D(ImageBase64);
        }
        
        /// <summary>
        /// Convert original image (before background removal) to Unity Texture2D
        /// </summary>
        public Texture2D OriginalToTexture2D()
        {
            return PlayKit_AIImageClient.Base64ToTexture2D(OriginalImageBase64);
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
    /// Supports both text-to-image and image-to-image (img2img) generation
    /// </summary>
    [System.Serializable]
    public class PlayKit_ImageGenerationOptions
    {
        /// <summary>
        /// Number of output images to generate (1-10)
        /// This is independent from the number of input images
        /// </summary>
        public int Count { get; set; } = 1;

        /// <summary>
        /// Output image size (e.g., "1024x1024", "1792x1024", "1024x1792")
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
        
        /// <summary>
        /// If true, automatically remove background from generated images.
        /// When enabled, ImageBase64 contains the transparent image and OriginalImageBase64 contains the original.
        /// </summary>
        public bool Transparent { get; set; } = false;

        /// <summary>
        /// Input reference images for img2img generation (optional)
        /// When provided, the model will use these as reference for generation
        /// Can provide 1 or more images; the output count is independent
        /// </summary>
        public List<Texture2D> InputImages { get; set; }

        /// <summary>
        /// Check if this is an img2img request (has input images)
        /// </summary>
        public bool HasInputImages => InputImages != null && InputImages.Count > 0;

        /// <summary>
        /// Add an input image for img2img generation
        /// </summary>
        public void AddInputImage(Texture2D texture)
        {
            if (InputImages == null) InputImages = new List<Texture2D>();
            InputImages.Add(texture);
        }

        /// <summary>
        /// Get input images as base64 encoded strings (internal use)
        /// </summary>
        internal List<string> GetInputImagesBase64()
        {
            if (!HasInputImages) return null;

            var base64List = new List<string>();
            foreach (var texture in InputImages)
            {
                if (texture != null)
                {
                    var base64 = PlayKit_ImageUtils.Texture2DToBase64(texture);
                    if (!string.IsNullOrEmpty(base64))
                    {
                        base64List.Add(base64);
                    }
                }
            }

            return base64List.Count > 0 ? base64List : null;
        }
    }
}