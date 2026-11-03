using UnityEngine;

public class DisableByPlatform : MonoBehaviour
{
    public bool DisableIfAndroid, DisableIfIOS, DisableIfWebGL = false;

    void Start()
    {
#if UNITY_IOS
        if(DisableIfIOS) gameObject.SetActive(false);
#elif UNITY_ANDROID
        if(DisableIfAndroid) gameObject.SetActive(false);
#elif UNITY_WEBGL
        if (DisableIfWebGL) gameObject.SetActive(false);
#endif
    }
}
