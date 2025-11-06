using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Developerworks_SDK;
using Developerworks_SDK.Public;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Developerworks_SDK.Example
{
    public class Demo_ChatSceneManager : MonoBehaviour
    {
        // Start is called before the first frame update
        [SerializeField] private Text _text;
        [SerializeField] private DW_NPCClient _npcClient;
        [SerializeField] private DW_AIChatClient _aiChatClient;
        [SerializeField] private Toggle useStreamToggle;
        [SerializeField] private Dropdown variantDropdown;
        [SerializeField] private Button saveBtn,loadBtn,userSendBtn;
        [SerializeField] private InputField userSendField;
        [SerializeField] private InputField npcSettingField;
        [SerializeField] private Image npcStatusIndicator;
        [SerializeField] private GameObject npcCanvas;
        [SerializeField] private Button npcRecordBtn;
        [SerializeField] private Button stopNpcRecordBtn;

        private static readonly Color gapColor = new Color(0, 0, 0,0);
        private static readonly Color notTalkingColor = new Color(1, 1, 0,1);
        private static readonly Color talkingColor = new Color(0, 1, 0,1);
        private static readonly Color uninitializedColor = new Color(1, 0, 0,1);

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

            useStreamToggle.onValueChanged.AddListener(OnUseStreamClicked);
            variantDropdown.onValueChanged.AddListener(OnVariantDropdownChanged);
            saveBtn.onClick.AddListener(OnNpcSave);
            loadBtn.onClick.AddListener(OnNpcLoad);
            npcRecordBtn.onClick.AddListener(OnNpcRecord);
            stopNpcRecordBtn.onClick.AddListener(OnStopNpcRecord);
            npcSettingField.onEndEdit.AddListener(OnNpcSettingChanged);
            OnVariantDropdownChanged(variantDropdown.value);
            OnUseStreamClicked(useStreamToggle.isOn);
            userSendBtn.onClick.AddListener(OnUserSendClicked);
            var recorder = _npcClient.GetComponent<DW_NPCClient_VoiceModule>().GetOrCreateRecorder();
            recorder.OnRecordingStarted += () =>
            {
                stopNpcRecordBtn.gameObject.SetActive(true);
                npcRecordBtn.gameObject.SetActive(false);
            };
            recorder.OnRecordingStopped += clip =>
            {
                _npcClient.GetComponent<DW_NPCClient_VoiceModule>().ListenOnly(clip);
                stopNpcRecordBtn.gameObject.SetActive(false);
                npcRecordBtn.gameObject.SetActive(true);
            };
        }

        private void OnStopNpcRecord()
        {
            var recorder = _npcClient.GetComponent<DW_NPCClient_VoiceModule>().GetOrCreateRecorder();
            recorder.StopRecording();
            
        }

        private void OnNpcRecord()
        {
            var recorder = _npcClient.GetComponent<DW_NPCClient_VoiceModule>().GetOrCreateRecorder();
            recorder.StartRecording();
            
        }

        private void OnUserSendClicked()
        {
            var userInput = userSendField.text;
            userSendField.text = "";
            if (variant == 0)
            {
                if (useStream)
                {
                    NpcChatStream(userInput);
                }
                else
                {
                    NpcChat(userInput);
                }
            }
            else
            {
                if (useStream)
                {
                    StandardChatStream(userInput);
                }
                else
                {
                    StandardChat(userInput);
                }
            }
            
        }
        

        private void OnNpcSettingChanged(string arg0)
        {
            _npcClient.SetSystemPrompt(arg0);
        }

        private bool useStream;

        private void OnUseStreamClicked(bool arg0)
        {
            useStream = arg0;
        }

        private int variant = 0;

        private void OnVariantDropdownChanged(int arg0)
        {
            variant = arg0;
            if (variant == 0)
            {
                StartCoroutine(CheckStatus());
                npcCanvas.gameObject.SetActive(true);
                
            }
            else
            {
                StopAllCoroutines();
                npcCanvas.gameObject.SetActive(false);
            }
        }

        private string npcSavedHistory = "";
        private void OnNpcSave()
        {
            var history = _npcClient.SaveHistory();
        }

        private void OnNpcLoad()
        {
            var history = _npcClient.LoadHistory(npcSavedHistory);
        }

        IEnumerator CheckStatus()
        {
            while (variant == 0)
            {
                if (variant == 0)
                {
                    var npcStatus_IsReady = _npcClient.IsReady;
                    var npcStatus_IsTalking = _npcClient.IsTalking;
                    if (!npcStatus_IsReady)
                    {
                        npcStatusIndicator.color  = uninitializedColor;
                    }
                    else
                    {
                        npcStatusIndicator.color = npcStatus_IsTalking ? talkingColor : notTalkingColor;
                    }
                }
                yield return new WaitForSeconds(0.5f);
                npcStatusIndicator.color = gapColor;
                yield return new WaitForSeconds(0.5f);

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
            var result = await DW_SDK.InitializeAsync("dev-ffaade38-b11d-4fc1-87e3-81867536512c");

            if (!result)
            {
                Debug.LogError("initialization failed, you should place a sdk object first, then fill in your gameId in the sdk object. 初始化失败，你需要放置一个sdk prefab，然后将你的游戏Id填写到sdk里");
                return;
            }
            _aiChatClient = DW_SDK.Factory.CreateChatClient(); 
            npcSettingField.text = _npcClient.CharacterDesign;


        }

        List<DW_ChatMessage> _selfManagedHistory = new List<DW_ChatMessage>();
        private bool chatClientSetSystem = false;
        async UniTask StandardChat(string _input)
        {
            /* 你需要自行管理AI的历史信息，自行创建一个历史记录，自行操作其中的内容
             * TextGeneration提供较高的自由度
             * You will need to manage Chat AI's history message: creating a history list, and
             * adding, modifying, deleting its content all by yourself.
             * This provided a higher flexibility.
             */
         
            if(!chatClientSetSystem)
            {
                _selfManagedHistory.Add(new DW_ChatMessage()
                {
                    Role = "system",
                    Content = "你扮演《底特律变人》的康纳"
                });
                chatClientSetSystem = true;
            }
            _selfManagedHistory.Add(new DW_ChatMessage()
            {
                Role = "user",
                Content = _input
            });
            var result = await _aiChatClient.TextGenerationAsync(new DW_ChatConfig(_selfManagedHistory)); //对话
            _selfManagedHistory.Add(new DW_ChatMessage()
            {
                Role = "assistant",
                Content = result.Response
            });
            _text.text = result.Response;
            
        }


        async UniTask NpcChat(string _input)
        {
            /*
             * 使用Npc可以更简单地搭建一个对话NPC。
             * 这个类会帮助你管理历史记录，重新设置Npc的系统提示此会替换掉原有的提示词
             * 使得Npc的语风不变的情况下，改变其目标和状态。
             * Npc is an easier way to build a talking npc.
             * This class will help you manage chat history
             * setting system prompt effectively replaces the previous settings,
             * and creating the feeling of npc having different purposes and states.
             */
            var npc = _npcClient;
            var reply = await npc.Talk(_input);
            _text.text = reply;
        }

        async UniTask StandardChatStream(string _input)
        {
            if(!chatClientSetSystem)
            {
                _selfManagedHistory.Add(new DW_ChatMessage()
                {
                    Role = "system",
                    Content = "你扮演《底特律变人》的康纳"
                });
                chatClientSetSystem = true;
            }
            _selfManagedHistory.Add(new DW_ChatMessage()
            {
                Role = "user",
                Content = _input
            });
            await _aiChatClient.TextChatStreamAsync(new DW_ChatStreamConfig(_selfManagedHistory),
                (s) =>
                {
                    var original = _text.text;
                    _text.text = original + s;
                },
                (s) =>
                {
                    _text.text = s; 
                    _selfManagedHistory.Add(new DW_ChatMessage()
                    {
                        Role = "assistant",
                        Content = s
                    });
                });
        }

        async UniTask NpcChatStream(string _input)
        {
            var chat = _npcClient;
            await chat.TalkStream(_input,
                (s) =>
                {
                    var original = _text.text;
                    _text.text = original + s;
                },
                (s) => { _text.text = s; });
        }

    }
}