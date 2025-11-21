using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VisionModels.Utilities;

namespace Sound
{
    public class AppEventTrigger : MonoBehaviour
    {
        //This script handles the logic for calling the event in app AppEvents eventually playing sound in FMOD

        private List<Vector2> _handholdCenters = new();
        private PoseData _poseData;
        private Vector2 _rightWristPos;
        private Vector2 _leftWristPos;
        private KeypointTracker _rightWristTracker = new();
        private KeypointTracker _leftWristTracker = new();

        private static int
            _keypointCount =
                Enum.GetValues(typeof(KeypointIndex)).Length; //Will get the length of the enum KeypointIndex

        private Queue<Vector2>[] _keypointPositionHistory = new Queue<Vector2>[_keypointCount];
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
            Debug.Log("Hand hold contact detected");
        }

        private void InitializePoseDataKeypoints()
        {
            for (int i = 0; i < _keypointCount; i++) //Fills all the Queues with one vectors
            {
                _keypointPositionHistory[i] = new Queue<Vector2>();
            }
        }

        private void CheckClimberFalling()
        {
            for (int i = 0; i < _keypointCount; i++) //Enqueus the current keypoint position for all visible keypoints
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
            for (int i = 0; i < _keypointCount; i++) //Calculates the current movement
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

        IEnumerator ClimberFallingEventCooldown()
        {
            _canTriggerFallingEvent = false;
            yield return new WaitForSeconds(3);
            _canTriggerFallingEvent = true;
        }


        
    }
}