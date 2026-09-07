using System;
using System.Collections.Generic;

namespace QuestPhoneStream.Interaction
{
    public static class InteractionBackendRegistry
    {
        private static readonly Dictionary<string, Func<IInteractionBackend>> Factories = new Dictionary<string, Func<IInteractionBackend>>();
        private static readonly List<string> FallbackOrder = new List<string>();

        public static void Register(string name, Func<IInteractionBackend> factory, bool fallback = false)
        {
            if (string.IsNullOrEmpty(name) || factory == null) return;
            Factories[name] = factory;
            if (fallback && !FallbackOrder.Contains(name)) FallbackOrder.Add(name);
        }

        public static IInteractionBackend Create(string name) =>
            !string.IsNullOrEmpty(name) && Factories.TryGetValue(name, out var factory) ? factory() : null;

        public static IInteractionBackend CreateFallback()
        {
            foreach (var name in FallbackOrder)
            {
                var backend = Create(name);
                if (backend != null && backend.IsAvailable) return backend;
            }
            foreach (var pair in Factories)
            {
                var backend = pair.Value();
                if (backend.IsAvailable) return backend;
            }
            return null;
        }

        /// <summary>
        /// Clears registered factories and fallback order. This keeps test setup deterministic
        /// and also provides an explicit reset point before a controlled backend bootstrap.
        /// Runtime callers should normally rely on each backend's EnsureRegistered method.
        /// </summary>
        public static void Clear()
        {
            Factories.Clear();
            FallbackOrder.Clear();
        }
    }
}
