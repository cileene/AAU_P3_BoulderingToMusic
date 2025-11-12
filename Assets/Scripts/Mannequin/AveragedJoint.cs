using PoseDetection;
using UnityEngine;

public class AveragedJoint : MannequinJoint
{
    // public Keypoint[] keypoints;
    // // Start is called once before the first execution of Update after the MonoBehaviour is created
    // void Start()
    // {
    //     
    // }
    //
    // Vector3 calcTargetPosition()
    // {
    //     var Position = Vector3.zero;
    //     foreach (var keypoint in keypoints)
    //     {
    //         Position += keypoint.innerCircle.GetPosition(0);
    //     }
    //     Position /= keypoints.Length;
    //     return Position;
    // }
    //
    // // Update is called once per frame
    // void Update()
    // {
    //     if (controller.isUpdating)
    //     {
    //         target = setTarget();
    //     }
    //     transform.position = target;
    // }
    //
    // public override Vector3 setTarget()
    // {
    //     return calcTargetPosition() * controller.skaletonPoseScalar;
    // }
}
