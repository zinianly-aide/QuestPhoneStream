using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace QuestPhoneStream
{
    [Serializable]
    public sealed class ObjectScanReconstructionOutputs
    {
        public string sparseModel;
        public string sparseText;
        public string sparsePreviewPly;
        public string questPoses;
    }

    [Serializable]
    public sealed class ObjectScanReconstructionResult
    {
        public string version;
        public string sessionId;
        public string backend;
        public string status;
        public string generatedUtc;
        public int inputFrames;
        public string cameraModel;
        public string[] warnings;
        public ObjectScanReconstructionOutputs outputs;
    }

    public static class ObjectScanResultPaths
    {
        public static string ResultUrl(string baseUrl, string sessionId) => Build(baseUrl, sessionId, "result");
        public static string PreviewUrl(string baseUrl, string sessionId) => Build(baseUrl, sessionId, "result/sparse-preview.ply");

        private static string Build(string baseUrl, string sessionId, string suffix)
        {
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(sessionId)) return string.Empty;
            return baseUrl.Trim().TrimEnd('/') + "/v1/scans/" + Uri.EscapeDataString(sessionId.Trim()) + "/" + suffix;
        }
    }

    /// <summary>
    /// G4 result client. It reads the worker's fixed reconstruction result endpoint and
    /// sends the fixed sparse-preview PLY URL into the existing GaussianSplatPocRenderer.
    /// The preview is reconstruction-local and is not claimed to be aligned to the real object.
    /// </summary>
    public sealed class ObjectScanResultClient : MonoBehaviour
    {
        public ObjectScanRecorder recorder;
        public ObjectScanUploader uploader;
        public GaussianSplatPocRenderer previewRenderer;
        public int timeoutSeconds = 15;

        public ObjectScanReconstructionResult LastResult { get; private set; }
        public bool IsChecking { get; private set; }
        public string StateText { get; private set; } = "Idle";

        private string WorkerBaseUrl => uploader != null && !string.IsNullOrWhiteSpace(uploader.workerBaseUrl)
            ? uploader.workerBaseUrl.Trim().TrimEnd('/')
            : PlayerPrefs.GetString(ObjectScanUploader.WorkerUrlPrefKey, string.Empty).Trim().TrimEnd('/');

        private void Awake() => EnsureDependencies();

        public void RefreshResult(Action<ObjectScanReconstructionResult, string> completed = null)
        {
            if (IsChecking) { completed?.Invoke(null, "Result check already in progress"); return; }
            StartCoroutine(RefreshResultRoutine(completed));
        }

        public void PreviewLatest(Action<bool, string> completed = null)
        {
            if (IsChecking) { completed?.Invoke(false, "Result check already in progress"); return; }
            StartCoroutine(PreviewLatestRoutine(completed));
        }

        public void ClearPreview()
        {
            EnsureDependencies();
            previewRenderer?.Clear();
            StateText = "Preview cleared";
        }

        private IEnumerator PreviewLatestRoutine(Action<bool, string> completed)
        {
            ObjectScanReconstructionResult result = null;
            string resultError = null;
            yield return RefreshResultRoutine((value, error) => { result = value; resultError = error; });
            if (result == null)
            {
                completed?.Invoke(false, resultError ?? "Reconstruction result unavailable");
                yield break;
            }
            if (!string.Equals(result.status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                var warning = result.warnings != null && result.warnings.Length > 0 ? " · " + string.Join(", ", result.warnings) : string.Empty;
                StateText = "Reconstruction " + (result.status ?? "unknown") + warning;
                completed?.Invoke(false, StateText);
                yield break;
            }

            EnsureDependencies();
            if (previewRenderer == null)
            {
                StateText = "Gaussian preview renderer unavailable";
                completed?.Invoke(false, StateText);
                yield break;
            }

            var url = ObjectScanResultPaths.PreviewUrl(WorkerBaseUrl, result.sessionId);
            if (string.IsNullOrEmpty(url))
            {
                StateText = "Preview URL unavailable";
                completed?.Invoke(false, StateText);
                yield break;
            }

            StateText = "Loading sparse preview";
            previewRenderer.LoadUrl(url);
            yield return null;
            while (previewRenderer.LoadState == GaussianSplatLoadState.Loading) yield return null;

            if (!previewRenderer.IsLoaded)
            {
                StateText = "Preview failed: " + (previewRenderer.LastError ?? previewRenderer.StateText);
                completed?.Invoke(false, StateText);
                yield break;
            }

            StateText = $"Sparse preview · {previewRenderer.SplatCount} points · not world-aligned";
            completed?.Invoke(true, StateText);
        }

        private IEnumerator RefreshResultRoutine(Action<ObjectScanReconstructionResult, string> completed)
        {
            EnsureDependencies();
            var sessionId = recorder?.SessionId;
            var url = ObjectScanResultPaths.ResultUrl(WorkerBaseUrl, sessionId);
            if (string.IsNullOrEmpty(url))
            {
                StateText = string.IsNullOrWhiteSpace(sessionId) ? "No scan session" : "Object scan worker URL is not configured";
                completed?.Invoke(null, StateText);
                yield break;
            }

            IsChecking = true;
            StateText = "Checking reconstruction";
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = timeoutSeconds;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    IsChecking = false;
                    StateText = "Result unavailable: " + (request.error ?? request.responseCode.ToString());
                    completed?.Invoke(null, StateText);
                    yield break;
                }

                ObjectScanReconstructionResult result;
                try { result = JsonUtility.FromJson<ObjectScanReconstructionResult>(request.downloadHandler.text); }
                catch (Exception error)
                {
                    IsChecking = false;
                    StateText = "Invalid reconstruction result: " + error.Message;
                    completed?.Invoke(null, StateText);
                    yield break;
                }
                if (result == null || result.version != "qps-object-scan-result-v1" || result.sessionId != sessionId)
                {
                    IsChecking = false;
                    StateText = "Reconstruction result identity mismatch";
                    completed?.Invoke(null, StateText);
                    yield break;
                }

                LastResult = result;
                IsChecking = false;
                StateText = "Reconstruction " + (result.status ?? "unknown");
                completed?.Invoke(result, null);
            }
        }

        private void EnsureDependencies()
        {
            if (recorder == null) recorder = GetComponent<ObjectScanRecorder>() ?? FindFirstObjectByType<ObjectScanRecorder>();
            if (uploader == null) uploader = GetComponent<ObjectScanUploader>() ?? FindFirstObjectByType<ObjectScanUploader>();
            if (previewRenderer == null) previewRenderer = FindFirstObjectByType<GaussianSplatPocRenderer>();
        }
    }

    internal static class ObjectScanResultBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            foreach (var signaling in UnityEngine.Object.FindObjectsOfType<QuestSignalingClient>())
            {
                var client = signaling.GetComponent<ObjectScanResultClient>() ?? signaling.gameObject.AddComponent<ObjectScanResultClient>();
                client.recorder = signaling.GetComponent<ObjectScanRecorder>() ?? UnityEngine.Object.FindFirstObjectByType<ObjectScanRecorder>();
                client.uploader = signaling.GetComponent<ObjectScanUploader>() ?? UnityEngine.Object.FindFirstObjectByType<ObjectScanUploader>();
                client.previewRenderer = UnityEngine.Object.FindFirstObjectByType<GaussianSplatPocRenderer>();
            }
        }
    }
}
