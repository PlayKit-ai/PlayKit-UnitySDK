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
    /// <summary>
    /// Manages local token storage for the PlayKit SDK.
    /// Tokens are stored locally per-game (not shared across games).
    /// </summary>
    public static class PlayKit_LocalSharedToken
    {
    private const string TokenFileName = "playkit_token.txt";

#if !UNITY_WEBGL
    // base64 转换成真正的 key/iv (仅在非 WebGL 平台使用)
    private static readonly byte[] AesKey = Convert.FromBase64String("/wu4uTqdUBpCIhutfM50qQ=="); // 16字节
    private static readonly byte[] AesIV  = Convert.FromBase64String("pCkXFJR0Ahco+YKvkNRq2Q=="); // 16字节
#endif

#if !UNITY_WEBGL
    /// <summary>
    /// Gets the local token file path using Application.persistentDataPath.
    /// Tokens are stored per-game, not shared across games.
    /// </summary>
    private static string GetSharedFilePath()
    {
        string folderPath = Application.persistentDataPath;

        if (string.IsNullOrEmpty(folderPath))
        {
            return null;
        }

        return Path.Combine(folderPath, TokenFileName);
    }
#endif

    public static void SaveToken(string token)
    {
        try
        {
#if UNITY_WEBGL
            // WebGL: Save token to localStorage
            PlayKit_WebGLStorage.SetItem("playkit_token", token);
            Debug.Log("[PlayKit SDK] Token saved to localStorage");
#else
            // Other platforms: Encrypt and store locally
            byte[] encrypted = EncryptStringToBytes_Aes(token, AesKey, AesIV);
            var path = GetSharedFilePath();
            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllBytes(path, encrypted);
            Debug.Log($"[PlayKit SDK] Token saved (encrypted) to: {path}");
#endif
        }
        catch (Exception e)
        {
            Debug.LogError("[PlayKit SDK] Failed to save token: " + e.Message);
        }
    }

    public static string LoadToken()
    {
        try
        {
#if UNITY_WEBGL
            // WebGL: Load token from localStorage
            if (PlayKit_WebGLStorage.HasKey("playkit_token"))
            {
                string token = PlayKit_WebGLStorage.GetItem("playkit_token");
                return token;
            }
            else
            {
                Debug.LogWarning("[PlayKit SDK] Token not found in localStorage.");
                return null;
            }
#else
            // Other platforms: Read and decrypt
            string path = GetSharedFilePath();
            if (string.IsNullOrEmpty(path)) return null;

            if (File.Exists(path))
            {
                byte[] encrypted = File.ReadAllBytes(path);
                var token = DecryptStringFromBytes_Aes(encrypted, AesKey, AesIV);
                Debug.Log($"[PlayKit SDK] Token loaded from: {path}");
                return token;
            }
            else
            {
                return null;
            }
#endif
        }
        catch (Exception e)
        {
            Debug.LogError("[PlayKit SDK] Failed to load token: " + e.Message);
            return null;
        }
    }

    public static void EraseToken()
    {
        try
        {
#if UNITY_WEBGL
            PlayKit_WebGLStorage.RemoveItem("playkit_token");
            Debug.Log("[PlayKit SDK] Token erased from localStorage.");
#else
            string path = GetSharedFilePath();
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log("[PlayKit SDK] Token erased.");
            }
#endif
        }
        catch (Exception e)
        {
            Debug.LogError("[PlayKit SDK] Failed to erase token: " + e.Message);
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
