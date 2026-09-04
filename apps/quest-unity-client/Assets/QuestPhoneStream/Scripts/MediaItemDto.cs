using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestPhoneStream
{
    [Serializable]
    public sealed class MediaItemDto
    {
        public string id;
        public string name;
        public string mimeType;
        public long size;
        public bool seekable;
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
