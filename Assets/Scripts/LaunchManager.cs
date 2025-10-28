using UnityEngine;
using UnityEngine.UI;
using Shared;
using VisionModels.HandholdDetection;
using VisionModels.PersonDetection;
using VisionModels.PoseDetection;

//TODO: Handle webcam / video / stills
//TODO: Detect person?
//TODO: Pose detect?
//TODO: Detect hand-holds?
//TODO: Choose hand-holds color


public class LaunchManager : MonoBehaviour
{
    private enum InputSource
    {
        Webcam,
        Video,
        Still
    }
    

    [Header("Input Settings")]
    [SerializeField] private InputSource inputSource;
    [SerializeField] private UseWebcam.WebcamResolution webcamResolution;
    [SerializeField] private string videoFilePath, stillFilePath;
    [SerializeField] private bool detectPerson, detectPose, detectHandHolds;
    [SerializeField] private HandholdsDetector.RouteColor routeColor;
    
    [Header("UI Settings")]
    [SerializeField] private RawImage imageDisplay;
    
    private void Awake()
    {
        HandleInput();
        HandleDetectionSettings();
    }

    private void HandleInput()
    {
        switch (inputSource)
        {
            case InputSource.Webcam:
                gameObject.AddComponent<UseWebcam>();
                AppEvents.RaiseRequestUseWebcam(webcamResolution);
                break;
            case InputSource.Video:
                gameObject.AddComponent<UseVideo>();
                AppEvents.RaiseRequestUseVideo(videoFilePath);
                break;
            case InputSource.Still:
                gameObject.AddComponent<UseStill>();
                AppEvents.RaiseRequestUseStill(stillFilePath);
                break;
        }
    }
    
    private void HandleDetectionSettings()
    {
        if (detectPerson)
        {
            var go = new GameObject("PersonDetector");
            go.transform.SetParent(transform);
            go.AddComponent<PersonDetector>();
        }

        if (detectPose)
        {
            var go = new GameObject("PoseDetector");
            go.transform.SetParent(transform);
            go.AddComponent<PoseDetector>();
        }

        if (detectHandHolds)
        {
            var go = new GameObject("HandHoldDetector");
            go.transform.SetParent(transform);
            go.AddComponent<HandholdsDetector>();
        }
    }
}