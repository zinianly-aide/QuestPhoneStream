using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

namespace QuestPhoneStream
{
    [Serializable]
    public sealed class GaussianSplatPoint
    {
        public Vector3 position;
        public Color32 color;
        public float size;
    }

    public enum GaussianSplatLoadState { Idle, Loading, Loaded, Error, Cancelled }

    public static class GaussianSplatPlyParser
    {
        public const int DefaultMaxSplats = 50000;

        public static List<GaussianSplatPoint> Parse(string text, int maxSplats = DefaultMaxSplats)
        {
            var result = new List<GaussianSplatPoint>();
            if (string.IsNullOrWhiteSpace(text) || maxSplats <= 0) return result;
            var lines = text.Replace("\r", string.Empty).Split('\n');
            if (lines.Length < 3 || lines[0].Trim() != "ply") return result;

            var properties = new List<string>();
            var vertexCount = 0;
            var inVertex = false;
            var dataStart = -1;
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("format ", StringComparison.Ordinal) && !line.StartsWith("format ascii", StringComparison.Ordinal)) return result;
                if (line.StartsWith("element vertex ", StringComparison.Ordinal))
                {
                    int.TryParse(line.Substring("element vertex ".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out vertexCount);
                    inVertex = true;
                    continue;
                }
                if (line.StartsWith("element ", StringComparison.Ordinal) && !line.StartsWith("element vertex ", StringComparison.Ordinal)) inVertex = false;
                if (inVertex && line.StartsWith("property ", StringComparison.Ordinal))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3) properties.Add(parts[parts.Length - 1]);
                    continue;
                }
                if (line == "end_header") { dataStart = i + 1; break; }
            }
            if (dataStart < 0 || vertexCount <= 0) return result;

            var ix = properties.IndexOf("x"); var iy = properties.IndexOf("y"); var iz = properties.IndexOf("z");
            if (ix < 0 || iy < 0 || iz < 0) return result;
            var ir = properties.IndexOf("red"); var ig = properties.IndexOf("green"); var ib = properties.IndexOf("blue");
            var if0 = properties.IndexOf("f_dc_0"); var if1 = properties.IndexOf("f_dc_1"); var if2 = properties.IndexOf("f_dc_2");
            var iOpacity = properties.IndexOf("opacity");
            var iScale = properties.IndexOf("scale");
            var is0 = properties.IndexOf("scale_0"); var is1 = properties.IndexOf("scale_1"); var is2 = properties.IndexOf("scale_2");

            var count = Math.Min(vertexCount, Math.Min(maxSplats, lines.Length - dataStart));
            for (var i = 0; i < count; i++)
            {
                var parts = lines[dataStart + i].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (!TryFloat(parts, ix, out var x) || !TryFloat(parts, iy, out var y) || !TryFloat(parts, iz, out var z)) continue;

                var color = new Color(0.8f, 0.8f, 0.8f, 1f);
                if (TryFloat(parts, ir, out var r) && TryFloat(parts, ig, out var g) && TryFloat(parts, ib, out var b))
                    color = new Color(Mathf.Clamp01(r / 255f), Mathf.Clamp01(g / 255f), Mathf.Clamp01(b / 255f), 1f);
                else if (TryFloat(parts, if0, out var f0) && TryFloat(parts, if1, out var f1) && TryFloat(parts, if2, out var f2))
                {
                    const float sh = 0.2820948f;
                    color = new Color(Mathf.Clamp01(0.5f + sh * f0), Mathf.Clamp01(0.5f + sh * f1), Mathf.Clamp01(0.5f + sh * f2), 1f);
                }
                if (TryFloat(parts, iOpacity, out var opacity)) color.a = Mathf.Clamp01(1f / (1f + Mathf.Exp(-opacity)));

                var size = 0.02f;
                if (TryFloat(parts, iScale, out var plainScale)) size = Mathf.Clamp(Mathf.Abs(plainScale), 0.002f, 0.25f);
                else
                {
                    var sum = 0f; var n = 0;
                    if (TryFloat(parts, is0, out var s0)) { sum += s0; n++; }
                    if (TryFloat(parts, is1, out var s1)) { sum += s1; n++; }
                    if (TryFloat(parts, is2, out var s2)) { sum += s2; n++; }
                    if (n > 0) size = Mathf.Clamp(Mathf.Exp(sum / n), 0.002f, 0.25f);
                }
                result.Add(new GaussianSplatPoint { position = new Vector3(x, y, -z), color = color, size = size });
            }
            return result;
        }

        private static bool TryFloat(string[] parts, int index, out float value)
        {
            value = 0f;
            return index >= 0 && index < parts.Length && float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }

    /// <summary>
    /// Deliberately limited P3 renderer: ASCII PLY, <=50k isotropic billboard splats.
    /// Input bytes are bounded before parsing to avoid unbounded Quest memory spikes.
    /// This is not a complete anisotropic/sorted GPU 3DGS implementation.
    /// </summary>
    public sealed class GaussianSplatPocRenderer : MonoBehaviour
    {
        public const int DefaultMaxDownloadBytes = 32 * 1024 * 1024;

        public QuestSignalingClient signaling;
        public int maxSplats = GaussianSplatPlyParser.DefaultMaxSplats;
        public int maxDownloadBytes = DefaultMaxDownloadBytes;
        public float globalSize = 1f;

        private GameObject _renderObject;
        private Mesh _mesh;
        private Material _material;
        private Shader _shader;
        private Coroutine _loadRoutine;
        private UnityWebRequest _activeRequest;
        private int _loadGeneration;

        public int SplatCount { get; private set; }
        public long LastLoadMs { get; private set; }
        public string LastError { get; private set; }
        public string LastSource { get; private set; }
        public GaussianSplatLoadState LoadState { get; private set; } = GaussianSplatLoadState.Idle;
        public bool IsLoaded => LoadState == GaussianSplatLoadState.Loaded && _mesh != null && SplatCount > 0;
        public bool IsAvailable => ResolveShader() != null;
        public string StateText => !IsAvailable ? "Shader unavailable" :
            LoadState == GaussianSplatLoadState.Loading ? "Loading" :
            LoadState == GaussianSplatLoadState.Error ? "Error: " + (LastError ?? "unknown") :
            LoadState == GaussianSplatLoadState.Cancelled ? "Cancelled" :
            IsLoaded ? $"{SplatCount} splats · POC" : "Ready · ASCII PLY POC";

        private void Start() => RefreshCapability();

        public Coroutine LoadUrl(string url)
        {
            CancelLoad(clearAsset: true);
            LastError = null;
            LastSource = url?.Trim();
            if (!TryValidateSource(LastSource, out var normalized, out var error))
            {
                LastError = error;
                LoadState = GaussianSplatLoadState.Error;
                RefreshCapability();
                return null;
            }
            LastSource = normalized;
            var generation = ++_loadGeneration;
            LoadState = GaussianSplatLoadState.Loading;
            RefreshCapability();
            _loadRoutine = StartCoroutine(LoadUrlRoutine(normalized, generation));
            return _loadRoutine;
        }

        public void CancelLoad(bool clearAsset = true)
        {
            var wasLoading = LoadState == GaussianSplatLoadState.Loading;
            _loadGeneration++;
            if (_activeRequest != null)
            {
                try { _activeRequest.Abort(); } catch { }
                _activeRequest = null;
            }
            if (_loadRoutine != null)
            {
                StopCoroutine(_loadRoutine);
                _loadRoutine = null;
            }
            if (clearAsset) ClearAsset();
            if (wasLoading) LoadState = GaussianSplatLoadState.Cancelled;
            RefreshCapability();
        }

        private IEnumerator LoadUrlRoutine(string url, int generation)
        {
            var watch = Stopwatch.StartNew();
            using (var request = UnityWebRequest.Get(url))
            {
                _activeRequest = request;
                var operation = request.SendWebRequest();
                var byteLimit = (ulong)Mathf.Max(1, maxDownloadBytes);
                while (!operation.isDone)
                {
                    if (request.downloadedBytes > byteLimit)
                    {
                        request.Abort();
                        LastError = $"Gaussian splat exceeds {byteLimit} byte POC limit";
                        LoadState = GaussianSplatLoadState.Error;
                        watch.Stop(); LastLoadMs = watch.ElapsedMilliseconds; _loadRoutine = null; _activeRequest = null; RefreshCapability();
                        yield break;
                    }
                    yield return null;
                }
                if (generation != _loadGeneration) yield break;
                _activeRequest = null;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    LastError = "Gaussian splat request failed: " + request.responseCode;
                    LoadState = GaussianSplatLoadState.Error;
                    watch.Stop(); LastLoadMs = watch.ElapsedMilliseconds; _loadRoutine = null; RefreshCapability();
                    yield break;
                }
                if (request.downloadedBytes > byteLimit)
                {
                    LastError = $"Gaussian splat exceeds {byteLimit} byte POC limit";
                    LoadState = GaussianSplatLoadState.Error;
                    watch.Stop(); LastLoadMs = watch.ElapsedMilliseconds; _loadRoutine = null; RefreshCapability();
                    yield break;
                }
                if (!LoadAsciiPlyInternal(request.downloadHandler.text))
                {
                    watch.Stop(); LastLoadMs = watch.ElapsedMilliseconds; _loadRoutine = null; RefreshCapability();
                    yield break;
                }
            }
            watch.Stop();
            LastLoadMs = watch.ElapsedMilliseconds;
            LoadState = GaussianSplatLoadState.Loaded;
            _loadRoutine = null;
            RefreshCapability();
        }

        public bool LoadAsciiPly(string text)
        {
            CancelLoad(clearAsset: true);
            LastSource = "inline:ascii-ply";
            LastError = null;
            LoadState = GaussianSplatLoadState.Loading;
            var watch = Stopwatch.StartNew();
            var loaded = LoadAsciiPlyInternal(text);
            watch.Stop();
            LastLoadMs = watch.ElapsedMilliseconds;
            if (loaded) LoadState = GaussianSplatLoadState.Loaded;
            RefreshCapability();
            return loaded;
        }

        private bool LoadAsciiPlyInternal(string text)
        {
            ClearAsset();
            if (!IsAvailable)
            {
                LastError = "Gaussian splat shader unavailable";
                LoadState = GaussianSplatLoadState.Error;
                return false;
            }
            if (string.IsNullOrEmpty(text) || Encoding.UTF8.GetByteCount(text) > Mathf.Max(1, maxDownloadBytes))
            {
                LastError = $"Gaussian splat exceeds {Mathf.Max(1, maxDownloadBytes)} byte POC limit";
                LoadState = GaussianSplatLoadState.Error;
                return false;
            }
            List<GaussianSplatPoint> points;
            try { points = GaussianSplatPlyParser.Parse(text, Mathf.Clamp(maxSplats, 1, GaussianSplatPlyParser.DefaultMaxSplats)); }
            catch (Exception error)
            {
                LastError = "ASCII PLY parse failed: " + error.Message;
                LoadState = GaussianSplatLoadState.Error;
                return false;
            }
            if (points.Count == 0)
            {
                LastError = "No supported ASCII PLY splats found";
                LoadState = GaussianSplatLoadState.Error;
                return false;
            }
            BuildMesh(points);
            return true;
        }

        public static bool TryValidateSource(string source, out string normalized, out string error)
        {
            normalized = null;
            error = null;
            if (string.IsNullOrWhiteSpace(source)) { error = "Gaussian splat source is empty"; return false; }
            if (!Uri.TryCreate(source.Trim(), UriKind.Absolute, out var uri)) { error = "Gaussian splat source must be an absolute URL"; return false; }
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeFile)
            {
                error = "Unsupported Gaussian splat URL scheme";
                return false;
            }
            normalized = uri.ToString();
            return true;
        }

        private Shader ResolveShader()
        {
            if (_shader == null) _shader = Shader.Find("QuestPhoneStream/GaussianSplatPoc");
            return _shader;
        }

        private void BuildMesh(List<GaussianSplatPoint> points)
        {
            ClearAsset();
            var shader = ResolveShader();
            if (shader == null)
            {
                LastError = "Gaussian splat shader unavailable";
                LoadState = GaussianSplatLoadState.Error;
                return;
            }
            _renderObject = new GameObject("GaussianSplatPOC");
            _renderObject.transform.SetParent(transform, false);
            var filter = _renderObject.AddComponent<MeshFilter>();
            var renderer = _renderObject.AddComponent<MeshRenderer>();
            _mesh = new Mesh { name = "GaussianSplatPOC", indexFormat = IndexFormat.UInt32 };

            var vertices = new Vector3[points.Count * 4];
            var uv = new Vector2[vertices.Length];
            var uv2 = new Vector2[vertices.Length];
            var colors = new Color32[vertices.Length];
            var triangles = new int[points.Count * 6];
            var corners = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                var v = i * 4;
                for (var c = 0; c < 4; c++)
                {
                    vertices[v + c] = p.position;
                    uv[v + c] = corners[c];
                    uv2[v + c] = new Vector2(p.size, 0);
                    colors[v + c] = p.color;
                }
                var t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            _mesh.vertices = vertices; _mesh.uv = uv; _mesh.uv2 = uv2; _mesh.colors32 = colors; _mesh.triangles = triangles;
            _mesh.RecalculateBounds();
            var bounds = _mesh.bounds; bounds.Expand(0.5f); _mesh.bounds = bounds;
            filter.sharedMesh = _mesh;
            _material = new Material(shader);
            _material.SetFloat("_GlobalSize", Mathf.Max(0.01f, globalSize));
            renderer.sharedMaterial = _material;
            SplatCount = points.Count;
        }

        public void Clear()
        {
            CancelLoad(clearAsset: true);
            LoadState = GaussianSplatLoadState.Idle;
            LastError = null;
            LastSource = null;
            RefreshCapability();
        }

        private void ClearAsset()
        {
            if (_renderObject != null) Destroy(_renderObject);
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
            _renderObject = null; _mesh = null; _material = null; SplatCount = 0;
        }

        private void RefreshCapability() => signaling?.ReportCapabilityState("media.gaussian-splat.render",
            available: IsAvailable, authorized: IsAvailable, active: IsLoaded);

        private void OnDestroy()
        {
            _loadGeneration++;
            if (_activeRequest != null) { try { _activeRequest.Abort(); } catch { } }
            ClearAsset();
            signaling?.ReportCapabilityState("media.gaussian-splat.render", active: false);
        }
    }

    internal static class GaussianSplatBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            foreach (var receiver in UnityEngine.Object.FindObjectsOfType<QuestWebRtcReceiver>())
            {
                var service = receiver.GetComponent<GaussianSplatPocRenderer>() ?? receiver.gameObject.AddComponent<GaussianSplatPocRenderer>();
                service.signaling = receiver.signaling;
            }
        }
    }
}
