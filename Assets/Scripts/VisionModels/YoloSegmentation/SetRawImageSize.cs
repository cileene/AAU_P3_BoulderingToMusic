using UnityEngine;
using UnityEngine.UI;

namespace VisionModels.YoloSegmentation
{
    public class SetRawImageSize : MonoBehaviour
    {
        private RawImage _rawImage;
        private void OnEnable()
        {
            AppEvents.SegRunning += HandleSegRunning;
        }

        private void OnDisable()
        {
            AppEvents.SegRunning -= HandleSegRunning;
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
}