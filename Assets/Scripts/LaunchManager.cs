using Configs;
using Sound;
using UnityEngine;
using UnityEngine.UI;
using Unity.InferenceEngine;
using VisionModels.Input;
using VisionModels.ModelRunners;
using VisionModels.Utilities;


public class LaunchManager : MonoBehaviour
{
    private enum InputSource { Webcam, Video, Still }

    [Header("Input")]
    [SerializeField] private InputSource inputSource;
    [SerializeField] private UseWebcam.WebcamResolution webcamResolution = UseWebcam.WebcamResolution.Macbook;
    [SerializeField] private string webcamDeviceName;
    [SerializeField] private string videoName, stillFilePath;

    [Header("Settings")]
    [SerializeField] private bool runLogic;
    [SerializeField] private bool showDebug;
    [SerializeField] private int targetFrameRate = 40;
    [Tooltip("Drag a border box texture here")]
    [SerializeField] private Texture2D borderTexture;
    [Tooltip("Select an appropriate font for the labels")]
    [SerializeField] private Font font;
    [SerializeField] private RawImage imageDisplay;
    
    [Header("Detect Person")]
    [SerializeField] private bool detectPerson;
    [Tooltip("Drag a YOLO model .onnx file here")]
    [SerializeField] private ModelAsset detectPersonModel;
    [Tooltip("Drag the classes.txt here")]
    [SerializeField] private TextAsset detectPersonClasses;
    
    [Header("Detect Handholds")]
    [SerializeField] private bool detectHandholds;
    [SerializeField] private bool continuousHandholdDetection;
    [SerializeField] private HandholdsDetector.ProblemColor problemColor = HandholdsDetector.ProblemColor.All;
    [SerializeField] private int handholdPersistenceFrames = 30;
    [Tooltip("Drag a YOLO model .onnx file here")]
    public ModelAsset detectHandholdsModel;
    [Tooltip("Drag the classes.txt here")]
    public TextAsset classesAsset;
    
    [Header("Detect Pose")]
    [SerializeField] private bool detectPose;
    [Tooltip("Drag your YOLO11n-pose .onnx model here")]
    public ModelAsset modelAsset;

    [Header("FMOD Sound")] 
    [SerializeField] private GameObject handholdsSound;
    [SerializeField] private GameObject bgmSound;
    [SerializeField] private GameObject winSound;
    [SerializeField] private GameObject deathSound;
    [SerializeField] private bool enableHeightTracking;
    
    private void Awake()
    {
        Application.targetFrameRate = targetFrameRate;
    }
    
    private void Start()
    {
        if (enableHeightTracking) gameObject.AddComponent<HeightTracker>();
        gameObject.AddComponent<SoundPlayer>();
        if (runLogic) gameObject.AddComponent<AppEventTrigger>();
        HandleSoundConfig();
        if (showDebug) gameObject.AddComponent<ModelDebugger>();
        HandleDetectionSettings();
        HandleInput();
    }
    
    private void HandleSoundConfig()
    {
        var config = new SoundConfig
        {
            HandholdsSound = handholdsSound,
            BgmSound = bgmSound,
            WinSound = winSound,
            FallSound = deathSound
        };
        
        AppEvents.RaiseSoundConfig(config);
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
                AppEvents.RaiseRequestUseVideo(videoName);
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
            
            var config = new PersonDetectorConfig
            {
                Model = detectPersonModel,
                Classes = detectPersonClasses,
                RawImage = imageDisplay,
                Font = font,
                BorderTexture = borderTexture
            };
            
            AppEvents.RaiseConfigurePersonDetector(config);
        }

        if (detectPose)
        {
            new GameObject("PoseDetector", 
                typeof(PoseDetector)).transform.SetParent(transform);
            
            var config = new PoseDetectorConfig
            {
                Model = modelAsset,
                RawImage = imageDisplay,
                BorderTexture = borderTexture
            };
            
            AppEvents.RaiseConfigurePoseDetector(config);
        }

        if (detectHandholds)
        {
            new GameObject("HandholdsDetector", 
                typeof(HandholdsDetector)).transform.SetParent(transform);
            
            var config = new HandholdsDetectorConfig
            {
                Model = detectHandholdsModel,
                Classes = classesAsset,
                ProblemColor = problemColor,
                RawImage = imageDisplay,
                Font = font,
                BorderTexture = borderTexture,
                KeepHandholdsFrames = handholdPersistenceFrames,
                ContinuousHandholdDetection = continuousHandholdDetection
            };

            AppEvents.RaiseConfigureHandholdsDetector(config);
        }
    }
}