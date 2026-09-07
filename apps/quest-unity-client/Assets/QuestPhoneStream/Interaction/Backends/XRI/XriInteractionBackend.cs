using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace QuestPhoneStream.Interaction.Backends.XRI
{
    public sealed class XriRuntimeDependencies
    {
        public XRRayInteractor leftRay;
        public XRRayInteractor rightRay;
        public InputAction leftClick;
        public InputAction rightClick;
        public InputAction leftGrab;
        public InputAction rightGrab;
    }

    public sealed class XriInteractionBackend : IInteractionBackend
    {
        public const string BackendName = "XRI";
        private XriPointerSource _pointer;
        private XriManipulationSource _manipulation;
        private GameObject _host;
        public string Name => BackendName;
        public bool IsAvailable => true;
        public InteractionCapabilities Capabilities => InteractionCapabilities.Ray | InteractionCapabilities.Poke |
            InteractionCapabilities.Grab | InteractionCapabilities.TwoHandTransform |
            InteractionCapabilities.HandTracking | InteractionCapabilities.Controller;
        public IPointerSource Pointer => _pointer;
        public IManipulationSource Manipulation => _manipulation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => EnsureRegistered();
        public static void EnsureRegistered() => InteractionBackendRegistry.Register(BackendName, () => new XriInteractionBackend(), true);

        public void Initialize(InteractionBackendContext context)
        {
            if (!(context.runtimeDependencies is XriRuntimeDependencies dependencies)) return;
            _host = new GameObject("XRI PhonePanel Interaction Backend");
            _host.transform.SetParent(context.panelRoot.transform, false);
            _pointer = _host.AddComponent<XriPointerSource>();
            _manipulation = _host.AddComponent<XriManipulationSource>();
            _pointer.Configure(context.screenCollider, dependencies);
            _manipulation.Configure(context.panelRoot.transform, context.grabCollider, dependencies);
        }
        public void Shutdown()
        {
            if (_host != null) Object.Destroy(_host);
            _host = null; _pointer = null; _manipulation = null;
        }
    }
}
