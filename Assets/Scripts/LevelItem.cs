using UnityEngine;
using UnityEngine.EventSystems;

public class LevelItem : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
{
    public string b64;
    public TMPro.TMP_Text levelNumberText;
    public TMPro.TMP_Text bestTimeText;
    
    public static Color NormalColor  = new Color32(0x53, 0xC3, 0x25, 0xFF);
    public static Color SelectedColor = new Color32(0xFF, 0xEB, 0x04, 0xFF);
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        EventSystem.current.SetSelectedGameObject(this.gameObject);
    }
    
    public void OnSelect(BaseEventData eventData)
    {
        SetColor(SelectedColor);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        SetColor(NormalColor);
    }
    
    private void SetColor(Color color)
    {
        levelNumberText.color = color;
    }
}
