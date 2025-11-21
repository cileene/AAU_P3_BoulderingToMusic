using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sound
{
    public class KeypointTracker : MonoBehaviour
            /*This helper class is used to detect whether a keypoint is still and next to a handhold
             * (likely indicating that the climber touched a hold).
             * Call EvaluateStillness() inside Update() in AppEventTrigger
             * and use the bool IsOnHold to trigger logic in FMOD.
             */
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
                    stillnessTimer =
                        Mathf.Max(0,
                            stillnessTimer -
                            Time.deltaTime *
                            stillnessTimerDecayRate); //Smooths the decay of the stillnessTimer so a keypoint jittering will not affect stillness detection too much
                    keypointIsStill = false;
                }
            }

            //Hold proximity check
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
                    if (i == 0)
                    {
                        Debug.Log("Distance to hold " + i + ": " + distance);
                    }

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
                    else if (i == handholdCenters.Count - 1)
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