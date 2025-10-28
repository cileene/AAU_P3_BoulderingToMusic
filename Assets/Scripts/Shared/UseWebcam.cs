using UnityEngine;

namespace Shared
{
    public class UseWebcam : MonoBehaviour
    {
        public enum WebcamResolution //TODO: Handle iPhone specific resolutions
        {
            HD1440p = 0, // 1920*1440
            HD1080p = 1, // 1920*1080
            HD720p = 2, // 1280*720
            VGA = 3, // 640*480
            MacBook = 4 // 1552*1552
        }
        
        private WebCamTexture _cam;
        
        private void OnEnable() => AppEvents.RequestUseWebcam += OnConfigureWebcam;
        
        private void OnDisable() => AppEvents.RequestUseWebcam -= OnConfigureWebcam;

        private void OnConfigureWebcam(WebcamResolution resolution, string deviceName)
        {
            int width = resolution switch
            {
                WebcamResolution.HD1440p => 1920,
                WebcamResolution.HD1080p => 1920,
                WebcamResolution.HD720p => 1280,
                WebcamResolution.VGA => 640,
                WebcamResolution.MacBook => 1552
            };
    
            int height = resolution switch
            {
                WebcamResolution.HD1440p => 1440,
                WebcamResolution.HD1080p => 1080,
                WebcamResolution.HD720p => 720,
                WebcamResolution.VGA => 480,
                WebcamResolution.MacBook => 1552
            };
            
            if (string.IsNullOrEmpty(deviceName))
            {
                _cam = new WebCamTexture(requestedWidth: width, requestedHeight: height);
            }
            else
            {
                _cam = new WebCamTexture(deviceName, width, height);
            }
            
            _cam.Play();
            AppEvents.RaiseWebcamReady(_cam);
        }
    }
}