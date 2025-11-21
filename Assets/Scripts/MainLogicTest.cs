using System.Collections.Generic;
using UnityEngine;
using VisionModels.Utilities;

public class MainLogicTest : MonoBehaviour
{
    //private List<Vector2, bool> _handholdCenters = new();
    private Dictionary<Vector2, bool> _handholdCenters = new();
    private List<DetectedHandhold> _detectedHandholds = new();
    private PoseData _poseData;
    private Vector2 _rightWristPos;
    private Vector2 _leftWristPos;
    
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
        _handholdCenters.Add(center, handhold.HasBeenDetected);
        
        _detectedHandholds.Add(handhold);
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
            _leftWristPos   = _poseData.GetKeypoint(KeypointIndex.LeftWrist);
            //Debug.Log($"Right Wrist Position: {rightWristPos}");
        }
        
        //if (_handholdCenters == null || _handholdCenters.Count == 0) return;
        if (_detectedHandholds == null || _detectedHandholds.Count == 0) return;
        CheckHandholdProximity();
    }
    
    private void CheckHandholdProximity()
    {
        
        float proximityThreshold = 40.0f; // Define a threshold distance
        foreach (var handhold in _detectedHandholds)
        {
            if (handhold.HasBeenDetected) return;
            Vector2 center = new Vector2(handhold.Box.CenterX, handhold.Box.CenterY);
            float distanceRight = Vector2.Distance(_rightWristPos, center);
            if (distanceRight < proximityThreshold)
            {
                Debug.Log($"Right wrist is close to handhold at {handhold} with distance {distanceRight}");
                //AudioManager.Instance.PlayOneShot(FMODEvents.Instance.HandholdContact, this.transform.position);
                
                AppEvents.RaisePotentialHandholdContact();
                handhold.HasBeenDetected = true;

            }
            float distanceLeft = Vector2.Distance(_leftWristPos, center);
            if (distanceLeft < proximityThreshold)
            {
                Debug.Log($"Left wrist is close to handhold at {handhold} with distance {distanceLeft}");
                //AudioManager.Instance.PlayOneShot(FMODEvents.Instance.HandholdContact, this.transform.position);
                
                AppEvents.RaisePotentialHandholdContact();
                handhold.HasBeenDetected = true;
            }
        }
    }
}