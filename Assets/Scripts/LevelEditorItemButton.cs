using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LevelEditorItemButton : MonoBehaviour
{
    public string ItemButtonName;
    public GameObject border;

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

    // Scroll order follows the layout: the whole top row left to right, then the whole bottom
    // row left to right, then back to the start.
    private static readonly List<LevelEditorItemButton> _order = new List<LevelEditorItemButton>();

    private static int CompareLayout(LevelEditorItemButton a, LevelEditorItemButton b)
    {
        Vector2 pa = ((RectTransform)a.transform).anchoredPosition;
        Vector2 pb = ((RectTransform)b.transform).anchoredPosition;
        // Buttons in the same row can differ slightly in y, so compare rows coarsely.
        int rowA = Mathf.RoundToInt(pa.y / 20f), rowB = Mathf.RoundToInt(pb.y / 20f);
        if (rowA != rowB) return rowB.CompareTo(rowA); // higher row first
        return pa.x.CompareTo(pb.x);
    }

    private void ClickOffset(int offset)
    {
        _order.Clear();
        transform.parent.GetComponentsInChildren(_order);
        if (_order.Count == 0) return;
        _order.Sort(CompareLayout);
        int i = _order.IndexOf(this);
        if (i < 0) return;
        LevelEditorItemButton target = _order[(i + offset + _order.Count) % _order.Count];
        target.Click(target.ItemButtonName);
    }

    public void ClickNext() => ClickOffset(1);

    public void ClickPrev() => ClickOffset(-1);
}
