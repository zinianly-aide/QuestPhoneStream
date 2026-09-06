using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestPhoneStream
{
    public enum ProjectionMode { Flat, Equirectangular }
    public enum StereoMode { Mono, Sbs }
    public enum EyeOrder { Lr, Rl }
    public enum MediaRouteKind { Video, SixDof, GaussianSplat }

    [Serializable]
    public struct MediaVideoProfile
    {
        public ProjectionMode projection;
        public int fov;
        public StereoMode stereo;
        public EyeOrder eyeOrder;
        public static MediaVideoProfile Default => new MediaVideoProfile { projection = ProjectionMode.Flat, fov = 360, stereo = StereoMode.Mono, eyeOrder = EyeOrder.Lr };
        public MediaVideoProfile Normalize() { if (fov != 180 && fov != 360) fov = 360; if (projection == ProjectionMode.Flat) fov = 360; return this; }
        public static MediaVideoProfile From(MediaItemDto item)
        {
            if (item == null) return Default;
            var profile = Default;
            if (string.Equals(item.projection, "equirectangular", StringComparison.OrdinalIgnoreCase)) profile.projection = ProjectionMode.Equirectangular;
            if (item.fov == 180 || item.fov == 360) profile.fov = item.fov;
            if (string.Equals(item.stereo, "sbs", StringComparison.OrdinalIgnoreCase)) profile.stereo = StereoMode.Sbs;
            if (string.Equals(item.eyeOrder, "rl", StringComparison.OrdinalIgnoreCase)) profile.eyeOrder = EyeOrder.Rl;
            return profile.Normalize();
        }
        public string Label => projection == ProjectionMode.Flat ? "Flat" : fov + "° · " + (stereo == StereoMode.Sbs ? "SBS" : "Mono");
    }

    [Serializable]
    public sealed class MediaItemDto
    {
        public string id;
        public string name;
        public string mimeType;
        public long size;
        public bool seekable;
        public string projection;
        public int fov;
        public string stereo;
        public string eyeOrder;
        public string spatialFormat;
        public string manifestUrl;
        public string referenceSpace;
        public SixDofMediaBounds spatialBounds;

        public bool IsSixDof => SixDofMediaDescriptor.IsSixDofFormat(spatialFormat);
        public bool IsGaussianSplat
        {
            get
            {
                if (string.IsNullOrWhiteSpace(spatialFormat)) return false;
                var value = spatialFormat.Trim().ToLowerInvariant();
                return value == "gaussian-splat" || value == "3dgs" || value == "ply-splat";
            }
        }

        public MediaRouteKind Route => IsGaussianSplat ? MediaRouteKind.GaussianSplat : IsSixDof ? MediaRouteKind.SixDof : MediaRouteKind.Video;
        public string RouteLabel => Route == MediaRouteKind.GaussianSplat ? "3DGS POC" : Route == MediaRouteKind.SixDof ? "6DoF POC" : MediaVideoProfile.From(this).Label;
    }

    [Serializable] internal sealed class MediaItemArray { public MediaItemDto[] items; }

    public static class MediaCatalogJson
    {
        public static List<MediaItemDto> Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<MediaItemDto>();
            var trimmed = json.Trim();
            if (trimmed.StartsWith("[")) trimmed = "{\"items\":" + trimmed + "}";
            var result = JsonUtility.FromJson<MediaItemArray>(trimmed);
            return result?.items == null ? new List<MediaItemDto>() : new List<MediaItemDto>(result.items);
        }
    }

    public static class MediaUrlBuilder
    {
        public static string Metadata(string baseUrl, string id) => Join(baseUrl, "/v1/media/" + Uri.EscapeDataString(id));
        public static string PlayToken(string baseUrl, string id) => Join(baseUrl, "/v1/media/" + Uri.EscapeDataString(id) + "/play-token");
        public static string Content(string baseUrl, string id, string capability) => Join(baseUrl, "/v1/media/" + Uri.EscapeDataString(id) + "/content?cap=" + Uri.EscapeDataString(capability));
        public static string ResolveManifest(string baseUrl, string manifestUrl, string fallback)
        {
            if (string.IsNullOrWhiteSpace(manifestUrl)) return fallback;
            if (Uri.TryCreate(manifestUrl.Trim(), UriKind.Absolute, out var absolute)) return absolute.ToString();
            if (!Uri.TryCreate((baseUrl ?? string.Empty).TrimEnd('/') + "/", UriKind.Absolute, out var root)) return fallback;
            return Uri.TryCreate(root, manifestUrl.TrimStart('/'), out var resolved) ? resolved.ToString() : fallback;
        }
        private static string Join(string baseUrl, string path) => (baseUrl ?? string.Empty).TrimEnd('/') + path;
    }
}
