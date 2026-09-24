namespace QuestPhoneStream.Interaction
{
    public interface IInteractionBackend
    {
        string Name { get; }
        bool IsAvailable { get; }
        InteractionCapabilities Capabilities { get; }
        IPointerSource Pointer { get; }
        IManipulationSource Manipulation { get; }
        void Initialize(InteractionBackendContext context);
        void Shutdown();
    }
}
