using UnityEngine;
using UnityEngine.UI;

namespace QuestPhoneStream
{
    public sealed class QuestAiResultPanel : MonoBehaviour
    {
        public QuestAiClient client;
        public Camera xrCamera;
        public Text text;

        private void Start()
        {
            if (client == null) client = FindObjectOfType<QuestAiClient>();
            if (xrCamera == null) xrCamera = Camera.main;
            EnsureUi();
            if (client != null) client.ResultReceived += OnResult;
        }

        private void EnsureUi()
        {
            if (text != null) return;
            var canvasObject = new GameObject("AiVisionResultCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObject.AddComponent<CanvasScaler>();
            var rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(720, 240);
            var anchor = xrCamera != null ? xrCamera.transform : transform;
            rect.position = anchor.position + anchor.forward * 1.4f + Vector3.up * 0.15f;
            rect.rotation = Quaternion.LookRotation(rect.position - anchor.position);
            rect.localScale = Vector3.one * 0.0015f;

            var textObject = new GameObject("Result");
            textObject.transform.SetParent(canvasObject.transform, false);
            text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 28;
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.white;
            text.text = "AI Vision ready";
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16, 16);
            textRect.offsetMax = new Vector2(-16, -16);
        }

        private void OnResult(AiVisionResult result)
        {
            if (text == null || result == null) return;
            var objectCount = result.objects?.Length ?? 0;
            var actionCount = result.actions?.Length ?? 0;
            text.text = string.IsNullOrWhiteSpace(result.text)
                ? $"Objects: {objectCount} · Actions: {actionCount}"
                : $"{result.text}\n\nObjects: {objectCount} · Actions: {actionCount}";
        }

        private void OnDestroy()
        {
            if (client != null) client.ResultReceived -= OnResult;
        }
    }

    internal static class QuestAiResultPanelBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            foreach (var client in Object.FindObjectsOfType<QuestAiClient>())
            {
                var panel = client.GetComponent<QuestAiResultPanel>() ?? client.gameObject.AddComponent<QuestAiResultPanel>();
                panel.client = client;
                panel.xrCamera = Camera.main;
            }
        }
    }
}
