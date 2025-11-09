using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayKit_SDK.Provider.AI
{
    /// <summary>
    /// Provider for the platform audio transcription endpoint (/ai/{gameId}/v1/audio/transcriptions)
    /// </summary>
    internal class AITranscriptionProvider : ITranscriptionProvider
    {
        private readonly Auth.PlayKit_AuthManager _authManager;
        // private readonly bool _useOversea;

        public AITranscriptionProvider(Auth.PlayKit_AuthManager authManager, bool useOversea = false)
        {
            _authManager = authManager;
            // _useOversea = useOversea;
        }

        private string GetTranscriptionUrl()
        {
            if (_authManager == null || string.IsNullOrEmpty(_authManager.gameId))
            {
                throw new InvalidOperationException("PublishableKey (GameId) is not available from AuthManager.");
            }

            // if (_useOversea)
            // {
            //     return $"https://dwoversea.agentlandlab.com/ai/{_authManager.gameId}/v1/audio/transcriptions";
            // }
            return $"https://playkit.agentlandlab.com/ai/{_authManager.gameId}/v1/audio/transcriptions";
        }

        private string GetAuthToken()
        {
            if (_authManager == null || string.IsNullOrEmpty(_authManager.AuthToken))
            {
                throw new InvalidOperationException("Authentication token is not available.");
            }
            return _authManager.AuthToken;
        }

        public async UniTask<TranscriptionResponse> TranscribeAsync(
            TranscriptionRequest request,
            CancellationToken cancellationToken = default)
        {
            // Serialize request to JSON
            var json = JsonConvert.SerializeObject(request, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            using (var webRequest = new UnityWebRequest(GetTranscriptionUrl(), "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(new UTF8Encoding().GetBytes(json));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", $"Bearer {GetAuthToken()}");

                try
                {
                    await webRequest.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    Debug.LogError($"[AITranscriptionProvider] API request failed: {ex.Message}");
                    return null;
                }

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[AITranscriptionProvider] API Error: {webRequest.responseCode} - {webRequest.error}\n{webRequest.downloadHandler.text}");
                    return null;
                }

                // Parse response
                return JsonConvert.DeserializeObject<TranscriptionResponse>(webRequest.downloadHandler.text);
            }
        }
    }
}
