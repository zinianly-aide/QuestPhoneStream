using UnityEngine;

namespace QuestPhoneStream
{
    public sealed class MenuController : MonoBehaviour
    {
        public SettingsUI settingsUI;
        public QuestSignalingClient signalingClient;

        private bool _menuVisible;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Menu) || Input.GetKeyDown(KeyCode.Escape))
            {
                ToggleMenu();
            }
        }

        public void ToggleMenu()
        {
            _menuVisible = !_menuVisible;
            if (settingsUI != null)
            {
                if (_menuVisible)
                    settingsUI.Show();
                else
                    settingsUI.Hide();
            }
        }
    }
}
