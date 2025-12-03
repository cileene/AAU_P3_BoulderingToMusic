using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VisionModels.ModelRunners;

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
        private float _stillnessMovementThreshold = 50f;
        private float _stillnessTimeThreshold = 0.7f;

        //Increments when keypoint is still. Used to check against stillnessTimeThreshold
        private float _stillnessTimer = 0;
        private float _stillnessTimerDecayRate = 1f;

        //Hold proximity check

        private float _holdProximityThreshold = 0.015f;
        public bool isOnHold = false;
        public bool IsOnHighestHold { get; private set; } = false;

        public bool canRaiseEvent = true;
        public int lastTouchedHold = -1;

        public event Action OnHoldContactDetected;
        public event Action OnHighestHoldContactDetected;


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
        public void EvaluateHoldContact(List<Vector2> handholdCenters, Vector2 keypoint)
        {
            EvaluateStillness(keypoint);

            if (!_keypointIsStill)
            {
                isOnHold = false;
                IsOnHighestHold = false;
                return;
            }
            
            //Debug.Log(handholdCenters[0]);
            Vector2[] normalizedHandholdCenters = new Vector2[handholdCenters.Count];
            for (int i = 0; i < handholdCenters.Count; i++)
            {
                normalizedHandholdCenters[i] = handholdCenters[i] / (float)640;
            }

            Vector2 normalizedKeypoint = keypoint / PoseDetector.ImageHeight;
            //Debug.Log(normalizedKeypoint.y);
            
            for (int i = 0; i < normalizedHandholdCenters.Length; i++)
            {
                var center = normalizedHandholdCenters[i];
                float distance = Vector2.Distance(normalizedKeypoint, center);
                if (i==0)
                {
                //Debug.Log(normalizedHandholdCenters[i]);
                }
                
                if (distance < _holdProximityThreshold)
                {
                    if (canRaiseEvent == false) break;

                    if (i != 0)
                    {
                        isOnHold = true;
                        OnHoldContactDetected?.Invoke();
                    }
                    else
                    {
                        IsOnHighestHold = true;
                        OnHighestHoldContactDetected?.Invoke();
                    }

                    canRaiseEvent = false;
                    lastTouchedHold = i;

                    break;
                }

                if (i == normalizedHandholdCenters.Length - 1)
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