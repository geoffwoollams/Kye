using UnityEngine;
using UnityEngine.UI;

public class LevelEditorItemButton : MonoBehaviour
{
    public string ItemButtonName;
    public GameObject border;
    public LevelEditorItemButton PreviousButton, NextButton;

    void Start()
    {
        border = Instantiate(GameController.Instance.LevelEditorItemBorder, Vector3.zero, Quaternion.identity, gameObject.transform);
        border.transform.localPosition = GameController.Instance.LevelEditorItemBorder.transform.localPosition;

        if (this.name == "Kye")
        {
            border.SetActive(true);
            GameController.Instance.levelEditor.SelectedButton = this;
        }
        else
        {
            border.SetActive(false);
        }
    }

    public void Click(string itemName)
    {
        if (GameController.Instance.levelEditor.SelectedButton != null)
        {
            GameController.Instance.levelEditor.SelectedButton.Unclick();
        }

        GameController.Instance.levelEditor.SelectItem(ItemButtonName);
        GameController.Instance.levelEditor.SelectedButton = this;
        border.SetActive(true);
    }

    public void Unclick()
    {
        border.SetActive(false);
    }

    public void ClickNext()
    {
        if (NextButton != null)
        {
            NextButton.Click(NextButton.ItemButtonName);
        }
    }
    
    public void ClickPrev()
    {
        if (PreviousButton != null)
        {
            PreviousButton.Click(PreviousButton.ItemButtonName);
        }
    }
}
