using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class WebcamTexture : MonoBehaviour
{
    private WebCamTexture _cam;
    public bool useWebcam = true;
    public string webcamDeviceName;
    public bool preferFrontCamera = true;
    public string videoFilename;
    public RawImage displayImage; // Assign in Inspector
    private VideoPlayer _video;
    
    
    private void Start()
    {
        SetupInput();
    }

    private void Update()
    {
        if (useWebcam && _cam != null && _cam.isPlaying)
            displayImage.texture = _cam;
        else if (!useWebcam && _video != null && _video.texture != null)
            displayImage.texture = _video.texture;
    }
    
    private void SetupInput() //TODO: the non webcam part could/should be removed
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
            if (!chosen.HasValue && preferFrontCamera)
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
            _video = gameObject.AddComponent<VideoPlayer>();
            _video.renderMode = VideoRenderMode.APIOnly;
            _video.source = VideoSource.Url;
            _video.url = Path.Join(Application.streamingAssetsPath, videoFilename);
            _video.isLooping = true;
            _video.Play();
        }
    }
}