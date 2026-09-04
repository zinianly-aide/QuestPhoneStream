using UnityEngine;

namespace QuestPhoneStream
{
    public sealed class MenuController : MonoBehaviour
    {
        public SettingsUI settingsUI;
        public QuestSignalingClient signalingClient;

        public void ToggleMenu()
        {
            settingsUI?.Toggle();
        }
    }
}
