using UnityEngine;
using UnityEngine.UI;

public class UseWebcam : MonoBehaviour
{
    [SerializeField] public RawImage displayImage;
    [SerializeField] public WebcamResolution resolution = WebcamResolution.HD1440p;
    [HideInInspector] public WebCamTexture cam;

    public enum WebcamResolution
    {
        HD1440p = 0, // 1920*1440
        HD1080p = 1, // 1920*1080
        HD720p = 2, // 1280*720
        VGA = 3 // 640*480
    }

    private void Start()
    {
        SetupInput();
    }

    private void Update()
    {
        if (cam != null && cam.isPlaying)
            displayImage.texture = cam;
    }

    private void SetupInput()
    {
        int width = resolution switch
        {
            WebcamResolution.HD1440p => 1920,
            WebcamResolution.HD1080p => 1920,
            WebcamResolution.HD720p => 1280,
            WebcamResolution.VGA => 640,
        };
    
        int height = resolution switch
        {
            WebcamResolution.HD1440p => 1440,
            WebcamResolution.HD1080p => 1080,
            WebcamResolution.HD720p => 720,
            WebcamResolution.VGA => 480,
        };
        
        cam = new WebCamTexture(requestedWidth: width, requestedHeight: height);
        cam.Play();
    }
}