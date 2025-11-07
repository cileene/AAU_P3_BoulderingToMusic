using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VisionModels.Utilities
{
    public class PoseData
    {
        public List<Vector2> Keypoints { get; private set; }
        public float Confidence { get; set; }
        public int VisibleKeypointCount => Keypoints.Count(k => k != Vector2.zero);

        public PoseData()
        {
            Keypoints = new List<Vector2>();
        }

        public void UpdateKeypoints(List<Vector2> newKeypoints, float confidence)
        {
            Keypoints.Clear();
            Keypoints.AddRange(newKeypoints);
            Confidence = confidence;
        }

        public Vector2 GetKeypoint(KeypointIndex index)
        {
            int i = (int)index;
            return i < Keypoints.Count ? Keypoints[i] : Vector2.zero;
        }
    }
}

public enum KeypointIndex
{
    Nose = 0,
    LeftEye = 1,
    RightEye = 2,
    LeftEar = 3,
    RightEar = 4,
    LeftShoulder = 5,
    RightShoulder = 6,
    LeftElbow = 7,
    RightElbow = 8,
    LeftWrist = 9,
    RightWrist = 10,
    LeftHip = 11,
    RightHip = 12,
    LeftKnee = 13,
    RightKnee = 14,
    LeftAnkle = 15,
    RightAnkle = 16
}