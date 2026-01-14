using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using PlayKit_SDK;
using PlayKit_SDK.Auth;
using PlayKit_SDK.Public;
using UnityEngine;
using UnityEngine.UI;

public class Demo_ExampleGameManager : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private Text _text;
    [SerializeField] private Image _image;
    async void Start()
    {
        /* 初始化 PlayKit SDK。
         SDK 会自动创建，无需在场景中拖入 Prefab。
         在使用任何功能前调用 InitializeAsync()，它会读取配置（Tools > PlayKit SDK > Settings）并处理玩家认证。
         如果未登录，会自动打开登录窗口。

         Initialize PlayKit SDK.
         The SDK instance is created automatically - no need to drag prefabs into scenes.
         Call InitializeAsync() before using any features. It loads configuration (Tools > PlayKit SDK > Settings)
         and handles player authentication. If not logged in, it will automatically show the login modal.
         */
        var result = await PlayKitSDK.InitializeAsync();

        if(!result)
        {
            Debug.LogError("SDK initialization failed. Please check your configuration in Tools > PlayKit SDK > Settings");
            return;
        }

        SimpleChatStream();

    }
    
    List<PlayKit_ChatMessage> _selfManagedHistory = new List<PlayKit_ChatMessage>();


    async UniTask StandardImageGen()
    {
        var imageGen = PlayKitSDK.Factory.CreateImageClient();
        var genResult = await imageGen.GenerateImageAsync("a futuristic city","1024x1024");
        _image.sprite =  genResult.ToSprite();
    }
    async UniTask StandardChat()
    {
        //你需要自行管理AI的历史信息，自行创建一个历史记录，自行操作其中的内容
        //是否支持设置多个system信息，不同的模型行为各不相同，但TextGeneration提供较高的自由度，所以并不在这里做任何限制
        _selfManagedHistory.Add(new PlayKit_ChatMessage()
        {
            Role = "system",
            Content = "你扮演《底特律变人》的康纳"
        });
        _selfManagedHistory.Add(new PlayKit_ChatMessage()
        {
            Role = "user",
            Content = "你的工作是什么"
        });
        var chat = PlayKitSDK.Factory.CreateChatClient();//新建一个对话客户端
        var result = await chat.TextGenerationAsync(new PlayKit_ChatConfig(_selfManagedHistory));//对话
        _selfManagedHistory.Add(new PlayKit_ChatMessage()
        {
            Role = "assistant",
            Content = result.Response
        });
        _selfManagedHistory.Add(new PlayKit_ChatMessage()
        {
            Role = "user",
            Content = "你喜欢你的工作吗"
        });
        _selfManagedHistory.Add(new PlayKit_ChatMessage()
        {
            Role = "system",
            Content = "你扮演一个普通人"
        });
        result = await chat.TextGenerationAsync(new PlayKit_ChatConfig(_selfManagedHistory));//对话
        Debug.Log(result.Response);
        
    }
    
    [SerializeField] private PlayKit_NPC _npcClient,_npcClient2;
    async UniTask SimpleChat()
    {
        var npc =_npcClient;
        var reply = await npc.Talk("1+1等于几");
        Debug.Log(reply);
        var history = npc.SaveHistory();
        //Npc则会帮助你管理历史记录，设置系统提示词时会
        npc.SetSystemPrompt("扮演一个恨铁不成钢的老师");
        await Task.Delay(5000);
        reply = await npc.Talk("再+2呢？");
        Debug.Log(reply);
        var npc2 = _npcClient2;
        npc2.LoadHistory(history);
        reply = await npc2.Talk("再+2呢？");
        Debug.Log(reply);

    }
    
    async UniTask StandardChatStream()
    {

        var chat = PlayKitSDK.Factory.CreateChatClient();
        _selfManagedHistory.Add(new PlayKit_ChatMessage()
        {
            Role = "system",
            Content = "一千零一夜的故事是什么？"
        });
        _selfManagedHistory.Add(new PlayKit_ChatMessage()
        {
            Role = "user",
            Content = "你的工作是什么"
        });
        await chat.TextChatStreamAsync(new PlayKit_ChatStreamConfig(_selfManagedHistory), 
            (s) => {
                var original = _text.text;
                _text.text = original + s;
            },
            (s) =>
            {
                _text.text = s;
            });
    }
    
    async void SimpleChatStream()
    {

        var chat = _npcClient;
        await chat.TalkStream("东京怎么玩？", 
            (s) => {
                var original = _text.text;
                _text.text = original + s;
            },
            (s) =>
            {
                _text.text = s;
            });

    }

}
