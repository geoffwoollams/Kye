using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OutlineOnSelected : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    public Outline outline;

    void Awake()
    {
        outline.enabled = false;
    }

    public void OnSelect(BaseEventData eventData)
    {
        outline.enabled = true;
    }
    
    public void OnDeselect(BaseEventData eventData)
    {
        outline.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        EventSystem.current.SetSelectedGameObject(gameObject);
        outline.enabled = true;
    }
}
