using PoseDetection;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class JointIK : MannequinJoint
{
    public Keypoint keypoint;
    [SerializeField]private Vector3 m_offset;
    public Transform targetTransform;
    public Transform hintTransform;
    public float hintOffset = -5;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        //m_offset = poseDetectionScale;
        if (controller.isUpdating)
        {
            target = setTarget();
        }
        targetTransform.position = target;
    }

    public override Vector3 setTarget()
    {
        return keypoint.Position  * controller.skaletonPoseScalar;
    }
}
