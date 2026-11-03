using System.Collections.Generic;
using UnityEngine;

public class PortraitScaler : MonoBehaviour
{
    [Header("Targets")]
    public List<RectTransform> targets = new List<RectTransform>();

    [Header("Scales")]
    public float landscapeScale = 1f;
    public float portraitScale = 1.5f;
    public float tallPortraitScale = 2f;

    [Header("Thresholds (height / width)")]
    [Tooltip("Aspect at or above which tallPortraitScale is used. 16:9 portrait = 1.78, 19.5:9 = 2.17")]
    public float tallPortraitAspect = 1.7f;

    // Remember each target's original scale so we multiply from it rather than compounding.
    private readonly Dictionary<RectTransform, Vector3> _originalScales = new Dictionary<RectTransform, Vector3>();

    private int _lastWidth;
    private int _lastHeight;

    private void OnEnable()
    {
        Apply();
    }

    private void OnDisable()
    {
        Restore();
    }

    private void Update()
    {
        // Unity has no reliable orientation-changed event, so poll the screen size.
        if (Screen.width != _lastWidth || Screen.height != _lastHeight)
        {
            Apply();
        }
    }

    /// <summary>Call this if you add/remove targets at runtime.</summary>
    public void Apply()
    {
        _lastWidth = Screen.width;
        _lastHeight = Screen.height;

        float factor = GetScaleFactor();

        foreach (RectTransform rt in targets)
        {
            if (rt == null) continue;

            if (!_originalScales.ContainsKey(rt))
                _originalScales[rt] = rt.localScale;

            if (rt.name.Contains("LevelCompleteStuff") && factor > portraitScale)
                rt.localScale = _originalScales[rt] * portraitScale;
            else
                rt.localScale = _originalScales[rt] * factor;
        }
    }

    private float GetScaleFactor()
    {
        if (Screen.width <= 0) return landscapeScale;

        float aspect = (float)Screen.height / Screen.width;

        if (aspect <= 1f) return landscapeScale;              // landscape or square
        if (aspect < tallPortraitAspect) return portraitScale; // portrait
        return tallPortraitScale;                              // tall portrait
    }

    private void Restore()
    {
        foreach (var kvp in _originalScales)
        {
            if (kvp.Key != null)
                kvp.Key.localScale = kvp.Value;
        }
        _originalScales.Clear();
    }
}