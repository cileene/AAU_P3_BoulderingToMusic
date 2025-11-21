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

        private static int
            keypointCount =
                Enum.GetValues(typeof(KeypointIndex)).Length; //Will get the length of the enum KeypointIndex

        private Queue<Vector2>[] _keypointPositionHistory = new Queue<Vector2>[keypointCount];
        private int _keypointHistorySize = 5;

        private float _climberFallingThreshold = -500f;
        private bool _climberIsFalling = false;
        private bool _canTriggerFallingEvent = true;

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

        private void Update()
        {
            CheckClimberFalling();

            if (_poseData != null)
            {
                _rightWristPos = _poseData.GetKeypoint(KeypointIndex.RightWrist);
                _leftWristPos = _poseData.GetKeypoint(KeypointIndex.LeftWrist);
            }

            if (_handholdCenters != null && _handholdCenters.Count != 0)
            {
                _rightWristTracker.EvaluateHoldContact(_handholdCenters, _rightWristPos);
                _leftWristTracker.EvaluateHoldContact(_handholdCenters, _leftWristPos);
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
        }
        
        private void InitializePoseDataKeypoints()
        {
            for (int i = 0; i < keypointCount; i++) //Fills all the Queues with one vectors
            {
                _keypointPositionHistory[i] = new Queue<Vector2>();
            }
        }

        private void CheckClimberFalling()
        {
            for (int i = 0; i < keypointCount; i++) //Enqueus the current keypoint position for all visible keypoints
            {
                KeypointIndex currentIndex = (KeypointIndex)i;

                if (_poseData.GetKeypoint(currentIndex) ==
                    Vector2.zero) //If the keypoint isn't detected by the model it returns a zero vector, meaning this loop should be skipped
                {
                    continue;
                }

                _keypointPositionHistory[i].Enqueue(_poseData.GetKeypoint(currentIndex));
                if (_keypointPositionHistory[i].Count > _keypointHistorySize)
                {
                    _keypointPositionHistory[i].Dequeue();
                }
            }

            float sumOfDeltas = 0;
            for (int i = 0; i < keypointCount; i++) //Calculates the current movement
            {
                if (_keypointPositionHistory[0].Count() < _keypointHistorySize)
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
        
        private IEnumerator ClimberFallingEventCooldown()
        {
            _canTriggerFallingEvent = false;
            yield return new WaitForSeconds(3);
            _canTriggerFallingEvent = true;
        }
        


        /* This helper class is used to detect whether a keypoint is still and next to a handhold
         * (likely indicating that the climber touched a hold).
         * Call EvaluateStillness() inside Update() in AppEventTrigger
         * and use the bool IsOnHold to trigger logic in FMOD. */
        private class KeypointTracker
        {
            //Movement tracking
            private Queue<float> movementDeltas = new();
            public float averageMovement;
            private int movementSampleSize = 10;
            private Vector2 previousPosition = Vector2.zero;

            //Stillness detection
            private bool keypointIsStill = false;
            private float stillnessMovementThreshold = 200f;
            private float stillnessTimeThreshold = 0.2f;

            private float
                stillnessTimer = 0; //Increments when keypoint is still. Used to check against stillnessTimeThreshold

            private float stillnessTimerDecayRate = 0.3f;

            //Hold proximity check
            private float holdProximityThreshold = 70.0f;
            private bool isOnHold = false;

            public bool IsOnHold
            {
                get { return isOnHold; }
            }

            private bool isOnHighestHold = false;

            public bool IsOnHighestHold
            {
                get { return isOnHighestHold; }
            }

            public bool canRaiseEvent = true;
            public int lastTouchedHold = -1;

            public event Action OnHoldContactDetected;

            
            
            public KeypointTracker()
            {
            }

            public KeypointTracker(float thresholdProximity)
            {
                holdProximityThreshold = thresholdProximity;
            }

            //Movement calculation
            public void UpdateMovementAverage(Vector2 keypoint)
            {
                if (previousPosition == Vector2.zero)
                {
                    previousPosition = keypoint;
                    return;
                }

                float delta = Vector2.Distance(keypoint, previousPosition) /
                              Time.deltaTime; //Scales delta so it isn't frame rate dependant 
                movementDeltas.Enqueue(delta);
                if (movementDeltas.Count > movementSampleSize)
                {
                    movementDeltas.Dequeue();
                }

                averageMovement = movementDeltas.Average();
                previousPosition = keypoint;
            }

            //Stillness detection
            public void EvaluateStillness(Vector2 keypoint)
            {
                UpdateMovementAverage(keypoint);
                if (averageMovement < stillnessMovementThreshold)
                {
                    stillnessTimer += Time.deltaTime;
                    if (stillnessTimer > stillnessTimeThreshold)
                    {
                        keypointIsStill = true;
                    }
                    else
                    {
                        keypointIsStill = false;
                    }
                }
                else
                {
                    // Smooths the decay of the stillnessTimer so a keypoint jittering
                    // will not affect stillness detection too much
                    stillnessTimer = Mathf.Max(0, stillnessTimer - Time.deltaTime * stillnessTimerDecayRate);
                    keypointIsStill = false;
                }
            }

            // Hold proximity check
            public void EvaluateHoldContact(List<Vector2> handholdCenters, Vector2 keypoint)
            {
                EvaluateStillness(keypoint);

                if (!keypointIsStill)
                {
                    isOnHold = false;
                    isOnHighestHold = false;
                    return;
                }

                for (int i = 0; i < handholdCenters.Count; i++)
                {
                    var center = handholdCenters[i];
                    float distance = Vector2.Distance(keypoint, center);

                    if (distance < holdProximityThreshold)
                    {
                        if (canRaiseEvent == false || lastTouchedHold == i) break;

                        if (i != 0)
                        {
                            isOnHold = true;
                        }
                        else
                        {
                            isOnHighestHold = true;
                            Debug.Log("On highest hold!");
                            AppEvents.RaisePotentialHighestHandholdContact();
                        }

                        canRaiseEvent = false;
                        lastTouchedHold = i;

                        OnHoldContactDetected?.Invoke();
                        break;
                    }

                    if (i == handholdCenters.Count - 1)
                    {
                        isOnHold = false;
                        isOnHighestHold = false;
                        canRaiseEvent = true;
                        lastTouchedHold = -1;
                    }
                }
            }
        }
    }
}