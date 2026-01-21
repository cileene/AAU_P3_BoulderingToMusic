using System.Collections.Generic;
using UnityEngine;

namespace VisionModels.Utilities
{
    public class ModelDebugger : MonoBehaviour
    {
        private List<DetectedHandhold> _handholds = new();
        private PoseData _poseData;

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
            // Update existing or add new
            var existing = _handholds.Find(h => h.Id == handhold.Id);
            if (existing != null)
            {
                int index = _handholds.IndexOf(existing);
                _handholds[index] = handhold;
            }
            else
            {
                _handholds.Add(handhold);
            }

            // Clean up old entries (optional)
            _handholds.RemoveAll(h => h.FramesSinceLastSeen > 30);
        }

        private void OnNewPoseDetected(PoseData poseData)
        {
            _poseData = poseData;
        }

        private void OnGUI()
        {
            // Save the original matrix
            Matrix4x4 originalMatrix = GUI.matrix;

            // Create a custom style with larger line height
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                padding = new RectOffset(0, 0, 3, 3) // Add vertical padding
            };

            // Define the pivot point and rotation
            Vector2 pivot = new Vector2(10, 10);
            GUIUtility.RotateAroundPivot(90f, pivot);

            GUILayout.BeginArea(new Rect(200, -1000, 700, 1100));

            GUILayout.Label($"Handholds: {_handholds.Count}", labelStyle);
            foreach (var handhold in _handholds)
            {
                GUILayout.Label($"ID: {handhold.Id} | {handhold.Label} | " +
                                $"Pos: ({handhold.Box.CenterX:F0}, {handhold.Box.CenterY:F0})", labelStyle);
            }

            GUILayout.Space(20);

            if (_poseData != null)
            {
                GUILayout.Label($"Pose - Confidence: {_poseData.Confidence:F2}", labelStyle);
                GUILayout.Label($"Visible Keypoints: {_poseData.VisibleKeypointCount}", labelStyle);

                GUILayout.Space(20);

                string[] keypointNames = new string[]
                {
                    "Nose", "Left Eye", "Right Eye", "Left Ear", "Right Ear",
                    "Left Shoulder", "Right Shoulder", "Left Elbow", "Right Elbow",
                    "Left Wrist", "Right Wrist", "Left Hip", "Right Hip",
                    "Left Knee", "Right Knee", "Left Ankle", "Right Ankle"
                };

                for (int i = 0; i < 17; i++)
                {
                    if (i < _poseData.Keypoints.Count)
                    {
                        var kp = _poseData.Keypoints[i];
                        GUILayout.Label($"{keypointNames[i]}: ({kp.x:F1}, {kp.y:F1})", labelStyle);
                    }
                    else
                    {
                        GUILayout.Label($"{keypointNames[i]}: N/A", labelStyle);
                    }
                }
            }
            else
            {
                GUILayout.Label("No pose detected", labelStyle);
            }

            GUILayout.EndArea();

            // Restore the original matrix
            GUI.matrix = originalMatrix;
        }

    }
}
