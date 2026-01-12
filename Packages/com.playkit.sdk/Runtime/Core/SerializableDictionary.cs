using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayKit_SDK
{
    /// <summary>
    /// Serializable dictionary for Unity Inspector.
    /// Unity cannot serialize Dictionary directly, so we use parallel lists.
    /// </summary>
    [Serializable]
    public class SerializableDictionary<TKey, TValue>
    {
        [SerializeField] private List<TKey> keys = new List<TKey>();
        [SerializeField] private List<TValue> values = new List<TValue>();

        // Runtime cache for fast lookup
        private Dictionary<TKey, TValue> _dictionary;
        private bool _isDirty = true;

        /// <summary>
        /// Get the underlying dictionary (cached for performance)
        /// </summary>
        private Dictionary<TKey, TValue> Dictionary
        {
            get
            {
                if (_isDirty || _dictionary == null)
                {
                    RebuildDictionary();
                }
                return _dictionary;
            }
        }

        private void RebuildDictionary()
        {
            _dictionary = new Dictionary<TKey, TValue>();
            for (int i = 0; i < keys.Count && i < values.Count; i++)
            {
                if (keys[i] != null && !_dictionary.ContainsKey(keys[i]))
                {
                    _dictionary[keys[i]] = values[i];
                }
            }
            _isDirty = false;
        }

        /// <summary>
        /// Get or set value by key
        /// </summary>
        public TValue this[TKey key]
        {
            get
            {
                if (Dictionary.TryGetValue(key, out TValue value))
                {
                    return value;
                }
                throw new KeyNotFoundException($"Key '{key}' not found in SerializableDictionary");
            }
            set
            {
                int index = keys.IndexOf(key);
                if (index >= 0)
                {
                    values[index] = value;
                }
                else
                {
                    keys.Add(key);
                    values.Add(value);
                }
                _isDirty = true;
            }
        }

        /// <summary>
        /// Try to get value by key
        /// </summary>
        public bool TryGetValue(TKey key, out TValue value)
        {
            return Dictionary.TryGetValue(key, out value);
        }

        /// <summary>
        /// Get value or default if key doesn't exist
        /// </summary>
        public TValue GetValueOrDefault(TKey key, TValue defaultValue = default)
        {
            if (Dictionary.TryGetValue(key, out TValue value))
            {
                return value;
            }
            return defaultValue;
        }

        /// <summary>
        /// Check if key exists
        /// </summary>
        public bool ContainsKey(TKey key)
        {
            return Dictionary.ContainsKey(key);
        }

        /// <summary>
        /// Add or update a key-value pair
        /// </summary>
        public void Set(TKey key, TValue value)
        {
            this[key] = value;
        }

        /// <summary>
        /// Remove a key-value pair
        /// </summary>
        public bool Remove(TKey key)
        {
            int index = keys.IndexOf(key);
            if (index >= 0)
            {
                keys.RemoveAt(index);
                values.RemoveAt(index);
                _isDirty = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clear all entries
        /// </summary>
        public void Clear()
        {
            keys.Clear();
            values.Clear();
            _isDirty = true;
        }

        /// <summary>
        /// Get all keys
        /// </summary>
        public IEnumerable<TKey> Keys => Dictionary.Keys;

        /// <summary>
        /// Get all values
        /// </summary>
        public IEnumerable<TValue> Values => Dictionary.Values;

        /// <summary>
        /// Get count of entries
        /// </summary>
        public int Count => keys.Count;

        /// <summary>
        /// Convert to regular Dictionary for easy iteration
        /// </summary>
        public Dictionary<TKey, TValue> ToDictionary()
        {
            return new Dictionary<TKey, TValue>(Dictionary);
        }

        /// <summary>
        /// Initialize from a regular Dictionary
        /// </summary>
        public void FromDictionary(Dictionary<TKey, TValue> dictionary)
        {
            Clear();
            if (dictionary != null)
            {
                foreach (var kvp in dictionary)
                {
                    keys.Add(kvp.Key);
                    values.Add(kvp.Value);
                }
            }
            _isDirty = true;
        }
    }
}
