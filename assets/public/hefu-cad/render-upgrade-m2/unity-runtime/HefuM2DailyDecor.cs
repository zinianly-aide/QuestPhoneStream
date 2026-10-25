using UnityEngine;
namespace HefuVR {
/// <summary>Independent, saved user preference for visual-only daily accents.
/// Never serializes furniture layout; no birthday-scene dependency.</summary>
public sealed class HefuM2DailyDecor:MonoBehaviour {
 const string Pref="Hefu_M2_DailyDecorVisible_v1";
 public GameObject[] items;
 [SerializeField] bool visible=true;
 public bool Visible=>visible;
 public void Initialize(){visible=PlayerPrefs.GetInt(Pref,1)!=0;Apply();}
 public void SetVisible(bool value){visible=value;Apply();}
 public void Toggle(){SetVisible(!visible);PlayerPrefs.SetInt(Pref,visible?1:0);PlayerPrefs.Save();}
 void Apply(){if(items==null)return;foreach(var item in items)if(item&&item.activeSelf!=visible)item.SetActive(visible);}
}
}
