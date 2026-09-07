using System;

namespace QuestPhoneStream.Interaction
{
    public interface IPointerSource
    {
        event Action<PointerEvent> PointerEventRaised;
    }
}
