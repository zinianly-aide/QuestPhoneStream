using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace QuestPhoneStream
{
    public static class ObjectScanTransferPaths
    {
        public static bool IsFramePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) || !relativePath.StartsWith("frames/", StringComparison.Ordinal)) return false;
            var name = relativePath.Substring("frames/".Length);
            if (name.Length != 10 || !name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)) return false;
            for (var i = 0; i < 6; i++) if (!char.IsDigit(name[i])) return false;
            return true;
        }

        public static string FileUrl(string baseUrl, string sessionId, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) return string.Empty;
            if (string.IsNullOrWhiteSpace(sessionId)) return string.Empty;
            if (relativePath != "manifest.json" && !IsFramePath(relativePath)) return string.Empty;
            var encodedPath = relativePath == "manifest.json"
                ? "manifest.json"
                : "frames/" + Uri.EscapeDataString(relativePath.Substring("frames/".Length));
            return baseUrl.TrimEnd('/') + "/v1/scans/" + Uri.EscapeDataString(sessionId) + "/files/" + encodedPath;
        }

        public static string FinalizeUrl(string baseUrl, string sessionId) =>
            string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(sessionId)
                ? string.Empty
                : baseUrl.TrimEnd('/') + "/v1/scans/" + Uri.EscapeDataString(sessionId) + "/finalize";
    }

    /// <summary>
    /// G2 LAN exporter. Uploads one local scan session directly to the desktop scan
    /// worker. Existing same-sized files are skipped via HEAD so retrying an interrupted
    /// transfer does not re-send complete JPEGs. Bulk data intentionally stays outside
    /// Spatial Protocol v1.
    /// </summary>
    public sealed class ObjectScanUploader : MonoBehaviour
    {
        public const string WorkerUrlPrefKey = "QuestPhoneStream_ObjectScanWorkerUrl";
        public ObjectScanRecorder recorder;
        public string workerBaseUrl = "";
        public int timeoutSeconds = 20;

        public bool IsUploading { get; private set; }
        public string StateText { get; private set; } = "Idle";
        public int UploadedFiles { get; private set; }
        public int SkippedFiles { get; private set; }

        private void Awake()
        {
            if (recorder == null) recorder = GetComponent<ObjectScanRecorder>();
            if (string.IsNullOrWhiteSpace(workerBaseUrl))
                workerBaseUrl = PlayerPrefs.GetString(WorkerUrlPrefKey, string.Empty);
        }

        public void SetWorkerBaseUrl(string value)
        {
            workerBaseUrl = (value ?? string.Empty).Trim().TrimEnd('/');
            PlayerPrefs.SetString(WorkerUrlPrefKey, workerBaseUrl);
            PlayerPrefs.Save();
        }

        public void UploadCurrent(Action<bool, string> completed = null)
        {
            if (IsUploading) { completed?.Invoke(false, "Upload already in progress"); return; }
            StartCoroutine(UploadCurrentRoutine(completed));
        }

        private IEnumerator UploadCurrentRoutine(Action<bool, string> completed)
        {
            IsUploading = true;
            UploadedFiles = 0;
            SkippedFiles = 0;
            try
            {
                if (recorder == null || recorder.IsScanning)
                {
                    completed?.Invoke(false, "Stop the scan before uploading");
                    yield break;
                }
                if (string.IsNullOrWhiteSpace(workerBaseUrl))
                {
                    completed?.Invoke(false, "Object scan worker URL is not configured");
                    yield break;
                }
                var sessionPath = recorder.SessionPath;
                if (string.IsNullOrWhiteSpace(sessionPath) || !Directory.Exists(sessionPath))
                {
                    completed?.Invoke(false, "No scan dataset is available");
                    yield break;
                }
                var manifestPath = Path.Combine(sessionPath, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    completed?.Invoke(false, "manifest.json is missing");
                    yield break;
                }

                ObjectScanManifest manifest;
                try { manifest = JsonUtility.FromJson<ObjectScanManifest>(File.ReadAllText(manifestPath)); }
                catch (Exception error) { completed?.Invoke(false, "Invalid manifest: " + error.Message); yield break; }
                if (manifest == null || string.IsNullOrWhiteSpace(manifest.sessionId))
                {
                    completed?.Invoke(false, "Manifest sessionId is missing");
                    yield break;
                }

                foreach (var frame in manifest.frames)
                {
                    if (frame == null || !ObjectScanTransferPaths.IsFramePath(frame.image))
                    {
                        completed?.Invoke(false, "Manifest contains an invalid frame path");
                        yield break;
                    }
                    var localPath = Path.Combine(sessionPath, frame.image.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(localPath))
                    {
                        completed?.Invoke(false, "Missing local frame: " + frame.image);
                        yield break;
                    }
                    StateText = $"Uploading {UploadedFiles + SkippedFiles + 1}/{manifest.frames.Count}";
                    var result = new TransferResult();
                    yield return UploadFileIfNeeded(
                        ObjectScanTransferPaths.FileUrl(workerBaseUrl, manifest.sessionId, frame.image),
                        localPath,
                        "image/jpeg",
                        result);
                    if (!result.ok)
                    {
                        completed?.Invoke(false, result.error);
                        yield break;
                    }
                }

                var manifestResult = new TransferResult();
                yield return PutFile(
                    ObjectScanTransferPaths.FileUrl(workerBaseUrl, manifest.sessionId, "manifest.json"),
                    manifestPath,
                    "application/json",
                    manifestResult);
                if (!manifestResult.ok)
                {
                    completed?.Invoke(false, manifestResult.error);
                    yield break;
                }

                StateText = "Finalizing";
                using (var finalize = new UnityWebRequest(
                           ObjectScanTransferPaths.FinalizeUrl(workerBaseUrl, manifest.sessionId),
                           UnityWebRequest.kHttpVerbPOST))
                {
                    finalize.downloadHandler = new DownloadHandlerBuffer();
                    finalize.timeout = timeoutSeconds;
                    yield return finalize.SendWebRequest();
                    if (finalize.result != UnityWebRequest.Result.Success)
                    {
                        completed?.Invoke(false, "Finalize failed: " + (finalize.error ?? finalize.responseCode.ToString()));
                        yield break;
                    }
                }

                StateText = $"Uploaded {UploadedFiles}, skipped {SkippedFiles}";
                completed?.Invoke(true, StateText);
            }
            finally { IsUploading = false; }
        }

        private IEnumerator UploadFileIfNeeded(string url, string localPath, string contentType, TransferResult result)
        {
            var localBytes = new FileInfo(localPath).Length;
            using (var head = UnityWebRequest.Head(url))
            {
                head.timeout = timeoutSeconds;
                yield return head.SendWebRequest();
                if (head.result == UnityWebRequest.Result.Success &&
                    long.TryParse(head.GetResponseHeader("Content-Length"), out var remoteBytes) &&
                    remoteBytes == localBytes)
                {
                    SkippedFiles++;
                    result.ok = true;
                    yield break;
                }
            }
            yield return PutFile(url, localPath, contentType, result);
        }

        private IEnumerator PutFile(string url, string localPath, string contentType, TransferResult result)
        {
            byte[] bytes;
            try { bytes = File.ReadAllBytes(localPath); }
            catch (Exception error) { result.error = "Read failed: " + error.Message; yield break; }

            using (var request = UnityWebRequest.Put(url, bytes))
            {
                request.SetRequestHeader("Content-Type", contentType);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = timeoutSeconds;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    result.error = "Upload failed: " + (request.error ?? request.responseCode.ToString());
                    yield break;
                }
            }
            UploadedFiles++;
            result.ok = true;
        }

        private sealed class TransferResult
        {
            public bool ok;
            public string error;
        }
    }

    internal static class ObjectScanUploaderBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            foreach (var signaling in UnityEngine.Object.FindObjectsOfType<QuestSignalingClient>())
            {
                var recorder = signaling.GetComponent<ObjectScanRecorder>() ?? signaling.gameObject.AddComponent<ObjectScanRecorder>();
                var uploader = signaling.GetComponent<ObjectScanUploader>() ?? signaling.gameObject.AddComponent<ObjectScanUploader>();
                uploader.recorder = recorder;
            }
        }
    }
}
