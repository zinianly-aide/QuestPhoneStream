using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace QuestPhoneStream
{
    [Serializable] public sealed class AiVisionBoundingBox { public float x; public float y; public float width; public float height; }
    [Serializable] public sealed class AiVisionObject { public string label; public AiVisionBoundingBox bbox; public float confidence; }
    [Serializable] public sealed class AiVisionAction { public string type; public string label; public AiVisionBoundingBox bbox; public float confidence; }
    [Serializable]
    public sealed class AiVisionResult
    {
        public string text;
        public AiVisionObject[] objects = Array.Empty<AiVisionObject>();
        public AiVisionAction[] actions = Array.Empty<AiVisionAction>();
    }

    [Serializable] internal sealed class OpenAiImageUrl { public string url; }
    [Serializable] internal sealed class OpenAiContentPart { public string type; public string text; public OpenAiImageUrl image_url; }
    [Serializable] internal sealed class OpenAiMessage { public string role; public OpenAiContentPart[] content; }
    [Serializable] internal sealed class OpenAiResponseFormat { public string type = "json_object"; }
    [Serializable]
    internal sealed class OpenAiVisionRequest
    {
        public string model;
        public OpenAiMessage[] messages;
        public OpenAiResponseFormat response_format = new OpenAiResponseFormat();
        public float temperature = 0f;
    }
    [Serializable] internal sealed class OpenAiResponseMessage { public string content; }
    [Serializable] internal sealed class OpenAiChoice { public OpenAiResponseMessage message; }
    [Serializable] internal sealed class OpenAiVisionResponse { public OpenAiChoice[] choices; }

    public static class QuestAiResponseParser
    {
        public static bool TryParseTransportResponse(string json, out AiVisionResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(json)) return false;

            var structured = NormalizeJson(json);
            if (LooksStructured(structured) && TryParseStructured(structured, out result)) return true;

            try
            {
                var transport = JsonUtility.FromJson<OpenAiVisionResponse>(json);
                var content = transport?.choices != null && transport.choices.Length > 0 ? transport.choices[0]?.message?.content : null;
                if (string.IsNullOrWhiteSpace(content)) return false;
                return TryParseStructured(NormalizeJson(content), out result);
            }
            catch { return false; }
        }

        public static bool TryParseStructured(string json, out AiVisionResult result)
        {
            result = null;
            try
            {
                var parsed = JsonUtility.FromJson<AiVisionResult>(NormalizeJson(json));
                if (parsed == null) return false;
                parsed.text = parsed.text ?? string.Empty;
                parsed.objects = parsed.objects ?? Array.Empty<AiVisionObject>();
                parsed.actions = parsed.actions ?? Array.Empty<AiVisionAction>();
                result = parsed;
                return true;
            }
            catch { return false; }
        }

        private static bool LooksStructured(string json) => json.IndexOf("\"text\"", StringComparison.Ordinal) >= 0 ||
                                                            json.IndexOf("\"objects\"", StringComparison.Ordinal) >= 0;

        private static string NormalizeJson(string value)
        {
            var trimmed = value.Trim();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0) trimmed = trimmed.Substring(firstNewline + 1);
            var fence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            return (fence >= 0 ? trimmed.Substring(0, fence) : trimmed).Trim();
        }
    }

    public sealed class QuestAiClient : MonoBehaviour
    {
        public QuestSignalingClient signaling;
        public QuestVisionService vision;
        public string endpointUrl = "";
        public string model = "";
        public string apiKey = "";
        public bool allowAnonymousEndpoint = true;
        [TextArea(2, 8)] public string systemPrompt = "Return JSON with text, objects[{label,bbox{x,y,width,height},confidence}], and actions[]. Bounding boxes are normalized 0..1.";

        public long LastLatencyMs { get; private set; }
        public AiVisionResult LastResult { get; private set; }
        public string LastError { get; private set; }
        public bool IsRequestActive { get; private set; }
        public event Action<AiVisionResult> ResultReceived;

        private void Awake()
        {
            if (signaling == null) signaling = GetComponent<QuestSignalingClient>();
            if (vision == null) vision = GetComponent<QuestVisionService>();
            endpointUrl = PlayerPrefs.GetString("QuestPhoneStream_AiEndpoint", endpointUrl);
            model = PlayerPrefs.GetString("QuestPhoneStream_AiModel", model);
            apiKey = PlayerPrefs.GetString("QuestPhoneStream_AiApiKey", apiKey);
        }

        private void Start() => RefreshCapabilityState();

        public void Configure(string endpoint, string modelName, string key = null, bool allowAnonymous = true)
        {
            endpointUrl = endpoint?.Trim() ?? string.Empty;
            model = modelName?.Trim() ?? string.Empty;
            apiKey = key ?? string.Empty;
            allowAnonymousEndpoint = allowAnonymous;
            PlayerPrefs.SetString("QuestPhoneStream_AiEndpoint", endpointUrl);
            PlayerPrefs.SetString("QuestPhoneStream_AiModel", model);
            PlayerPrefs.SetString("QuestPhoneStream_AiApiKey", apiKey);
            PlayerPrefs.Save();
            RefreshCapabilityState();
        }

        public bool CanRequest => Uri.TryCreate(endpointUrl, UriKind.Absolute, out var uri) &&
                                  (uri.Scheme == "http" || uri.Scheme == "https") &&
                                  !string.IsNullOrWhiteSpace(model) && (allowAnonymousEndpoint || !string.IsNullOrWhiteSpace(apiKey));

        public Coroutine AnalyzeLastFrame(Rect? normalizedCrop = null)
        {
            var frame = vision?.LastFrame;
            return frame == null ? null : StartCoroutine(AnalyzeFrame(frame, normalizedCrop));
        }

        public IEnumerator AnalyzeFrame(QuestVisionFrame frame, Rect? normalizedCrop = null)
        {
            if (frame?.texture == null || !CanRequest || IsRequestActive) yield break;
            IsRequestActive = true;
            LastError = null;
            RefreshCapabilityState();
            var started = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Texture2D ownedCrop = null;
            try
            {
                var source = frame.texture;
                if (normalizedCrop.HasValue)
                {
                    ownedCrop = Crop(source, normalizedCrop.Value);
                    if (ownedCrop != null) source = ownedCrop;
                }
                var jpeg = ImageConversion.EncodeToJPG(source, 85);
                if (jpeg == null || jpeg.Length == 0) { LastError = "Unable to encode vision frame"; yield break; }

                var requestPayload = BuildRequest(Convert.ToBase64String(jpeg));
                var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(requestPayload));
                using (var request = new UnityWebRequest(endpointUrl, UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler = new UploadHandlerRaw(bytes);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    if (!string.IsNullOrWhiteSpace(apiKey)) request.SetRequestHeader("Authorization", "Bearer " + apiKey.Trim());
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        LastError = "AI endpoint request failed: " + request.responseCode;
                        yield break;
                    }
                    if (!QuestAiResponseParser.TryParseTransportResponse(request.downloadHandler.text, out var result))
                    {
                        LastError = "AI endpoint returned an invalid structured response";
                        yield break;
                    }
                    LastResult = result;
                    ResultReceived?.Invoke(result);
                }
            }
            finally
            {
                if (ownedCrop != null) Destroy(ownedCrop);
                LastLatencyMs = Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - started);
                IsRequestActive = false;
                RefreshCapabilityState();
            }
        }

        private OpenAiVisionRequest BuildRequest(string imageBase64) => new OpenAiVisionRequest
        {
            model = model,
            messages = new[]
            {
                new OpenAiMessage
                {
                    role = "system",
                    content = new[] { new OpenAiContentPart { type = "text", text = systemPrompt } }
                },
                new OpenAiMessage
                {
                    role = "user",
                    content = new[]
                    {
                        new OpenAiContentPart { type = "text", text = "Analyze this selected Quest camera frame and return only the requested JSON object." },
                        new OpenAiContentPart { type = "image_url", image_url = new OpenAiImageUrl { url = "data:image/jpeg;base64," + imageBase64 } }
                    }
                }
            }
        };

        private static Texture2D Crop(Texture2D source, Rect normalized)
        {
            var x = Mathf.Clamp(Mathf.FloorToInt(normalized.x * source.width), 0, source.width - 1);
            var y = Mathf.Clamp(Mathf.FloorToInt(normalized.y * source.height), 0, source.height - 1);
            var width = Mathf.Clamp(Mathf.CeilToInt(normalized.width * source.width), 1, source.width - x);
            var height = Mathf.Clamp(Mathf.CeilToInt(normalized.height * source.height), 1, source.height - y);
            try
            {
                var copy = new Texture2D(width, height, TextureFormat.RGB24, false);
                copy.SetPixels(source.GetPixels(x, y, width, height));
                copy.Apply(false, false);
                return copy;
            }
            catch { return null; }
        }

        private void RefreshCapabilityState()
        {
            signaling?.ReportCapabilityState("ai.vision", available: true, authorized: CanRequest, active: IsRequestActive);
        }

        private void OnDestroy()
        {
            if (signaling != null) signaling.ReportCapabilityState("ai.vision", active: false);
        }
    }

    internal static class QuestAiBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            foreach (var signaling in UnityEngine.Object.FindObjectsOfType<QuestSignalingClient>())
            {
                var client = signaling.GetComponent<QuestAiClient>() ?? signaling.gameObject.AddComponent<QuestAiClient>();
                client.signaling = signaling;
                client.vision = signaling.GetComponent<QuestVisionService>();
            }
        }
    }
}
