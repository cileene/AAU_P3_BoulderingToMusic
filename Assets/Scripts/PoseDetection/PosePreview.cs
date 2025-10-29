using UnityEngine;

namespace PoseDetection
{
    public class PosePreview : MonoBehaviour
    {
        public BoundingBox boundingBox;
        public BoundingCircle boundingCircle;
        public Keypoint[] keypoints;
        public KeypointLine[] keyPointLines;

        private void Update()
        {
            
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
            boundingCircle.Set(active, position, radius);
        }

        public void SetKeypoint(int index, bool active, Vector3 position)
        {
            if (keypoints[index] != null)
            {
                keypoints[index].Set(active, position);
            }
        }
    }
}
