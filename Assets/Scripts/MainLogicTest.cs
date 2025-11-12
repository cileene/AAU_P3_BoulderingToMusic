using System.Collections.Generic;
using UnityEngine;
using VisionModels.Utilities;

public class MainLogicTest : MonoBehaviour
{
    private List<Vector2> _handholdCenters = new();
    private PoseData _poseData;
    private Vector2 _rightWristPos;
    
    private void OnEnable()
    {
        AppEvents.NewHandholdDetected += OnNewHandholdDetected;
        AppEvents.NewPoseDetected += OnNewPoseDetected;
    }
    
    private void OnDisable()
    {
        AppEvents.NewHandholdDetected -= OnNewHandholdDetected;
        AppEvents.NewPoseDetected -= OnNewPoseDetected;
    }
    
    private void OnNewHandholdDetected(DetectedHandhold handhold)
    {
        BoundingBox bob = handhold.Box;
        Vector2 center = new Vector2(bob.CenterX, bob.CenterY);
        _handholdCenters.Add(center);
        Debug.Log($"Handhold Detected: {handhold.Label} at {center}");
    }
    
    private void OnNewPoseDetected(PoseData poseData)
    {
        _poseData = poseData;
    }
    
    private void Update()
    {
        if (_poseData != null)
        {
            _rightWristPos  = _poseData.GetKeypoint(KeypointIndex.RightWrist);
            //Debug.Log($"Right Wrist Position: {_rightWristPos}");
        }
        
        if (_handholdCenters == null || _handholdCenters.Count == 0) return;
        CheckHandholdProximity();
    }
    
    private void CheckHandholdProximity()
    {
        float proximityThreshold = 25.0f; // Define a threshold distance
        foreach (var center in _handholdCenters)
        {
            float distance = Vector2.Distance(_rightWristPos, center);
            if (distance < proximityThreshold)
            {
                Debug.Log($"Right wrist is close to handhold at {center} with distance {distance}");
                //AudioManager.Instance.PlayOneShot(FMODEvents.Instance.HandholdContact, this.transform.position);
            }
        }
    }
}