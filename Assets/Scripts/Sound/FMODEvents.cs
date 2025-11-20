using FMODUnity;
using UnityEngine;
using VisionModels.Utilities;

namespace Sound
{
    public class FMODEvents : MonoBehaviour
    {
        [field: SerializeField] public EventReference HandholdContact {  get; private set; }
    
        [field: SerializeField] public EventReference Ambience {  get; private set; }
        private PoseData poseData;

        [SerializeField] private GameObject HandholdsEmitter;

        public static FMODEvents Instance { get; private set; }

        private GameObject _handHolds;
        
        
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

        private void OnEnable()
        {
            AppEvents.NewPoseDetected += OnNewPoseDetected;
            AppEvents.NewHandholdDetected += OnPotentialHandholdContact;
            AppEvents.SoundConfig += OnSoundConfig;
        }
        
        private void OnDisable()
        {
            AppEvents.NewPoseDetected -= OnNewPoseDetected;
            AppEvents.NewHandholdDetected -= OnPotentialHandholdContact;
        }

        private void OnSoundConfig(GameObject handHolds, GameObject bgm, GameObject win, GameObject death)
        {
            
        }

        private void OnNewPoseDetected(PoseData pose)
        {
            poseData = pose;
        }

        private void OnPotentialHandholdContact(DetectedHandhold handhold)
        {
            //AudioManager.Instance.PlayOneShot(HandholdContact, transform.position);
            Debug.Log("Handhold contact sound played");
            HandholdsEmitter.SetActive(true);
        }
    }
}

