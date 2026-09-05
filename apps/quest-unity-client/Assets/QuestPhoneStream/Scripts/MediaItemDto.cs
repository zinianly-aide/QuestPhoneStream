using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestPhoneStream
{
    public enum ProjectionMode { Flat, Equirectangular }
    public enum StereoMode { Mono, Sbs }
    public enum EyeOrder { Lr, Rl }

    [Serializable]
    public struct MediaVideoProfile
    {
        public ProjectionMode projection;
        public int fov;
        public StereoMode stereo;
        public EyeOrder eyeOrder;

        public static MediaVideoProfile Default => new MediaVideoProfile {
            projection = ProjectionMode.Flat, fov = 360, stereo = StereoMode.Mono, eyeOrder = EyeOrder.Lr
        };

        public MediaVideoProfile Normalize()
        {
            if (fov != 180 && fov != 360) fov = 360;
            if (projection == ProjectionMode.Flat) fov = 360;
            return this;
        }

        public static MediaVideoProfile From(MediaItemDto item)
        {
            if (item == null) return Default;
            var profile = Default;
            if (string.Equals(item.projection, "equirectangular", StringComparison.OrdinalIgnoreCase))
                profile.projection = ProjectionMode.Equirectangular;
            if (item.fov == 180 || item.fov == 360) profile.fov = item.fov;
            if (string.Equals(item.stereo, "sbs", StringComparison.OrdinalIgnoreCase))
                profile.stereo = StereoMode.Sbs;
            if (string.Equals(item.eyeOrder, "rl", StringComparison.OrdinalIgnoreCase))
                profile.eyeOrder = EyeOrder.Rl;
            return profile.Normalize();
        }

        public string Label => projection == ProjectionMode.Flat ? "Flat" : fov + "° · " +
            (stereo == StereoMode.Sbs ? "SBS" : "Mono");
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
    }

    [Serializable]
    internal sealed class MediaItemArray
    {
        public MediaItemDto[] items;
    }

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
        public static string Metadata(string baseUrl, string id) =>
            Join(baseUrl, "/v1/media/" + Uri.EscapeDataString(id));

        public static string PlayToken(string baseUrl, string id) =>
            Join(baseUrl, "/v1/media/" + Uri.EscapeDataString(id) + "/play-token");

        public static string Content(string baseUrl, string id, string capability) =>
            Join(baseUrl, "/v1/media/" + Uri.EscapeDataString(id) + "/content?cap=" + Uri.EscapeDataString(capability));

        private static string Join(string baseUrl, string path) => (baseUrl ?? string.Empty).TrimEnd('/') + path;
    }
}
