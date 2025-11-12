using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using VisionModels.Utilities;
using Vector2 = UnityEngine.Vector2;

public class AppEventTrigger : MonoBehaviour
{
    //This script handles the logic for calling the event in app AppEvents eventually playing sound in FMOD

    private List<Vector2> _handholdCenters = new();
    private PoseData _poseData;
    private Vector2 _rightWristPos;
    private Vector2 _leftWristPos;

    private Vector2 previousKeypoint;
    private Queue<Vector2> keypointDelta = new();
    private KeypointDelta RightWristDelta = new();

    public static AppEventTrigger Instance { get; private set; }
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
        //print($"The average movement is: {RightWristDelta.AverageDelta(_rightWristPos)}");
        //RightWristDelta.KeypointStill();
        if (_poseData != null)
        {
            _rightWristPos = _poseData.GetKeypoint(KeypointIndex.RightWrist);
            _leftWristPos = _poseData.GetKeypoint(KeypointIndex.LeftWrist);
        }

        if (_handholdCenters != null && _handholdCenters.Count != 0)
        {
            CheckHandholdProximity();
        }
    }
    /*
    private void CheckIfKeypointIsStill(Vector2 currentKeypoint, float movementThreshold, float stillTime)
    {
        if (previousKeypoint == null)
        {
            previousKeypoint = currentKeypoint;
            return;
        }

        if (delta < movementThreshold)
        {
            stillTimer += deltaTime;
            if (stillTimer >= stillTimeThreshold)
                isStill = true;
        }
        else
        {
            stillTimer = 0f;
            isStill = false;
        }

        previousKeypoint = currentKeypoint;
    }
    */
    private void SortHandholdCentersByY()
    {
        _handholdCenters.Sort((a, b) => b.y.CompareTo(a.y));
        print($"Highest handhold is at y={_handholdCenters[0].y}");
    }
    private void OnNewHandholdDetected(DetectedHandhold handhold) //Remember to add logic that removes handholds again if they should disappear
    {
        BoundingBox bob = handhold.Box;
        Vector2 center = new Vector2(bob.CenterX, bob.CenterY);
        _handholdCenters.Add(center);
        //Debug.Log($"Handhold Detected: {handhold.Label} at {center}");
        SortHandholdCentersByY();
    }
    private void CheckHandholdProximity() //Checks whether a wrist keypoint is close to a handhold center
    {
        float proximityThreshold = 25.0f; //Define a threshold distance
        for (int i = 0; i < _handholdCenters.Count; i++)
        {
            var center = _handholdCenters[i];
            float rightWristDistance = Vector2.Distance(_rightWristPos, center);
            float leftWristDistance = Vector2.Distance(_leftWristPos, center);

            if (rightWristDistance < proximityThreshold || leftWristDistance < proximityThreshold)
            {
                Debug.Log($"A wrist is close to handhold {i} at {center}");
                AppEvents.RaisePotentialHandholdContact();

                if (i != 0) break; //Checks if the contacted handhold is the first index, meaning that it has the highest y value
                
                AppEvents.RaisePotentialHighestHandholdContact();
                print($"Highest handhold was touched!");
                
            }
        }
    }
}
public class KeypointDelta : MonoBehaviour
{
    private Queue<float> deltas = new();
    private float currentDelta;
    private int deltasMaxLength = 10;
    private Vector2 previousPoint = Vector2.zero;

    public bool KeypointStill 
    { 
        get { return _keypointStill; }
    }
    private bool _keypointStill = false;
    
    private float movementThreshold = 10f;
    private float stillnessTimeThreshold = 0.5f;
    private float timer = 0; //Increments when keypoint is still. Used to check against stillnessTimeThreshold

    /// <summary>
    /// Will calculate the average movement over a number of points. The number of points is set by deltasMaxLength
    /// </summary>
    /// <param name="keypoint"></param>
    public void AverageDelta(Vector2 keypoint)
    {
        if (previousPoint == Vector2.zero)
        {
            previousPoint = keypoint;
            return;
        }

        float delta = Vector2.Distance(keypoint, previousPoint) / Time.deltaTime; //Scales delta so it isn't frame rate dependant 
        deltas.Enqueue(delta);
        if (deltas.Count > deltasMaxLength)
        {
            deltas.Dequeue();
        }
        currentDelta = deltas.Average();
        previousPoint = keypoint;
    }

    public void CheckIfKeypointIsStill(Vector2 keypoint)
    {
        AverageDelta(keypoint);
        if (currentDelta < movementThreshold)
        {
            timer += Time.deltaTime;
            if (timer > stillnessTimeThreshold)
            {
                _keypointStill = true;
                print("Hand still?:"+_keypointStill);
            }
            else
            {
                _keypointStill = false;
            }
        }
        else
        {
            timer = 0;
            _keypointStill = false;
        }
    }
}