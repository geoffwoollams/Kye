using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class CameraAspect : MonoBehaviour
{
    private const float ReferenceAspect = 820f / 600f;
    private const float ReferenceOrthoSize = 11f;

    private Camera cam;
    private int lastWidth;
    private int lastHeight;
    
    public CanvasScaler canvasScaler;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        UpdateCamera();
    }

    private void OnEnable()
    {
        UpdateCamera();
    }

    private void Update()
    {
        // Only do the calculation when the screen size actually changes.
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            UpdateCamera();
        }
    }

    private void UpdateCamera()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;

        float aspect = (float)Screen.width / Screen.height;

        // Never zoom out on screens wider than our reference aspect.
        if (aspect >= ReferenceAspect)
        {
            cam.orthographicSize = ReferenceOrthoSize;
            canvasScaler.matchWidthOrHeight = 1;
        }
        else
        {
            cam.orthographicSize =
                ReferenceOrthoSize * ReferenceAspect / aspect;
            canvasScaler.matchWidthOrHeight = 0;
        }
    }
}