using UnityEngine;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using PlayKit_SDK;
using PlayKit_SDK.Public;

namespace PlayKit_SDK.Example
{
    /// <summary>
    /// Simplified example demonstrating the new schema library system for AI Object Generation
    /// This example shows how to use JObject for maximum flexibility without predefined classes
    /// </summary>
    public class Demo_SimpleObjectGenerationExample : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private PlayKit_SchemaLibrary customSchemaLibrary; // Optional custom library
        [SerializeField] private PlayKit_NPCClient npcClient;
        
        private PlayKit_AIChatClient chatClient;

        private void Start()
        {
            InitializeExample().Forget();
        }

        private async UniTask InitializeExample()
        {
            await PlayKit_SDK.InitializeAsync();
            // Wait for SDK to be ready
            await UniTask.WaitUntil(() => PlayKit_SDK.IsReady());
            
            // Create chat client with structured output capabilities
            chatClient = PlayKit_SDK.Factory.CreateChatClient();
            
            if (chatClient == null)
            {
                Debug.LogError("Failed to create chat client. Make sure SDK is initialized and model name is valid.");
                return;
            }

            // Optionally set custom schema library
            if (customSchemaLibrary != null)
            {
                chatClient.SetSchemaLibrary(customSchemaLibrary);
                Debug.Log("Using custom schema library");
            }
            
            Debug.Log($"Chat Client with Structured Output ready! Available schemas: {string.Join(", ", chatClient.GetAvailableSchemas())}");
        }

