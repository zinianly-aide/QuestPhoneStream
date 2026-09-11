using UnityEngine;
namespace QuestPhoneStream.Interaction
{
    public static class PokeHysteresis
    {
        public static bool IsPressed(bool pressed, float distance, float press, float release) =>
            distance <= (pressed ? Mathf.Max(press + .001f, release) : press);
    }
}
