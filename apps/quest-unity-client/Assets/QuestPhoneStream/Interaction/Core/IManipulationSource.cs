using System;

namespace QuestPhoneStream.Interaction
{
    public interface IManipulationSource
    {
        event Action<TransformEvent> TransformEventRaised;
    }
}
