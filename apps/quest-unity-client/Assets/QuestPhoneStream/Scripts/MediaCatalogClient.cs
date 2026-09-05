using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace QuestPhoneStream
{
    public sealed class MediaCatalogClient : MonoBehaviour
    {
        public string baseUrl = "http://192.168.1.11:8788";
        public int timeoutSeconds = 10;

        public IEnumerator GetMedia(Action<List<MediaItemDto>, string> completed)
        {
            using (var request = UnityWebRequest.Get((baseUrl ?? string.Empty).TrimEnd('/') + "/v1/media"))
            {
                request.timeout = timeoutSeconds;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    completed?.Invoke(null, request.error ?? "Media catalog request failed");
                    yield break;
                }
                try { completed?.Invoke(MediaCatalogJson.Parse(request.downloadHandler.text), null); }
                catch (Exception error) { completed?.Invoke(null, error.Message); }
            }
        }

        public IEnumerator RequestPlayToken(string id, Action<string, string> completed)
        {
            using (var request = new UnityWebRequest(MediaUrlBuilder.PlayToken(baseUrl, id), UnityWebRequest.kHttpVerbPOST))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = timeoutSeconds;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    completed?.Invoke(null, request.error ?? "Play token request failed");
                    yield break;
                }
                try
                {
                    var token = JsonUtility.FromJson<PlayTokenDto>(request.downloadHandler.text);
                    if (token == null || string.IsNullOrEmpty(token.token)) throw new InvalidOperationException("Missing play token");
                    completed?.Invoke(token.token, null);
                }
                catch (Exception error) { completed?.Invoke(null, error.Message); }
            }
        }

        [Serializable]
        private sealed class PlayTokenDto
        {
            public string token;
        }

        public string BuildContentUrl(string id, string capability) => MediaUrlBuilder.Content(baseUrl, id, capability);
    }
}
