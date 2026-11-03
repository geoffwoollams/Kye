using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class ScrollToSelected : MonoBehaviour
{
    private ScrollRect scrollRect;
    private RectTransform viewportRT;
    private GameObject lastSelected;
    private Coroutine scrollCoroutine;

    [Header("Scrolling Settings")]
    public float padding = 10f; // Extra space so buttons don't hit the edge
    public float scrollDuration = 0.12f; // Lower is faster (was 0.2f)

    void Start()
    {
        scrollRect = GetComponent<ScrollRect>();
        viewportRT = scrollRect.viewport;
    }

    void LateUpdate()
    {
        GameObject current = EventSystem.current.currentSelectedGameObject;

        if (current == null || current == lastSelected) return;
        if (!current.transform.IsChildOf(scrollRect.content)) return;

        lastSelected = current;
        RectTransform targetRT = current.GetComponent<RectTransform>();

        if (scrollCoroutine != null) StopCoroutine(scrollCoroutine);
        scrollCoroutine = StartCoroutine(SmoothKeepInView(targetRT));
    }

    IEnumerator SmoothKeepInView(RectTransform target)
    {
        float contentHeight = scrollRect.content.rect.height;
        float viewportHeight = viewportRT.rect.height;
        float scrollRange = contentHeight - viewportHeight;

        if (scrollRange <= 0) yield break;

        Vector3[] viewCorners = new Vector3[4];
        viewportRT.GetWorldCorners(viewCorners);

        // Apply padding to the viewport boundaries
        float viewBottom = viewCorners[0].y + padding;
        float viewTop = viewCorners[1].y - padding;

        Vector3[] targetCorners = new Vector3[4];
        target.GetWorldCorners(targetCorners);
        float targetBottom = targetCorners[0].y;
        float targetTop = targetCorners[1].y;

        float currentNorm = scrollRect.verticalNormalizedPosition;
        float destinationNorm = currentNorm;

        // Calculate step based on world-space difference
        if (targetBottom < viewBottom)
        {
            // Moving down: add padding to the calculation
            float step = (viewBottom - targetBottom) / scrollRange;
            destinationNorm = currentNorm - step;
        }
        else if (targetTop > viewTop)
        {
            // Moving up: subtract padding from the calculation
            float step = (targetTop - viewTop) / scrollRange;
            destinationNorm = currentNorm + step;
        }
        else
        {
            yield break; // Already in view
        }

        float elapsed = 0f;
        while (elapsed < scrollDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // Use SmoothStep for a more premium "UI feel" than linear Lerp
            float t = Mathf.SmoothStep(0, 1, elapsed / scrollDuration);
            scrollRect.verticalNormalizedPosition = Mathf.Lerp(currentNorm, destinationNorm, t);
            yield return null;
        }

        scrollRect.verticalNormalizedPosition = destinationNorm;
    }
}