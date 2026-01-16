using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlayKit_SDK.Example
{
    public class Demo_MenuUI : MonoBehaviour
    {
        public static Demo_MenuUI instance;
        [SerializeField] private GameObject tab, frontpage;
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }
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
        public void ShowMenuScene()
        {
            SceneManager.LoadScene("0-Menu");
            frontpage.SetActive(true);
            tab.SetActive(false);
        }
        
        public void ShowChatScene()
        {
            SceneManager.LoadScene("1-Chat");
            frontpage.SetActive(false);
            tab.SetActive(true);
        }

        public void ShowImageScene()
        {
            SceneManager.LoadScene("2-Image");
            frontpage.SetActive(false);
            tab.SetActive(true);
        }

        public void ShowStructuredScene()
        {
            SceneManager.LoadScene("3-Structured");
            frontpage.SetActive(false);
            tab.SetActive(true);
        }
    }
}