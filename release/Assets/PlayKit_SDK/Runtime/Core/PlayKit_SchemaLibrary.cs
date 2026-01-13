using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace PlayKit_SDK
{
    /// <summary>
    /// ScriptableObject that contains a collection of JSON schemas for AI object generation
    /// This allows designers to manage all schemas in one place
    /// </summary>
    [CreateAssetMenu(fileName = "SchemaLibrary", menuName = "PlayKit SDK/Schema Library")]
    public class PlayKit_SchemaLibrary : ScriptableObject
    {
        [System.Serializable]
        public class SchemaEntry
        {
            [SerializeField] public string name;
            [SerializeField, TextArea(3, 5)] public string description;
            [SerializeField, TextArea(10, 20)] public string jsonSchema;

            /// <summary>
            /// Validate that this schema entry has valid JSON
            /// </summary>
            public bool IsValid()
            {
                if (string.IsNullOrEmpty(name))
                {
                    Debug.LogError($"Schema entry missing name");
                    return false;
                }

                if (string.IsNullOrEmpty(jsonSchema))
                {
                    Debug.LogError($"Schema '{name}' missing JSON schema");
                    return false;
                }

                try
                {
                    JObject.Parse(jsonSchema);
                    return true;
                }
                catch (JsonException ex)
                {
                    Debug.LogError($"Schema '{name}' has invalid JSON: {ex.Message}");
                    return false;
                }
            }

            /// <summary>
            /// Get the parsed JSON schema as JObject
            /// </summary>
            public JObject GetParsedSchema()
            {
                if (!IsValid()) return null;
                
                try
                {
                    return JObject.Parse(jsonSchema);
                }
                catch (JsonException ex)
                {
                    Debug.LogError($"Failed to parse schema '{name}': {ex.Message}");
                    return null;
                }
            }
        }

        [Header("Schema Collection")]
        [SerializeField] private SchemaEntry[] schemas = new SchemaEntry[0];

        /// <summary>
        /// Get all schema entries
        /// </summary>
        public SchemaEntry[] GetAllSchemas() => schemas;

        /// <summary>
        /// Find a schema by name
        /// </summary>
        /// <param name="schemaName">Name of the schema to find</param>
        /// <returns>Schema entry or null if not found</returns>
        public SchemaEntry FindSchema(string schemaName)
        {
            if (string.IsNullOrEmpty(schemaName)) return null;
            
            return schemas?.FirstOrDefault(s => s.name == schemaName);
        }

        /// <summary>
        /// Get the JSON schema string for a given schema name
        /// </summary>
        /// <param name="schemaName">Name of the schema</param>
        /// <returns>JSON schema string or null if not found</returns>
        public string GetSchemaJson(string schemaName)
        {
            var schema = FindSchema(schemaName);
            return schema?.jsonSchema;
        }

        /// <summary>
        /// Get the parsed JSON schema as JObject
        /// </summary>
        /// <param name="schemaName">Name of the schema</param>
        /// <returns>JObject representing the schema or null if not found/invalid</returns>
        public JObject GetParsedSchema(string schemaName)
        {
            var schema = FindSchema(schemaName);
            return schema?.GetParsedSchema();
        }

        /// <summary>
        /// Check if a schema exists and is valid
        /// </summary>
        /// <param name="schemaName">Name of the schema</param>
        /// <returns>True if schema exists and is valid</returns>
        public bool HasValidSchema(string schemaName)
        {
            var schema = FindSchema(schemaName);
            return schema?.IsValid() == true;
        }

        /// <summary>
        /// Get all valid schema names
        /// </summary>
        /// <returns>Array of schema names that are valid</returns>
        public string[] GetValidSchemaNames()
        {
            return schemas?.Where(s => s.IsValid()).Select(s => s.name).ToArray() ?? new string[0];
        }

        /// <summary>
        /// Add a new schema entry (Editor only)
        /// </summary>
        /// <param name="name">Schema name</param>
        /// <param name="description">Schema description</param>
        /// <param name="jsonSchema">JSON schema string</param>
        public void AddSchema(string name, string description, string jsonSchema)
        {
#if UNITY_EDITOR
            var newEntry = new SchemaEntry
            {
                name = name,
                description = description,
                jsonSchema = jsonSchema
            };

            var schemaList = new List<SchemaEntry>(schemas ?? new SchemaEntry[0]);
            schemaList.Add(newEntry);
            schemas = schemaList.ToArray();

            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// Remove a schema by name (Editor only)
        /// </summary>
        /// <param name="name">Name of schema to remove</param>
        public void RemoveSchema(string name)
        {
#if UNITY_EDITOR
            if (schemas == null) return;
            
            schemas = schemas.Where(s => s.name != name).ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Validate all schemas in the library (Editor only)
        /// </summary>
        private void OnValidate()
        {
            if (schemas == null) return;

            foreach (var schema in schemas)
            {
                if (!string.IsNullOrEmpty(schema.jsonSchema))
                {
                    schema.IsValid(); // This will log errors if invalid
                }
            }
        }
#endif
    }
}