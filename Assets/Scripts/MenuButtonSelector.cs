 using System;
 using System.Collections;
 using System.Collections.Generic;
 using UnityEngine;
 using UnityEngine.EventSystems;
 using UnityEngine.UI;


[RequireComponent(typeof(Selectable))]
public class MenuButtonSelector : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
{
    private Outline outline;
    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        outline = GetComponent<Outline>();
        outline.enabled = false;
    }

    public void Update()
    {
        if (EventSystem.current.currentSelectedGameObject == this.gameObject)
        {
            outline.enabled = true;
        }
        else
        {
            outline.enabled = false;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log(eventData.pointerEnter.name);
        
        if(button && !button.interactable)
            return;

        //if (!EventSystem.current.alreadySelecting)
            EventSystem.current.SetSelectedGameObject(this.gameObject);
    }
    
    public void OnSelect(BaseEventData eventData)
    {
        if (button && !button.interactable)
        {
            StartCoroutine(RedirectSelection());
        }
    }

    private IEnumerator RedirectSelection()
    {
        yield return null;
        EventSystem.current.SetSelectedGameObject(button.navigation.selectOnUp.gameObject);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (eventData.selectedObject == null)
        {
            EventSystem.current.SetSelectedGameObject(this.gameObject);
        }
    }
 }