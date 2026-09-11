using UnityEngine;

namespace QuestPhoneStream
{
    public interface IMediaRenderer
    {
        void Prepare(int width, int height);
        void SetTexture(Texture texture);
        void Release();
    }

    public sealed class FlatMediaRenderer : MonoBehaviour, IMediaRenderer
    {
        public Renderer targetRenderer;
        public Material targetMaterial;
        public RenderTexture RenderTexture { get; private set; }

        public void Prepare(int width, int height)
        {
            Release();
            RenderTexture = new RenderTexture(Mathf.Max(2, width), Mathf.Max(2, height), 0, RenderTextureFormat.ARGB32);
            RenderTexture.Create();
        }

        public void SetTexture(Texture texture)
        {
            if (targetRenderer != null) targetRenderer.material.mainTexture = texture;
            if (targetMaterial != null) targetMaterial.mainTexture = texture;
        }

        public void Release()
        {
            if (RenderTexture == null) return;
            RenderTexture.Release();
            Destroy(RenderTexture);
            RenderTexture = null;
            SetTexture(null);
        }

        private void OnDestroy() { Release(); }
    }
}
