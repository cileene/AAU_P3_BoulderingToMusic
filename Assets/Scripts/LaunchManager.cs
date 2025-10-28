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
    private enum InputSource { Webcam, Video, Still }

    [Header("Input Settings")]
    [SerializeField] private InputSource inputSource;
    [SerializeField] private UseWebcam.WebcamResolution webcamResolution;
    [SerializeField] private string webcamDeviceName;
    [SerializeField] private string videoFilePath, stillFilePath;
    
    [Header("Detection Settings")]
    [SerializeField] private bool detectPerson;
    [SerializeField] private bool detectPose;
    [SerializeField] private bool detectHandholds;
    [SerializeField] private HandholdsDetector.ProblemColor problemColor;
    
    [Header("UI Settings")]
    [SerializeField] private RawImage imageDisplay;
    
    private void Start()
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
                AppEvents.RaiseRequestUseWebcam(webcamResolution, webcamDeviceName);
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
            new GameObject("PersonDetector", 
                typeof(PersonDetector)).transform.SetParent(transform);
        }

        if (detectPose)
        {
            new GameObject("PoseDetector", 
                typeof(PoseDetector)).transform.SetParent(transform);
        }

        if (detectHandholds)
        {
            new GameObject("HandHoldDetector", 
                typeof(HandholdsDetector)).transform.SetParent(transform);
            
            AppEvents.RaiseRequestProblemColor(problemColor);
        }
    }
}