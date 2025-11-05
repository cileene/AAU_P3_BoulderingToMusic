using UnityEngine;
using FMODUnity;
using VisionModels.Utilities;

public class FMODEvents : MonoBehaviour
{
    [field: Header("Handhold contact")]
    [field: SerializeField] public EventReference HandholdContact {  get; private set; }
    
    [field: Header("Ambience")]
    [field: SerializeField] public EventReference Ambience {  get; private set; }

    public static FMODEvents Instance { get; private set; }
    private void Awake() //Singleton logic
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
    }


    private PoseData poseData;
    private void OnEnable()
    {
        AppEvents.NewPoseDetected += OnNewPoseDetected;
        AppEvents.NewHandholdDetected += OnNewHandholdDetected;
    }
    private void OnDisable()
    {
        AppEvents.NewPoseDetected -= OnNewPoseDetected;
        AppEvents.NewHandholdDetected -= OnNewHandholdDetected;
    }

    private void OnNewPoseDetected(PoseData pose)
    {
        poseData = pose;
    }

    private void OnNewHandholdDetected(DetectedHandhold handhold)
    {
        //if (condition)
        AudioManager.Instance.PlayOneShot(HandholdContact, transform.position);
    }
}

