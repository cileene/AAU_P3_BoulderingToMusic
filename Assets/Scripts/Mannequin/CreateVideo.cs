using System.Collections.Generic;
using UnityEditor.Media;
using UnityEngine;

namespace Mannequin
{
    public class CreateVideo : MonoBehaviour
    {
        private WebCamTexture _cam;
        private List<Color32[]> images = new List<Color32[]>();
        private MediaEncoder encoder;
        private string videoPath;
        private int videoWidth;
        private int videoHeight;
        public string VideoPath => videoPath;
        public bool recording = false;
        
        private void RecordTexture(WebCamTexture webcamTexture)
        {
            if (recording)
            {
                videoHeight = webcamTexture.height;
                videoWidth = webcamTexture.width;
                var textureBytes = webcamTexture.GetPixels32();
                images.Add(textureBytes);
            }
        }

        public void FormVideo()
        {
            foreach (var image in images)
            {
                Texture2D tex = new Texture2D(videoWidth, videoHeight, TextureFormat.RGBA32, false);
                tex.SetPixels32(image);
                tex.Apply();
                encoder.AddFrame(tex);
            }
        }

        public void EraseVideo()
        {
            DestroyVideo();
        }

        private void OnEnable()
        {
            AppEvents.WebcamReady += RecordTexture;
            videoPath = Application.streamingAssetsPath + "/Videos/" + "Video.mp4";
            var videoAttr = new VideoTrackAttributes
            {
                frameRate = new MediaRational(50),
                width = (uint) _cam.width,
                height = (uint) _cam.height,
                includeAlpha = false
            };
            encoder = new MediaEncoder(videoPath, videoAttr);
        }
        private void OnDisable()
        {
            DestroyVideo();
        }
        private void DestroyVideo()
        {
            images.Clear();
            encoder.Dispose();
        }
    }
}
