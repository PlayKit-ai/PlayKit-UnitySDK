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
            /* 初始化 PlayKit SDK。
             * 这是使用SDK任何功能之前都必须调用的第一步，这会开始读取本地的玩家信息，如果未登录则自动打开登录窗口。
             * 如果传入您的开发者密钥（Developer Key），则会跳过任何鉴权。
             * Initialize PlayKit SDK.
             * This must be called before everything, and it will start to read local player information
             * and if there is not, it will automatically start up the login modal.
             * If you pass in your developer key, the sdk skips player validation.
             */
            var result = await PlayKit_SDK.InitializeAsync();

            if (!result)
            {
                Debug.LogError(
                    "initialization failed, you should place a sdk object first, then fill in your gameId in the sdk object. 初始化失败，你需要放置一个sdk prefab，然后将你的游戏Id填写到sdk里");
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