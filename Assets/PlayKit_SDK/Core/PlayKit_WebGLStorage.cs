using System.Runtime.InteropServices;
using UnityEngine;

namespace PlayKit_SDK.Core
{
    public static class PlayKit_WebGLStorage
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PlayKit_SetLocalStorage(string key, string value);

        [DllImport("__Internal")]
        private static extern string PlayKit_GetLocalStorage(string key);

        [DllImport("__Internal")]
        private static extern void PlayKit_RemoveLocalStorage(string key);

        [DllImport("__Internal")]
        private static extern bool PlayKit_HasLocalStorageKey(string key);
#endif

        /// <summary>
        /// 设置localStorage值
        /// </summary>
        public static void SetItem(string key, string value)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PlayKit_SetLocalStorage(key, value);
#else
            // 非WebGL平台使用PlayerPrefs作为备用
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
#endif
        }

        /// <summary>
        /// 获取localStorage值
        /// </summary>
        public static string GetItem(string key, string defaultValue = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string value = PlayKit_GetLocalStorage(key);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
#else
            // 非WebGL平台使用PlayerPrefs作为备用
            return PlayerPrefs.GetString(key, defaultValue);
#endif
        }

        /// <summary>
        /// 删除localStorage项
        /// </summary>
        public static void RemoveItem(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PlayKit_RemoveLocalStorage(key);
#else
            // 非WebGL平台使用PlayerPrefs作为备用
            PlayerPrefs.DeleteKey(key);
#endif
        }

        /// <summary>
        /// 检查localStorage是否有指定键
        /// </summary>
        public static bool HasKey(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return PlayKit_HasLocalStorageKey(key);
#else
            // 非WebGL平台使用PlayerPrefs作为备用
            return PlayerPrefs.HasKey(key);
#endif
        }
    }
}