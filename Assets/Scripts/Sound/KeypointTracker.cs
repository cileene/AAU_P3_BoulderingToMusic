using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sound
{
    /// <summary>
    /// This helper class is used to detect whether a keypoint is still and next to a handhold
    /// (likely indicating that the climber touched a hold).
    /// Call EvaluateStillness() inside Update() in AppEventTrigger
    /// and use the bool IsOnHold to trigger logic in FMOD.
    /// </summary>
    public class KeypointTracker : MonoBehaviour
    {
        //Movement tracking
        private Queue<float> _movementDeltas = new();
        public float _averageMovement;
        private int _movementSampleSize = 10;
        private Vector2 _previousPosition = Vector2.zero;

        //Stillness detection
        private bool _keypointIsStill = false;
        private float _stillnessMovementThreshold = 200f;
        private float _stillnessTimeThreshold = 0.2f;

        //Increments when keypoint is still. Used to check against stillnessTimeThreshold
        private float _stillnessTimer = 0;

        private float _stillnessTimerDecayRate = 0.3f;

        //Hold proximity check
        private float _holdProximityThreshold = 70.0f;
        public bool isOnHold = false;

        public bool IsOnHighestHold { get; private set; } = false;

        public bool canRaiseEvent = true;
        public int lastTouchedHold = -1;

        private bool _leftHandSeen = false;
        private bool _rightHandSeen = false;

        public event Action OnHoldContactDetected;


        public KeypointTracker()
        {
        }

        public KeypointTracker(float thresholdProximity)
        {
            _holdProximityThreshold = thresholdProximity;
        }


        //Movement calculation
        public void UpdateMovementAverage(Vector2 keypoint)
        {
            if (_previousPosition == Vector2.zero)
            {
                _previousPosition = keypoint;
                return;
            }

            //Scales delta so it isn't frame rate dependant 
            float delta = Vector2.Distance(keypoint, _previousPosition) / Time.deltaTime;
            _movementDeltas.Enqueue(delta);
            if (_movementDeltas.Count > _movementSampleSize)
            {
                _movementDeltas.Dequeue();
            }

            _averageMovement = _movementDeltas.Average();
            _previousPosition = keypoint;
        }

        //Stillness detection
        public void EvaluateStillness(Vector2 keypoint)
        {
            UpdateMovementAverage(keypoint);
            if (_averageMovement < _stillnessMovementThreshold)
            {
                _stillnessTimer += Time.deltaTime;
                if (_stillnessTimer > _stillnessTimeThreshold)
                {
                    _keypointIsStill = true;
                }
                else
                {
                    _keypointIsStill = false;
                }
            }
            else
            {
                //Smooths the decay of the stillnessTimer so a keypoint jittering will not affect stillness detection too much
                _stillnessTimer = Mathf.Max(0, _stillnessTimer - Time.deltaTime * _stillnessTimerDecayRate);
                _keypointIsStill = false;
            }
        }

        //Hold proximity check
        public void EvaluateHoldContact(List<Vector2> handholdCenters, Vector2 keypoint, bool isRightHand)
        {
            EvaluateStillness(keypoint);

            if (isRightHand)
            {
                _rightHandSeen = true;
            }
            else
            {
                _leftHandSeen = true;
            }

            if (!_keypointIsStill)
            {
                isOnHold = false;
                IsOnHighestHold = false;
                return;
            }

            for (int i = 0; i < handholdCenters.Count; i++)
            {
                var center = handholdCenters[i];
                float distance = Vector2.Distance(keypoint, center);
                if (i == 0)
                {
                    //Debug.Log("Distance to hold " + i + ": " + distance);
                }

                if (distance < _holdProximityThreshold)
                {
                    if (canRaiseEvent == false || lastTouchedHold == i) break;

                    if (i != 0)
                    {
                        isOnHold = true;
                        OnHoldContactDetected?.Invoke();
                    }
                    else
                    {
                        Debug.Log("On highest hold!");
                        AppEvents.RaisePotentialHighestHandholdContact();
                        _leftHandSeen = false;
                        _rightHandSeen = false;
                        //TODO: Consider whether to raise event only if both hands are seen
                    }

                    canRaiseEvent = false;
                    lastTouchedHold = i;

                    break;
                }

                if (i == handholdCenters.Count - 1)
                {
                    isOnHold = false;
                    IsOnHighestHold = false;
                    canRaiseEvent = true;
                    lastTouchedHold = -1;
                }
            }
        }
    }
}