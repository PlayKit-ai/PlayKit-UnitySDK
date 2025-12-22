using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Events;
using PlayKit_SDK.Provider.AI;

namespace PlayKit_SDK.Public
{
    /// <summary>
    /// Interface for implementing NPC action handlers (sync version).
    /// ActionsModule auto-discovers handlers on same GameObject and children.
    ///
    /// Recommended: Inherit from NpcActionHandlerBase for Inspector support.
    /// Only implement this interface directly if you need custom serialization.
    /// </summary>
    public interface INpcActionHandler
    {
        /// <summary>
        /// List of action definitions this handler can process.
        /// </summary>
        List<NpcAction> ActionDefinitions { get; }

        /// <summary>
        /// Execute the action synchronously.
        /// Use args.ActionName to determine which action was triggered.
        /// </summary>
        /// <param name="args">Action call arguments from AI</param>
        /// <returns>Result string to report back to NPC, or null for auto "success"</returns>
        string Execute(NpcActionCallArgs args);
    }

    /// <summary>
    /// Async version of INpcActionHandler for actions that need async operations.
    /// Use this when your action involves API calls, loading assets, or other async work.
    ///
    /// Recommended: Inherit from NpcActionHandlerAsyncBase for Inspector support.
    /// Only implement this interface directly if you need custom serialization.
    /// </summary>
    public interface INpcActionHandlerAsync
    {
        /// <summary>
        /// List of action definitions this handler can process.
        /// </summary>
        List<NpcAction> ActionDefinitions { get; }

        /// <summary>
        /// Execute the action asynchronously.
        /// Use args.ActionName to determine which action was triggered.
        /// </summary>
        /// <param name="args">Action call arguments from AI</param>
        /// <returns>Result string to report back to NPC, or null for auto "success"</returns>
        UniTask<string> ExecuteAsync(NpcActionCallArgs args);
    }

    /// <summary>
    /// Abstract base class for sync action handlers.
    /// Inherit from this class for quick setup with Inspector support.
    /// </summary>
    /// <example>
    /// <code>
    /// public class ShopActionHandler : NpcActionHandlerBase
    /// {
    ///     protected override void Reset()
    ///     {
    ///         base.Reset();
    ///         _actionDefinitions = new List&lt;NpcAction&gt;
    ///         {
    ///             new NpcAction("openShop", "打开商店"),
    ///             new NpcAction("buyItem", "购买物品").AddStringParam("itemId", "物品ID")
    ///         };
    ///     }
    ///
    ///     public override string Execute(NpcActionCallArgs args)
    ///     {
    ///         switch (args.ActionName)
    ///         {
    ///             case "openShop": return "商店已打开";
    ///             case "buyItem": return Buy(args.GetString("itemId"));
    ///             default: return null;
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    public abstract class NpcActionHandlerBase : MonoBehaviour, INpcActionHandler
    {
        [SerializeField]
        [Tooltip("List of actions this handler can process")]
        protected List<NpcAction> _actionDefinitions = new List<NpcAction>();

        public List<NpcAction> ActionDefinitions => _actionDefinitions;

        public abstract string Execute(NpcActionCallArgs args);

        protected virtual void Reset()
        {
            // Override in subclass to set default actions
        }
    }

    /// <summary>
    /// Abstract base class for async action handlers.
    /// Inherit from this class for quick setup with Inspector support.
    /// </summary>
    /// <example>
    /// <code>
    /// public class DataActionHandler : NpcActionHandlerAsyncBase
    /// {
    ///     protected override void Reset()
    ///     {
    ///         base.Reset();
    ///         _actionDefinitions = new List&lt;NpcAction&gt;
    ///         {
    ///             new NpcAction("fetchData", "获取数据").AddStringParam("dataId", "数据ID")
    ///         };
    ///     }
    ///
    ///     public override async UniTask&lt;string&gt; ExecuteAsync(NpcActionCallArgs args)
    ///     {
    ///         switch (args.ActionName)
    ///         {
    ///             case "fetchData":
    ///                 var data = await FetchData(args.GetString("dataId"));
    ///                 return $"获取成功: {data}";
    ///             default: return null;
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    public abstract class NpcActionHandlerAsyncBase : MonoBehaviour, INpcActionHandlerAsync
    {
        [SerializeField]
        [Tooltip("List of actions this handler can process")]
        protected List<NpcAction> _actionDefinitions = new List<NpcAction>();

        public List<NpcAction> ActionDefinitions => _actionDefinitions;

        public abstract UniTask<string> ExecuteAsync(NpcActionCallArgs args);

        protected virtual void Reset()
        {
            // Override in subclass to set default actions
        }
    }

    /// <summary>
    /// Action parameter type enumeration
    /// </summary>
    public enum NpcActionParamType
    {
        String,
        Number,
        Boolean,
        StringEnum
    }

    /// <summary>
    /// Single action parameter definition (Inspector serializable)
    /// </summary>
    [System.Serializable]
    public class NpcActionParameter
    {
        [Tooltip("Parameter name (English, camelCase)")]
        public string name;

        [Tooltip("Parameter description (for AI to understand)")]
        public string description;

        [Tooltip("Parameter type")]
        public NpcActionParamType type = NpcActionParamType.String;

        [Tooltip("Is this parameter required?")]
        public bool required = true;

        [Tooltip("Enum options (only for StringEnum type)")]
        public string[] enumOptions;
    }

