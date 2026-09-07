namespace QuestPhoneStream.Interaction.Backends.Meta
{
    /// <summary>Placeholder only: this assembly deliberately has no Meta SDK dependency.</summary>
    public sealed class MetaInteractionBackend : IInteractionBackend
    {
        public string Name => "Meta";
        public bool IsAvailable => false;
        public InteractionCapabilities Capabilities => InteractionCapabilities.None;
        public IPointerSource Pointer => null;
        public IManipulationSource Manipulation => null;
        public void Initialize(InteractionBackendContext context) { }
        public void Shutdown() { }
    }
}
