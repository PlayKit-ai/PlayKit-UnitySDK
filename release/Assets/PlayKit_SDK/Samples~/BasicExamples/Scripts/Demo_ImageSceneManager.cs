using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlayKit_SDK.Example
{
    public class Demo_ImageSceneManager : MonoBehaviour
    {
        async void Start()
        {
            /* PlayKit SDK 现在会在游戏启动时自动初始化。
             * 调用 InitializeAsync() 等待初始化完成 - 如果已完成会立即返回。
             * 配置请在 Tools > PlayKit SDK > Settings 中设置。
             *
             * PlayKit SDK now auto-initializes at game startup.
             * Call InitializeAsync() to wait for completion - returns immediately if already done.
             * Configure via Tools > PlayKit SDK > Settings.
             */
            var result = await PlayKitSDK.InitializeAsync();

            if (!result)
            {
                Debug.LogError(
                    "SDK initialization failed. Please check your configuration in Tools > PlayKit SDK > Settings");
                return;
            }

        }
        
        [SerializeField] private InputField userInputField;
        [SerializeField] private Image _image;
        [SerializeField] private Button sendBtn;
        [SerializeField] private PlayKit_Image imageGenerator;

        private void Awake()
        {
            // Ensure EventSystem exists for UI interaction
            if (EventSystem.current == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                // New Input System only
                var inputModule = eventSystem.AddComponent(System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem"));
#elif ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
                // Legacy Input Manager only
                eventSystem.AddComponent<StandaloneInputModule>();
#else
                // Both enabled - try new Input System first, fallback to legacy
                var inputSystemType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputSystemType != null)
                {
                    eventSystem.AddComponent(inputSystemType);
                }
                else
                {
                    eventSystem.AddComponent<StandaloneInputModule>();
                }
#endif
            }

            sendBtn.onClick.AddListener(()=>OnButtonClicked());
        }

        private async UniTaskVoid OnButtonClicked()
        {
            sendBtn.interactable = false;
            var imageGen = imageGenerator;
            try
            {
                var genResult = await imageGen.GenerateImageAsync(userInputField.text);
                _image.sprite =  genResult.ToSprite();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                // throw;
            }
           
            sendBtn.interactable = true;

        }
    }
    
}