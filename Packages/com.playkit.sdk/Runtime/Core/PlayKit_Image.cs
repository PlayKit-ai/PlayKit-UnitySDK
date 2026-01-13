using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace PlayKit_SDK
{
    /// <summary>
    /// MonoBehaviour wrapper for PlayKit_AIImageClient.
    /// Provides Inspector configuration, UnityEvent support, and automatic lifecycle management.
    ///
    /// For advanced usage, use PlayKit_AIImageClient directly via GetUnderlyingClient().
    /// </summary>
    public class PlayKit_Image : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Image Generation Configuration")]
        [Tooltip("Image model name (leave empty to use SDK default) 图像模型名称（留空则使用SDK默认值）")]
        [SerializeField] private string imageModel;

        [Tooltip("Default image size 默认图像尺寸")]
        [SerializeField] private string defaultSize = "1024x1024";

        [Tooltip("Default number of images to generate 默认生成图像数量")]
        [Range(1, 10)]
        [SerializeField] private int defaultCount = 1;

        [Header("Output Options")]
        [Tooltip("Automatically convert generated images to Texture2D 自动转换为Texture2D")]
        [SerializeField] private bool autoConvertToTexture = true;

        [Header("Debug Options")]
        [Tooltip("Log generation status to console 在控制台输出生成状态")]
        [SerializeField] private bool logGeneration = false;

        #endregion

        #region UnityEvents

        [Header("Events")]
        [Tooltip("Called when a single image is generated (returns Texture2D) 生成单张图像时触发")]
        public UnityEvent<Texture2D> OnTextureGenerated;

        [Tooltip("Called when a single image is generated (returns Sprite) 生成Sprite时触发")]
        public UnityEvent<Sprite> OnSpriteGenerated;

        [Tooltip("Called when multiple images are generated 生成多张图像时触发")]
        public UnityEvent<List<PlayKit_GeneratedImage>> OnImagesGenerated;

        [Tooltip("Called when generation starts 开始生成时触发")]
        public UnityEvent OnGenerationStarted;

        [Tooltip("Called when generation completes (success or failure) 生成结束时触发")]
        public UnityEvent OnGenerationEnded;

        [Tooltip("Called when an error occurs 发生错误时触发")]
        public UnityEvent<string> OnError;

        #endregion

        #region Private Fields

        private PlayKit_AIImageClient _imageClient;
        private bool _isReady;
        private bool _isGenerating;
        private Texture2D _lastGeneratedTexture;
        private Sprite _lastGeneratedSprite;

        #endregion

        #region Public Properties

        /// <summary>
        /// Whether the image client is ready to use
        /// </summary>
        public bool IsReady => _isReady;

        /// <summary>
        /// Whether an image is currently being generated
        /// </summary>
        public bool IsGenerating => _isGenerating;

        /// <summary>
        /// The model name being used
        /// </summary>
        public string ModelName => imageModel;

        /// <summary>
        /// Default image size
        /// </summary>
        public string DefaultSize
        {
            get => defaultSize;
            set => defaultSize = value;
        }

        /// <summary>
        /// Default number of images to generate
        /// </summary>
        public int DefaultCount
        {
            get => defaultCount;
            set => defaultCount = Mathf.Clamp(value, 1, 10);
        }

        /// <summary>
        /// The last generated texture (for convenience)
        /// </summary>
        public Texture2D LastGeneratedTexture => _lastGeneratedTexture;

        /// <summary>
        /// The last generated sprite (for convenience)
        /// </summary>
        public Sprite LastGeneratedSprite => _lastGeneratedSprite;

        #endregion

        #region Lifecycle

        private void Start()
        {
            Initialize().Forget();
        }

        private void OnDestroy()
        {
            // Clean up textures if needed
            if (_lastGeneratedTexture != null)
            {
                Destroy(_lastGeneratedTexture);
                _lastGeneratedTexture = null;
            }
            if (_lastGeneratedSprite != null)
            {
                Destroy(_lastGeneratedSprite);
                _lastGeneratedSprite = null;
            }
        }

        private async UniTask Initialize()
        {
            await UniTask.WaitUntil(() => PlayKitSDK.IsReady());

            string model = string.IsNullOrEmpty(imageModel) ? null : imageModel;
            _imageClient = PlayKitSDK.Factory.CreateImageClient(model);

            if (_imageClient == null)
            {
                Debug.LogError("[PlayKit_Image] Failed to create image client");
                return;
            }

            _isReady = true;

            if (logGeneration)
            {
                Debug.Log($"[PlayKit_Image] Ready with model '{model ?? "default"}'");
            }
        }

        #endregion

        #region Public API - Generation Methods

        /// <summary>
        /// Generate a single image and return as Texture2D
        /// </summary>
        /// <param name="prompt">Text description of the desired image</param>
        /// <param name="size">Image size (optional, uses defaultSize if null)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Generated Texture2D</returns>
        public async UniTask<Texture2D> GenerateTextureAsync(
            string prompt,
            string size = null,
            CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();

            if (!ValidateState(prompt))
            {
                return null;
            }

            _isGenerating = true;
            OnGenerationStarted?.Invoke();

            try
            {
                var result = await _imageClient.GenerateImageAsync(
                    prompt,
                    size ?? defaultSize,
                    null,
                    token
                );

                if (result != null)
                {
                    var texture = result.ToTexture2D();

                    if (texture != null)
                    {
                        _lastGeneratedTexture = texture;

                        if (logGeneration)
                        {
                            Debug.Log($"[PlayKit_Image] Generated texture: {texture.width}x{texture.height}");
                        }

                        OnTextureGenerated?.Invoke(texture);
                        return texture;
                    }
                }

                OnError?.Invoke("Failed to generate image");
                return null;
            }
            catch (OperationCanceledException)
            {
                if (logGeneration)
                {
                    Debug.Log("[PlayKit_Image] Generation cancelled");
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_Image] Error: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
            finally
            {
                _isGenerating = false;
                OnGenerationEnded?.Invoke();
            }
        }

        /// <summary>
        /// Generate a single image and return as Sprite
        /// </summary>
        /// <param name="prompt">Text description of the desired image</param>
        /// <param name="size">Image size (optional, uses defaultSize if null)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Generated Sprite</returns>
        public async UniTask<Sprite> GenerateSpriteAsync(
            string prompt,
            string size = null,
            CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();

            if (!ValidateState(prompt))
            {
                return null;
            }

            _isGenerating = true;
            OnGenerationStarted?.Invoke();

            try
            {
                var result = await _imageClient.GenerateImageAsync(
                    prompt,
                    size ?? defaultSize,
                    null,
                    token
                );

                if (result != null)
                {
                    var sprite = result.ToSprite();

                    if (sprite != null)
                    {
                        _lastGeneratedSprite = sprite;

                        if (logGeneration)
                        {
                            Debug.Log($"[PlayKit_Image] Generated sprite: {sprite.rect.width}x{sprite.rect.height}");
                        }

                        OnSpriteGenerated?.Invoke(sprite);
                        return sprite;
                    }
                }

                OnError?.Invoke("Failed to generate sprite");
                return null;
            }
            catch (OperationCanceledException)
            {
                if (logGeneration)
                {
                    Debug.Log("[PlayKit_Image] Generation cancelled");
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_Image] Error: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
            finally
            {
                _isGenerating = false;
                OnGenerationEnded?.Invoke();
            }
        }

        /// <summary>
        /// Generate a single image with full metadata
        /// </summary>
        /// <param name="prompt">Text description of the desired image</param>
        /// <param name="size">Image size (optional)</param>
        /// <param name="seed">Seed for reproducible results (optional)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Generated image with metadata</returns>
        public async UniTask<PlayKit_GeneratedImage> GenerateImageAsync(
            string prompt,
            string size = null,
            int? seed = null,
            CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();

            if (!ValidateState(prompt))
            {
                return null;
            }

            _isGenerating = true;
            OnGenerationStarted?.Invoke();

            try
            {
                var result = await _imageClient.GenerateImageAsync(
                    prompt,
                    size ?? defaultSize,
                    seed,
                    token
                );

                if (result != null)
                {
                    if (logGeneration)
                    {
                        Debug.Log($"[PlayKit_Image] Generated image for prompt: {prompt.Substring(0, Math.Min(50, prompt.Length))}...");
                    }

                    // Auto-convert if enabled
                    if (autoConvertToTexture)
                    {
                        var texture = result.ToTexture2D();
                        if (texture != null)
                        {
                            _lastGeneratedTexture = texture;
                            OnTextureGenerated?.Invoke(texture);
                        }
                    }

                    return result;
                }

                OnError?.Invoke("Failed to generate image");
                return null;
            }
            catch (OperationCanceledException)
            {
                if (logGeneration)
                {
                    Debug.Log("[PlayKit_Image] Generation cancelled");
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_Image] Error: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
            finally
            {
                _isGenerating = false;
                OnGenerationEnded?.Invoke();
            }
        }

        /// <summary>
        /// Generate multiple images
        /// </summary>
        /// <param name="prompt">Text description of the desired images</param>
        /// <param name="count">Number of images to generate (uses defaultCount if 0)</param>
        /// <param name="size">Image size (optional)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>List of generated images with metadata</returns>
        public async UniTask<List<PlayKit_GeneratedImage>> GenerateImagesAsync(
            string prompt,
            int count = 0,
            string size = null,
            CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();

            if (!ValidateState(prompt))
            {
                return null;
            }

            _isGenerating = true;
            OnGenerationStarted?.Invoke();

            try
            {
                int imageCount = count > 0 ? count : defaultCount;

                var results = await _imageClient.GenerateImagesAsync(
                    prompt,
                    imageCount,
                    size ?? defaultSize,
                    null,
                    token
                );

                if (results != null && results.Count > 0)
                {
                    if (logGeneration)
                    {
                        Debug.Log($"[PlayKit_Image] Generated {results.Count} images");
                    }

                    // Auto-convert first image if enabled
                    if (autoConvertToTexture && results.Count > 0)
                    {
                        var texture = results[0].ToTexture2D();
                        if (texture != null)
                        {
                            _lastGeneratedTexture = texture;
                            OnTextureGenerated?.Invoke(texture);
                        }
                    }

                    OnImagesGenerated?.Invoke(results);
                    return results;
                }

                OnError?.Invoke("Failed to generate images");
                return null;
            }
            catch (OperationCanceledException)
            {
                if (logGeneration)
                {
                    Debug.Log("[PlayKit_Image] Generation cancelled");
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_Image] Error: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
            finally
            {
                _isGenerating = false;
                OnGenerationEnded?.Invoke();
            }
        }

        #endregion

        #region Public API - Image-to-Image Generation (img2img)

        /// <summary>
        /// Generate a single texture from a reference image (img2img)
        /// </summary>
        /// <param name="prompt">Text description of the desired image</param>
        /// <param name="inputImage">Reference image to base generation on</param>
        /// <param name="size">Output image size (optional)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Generated Texture2D</returns>
        public async UniTask<Texture2D> GenerateTextureFromImageAsync(
            string prompt,
            Texture2D inputImage,
            string size = null,
            CancellationToken? cancellationToken = null)
        {
            return await GenerateTextureFromImagesAsync(prompt, new List<Texture2D> { inputImage }, size, cancellationToken);
        }

        /// <summary>
        /// Generate a single texture from multiple reference images (img2img)
        /// </summary>
        /// <param name="prompt">Text description of the desired image</param>
        /// <param name="inputImages">Reference images to base generation on</param>
        /// <param name="size">Output image size (optional)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Generated Texture2D</returns>
        public async UniTask<Texture2D> GenerateTextureFromImagesAsync(
            string prompt,
            List<Texture2D> inputImages,
            string size = null,
            CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();

            if (!ValidateState(prompt))
            {
                return null;
            }

            if (inputImages == null || inputImages.Count == 0)
            {
                Debug.LogError("[PlayKit_Image] At least one input image is required for img2img");
                OnError?.Invoke("Input image required");
                return null;
            }

            _isGenerating = true;
            OnGenerationStarted?.Invoke();

            try
            {
                var result = await _imageClient.GenerateImageAsync(
                    prompt,
                    inputImages,
                    size ?? defaultSize,
                    null,
                    token
                );

                if (result != null)
                {
                    var texture = result.ToTexture2D();

                    if (texture != null)
                    {
                        _lastGeneratedTexture = texture;

                        if (logGeneration)
                        {
                            Debug.Log($"[PlayKit_Image] Generated texture from {inputImages.Count} input image(s): {texture.width}x{texture.height}");
                        }

                        OnTextureGenerated?.Invoke(texture);
                        return texture;
                    }
                }

                OnError?.Invoke("Failed to generate image from input");
                return null;
            }
            catch (OperationCanceledException)
            {
                if (logGeneration)
                {
                    Debug.Log("[PlayKit_Image] Generation cancelled");
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_Image] Error: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
            finally
            {
                _isGenerating = false;
                OnGenerationEnded?.Invoke();
            }
        }

        /// <summary>
        /// Generate a single sprite from a reference image (img2img)
        /// </summary>
        /// <param name="prompt">Text description of the desired image</param>
        /// <param name="inputImage">Reference image to base generation on</param>
        /// <param name="size">Output image size (optional)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Generated Sprite</returns>
        public async UniTask<Sprite> GenerateSpriteFromImageAsync(
            string prompt,
            Texture2D inputImage,
            string size = null,
            CancellationToken? cancellationToken = null)
        {
            var texture = await GenerateTextureFromImageAsync(prompt, inputImage, size, cancellationToken);
            return texture != null ? Texture2DToSprite(texture) : null;
        }

        /// <summary>
        /// Generate a single image with metadata from reference images (img2img)
        /// </summary>
        /// <param name="prompt">Text description of the desired image</param>
        /// <param name="inputImages">Reference images to base generation on</param>
        /// <param name="size">Output image size (optional)</param>
        /// <param name="seed">Seed for reproducible results (optional)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Generated image with metadata</returns>
        public async UniTask<PlayKit_GeneratedImage> GenerateImageFromImagesAsync(
            string prompt,
            List<Texture2D> inputImages,
            string size = null,
            int? seed = null,
            CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();

            if (!ValidateState(prompt))
            {
                return null;
            }

            if (inputImages == null || inputImages.Count == 0)
            {
                Debug.LogError("[PlayKit_Image] At least one input image is required for img2img");
                OnError?.Invoke("Input image required");
                return null;
            }

            _isGenerating = true;
            OnGenerationStarted?.Invoke();

            try
            {
                var options = new PlayKit_ImageGenerationOptions
                {
                    Count = 1,
                    Size = size ?? defaultSize,
                    Seed = seed,
                    InputImages = inputImages
                };

                var results = await _imageClient.GenerateImagesAsync(prompt, options, token);

                if (results != null && results.Count > 0)
                {
                    var result = results[0];

                    if (logGeneration)
                    {
                        Debug.Log($"[PlayKit_Image] Generated image from {inputImages.Count} reference(s)");
                    }

                    if (autoConvertToTexture)
                    {
                        var texture = result.ToTexture2D();
                        if (texture != null)
                        {
                            _lastGeneratedTexture = texture;
                            OnTextureGenerated?.Invoke(texture);
                        }
                    }

                    return result;
                }

                OnError?.Invoke("Failed to generate image from input");
                return null;
            }
            catch (OperationCanceledException)
            {
                if (logGeneration)
                {
                    Debug.Log("[PlayKit_Image] Generation cancelled");
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_Image] Error: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
            finally
            {
                _isGenerating = false;
                OnGenerationEnded?.Invoke();
            }
        }

        /// <summary>
        /// Generate multiple images from reference images (img2img)
        /// Input image count and output image count are independent.
        /// </summary>
        /// <param name="prompt">Text description of the desired images</param>
        /// <param name="inputImages">Reference images to base generation on (1 or more)</param>
        /// <param name="outputCount">Number of output images to generate (uses defaultCount if 0)</param>
        /// <param name="size">Output image size (optional)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>List of generated images with metadata</returns>
        public async UniTask<List<PlayKit_GeneratedImage>> GenerateImagesFromImagesAsync(
            string prompt,
            List<Texture2D> inputImages,
            int outputCount = 0,
            string size = null,
            CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? this.GetCancellationTokenOnDestroy();

            if (!ValidateState(prompt))
            {
                return null;
            }

            if (inputImages == null || inputImages.Count == 0)
            {
                Debug.LogError("[PlayKit_Image] At least one input image is required for img2img");
                OnError?.Invoke("Input image required");
                return null;
            }

            _isGenerating = true;
            OnGenerationStarted?.Invoke();

            try
            {
                int count = outputCount > 0 ? outputCount : defaultCount;

                var results = await _imageClient.GenerateImagesAsync(
                    prompt,
                    inputImages,
                    count,
                    size ?? defaultSize,
                    null,
                    token
                );

                if (results != null && results.Count > 0)
                {
                    if (logGeneration)
                    {
                        Debug.Log($"[PlayKit_Image] Generated {results.Count} images from {inputImages.Count} reference(s)");
                    }

                    if (autoConvertToTexture && results.Count > 0)
                    {
                        var texture = results[0].ToTexture2D();
                        if (texture != null)
                        {
                            _lastGeneratedTexture = texture;
                            OnTextureGenerated?.Invoke(texture);
                        }
                    }

                    OnImagesGenerated?.Invoke(results);
                    return results;
                }

                OnError?.Invoke("Failed to generate images from input");
                return null;
            }
            catch (OperationCanceledException)
            {
                if (logGeneration)
                {
                    Debug.Log("[PlayKit_Image] Generation cancelled");
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayKit_Image] Error: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
            finally
            {
                _isGenerating = false;
                OnGenerationEnded?.Invoke();
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Convert base64 image data to Texture2D
        /// </summary>
        public static Texture2D Base64ToTexture2D(string base64Data)
        {
            return PlayKit_AIImageClient.Base64ToTexture2D(base64Data);
        }

        /// <summary>
        /// Convert Texture2D to Sprite
        /// </summary>
        public static Sprite Texture2DToSprite(Texture2D texture)
        {
            return PlayKit_AIImageClient.Texture2DToSprite(texture);
        }

        #endregion

        #region Advanced Access

        /// <summary>
        /// Get the underlying PlayKit_AIImageClient for advanced operations
        /// </summary>
        /// <returns>The underlying image client</returns>
        public PlayKit_AIImageClient GetUnderlyingClient()
        {
            return _imageClient;
        }

        /// <summary>
        /// Setup with an existing image client (for advanced scenarios)
        /// </summary>
        /// <param name="client">The image client to use</param>
        public void Setup(PlayKit_AIImageClient client)
        {
            _imageClient = client;
            _isReady = true;

            if (logGeneration)
            {
                Debug.Log("[PlayKit_Image] Setup with external client");
            }
        }

        #endregion

        #region Private Helpers

        private bool ValidateState(string prompt)
        {
            if (!_isReady)
            {
                Debug.LogWarning("[PlayKit_Image] Client not ready. Please wait for initialization.");
                OnError?.Invoke("Client not ready");
                return false;
            }

            if (_imageClient == null)
            {
                Debug.LogError("[PlayKit_Image] Image client not initialized.");
                OnError?.Invoke("Client not initialized");
                return false;
            }

            if (string.IsNullOrEmpty(prompt))
            {
                Debug.LogWarning("[PlayKit_Image] Prompt cannot be empty.");
                OnError?.Invoke("Prompt cannot be empty");
                return false;
            }

            if (_isGenerating)
            {
                Debug.LogWarning("[PlayKit_Image] A generation is already in progress.");
                OnError?.Invoke("Generation already in progress");
                return false;
            }

            return true;
        }

        #endregion
    }
}
