using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VisionModels.Utilities;

namespace Sound
{
    //This script handles the logic for calling the event in app AppEvents eventually playing sound in FMOD
    public class AppEventTrigger : MonoBehaviour
    {
        private List<Vector2> _handholdCenters = new();
        private PoseData _poseData;
        private Vector2 _rightWristPos;
        private Vector2 _leftWristPos;
        private KeypointTracker _rightWristTracker = new();
        private KeypointTracker _leftWristTracker = new();
        //Will get the length of the enum KeypointIndex
        private static int _keypointCount = Enum.GetValues(typeof(KeypointIndex)).Length; 
        private Queue<Vector2>[] _keypointPositionHistory = new Queue<Vector2>[_keypointCount];
        private int _keypointHistorySize = 5;

        private float _climbFinishTimerThreshold = 0.5f;
        private float _climbFinishTimer = 0f;
        private float _climbFinishTimerDecayRate = 1f;
        private bool _climbFinished = false;
        private float _climbFinishedCooldown = 5f;

        private float _climberFallingThreshold = -500f;
        private bool _climberIsFalling = false;
        private bool _canTriggerFallingEvent = true;
        
        public float sumOfDeltas;
        public float hipHeight;

        public static AppEventTrigger Instance { get; private set; }


        private void OnEnable()
        {
            AppEvents.NewHandholdDetected += OnNewHandholdDetected; //Used for feeding data into this script
            AppEvents.NewPoseDetected += OnNewPoseDetected;
            _rightWristTracker.OnHoldContactDetected += OnHandHoldContact;
            _leftWristTracker.OnHoldContactDetected += OnHandHoldContact;
        }

        private void OnDisable()
        {
            AppEvents.NewHandholdDetected -= OnNewHandholdDetected;
            AppEvents.NewPoseDetected -= OnNewPoseDetected;
            _rightWristTracker.OnHoldContactDetected -= OnHandHoldContact;
            _leftWristTracker.OnHoldContactDetected -= OnHandHoldContact;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) //Singleton logic
            {
                Destroy(this);
            }
            else
            {
                Instance = this;
            }

            InitializePoseDataKeypoints();
        }
        
        private void Start()
        {
            AppEvents.RaiseAppEventTriggerReady(this);
        }

        private void Update()
        {
            CalculateHipHeight();
            CheckFinishClimb();
            CheckClimberFalling();

            if (_poseData != null)
            {
                _rightWristPos = _poseData.GetKeypoint(KeypointIndex.RightWrist);
                _leftWristPos = _poseData.GetKeypoint(KeypointIndex.LeftWrist);
            }

            if (_handholdCenters != null && _handholdCenters.Count != 0)
            {
                _rightWristTracker.EvaluateHoldContact(_handholdCenters, _rightWristPos, true);
                _leftWristTracker.EvaluateHoldContact(_handholdCenters, _leftWristPos, false);
            }
        }

        private void OnNewHandholdDetected(DetectedHandhold handhold)
        {
            BoundingBox bob = handhold.Box;
            Vector2 center = new Vector2(bob.CenterX, bob.CenterY);
            _handholdCenters.Add(center);
            _handholdCenters.Sort((a, b) =>
                b.y.CompareTo(a.y)); //Sorts handhold by their y-value in order to be able to find the highest hold
        }

        private void OnNewPoseDetected(PoseData poseData)
        {
            _poseData = poseData;
        }

        private void OnHandHoldContact()
        {
            AppEvents.RaisePotentialHandholdContact();
            Debug.Log("Hand hold contact detected");
        }

        private void InitializePoseDataKeypoints()
        {
            for (int i = 0; i < _keypointCount; i++) //Fills all the Queues with one vectors
            {
                _keypointPositionHistory[i] = new Queue<Vector2>();
            }
        }
        
        private void CalculateHipHeight()
        {
            if (_poseData == null) return;

            hipHeight = 0;
            for (int i = 11; i <= 12; i++) //TODO: Use keypoint index rather than magic numbers
            {
                Vector2 keypoint = _poseData.GetKeypoint((KeypointIndex)i);
                if (keypoint != Vector2.zero)
                {
                    hipHeight += keypoint.y;
                }
            }
        }

        private void CheckFinishClimb() //The rules in bouldering dictate that the climber must have both hand on the finished (top) hold and have "control" in that position
        {
            if (_climbFinished) return;

            if (_leftWristTracker.IsOnHighestHold && _rightWristTracker.IsOnHighestHold) 
            {
                _climbFinishTimer += Time.deltaTime;
                if (_climbFinishTimer >= _climbFinishTimerThreshold)
                {
                    AppEvents.RaisePotentialHighestHandholdContact();
                    _climbFinished = true;
                    StartCoroutine(FinishedClimbEventCooldown());
                }
            }
            else
            {
                _climbFinishTimer = Mathf.Max(0, _climbFinishTimer - Time.deltaTime * _climbFinishTimerDecayRate);
            }
        }

        IEnumerator FinishedClimbEventCooldown()
        {
            yield return new WaitForSeconds(3);
            _climbFinished = false;
        }

        private void CheckClimberFalling()
        {
            if (_climbFinished)
            {
                foreach(Queue<Vector2> queue in _keypointPositionHistory)
                {
                    queue.Clear();
                }
                return;
            }

            for (int i = 0; i < _keypointCount; i++) //Enqueus the current keypoint position for all visible keypoints
            {
                KeypointIndex currentIndex = (KeypointIndex)i;

                //If the keypoint isn't detected by the model it returns a zero vector, meaning this loop should be skipped
                if (_poseData.GetKeypoint(currentIndex) == Vector2.zero) 
                {
                    continue;
                }

                _keypointPositionHistory[i].Enqueue(_poseData.GetKeypoint(currentIndex));
                if (_keypointPositionHistory[i].Count > _keypointHistorySize)
                {
                    _keypointPositionHistory[i].Dequeue();
                }
            }
            
            sumOfDeltas = 0;
            for (int i = 0; i < _keypointCount; i++) //Calculates the current movement
            {
                if (_keypointPositionHistory[i].Count() < _keypointHistorySize)
                    continue; //Ensures that position is only calculated when the queues are the size dictated by keypointHistorySize

                Vector2[] currentHistory = _keypointPositionHistory[i].ToArray();
                for (int j = 0; j < currentHistory.Length - 1; j++)
                {
                    float currentY = currentHistory[j].y;
                    float nextY = currentHistory[j + 1].y;
                    sumOfDeltas += (nextY - currentY);
                }
            }

            _climberIsFalling = sumOfDeltas < _climberFallingThreshold;
            if (_climberIsFalling && _canTriggerFallingEvent)
            {
                AppEvents.RaisePotentialFallDetected();
                print("Climber is falling!");
                StartCoroutine(ClimberFallingEventCooldown());
            }
        }

        IEnumerator ClimberFallingEventCooldown()
        {
            _canTriggerFallingEvent = false;
            yield return new WaitForSeconds(3);
            _canTriggerFallingEvent = true;
        }
    }
}