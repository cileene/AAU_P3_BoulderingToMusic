using System.IO;
using UnityEngine;
using UnityEngine.Video;

public class CameraDisplay : MonoBehaviour
{
    // Inputs
    private VideoPlayer _video;
    private WebCamTexture _cam;
    
    [Header("Input")]
    [SerializeField] private bool useWebcam = true;
    [Tooltip("Use empty to pick default camera")]
    [SerializeField] private string webcamDeviceName = "";
    [Tooltip("Video file in Assets/StreamingAssets if not using webcam")]
    [SerializeField] private string videoFilename = "giraffes.mp4";
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SetupInput();
    }

    private void Update()
    {
        
    }

    private void SetupInput()
    {
        if (useWebcam)
        {
            WebCamDevice? chosen = null;

            // explicit name wins
            if (!string.IsNullOrEmpty(webcamDeviceName))
            {
                foreach (var d in WebCamTexture.devices)
                    if (d.name == webcamDeviceName) { chosen = d; break; }
            }

            // otherwise prefer front camera on mobile
            if (!chosen.HasValue)
            {
                foreach (var d in WebCamTexture.devices)
                    if (d.isFrontFacing) { chosen = d; break; }
            }

            // fallback: first available
            if (!chosen.HasValue && WebCamTexture.devices.Length > 0)
                chosen = WebCamTexture.devices[0];

            _cam = chosen.HasValue
                ? new WebCamTexture(chosen.Value.name, 1280, 720, 30)
                : new WebCamTexture(1280, 720, 30);

            _cam.Play();
        }
        else
        {
            Debug.Log("Playing video");
            _video = gameObject.AddComponent<VideoPlayer>();
            _video.renderMode = VideoRenderMode.APIOnly;
            _video.source = VideoSource.Url;
            _video.url = Path.Join(Application.streamingAssetsPath, videoFilename);
            _video.isLooping = true;
            _video.Play();
        }
    }
    // Update is called once per frame
}
