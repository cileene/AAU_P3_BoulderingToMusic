using UnityEngine;
using UnityEngine.UI;

namespace YoloSegmentation
{
    public class IEInferenceTrigger : MonoBehaviour
    {
        [SerializeField] private IEExecutor _ieExecutor;
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
            else if (_webcamReady && !_ieExecutor.IsRunning())
            {
                RunInference();
            }
        }

        private void RunStillImageInference()
        {
            if (!_testImage || _ieExecutor.IsRunning())
            {
                return;
            }

            _outputImage.texture = _testImage;
            _ieExecutor.RunInference(_testImage);
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

            _ieExecutor.RunInference(texture);

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