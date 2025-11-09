using System;
using System.IO;
#if !UNITY_WEBGL
using System.Security.Cryptography;
#endif
using System.Text;
using UnityEngine;
using PlayKit_SDK.Core;

namespace PlayKit_SDK.Auth
{
    public static class PlayKit_LocalSharedToken
    {
    private const string TokenFileName = "shared_token.txt";
    private const string SharedFolderName = "DeveloperWorks_SDK";

#if !UNITY_WEBGL
    // base64 转换成真正的 key/iv (仅在非 WebGL 平台使用)
    private static readonly byte[] AesKey = Convert.FromBase64String("/wu4uTqdUBpCIhutfM50qQ=="); // 16字节
    private static readonly byte[] AesIV  = Convert.FromBase64String("pCkXFJR0Ahco+YKvkNRq2Q=="); // 16字节
#endif

#if !UNITY_WEBGL
    private static string GetSharedFilePath()
    {
        string folderPath = "";

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); // %AppData%
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library/Application Support");
#else
        folderPath = Application.persistentDataPath;
#endif

        if (string.IsNullOrEmpty(folderPath))
        {
            return null;
        }

        // 使用DeveloperWorks SDK专用的跨游戏共享文件夹
        string fullPath = Path.Combine(folderPath, SharedFolderName);
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }

        return Path.Combine(fullPath, TokenFileName);
    }
#endif

    public static void SaveToken(string token)
    {
        try
        {
#if UNITY_WEBGL
            // WebGL：不保存token，只读取
            Debug.Log("WebGL版本不保存token到localStorage");
            return;
#else
            // 其他平台：加密后存储到跨游戏共享位置
            byte[] encrypted = EncryptStringToBytes_Aes(token, AesKey, AesIV);
            var path = GetSharedFilePath();
            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllBytes(path, encrypted);
            Debug.Log($"Token saved (encrypted) to shared location: {Path.GetDirectoryName(path)}");
#endif
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to save token: " + e.Message);
        }
    }

    public static string LoadToken()
    {
        try
        {
#if UNITY_WEBGL
            // WebGL：从localStorage读取，不解密
            if (DW_WebGLStorage.HasKey("shared_token"))
            {
                string token = DW_WebGLStorage.GetItem("shared_token");
                return token;
            }
            else
            {
                Debug.LogWarning("Token not found in localStorage.");
                return null;
            }
#else
            // 其他平台：读取并解密
            string path = GetSharedFilePath();
            if (string.IsNullOrEmpty(path)) return null;

            if (File.Exists(path))
            {
                byte[] encrypted = File.ReadAllBytes(path);
                var token = DecryptStringFromBytes_Aes(encrypted, AesKey, AesIV);
                Debug.Log($"Token loaded from shared location: {Path.GetDirectoryName(path)}");
                return token;
            }
            else
            {
                Debug.LogWarning($"Token file not found at shared location: {path}");
                return null;
            }
#endif
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to load token: " + e.Message);
            return null;
        }
    }

    public static void EraseToken()
    {
        try
        {
#if UNITY_WEBGL
            DW_WebGLStorage.RemoveItem("shared_token");
            Debug.Log("Token erased from localStorage.");
#else
            string path = GetSharedFilePath();
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log("Token erased.");
            }
#endif
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to erase token: " + e.Message);
        }
    }

#if !UNITY_WEBGL
    // === AES 加密方法 (仅在非 WebGL 平台使用) ===
    private static byte[] EncryptStringToBytes_Aes(string plainText, byte[] key, byte[] iv)
    {
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = key;
            aesAlg.IV  = iv;

            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
            using MemoryStream msEncrypt = new MemoryStream();
            using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
            {
                swEncrypt.Write(plainText);
            }
            return msEncrypt.ToArray();
        }
    }

    private static string DecryptStringFromBytes_Aes(byte[] cipherText, byte[] key, byte[] iv)
    {
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = key;
            aesAlg.IV  = iv;

            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
            using MemoryStream msDecrypt = new MemoryStream(cipherText);
            using CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using StreamReader srDecrypt = new StreamReader(csDecrypt);
            return srDecrypt.ReadToEnd();
        }
    }
#endif
    }
}