        /// <summary>
        /// Example 1: Generate using schema and access fields dynamically
        /// </summary>
        public async void GenerateNPCResponse()
        {
            if (chatClient == null)
            {
                Debug.LogError("Chat client not available");
                return;
            }

            string userMessage = "Hello there, innkeeper! Do you have any rumors about the haunted forest?";
            
            Debug.Log($"Generating NPC response for: '{userMessage}'");

            try
            {
                var response = await chatClient.GenerateStructuredAsync(
                    "NPCResponse", // Schema name
                    userMessage,
                    "You are a friendly innkeeper in a fantasy tavern."
                );

                if (response != null)
                {
                    Debug.Log($"NPC Response Generated (JObject):");
                    
                    // Access fields dynamically - no need for predefined classes!
                    Debug.Log($"Dialogue: {response["dialogue"]?.ToString()}");
                    Debug.Log($"Emotion: {response["emotion"]?.ToString()}");
                    
                    // Handle arrays
                    var actions = response["actions"] as JArray;
                    if (actions != null)
                    {
                        Debug.Log($"Actions: {string.Join(", ", actions.ToObject<string[]>())}");
                    }
                    
                    Debug.Log($"Confidence: {response["confidence"]?.ToString()}%");
                }
                else
                {
                    Debug.LogError("Failed to generate NPC response");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error generating NPC response: {ex.Message}");
            }
        }

        /// <summary>
        /// Example 2: Generate and deserialize to a strongly-typed class (optional)
        /// </summary>
        public async void GenerateQuestWithStrongType()
        {
            if (chatClient == null)
            {
                Debug.LogError("Chat client not available");
                return;
            }

            string questRequest = "Create a quest involving rescuing villagers from bandits in the northern mountains";
            
            Debug.Log($"Generating quest for: '{questRequest}'");

            try
            {
                // Use the generic overload to get a strongly-typed result
                var quest = await chatClient.GenerateStructuredAsync<QuestInfo>(
                    "QuestInfo", // Schema name
                    questRequest,
                    "You are a quest designer creating engaging adventures for players."
                );

                if (quest != null)
                {
                    Debug.Log($"Quest Generated (Strong Type):");
                    Debug.Log($"Title: {quest.title}");
                    Debug.Log($"Description: {quest.description}");
                    Debug.Log($"Objective: {quest.objective}");
                    Debug.Log($"Difficulty: {quest.difficulty}");
                    Debug.Log($"Rewards: {string.Join(", ", quest.rewards)}");
                    Debug.Log($"Main Quest: {quest.isMainQuest}");
                }
                else
                {
                    Debug.LogError("Failed to generate quest");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error generating quest: {ex.Message}");
            }
        }

        /// <summary>
        /// Example 3: Use direct schema JSON without library
        /// </summary>
        public async void GenerateWithDirectSchema()
        {
            if (chatClient == null)
            {
                Debug.LogError("Chat client not available");
                return;
            }

            // Define schema directly as JSON string
            string choiceSchema = @"{
                ""type"": ""object"",
                ""properties"": {
                    ""choice"": {
                        ""type"": ""string"",
                        ""enum"": [""north"", ""south"", ""east"", ""west""],
                        ""description"": ""Direction to go""
                    },
                    ""reasoning"": {
                        ""type"": ""string"",
                        ""description"": ""Why this direction was chosen""
                    }
                },
                ""required"": [""choice"", ""reasoning""]
            }";

            string situation = "The player stands at a crossroads. Which direction should they take?";
            
            Debug.Log($"Generating direction choice for: '{situation}'");

            try
            {
                var result = await chatClient.GenerateStructuredWithSchemaAsync(
                    choiceSchema,
                    situation,
                    "DirectionChoice", // Schema name for logging
                    "You are a dungeon master making strategic choices."
                );

                if (result != null)
                {
                    Debug.Log($"Direction Choice Generated:");
                    Debug.Log($"Choice: {result["choice"]}");
                    Debug.Log($"Reasoning: {result["reasoning"]}");
                }
                else
                {
                    Debug.LogError("Failed to generate direction choice");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error generating direction choice: {ex.Message}");
            }
        }

        /// <summary>
        /// Example 4: Use with NPC Client for structured conversations
        /// </summary>
        public async void TestStructuredNPCConversation()
        {
            if (npcClient == null)
            {
                Debug.LogError("NPC client not available");
                return;
            }

            await UniTask.WaitUntil(() => npcClient.IsReady);

            string playerMessage = "What can you tell me about the magical sword in your shop?";
            
            Debug.Log($"Having structured conversation: '{playerMessage}'");

            try
            {
                // Get JObject response
                var response = await npcClient.TalkStructured(playerMessage, "NPCResponse");

                if (response != null)
                {
                    Debug.Log($"Structured NPC Response (JObject):");
                    Debug.Log($"Dialogue: {response["dialogue"]}");
                    Debug.Log($"Emotion: {response["emotion"]}");
                    
                    var actions = response["actions"] as JArray;
                    if (actions != null)
                    {
                        Debug.Log($"Actions: {string.Join(", ", actions.ToObject<string[]>())}");
                    }
                }
                else
                {
                    Debug.LogError("Failed to get structured NPC response");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in structured NPC conversation: {ex.Message}");
            }
        }

        /// <summary>
        /// Example 5: Use messages for structured generation (conversation context)
        /// </summary>
        public async void GenerateWithMessages()
        {
            if (chatClient == null)
            {
                Debug.LogError("Chat client not available");
                return;
            }

            // Create a conversation with messages using Public.DW_ChatMessage
            var messages = new List<DW_ChatMessage>
            {
                new DW_ChatMessage
                {
                    Role = "system",
                    Content = "You are a helpful shop assistant in a fantasy RPG game."
                },
                new DW_ChatMessage
                {
                    Role = "user", 
                    Content = "What potions do you have for sale?"
                },
                new DW_ChatMessage
                {
                    Role = "assistant",
                    Content = "We have healing potions, mana potions, and strength potions available."
                },
                new DW_ChatMessage
                {
                    Role = "user",
                    Content = "Give me details about your healing potions in a structured format."
                }
            };
            
            Debug.Log("Generating structured response using messages...");

            try
            {
                var response = await chatClient.GenerateStructuredAsync("NPCResponse", messages);

                if (response != null)
                {
                    Debug.Log($"Structured Response from Messages:");
                    Debug.Log($"Dialogue: {response["dialogue"]}");
                    Debug.Log($"Emotion: {response["emotion"]}");
                    
                    var actions = response["actions"] as JArray;
                    if (actions != null)
                    {
                        Debug.Log($"Actions: {string.Join(", ", actions.ToObject<string[]>())}");
                    }
                }
                else
                {
                    Debug.LogError("Failed to generate structured response from messages");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error generating with messages: {ex.Message}");
            }
        }

        /// <summary>
        /// Example 6: Use strongly-typed NPC conversation
        /// </summary>
        public async void TestStronglyTypedNPCConversation()
        {
            if (npcClient == null)
            {
                Debug.LogError("NPC client not available");
                return;
            }

            await UniTask.WaitUntil(() => npcClient.IsReady);

            string playerMessage = "Can you give me a quest to earn some gold?";
            
            Debug.Log($"Requesting quest: '{playerMessage}'");

            try
            {
                // Get strongly-typed response
                var quest = await npcClient.TalkStructured<QuestInfo>(playerMessage, "QuestInfo");

                if (quest != null)
                {
                    Debug.Log($"Quest from NPC:");
                    Debug.Log($"Title: {quest.title}");
                    Debug.Log($"Description: {quest.description}");
                    Debug.Log($"Rewards: {string.Join(", ", quest.rewards)}");
                }
                else
                {
                    Debug.LogError("Failed to get quest from NPC");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in quest conversation: {ex.Message}");
            }
        }

        /// <summary>
        /// Example 7: Test NPC client's new history-aware structured responses
        /// </summary>
        public async void TestNPCStructuredWithHistory()
        {
            if (npcClient == null)
            {
                Debug.LogError("NPC client not available");
                return;
            }

            await UniTask.WaitUntil(() => npcClient.IsReady);

            try
            {
                // First, have a normal conversation to build history
                Debug.Log("Building conversation history...");
                await npcClient.Talk("Hello! I'm new to this town. Can you tell me about the local shops?");
                await npcClient.Talk("What about dangerous areas I should avoid?");
                
                // Now use the new messages-based structured method
                string playerMessage = "Based on our conversation, give me a structured summary of what I should know as a newcomer";
                
                Debug.Log($"Requesting structured response with full history: '{playerMessage}'");

                // Use the new method that automatically includes full conversation history
                var response = await npcClient.TalkStructuredWithHistory(playerMessage, "NPCResponse");

                if (response != null)
                {
                    Debug.Log($"Structured NPC Response with History (JObject):");
                    Debug.Log($"Dialogue: {response["dialogue"]}");
                    Debug.Log($"Emotion: {response["emotion"]}");
                    
                    var actions = response["actions"] as JArray;
                    if (actions != null)
                    {
                        Debug.Log($"Actions: {string.Join(", ", actions.ToObject<string[]>())}");
                    }
                    
                    Debug.Log($"Confidence: {response["confidence"]}%");
                    
                    Debug.Log($"Conversation history length: {npcClient.GetHistoryLength()} messages");
                }
                else
                {
                    Debug.LogError("Failed to get structured NPC response with history");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in TestNPCStructuredWithHistory: {ex.Message}");
            }
        }

        /// <summary>
        /// Example 8: Test new NPC history management methods
        /// </summary>
        public async void TestNPCHistoryManagement()
        {
            if (npcClient == null)
            {
                Debug.LogError("NPC client not available");
                return;
            }

            await UniTask.WaitUntil(() => npcClient.IsReady);

            try
            {
                Debug.Log("=== Testing NPC History Management ===");
                
                // Clear history and start fresh
                npcClient.ClearHistory();
                Debug.Log($"Initial history length: {npcClient.GetHistoryLength()}");
                
                // Manually append some messages
                npcClient.AppendChatMessage("user", "Hello! I'm a new adventurer.");
                npcClient.AppendChatMessage("assistant", "Welcome, brave soul! I am the village elder.");
                npcClient.AppendChatMessage("user", "What can you tell me about the forest?");
                
                Debug.Log($"After manual appends: {npcClient.GetHistoryLength()} messages");
                
                // Have a structured conversation that should extract "talk" field
                var response = await npcClient.TalkStructuredWithHistory(
                    "Give me advice about exploring the forest", 
                    "NPCResponse"
                );
                
                if (response != null)
                {
                    Debug.Log("=== Structured Response ===");
                    Debug.Log($"Full JSON: {response.ToString(Newtonsoft.Json.Formatting.Indented)}");
                    Debug.Log($"Talk field extracted: {response["dialogue"]}"); // Should be in history now
                }
                
                Debug.Log($"History after structured talk: {npcClient.GetHistoryLength()} messages");
                
                // Show full history
                var history = npcClient.GetHistory();
                for (int i = 0; i < history.Length; i++)
                {
                    Debug.Log($"History[{i}]: {history[i].Role} - {history[i].Content}");
                }
                
                // Test reverting messages
                Debug.Log("=== Testing Revert ===");
                int reverted = npcClient.RevertChatMessages(2);
                Debug.Log($"Reverted {reverted} messages. New length: {npcClient.GetHistoryLength()}");
                
                // Show history after revert
                var historyAfterRevert = npcClient.GetHistory();
                Debug.Log("History after revert:");
                for (int i = 0; i < historyAfterRevert.Length; i++)
                {
                    Debug.Log($"History[{i}]: {historyAfterRevert[i].Role} - {historyAfterRevert[i].Content}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in TestNPCHistoryManagement: {ex.Message}");
            }
        }

        /// <summary>
        /// Simple UI for testing
        /// </summary>
        private void OnGUI()
        {
            if (!PlayKit_SDK.IsReady()) return;

            GUILayout.BeginArea(new Rect(10, 10, 400, 300));
            GUILayout.Label("Simplified AI Object Generation Examples");
            
            if (GUILayout.Button("1. Generate NPC Response (JObject)"))
            {
                GenerateNPCResponse();
            }
            
            if (GUILayout.Button("2. Generate Quest (Strong Type)"))
            {
                GenerateQuestWithStrongType();
            }
            
            if (GUILayout.Button("3. Generate with Direct Schema"))
            {
                GenerateWithDirectSchema();
            }
            
            if (GUILayout.Button("4. Structured NPC Conversation (JObject)"))
            {
                TestStructuredNPCConversation();
            }
            
            if (GUILayout.Button("5. Generate with Messages"))
            {
                GenerateWithMessages();
            }
            
            if (GUILayout.Button("6. Strongly-Typed NPC Conversation"))
            {
                TestStronglyTypedNPCConversation();
            }
            
            if (GUILayout.Button("7. NPC Structured with History (Messages)"))
            {
                TestNPCStructuredWithHistory();
            }
            
            if (GUILayout.Button("8. Test NPC History Management"))
            {
                TestNPCHistoryManagement();
            }
            
            GUILayout.EndArea();
        }
    }

    // Optional: Define classes for strong typing (only when needed)
    [System.Serializable]
    public class QuestInfo
    {
        public string title;
        public string description;
        public string objective;
        public int difficulty;
        public string[] rewards;
        public bool isMainQuest;
    }
}