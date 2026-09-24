using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace QuestPhoneStream
{
    /// <summary>
    /// Bounded cache for optional runtime provider types. Misses are cached and may
    /// retry only a small number of times with backoff. Explicit Refresh resets a key.
    /// All AppDomain/GetTypes scanning lives here so providers never scan every frame.
    /// </summary>
    public static class OptionalProviderDiscovery
    {
        private sealed class Entry
        {
            public Type type;
            public int attempts;
            public float nextAttemptAt;
            public bool resolved;
        }

        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        public const int DefaultMaxAttempts = 3;
        public const float DefaultRetrySeconds = 5f;
        public static int AssemblyScanCount { get; private set; }

        public static Type ResolveType(
            string key,
            Predicate<Type> predicate,
            bool force = false,
            int maxAttempts = DefaultMaxAttempts,
            float retrySeconds = DefaultRetrySeconds)
        {
            if (string.IsNullOrWhiteSpace(key) || predicate == null) return null;
            if (!Entries.TryGetValue(key, out var entry))
            {
                entry = new Entry();
                Entries[key] = entry;
            }
            if (entry.type != null) return entry.type;

            var now = Time.realtimeSinceStartup;
            if (!force)
            {
                if (entry.resolved || entry.attempts >= Mathf.Max(1, maxAttempts)) return null;
                if (now < entry.nextAttemptAt) return null;
            }

            entry.attempts++;
            entry.nextAttemptAt = now + Mathf.Max(0.5f, retrySeconds);
            entry.type = Scan(predicate);
            if (entry.type != null) entry.resolved = true;
            else if (entry.attempts >= Mathf.Max(1, maxAttempts)) entry.resolved = true;
            return entry.type;
        }

        public static void Refresh(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            Entries.Remove(key);
        }

        public static void ResetAll()
        {
            Entries.Clear();
            AssemblyScanCount = 0;
        }

        private static Type Scan(Predicate<Type> predicate)
        {
            AssemblyScanCount++;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException error) { types = error.Types; }
                catch { continue; }
                if (types == null) continue;
                foreach (var type in types)
                {
                    if (type == null) continue;
                    try { if (predicate(type)) return type; }
                    catch { }
                }
            }
            return null;
        }
    }
}
