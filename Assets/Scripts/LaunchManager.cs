using UnityEngine;
using UnityEngine.UI;
using Shared;
using PersonDetection;
using PoseDetection;
using HandHoldDetection;

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
    
    private enum RouteColor //TODO: Fix the naming of colors in training
    {
        All,
        Gray,
        orange,
        black,
        blue,
        green,
        purple,
        red,
        Turquise,
        volume,
        white,
        yellow
    }

    [Header("Input Settings")]
    [SerializeField] private InputSource inputSource;
    [SerializeField] private bool detectPerson, detectPose, detectHandHolds;
    [SerializeField] private RouteColor routeColor;
    
    [Header("UI Settings")]
    [SerializeField] private RawImage imageDisplay;

    private UseWebcam _useWebcam;
    private UseVideo _useVideo;
    private UseStill _useStill;
    
    private PersonDetector personDetector;
    private PoseDetector _poseDetector;
    private HandHoldDetector _handHoldDetector;
    
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
                _useWebcam = gameObject.AddComponent<UseWebcam>();
                break;
            case InputSource.Video:
                _useVideo = gameObject.AddComponent<UseVideo>();
                break;
            case InputSource.Still:
                _useStill = gameObject.AddComponent<UseStill>();
                break;
            default:
                throw new System.ArgumentOutOfRangeException();
        }
    }
    
    private void HandleDetectionSettings()
    {
        if (detectPerson)
        {
            personDetector = gameObject.AddComponent<PersonDetector>();
        }

        if (detectPose)
        {
            _poseDetector = gameObject.AddComponent<PoseDetector>();
        }

        if (detectHandHolds)
        {
            _handHoldDetector = gameObject.AddComponent<HandHoldDetector>();
        }
    }
}