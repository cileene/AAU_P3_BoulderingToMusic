using UnityEngine;

namespace PoseDetection
{
    public class PosePreview : MonoBehaviour
    {
        public BoundingBox boundingBox;
        public BoundingCircle boundingCircle;
        public Keypoint[] keypoints;
        public KeypointLine[] keyPointLines;
        [SerializeField] private float skeletonScaleFactor = 1.0f;


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
            boundingCircle.Set(active, position, radius);
        }

        public void SetKeypoint(int index, bool active, Vector3 position)
        {
            //Debug.Log("Keypoint nr: " + index + " is being set at :" + position);
            keypoints[index].Set(active, position);
        }
    }
}
