using System.Collections.Generic;
using UnityEngine;
using VisionModels.Utilities;

public class AppEventTrigger : MonoBehaviour
{
    //This script handles the logic for calling the event in app AppEvents eventually playing sound in FMOD
    
    private List<Vector2> _handholdCenters = new();
    private Vector2 _highestHandHoldCenter;
    private PoseData _poseData;
    private Vector2 _rightWristPos;
    private Vector2 _leftWristPos;

    private void OnEnable()
    {
        AppEvents.NewHandholdDetected += OnNewHandholdDetected; //Used for feeding data into this script
        AppEvents.NewPoseDetected += OnNewPoseDetected;
    }
    private void OnDisable()
    {
        AppEvents.NewHandholdDetected -= OnNewHandholdDetected;
        AppEvents.NewPoseDetected -= OnNewPoseDetected;
    }
    private void OnNewPoseDetected(PoseData poseData)
    {
        _poseData = poseData;
    }

    private void Update()
    {
        if (_poseData != null)
        {
            _rightWristPos = _poseData.GetKeypoint(KeypointIndex.RightWrist);
            _leftWristPos = _poseData.GetKeypoint(KeypointIndex.LeftWrist);
        }

        if (_handholdCenters != null || _handholdCenters.Count != 0)
        {
            CheckHandholdProximity();
        }
    }

    private void FindHighestHandhold()
    {
        _handholdCenters.Sort((a, b) => b.y.CompareTo(a.y));
        print($"Highest handhold is at y: {_handholdCenters[0].y}");
    }
    private void OnNewHandholdDetected(DetectedHandhold handhold) //Remember to add logic that removes handholds again if they should disappear
    {
        BoundingBox bob = handhold.Box;
        Vector2 center = new Vector2(bob.CenterX, bob.CenterY);
        _handholdCenters.Add(center);
        //Debug.Log($"Handhold Detected: {handhold.Label} at {center}");
        FindHighestHandhold();
    }

    private void CheckHandholdProximity() //Checks whether a wrist keypoint is close to a handhold center
    {
        float proximityThreshold = 25.0f; //Define a threshold distance
        foreach (var center in _handholdCenters)
        {
            float rightWristDistance = Vector2.Distance(_rightWristPos, center);
            float leftWristDistance = Vector2.Distance(_leftWristPos, center);
            if (rightWristDistance < proximityThreshold || leftWristDistance < proximityThreshold)
            {
                Debug.Log($"A wrist is close to a handhold at {center}");
                AppEvents.RaisePotentialHandholdContact();
                //Add logic to check if the handhold is the highest handhold
            }
        }
    }
}