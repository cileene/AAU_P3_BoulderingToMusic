using System;
using UnityEngine;

public class PosePreview : MonoBehaviour
{
    public BoundingBox boundingBox;
    public BoundingCircle boundingCircle;
    public Keypoint[] keypoints;
    public KeypointLine[] keyPointLines;
    [SerializeField] float scaleFactor = 1.0f, skeletonScaleFactor = 1.0f;

    private void Update()
    {
        foreach (KeypointLine kpl in keyPointLines)
        {
            kpl.setLineWidth(skeletonScaleFactor);
        }
    }

    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }

    public void SetBoundingBox(bool active, Vector3 position, Vector2 size)
    {
        boundingBox.Set(active, position, size);
    }

    public void SetBoundingCircle(bool active, Vector3 position, float radius)
    {
        boundingCircle.Set(active, position, radius * scaleFactor);
    }

    public void SetKeypoint(int index, bool active, Vector3 position)
    {
        keypoints[index].Set(active, position * scaleFactor);
    }
}
