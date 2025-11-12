using System.Collections.Generic;
using System.IO;
using UnityEditor.Media;
using UnityEngine;
using VisionModels.Input;

namespace Mannequin
{
    public class CreateVideo
    {
        private WebCamTexture _cam;
        private List<byte[]> images = new List<byte[]>();
        private MediaEncoder encoder;
        private string videoPath;
        public string VideoPath => videoPath;
        public bool recording = false;
        public GameObject Player;

        public CreateVideo(UseWebcam useWebcam)
        {
            _cam = useWebcam.Cam;
        }
        public void RecordTexture(byte[] textureBytes)
        {
            if (recording)
            {
                images.Add(textureBytes);
            }
        }

        private void FormVideo()
        {
            foreach (byte[] image in images)
            {
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadRawTextureData(image);
                tex.Apply();
                byte[] bytes = tex.EncodeToPNG();
                encoder.AddFrame(tex);
            }
        }

        private void OnEnable()
        {
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
