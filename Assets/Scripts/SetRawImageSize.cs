using UnityEngine;
using UnityEngine.UI;

public class SetRawImageSize : MonoBehaviour
{
    private RawImage _rawImage;
    private void OnEnable()
    {
        AppEvents.OnSegRunning += HandleSegRunning;
    }

    private void OnDisable()
    {
        AppEvents.OnSegRunning -= HandleSegRunning;
    }
    
    private void Start()
    {
        _rawImage = GetComponent<RawImage>();
    }

    private void HandleSegRunning()
    {
        _rawImage.SetNativeSize();
        Debug.Log($"Webcam size {_rawImage.rectTransform.sizeDelta}");
    }
}