using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor.Media;
using UnityEngine;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using VisionModels.Input;

namespace Mannequin
{
    public class CreateVideo : MonoBehaviour
    {
        public WebCamTexture _cam;
        public string _camName;
        [SerializeField] private List<Texture2D> images = new List<Texture2D>();
        //private MediaEncoder encoder;
        private string savePath;
        [SerializeField] public bool isRecording = false;
        private VideoTrackAttributes videoAttr;
        private AudioTrackAttributes audioAttr;
        private int sampleFramesPerVideoFrame;
        
        private void SetupRecorder(WebCamTexture webcamTexture)
        {
            Debug.Log("Setting up the recorder");
            _cam = webcamTexture;
            _camName = _cam.deviceName;
        }

        private IEnumerator Record(WebCamTexture image)
        {
            while (isRecording)
            {
                Debug.Log("Recording video");
                Color32[] pixels = image.GetPixels32();
                Texture2D texture = new Texture2D(image.width, image.height, TextureFormat.RGBA32, false);
                texture.SetPixels32(pixels);
                images.Add(texture);
                yield return null;
            }
            Debug.Log("Finished recording");
            isRecording = false;
        }

        public void startRecording(UseWebcam useWebcam)
        {
            isRecording = true;
            SetPaths();
            SetupRecorder(useWebcam.Cam);
            Debug.Log("Setup recorder to record to " + savePath);
            StartCoroutine(Record(_cam));
        }

        private void OnEnable()
        {
            isRecording = FindAnyObjectByType<LaunchManager>().createRecording;
        }

        private void SetPaths()
        {
            Debug.Log("Streaming Path: " + Application.streamingAssetsPath);
            savePath = Application.streamingAssetsPath + "/saves/";
            Debug.Log("SavePath: " + savePath);
            
            int savePathLength = Directory.GetFiles(savePath).Length / 2;
            savePath = Path.Combine(savePath, "video" + savePathLength + ".mp4");
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        }

        private void OnDisable()
        {
            isRecording = false;
        }
    }
}
