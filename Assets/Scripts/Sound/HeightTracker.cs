using UnityEngine;
using FMODUnity;

namespace Sound
{
    public class HeightTracker : MonoBehaviour
    {
        [SerializeField] private AppEventTrigger trigger;
        [SerializeField] private float hipHeight;
        [SerializeField, Range(0f, 1f)] private float height;
        
        private void OnEnable()
        {
            AppEvents.AppEventTriggerReady += OnAppEventTriggerReady;
        }
        
        private void OnDisable()
        {
            AppEvents.AppEventTriggerReady -= OnAppEventTriggerReady;
        }
        
        private void OnAppEventTriggerReady(AppEventTrigger appEventTrigger)
        {
            trigger = appEventTrigger;
            
        }
        
        private void Update()
        {
            if (trigger)
            {
                hipHeight = trigger.hipHeight;
                height = Mathf.InverseLerp(-340f, 340f, hipHeight);
                RuntimeManager.StudioSystem.setParameterByName("height", height);
            }
        }
        
    }
}