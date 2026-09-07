using UnityEngine;

namespace QuestPhoneStream.Interaction
{
    public sealed class InteractionBackendManager : MonoBehaviour
    {
        [Tooltip("Registered backend name. XRI remains the safe fallback when the requested backend is unavailable.")]
        public string preferredBackend = "XRI";
        public IInteractionBackend ActiveBackend { get; private set; }

        public bool InitializeBackend(InteractionBackendContext context)
        {
            ShutdownBackend();
            var preferred = InteractionBackendRegistry.Create(preferredBackend);
            ActiveBackend = preferred != null && preferred.IsAvailable ? preferred : InteractionBackendRegistry.CreateFallback();
            if (ActiveBackend == null) return false;
            ActiveBackend.Initialize(context);
            return true;
        }

        public void ShutdownBackend()
        {
            ActiveBackend?.Shutdown();
            ActiveBackend = null;
        }

        private void OnDisable() => ShutdownBackend();
        private void OnDestroy() => ShutdownBackend();
    }
}
