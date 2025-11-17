using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Unity.VisualScripting;
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
    private KeypointDelta RightWristDelta = new();
    private KeypointDelta LeftWristDelta = new();

    private static int _poseDataKeypointLength = Enum.GetValues(typeof(KeypointIndex)).Length; //Will get the length of KeypointIndex
    private Queue<Vector2>[] _poseDataKeypoints = new Queue<Vector2>[_poseDataKeypointLength];
    private int maxPoseDataKeypointsLength = 5;
    private void InitializePoseDataKeypoints()
    {
        for (int i = 0;  i < _poseDataKeypointLength; i++) //Fills all the Queues with up vectors
        {
            _poseDataKeypoints[i] = new Queue<Vector2>(Enumerable.Repeat(Vector2.one, maxPoseDataKeypointsLength));
        }
    }
    private void Start()
    {
        InitializePoseDataKeypoints();
    }

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
        if (_poseData != null)
        {
            _rightWristPos = _poseData.GetKeypoint(KeypointIndex.RightWrist);
            _leftWristPos = _poseData.GetKeypoint(KeypointIndex.LeftWrist);
        }
        //print($"Right wrist still?: {RightWristDelta.KeypointStill}");
        if (_handholdCenters != null && _handholdCenters.Count != 0)
        {
            RightWristDelta.CheckHandholdProximity(_handholdCenters, _rightWristPos);
            LeftWristDelta.CheckHandholdProximity(_handholdCenters, _leftWristPos);
            //print($"Right wrist delta: {RightWristDelta.currentDelta}");
            //print($"Left wrist delta: {LeftWristDelta.currentDelta}");
            if (RightWristDelta.LikelyKeypointOnHold)
            {
                print("Climber touched hold with right hand!");
            }
            if (LeftWristDelta.LikelyKeypointOnHold)
            {
                print("Climber touched hold with left hand!");
            }
            if(LeftWristDelta.LikelyKeypointOnHighestHold && RightWristDelta.LikelyKeypointOnHighestHold)
            {
                print("Climber finished the climb!");
            }
        }
        for (int i = 0;  i < _poseDataKeypointLength; i++)
        {

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
    }
    private void OnNewHandholdDetected(DetectedHandhold handhold)
    {
        BoundingBox bob = handhold.Box;
        Vector2 center = new Vector2(bob.CenterX, bob.CenterY);
        _handholdCenters.Add(center);
        SortHandholdCentersByY();
    }

    private bool ClimberJumpedDown()
    {
        for (int i = 0; i < _poseDataKeypointLength; i++)
        {
            KeypointIndex currentIndex = (KeypointIndex)i;

            if (_poseData.GetKeypoint(currentIndex) == Vector2.zero) //If the keypoint isn't detected by the model it returns a zero vector
            {
                continue;
            }

            _poseDataKeypoints[i].Enqueue(_poseData.GetKeypoint(currentIndex));

            if (_poseDataKeypoints[i].Count > maxPoseDataKeypointsLength)
            {
                _poseDataKeypoints[i].Dequeue();
            }
        }

        for (int i = 0; i < _poseDataKeypointLength; i++)
        {
            for (int j = 0; j < _poseDataKeypoints[i].Count; j++)
            {
            }
        }

        return true;
    }
    private class KeypointDelta
    /*This helper class is used to detect whether a keypoint is still and next to a handhold
     * (likely indicating that the climber touched a hold).
     * Call CheckIfKeypointIsStill() inside Update() inside AppEventTrigger
     * and use the bool LikelyKeypointOnHold to trigger logic in FMOD.
    */
    {
        //Variables related to AverageDelta()
        private Queue<float> deltas = new();
        public float currentDelta;
        private int deltasMaxLength = 10;
        private Vector2 previousPoint = Vector2.zero;

        //Variables related to CheckIfKeypointIsStill()
        public bool KeypointStill
        {
            get { return _keypointStill; }
        }
        private bool _keypointStill = false;

        private float movementThreshold = 200f;
        private float stillnessTimeThreshold = 0.3f;
        private float timer = 0; //Increments when keypoint is still. Used to check against stillnessTimeThreshold
        private float timerDecay = 0.3f;

        //Variables related to CheckHandholdProximity()
        private float proximityThreshold = 50.0f;
        public bool LikelyKeypointOnHold
        {
            get { return _likelyKeypointOnHold; }
        }
        private bool _likelyKeypointOnHold = false;

        public bool LikelyKeypointOnHighestHold
        {
            get { return _likelyKeypointOnHighestHold; }
        }
        private bool _likelyKeypointOnHighestHold = false;

        public KeypointDelta()
        {

        }
        public KeypointDelta(float thresholdProximity)
        {
            proximityThreshold = thresholdProximity;
        }

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
        /// <summary>
        /// Will set _keypointStill to true if the keypoint input is still for a certain amount of time
        /// </summary>
        /// <param name="keypoint"></param>
        public void CheckIfKeypointIsStill(Vector2 keypoint)
        {
            AverageDelta(keypoint);
            if (currentDelta < movementThreshold)
            {
                timer += Time.deltaTime;
                if (timer > stillnessTimeThreshold)
                {
                    _keypointStill = true;
                }
                else
                {
                    _keypointStill = false;
                }
            }
            else
            {
                timer = Mathf.Max(0, timer - Time.deltaTime * timerDecay); //Smooths the decay of the timer so a keypoint jittering will not affect stillness detection too much
                _keypointStill = false;
            }
        }
        /// <summary>
        /// Will check if a keypoint is still and close to a handhold
        /// </summary>
        /// <param name="handholdCenters"></param>
        /// <param name="keypoint"></param>
        public void CheckHandholdProximity(List<Vector2> handholdCenters, Vector2 keypoint)
        {
            CheckIfKeypointIsStill(keypoint);
            if (!_keypointStill)
            {
                _likelyKeypointOnHold = false;
                _likelyKeypointOnHighestHold = false;
                return;
            }
            _likelyKeypointOnHold = false;
            _likelyKeypointOnHighestHold = false;

            for (int i = 0; i < handholdCenters.Count; i++)
            {
                var center = handholdCenters[i];
                float distance = Vector2.Distance(keypoint, center);

                if (distance < proximityThreshold)
                {
                    _likelyKeypointOnHold = true;

                    if (i == 0)
                    {
                        _likelyKeypointOnHighestHold = true;
                    }
                    break;
                }
            }
        }
    }
}