    /// <summary>
    /// NPC Action definition (serializable, supports Inspector and runtime modification)
    /// </summary>
    [System.Serializable]
    public class NpcAction
    {
        [Tooltip("Action name (English, camelCase, e.g., openShop)")]
        public string actionName;

        [TextArea(2, 4)]
        [Tooltip("Action description (for AI to understand when to trigger)")]
        public string description;

        [Tooltip("Parameter list for this action")]
        public List<NpcActionParameter> parameters = new List<NpcActionParameter>();

        [Tooltip("Is this action enabled?")]
        public bool enabled = true;

        // ===== Constructors =====

        public NpcAction() { }

        public NpcAction(string name, string desc)
        {
            actionName = name;
            description = desc;
        }

        // ===== Fluent API for adding parameters =====

        public NpcAction AddStringParam(string name, string desc, bool required = true)
        {
            parameters.Add(new NpcActionParameter
            {
                name = name,
                description = desc,
                type = NpcActionParamType.String,
                required = required
            });
            return this;
        }

        public NpcAction AddNumberParam(string name, string desc, bool required = true)
        {
            parameters.Add(new NpcActionParameter
            {
                name = name,
                description = desc,
                type = NpcActionParamType.Number,
                required = required
            });
            return this;
        }

        public NpcAction AddBoolParam(string name, string desc, bool required = true)
        {
            parameters.Add(new NpcActionParameter
            {
                name = name,
                description = desc,
                type = NpcActionParamType.Boolean,
                required = required
            });
            return this;
        }

        public NpcAction AddEnumParam(string name, string desc, string[] options, bool required = true)
        {
            parameters.Add(new NpcActionParameter
            {
                name = name,
                description = desc,
                type = NpcActionParamType.StringEnum,
                enumOptions = options,
                required = required
            });
            return this;
        }

        /// <summary>
        /// Convert to API request format (ChatTool)
        /// </summary>
        internal ChatTool ToTool()
        {
            return new ChatTool
            {
                Type = "function",
                Function = new ChatToolFunction
                {
                    Name = actionName,
                    Description = description,
                    Parameters = BuildJsonSchema()
                }
            };
        }

        private JObject BuildJsonSchema()
        {
            var properties = new JObject();
            var requiredArray = new JArray();

            foreach (var param in parameters)
            {
                var propDef = new JObject { ["description"] = param.description };

                switch (param.type)
                {
                    case NpcActionParamType.String:
                        propDef["type"] = "string";
                        break;
                    case NpcActionParamType.Number:
                        propDef["type"] = "number";
                        break;
                    case NpcActionParamType.Boolean:
                        propDef["type"] = "boolean";
                        break;
                    case NpcActionParamType.StringEnum:
                        propDef["type"] = "string";
                        if (param.enumOptions != null && param.enumOptions.Length > 0)
                        {
                            propDef["enum"] = new JArray(param.enumOptions);
                        }
                        break;
                }

                properties[param.name] = propDef;
                if (param.required)
                {
                    requiredArray.Add(param.name);
                }
            }

            return new JObject
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = requiredArray
            };
        }
    }

    /// <summary>
    /// Action call arguments passed to the callback
    /// </summary>
    [System.Serializable]
    public class NpcActionCallArgs
    {
        public string ActionName { get; private set; }
        public string CallId { get; private set; }
        private Dictionary<string, object> _values;

        public NpcActionCallArgs(ChatToolCall toolCall)
        {
            ActionName = toolCall.Function?.Name ?? "";
            CallId = toolCall.Id ?? "";
            _values = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(toolCall.Function?.Arguments))
            {
                try
                {
                    var parsed = JObject.Parse(toolCall.Function.Arguments);
                    foreach (var prop in parsed.Properties())
                    {
                        _values[prop.Name] = prop.Value.ToObject<object>();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NpcActionCallArgs] Failed to parse arguments: {ex.Message}");
                }
            }
        }

        public T Get<T>(string paramName)
        {
            if (_values.TryGetValue(paramName, out var value))
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default;
                }
            }
            return default;
        }

        public string GetString(string paramName) => Get<string>(paramName) ?? "";
        public float GetNumber(string paramName) => Get<float>(paramName);
        public int GetInt(string paramName) => Get<int>(paramName);
        public bool GetBool(string paramName) => Get<bool>(paramName);

        public bool HasParam(string paramName) => _values.ContainsKey(paramName);

        public IEnumerable<string> GetParamNames() => _values.Keys;
    }

    /// <summary>
    /// NPC action call result
    /// </summary>
    [System.Serializable]
    public class NpcActionCall
    {
        public string Id { get; set; }
        public string ActionName { get; set; }
        public JObject Arguments { get; set; }
    }

    /// <summary>
    /// Response from NPC with actions
    /// </summary>
    [System.Serializable]
    public class NpcActionResponse
    {
        public string Text { get; set; }
        public List<NpcActionCall> ActionCalls { get; set; } = new List<NpcActionCall>();
        public bool HasActions => ActionCalls != null && ActionCalls.Count > 0;
    }

    /// <summary>
    /// NPC Action binding (inline definition + UnityEvent callback)
    /// </summary>
    [System.Serializable]
    public class NpcActionBinding
    {
        [Tooltip("Action definition (editable in Inspector)")]
        public NpcAction action = new NpcAction();

        [Tooltip("Called when this action is triggered")]
        public UnityEvent<NpcActionCallArgs> onTriggered = new UnityEvent<NpcActionCallArgs>();
    }
}
