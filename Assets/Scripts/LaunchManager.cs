using UnityEngine;
using UnityEngine.UI;
using Unity.InferenceEngine;
using VisionModelsV2;
using VisionModelsV2.Input;
using VisionModelsV2.ModelRunners;
using VisionModelsV2.Utilities;

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
    
    private void Awake()
    {
        Application.targetFrameRate = targetFrameRate;
    }
    
    private void Start()
    {
        if (showDebug) gameObject.AddComponent<ModelDebugger>();
        if (runLogic) gameObject.AddComponent<MainLogicTest>();
        
        HandleDetectionSettings();
        HandleInput();
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
            
            AppEvents.RaiseConfigurePersonDetector(
                detectPersonModel,
                detectPersonClasses,
                imageDisplay,
                font,
                borderTexture);
        }

        if (detectPose)
        {
            new GameObject("PoseDetector", 
                typeof(PoseDetector)).transform.SetParent(transform);
            
            AppEvents.RaiseConfigurePoseDetector(
                modelAsset,
                imageDisplay,
                borderTexture);
        }

        if (detectHandholds)
        {
            new GameObject("HandholdsDetector", 
                typeof(HandholdsDetector)).transform.SetParent(transform);

            AppEvents.RaiseConfigureHandholdsDetector(
                detectHandholdsModel,
                classesAsset,
                problemColor,
                imageDisplay,
                font,
                borderTexture,
                handholdPersistenceFrames);
        }
    }
}