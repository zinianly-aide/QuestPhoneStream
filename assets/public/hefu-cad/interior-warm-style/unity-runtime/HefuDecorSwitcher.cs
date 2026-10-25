using UnityEngine;

namespace HefuVR
{
    public sealed class HefuDecorSwitcher : MonoBehaviour
    {
        public Renderer[] wallRenderers;
        public Renderer rug;
        public Renderer accent;
        public int preset;
        public string CurrentName => new [] { "Neutral", "Warm wood", "Cool gray" }[Mathf.Clamp(preset, 0, 2)];
        void Start() { Apply(); }
        public void Next() { preset = (preset + 1) % 3; Apply(); }
        public void Previous() { preset = (preset + 2) % 3; Apply(); }
        public void Apply()
        {
            Color[] walls={new Color(.932f,.914f,.872f), new Color(.95f,.895f,.815f), new Color(.79f,.85f,.91f)};
            Color[] rugs={new Color(.95f,.94f,.91f), new Color(.98f,.88f,.77f), new Color(.82f,.86f,.9f)};
            Color[] deco={new Color(.57f,.65f,.51f),new Color(.81f,.58f,.27f),new Color(.37f,.67f,.74f)};
            foreach (var renderer in wallRenderers) if(renderer && renderer.material) renderer.material.color = walls[preset];
            if (rug) rug.material.color = rugs[preset];
            if (accent) accent.material.color = deco[preset];
        }
    }
}
