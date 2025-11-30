using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Events;
using PlayKit_SDK.Provider.AI;

namespace PlayKit_SDK.Public
{
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
