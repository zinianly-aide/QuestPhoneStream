using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QuestPhoneStream
{
    // XRI can select a UGUI InputField without Unity's mobile pointer path
    // opening the native keyboard. Keep Unity's normal InputField behavior,
    // then provide a TouchScreenKeyboard fallback for Quest Android builds.
    public sealed class QuestKeyboardInputField : InputField
    {
        private Coroutine _openKeyboardRoutine;
        private TouchScreenKeyboard _keyboard;
        private string _lastKeyboardText;

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            if (_openKeyboardRoutine != null) StopCoroutine(_openKeyboardRoutine);
            _openKeyboardRoutine = StartCoroutine(OpenKeyboardIfNeeded());
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            if (_openKeyboardRoutine != null)
            {
                StopCoroutine(_openKeyboardRoutine);
                _openKeyboardRoutine = null;
            }
            _keyboard = null;
            base.OnDeselect(eventData);
        }

        private IEnumerator OpenKeyboardIfNeeded()
        {
            // Let InputField.ActivateInputField run first. The fallback only
            // opens when Unity/XRI did not already make a keyboard visible.
            yield return new WaitForSecondsRealtime(0.2f);
            _openKeyboardRoutine = null;

            if (!isActiveAndEnabled || EventSystem.current == null ||
                EventSystem.current.currentSelectedGameObject != gameObject ||
                !TouchScreenKeyboard.isSupported || TouchScreenKeyboard.visible)
                yield break;

            var secure = contentType == ContentType.Password;
            _keyboard = TouchScreenKeyboard.Open(
                text,
                TouchScreenKeyboardType.Default,
                false,
                lineType != LineType.SingleLine,
                secure);
            _lastKeyboardText = text;
        }

        private void Update()
        {
            if (_keyboard == null) return;

            if (_keyboard.text != _lastKeyboardText)
            {
                _lastKeyboardText = _keyboard.text;
                text = _keyboard.text;
                caretPosition = text.Length;
            }

            var status = _keyboard.status;
            if (status == TouchScreenKeyboard.Status.Done ||
                status == TouchScreenKeyboard.Status.Canceled ||
                status == TouchScreenKeyboard.Status.LostFocus)
            {
                _keyboard = null;
                if (isFocused) DeactivateInputField();
            }
        }
    }
}
