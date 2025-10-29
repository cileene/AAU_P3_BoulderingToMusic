using PoseDetection;
using UnityEngine;
using UnityEngine.Serialization;

public class MannequinController : MonoBehaviour
{
    public MannequinJoint[] joints;
    public PoseDetectionLive detectionLive;
    public Vector3 offset;
    public bool isUpdating = false;
    public float skaletonPoseScalar = 1;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        offset = transform.position - detectionLive.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

