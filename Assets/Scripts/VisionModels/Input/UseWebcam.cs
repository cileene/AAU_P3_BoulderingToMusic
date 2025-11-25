using System;
using UnityEngine;

namespace VisionModels.Input
{
    /// <summary>
    /// Class to assist in the setting up of the relevant webcam. This is the setting intended for live input, with live climbers. It ensures that there is a camera present and determines the sizes of the input,
    /// before calling the next action "RaiseWebcamReady". This event is subscribed to by HandholdsDetector.cs, PersonDetector.cs and PoseDetector.cs, the three modelrunners
    /// </summary>
    public class UseWebcam : MonoBehaviour
    {
        public enum WebcamResolution //TODO: Handle iPhone specific resolutions
        {
            HD1440p,    // 1920*1440
            HD1080p,    // 1920*1080
            HD720p,     // 1280*720
            VGA,        // 640*480
            Macbook     // 1552*1552
        }
        
        private WebCamTexture _cam;
        
        private void OnEnable() => AppEvents.RequestUseWebcam += OnConfigureWebcam;
        
        private void OnDisable()
        {
            AppEvents.RequestUseWebcam -= OnConfigureWebcam;
            
            if (_cam != null)
            {
                _cam.Stop();
                Destroy(_cam);
            }
        }

        private void OnConfigureWebcam(WebcamResolution resolution, string deviceName)
        {
            if (_cam != null)
            {
                _cam.Stop();
                Destroy(_cam);
            }

            (int width, int height) = resolution switch
            {
                WebcamResolution.HD1440p => (1920, 1440),
                WebcamResolution.HD1080p => (1920, 1080),
                WebcamResolution.HD720p => (1280, 720),
                WebcamResolution.VGA => (640, 480),
                WebcamResolution.Macbook => (1552, 1552),
                _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null)
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