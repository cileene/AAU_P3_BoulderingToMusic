// Adapted from: https://github.com/rikturnbull/xr-image-segmentation

using UnityEngine;
using UnityEngine.UI;

namespace YoloSegmentation
{
    public class InferenceTrigger : MonoBehaviour
    {
        [SerializeField] private Executor executor;
        [SerializeField] private RawImage _outputImage;
        [SerializeField] private bool _useStillImage;
        [SerializeField] private Texture2D _testImage;

        private WebCamTexture _webcamTexture;
        private bool _webcamReady;
        private bool _segEventCalld;

        private void OnEnable()
        {
            AppEvents.OnWebcamReady += HandleWebcamReady;
        }

        private void OnDisable()
        {
            AppEvents.OnWebcamReady -= HandleWebcamReady;
        }

        private void HandleWebcamReady(WebCamTexture webcamTexture)
        {
            _webcamTexture = webcamTexture;
            _webcamReady = true;
        
            if (!_useStillImage)
            {
                _outputImage.texture = _webcamTexture;
            }
        }

        private void Update()
        {
            if (_useStillImage)
            {
                RunStillImageInference();
            }
            else if (_webcamReady && !executor.IsRunning())
            {
                RunInference();
            }
        }

        private void RunStillImageInference()
        {
            if (!_testImage || executor.IsRunning())
            {
                return;
            }

            _outputImage.texture = _testImage;
            executor.RunInference(_testImage);
            //_hasRunStillImageInference = true;
        }

        private void RunInference()
        {
            if (!_webcamTexture || !_webcamReady)
            {
                return;
            }

            // Create a Texture2D from the current webcam frame
            Texture2D texture = new Texture2D(_webcamTexture.width, _webcamTexture.height);
            texture.SetPixels(_webcamTexture.GetPixels());
            texture.Apply();

            executor.RunInference(texture);

            // Clean up the temporary texture
            Destroy(texture);

            if (!_segEventCalld)
            {
                AppEvents.RaiseSegRunning();
                _segEventCalld = true;
            }
        }
    }
}