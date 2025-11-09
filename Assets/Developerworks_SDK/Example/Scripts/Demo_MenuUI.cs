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
            /* 初始化 Developerworks SDK。
             * 这是使用SDK任何功能之前都必须调用的第一步，这会开始读取本地的玩家信息，如果未登录则自动打开登录窗口。
             * 如果传入您的开发者密钥（Developer Key），则会跳过任何鉴权。
             * Initialize Developerworks SDK.
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