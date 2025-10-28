using UnityEngine;

namespace PoseDetection
{
    public class KeypointLine : MonoBehaviour
    {
        public LineRenderer lineRenderer;
        public Keypoint start;
        public Keypoint end;
        public Color color;
        public float width;

        private void Start()
        {
            //lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.positionCount = 2;
        }
    
        public void setLineWidth(float w)
        {
            lineRenderer.startWidth = w;
            lineRenderer.endWidth = w;
        }

        private void Update()
        {
            lineRenderer.SetPosition(0, start.Position);
            lineRenderer.SetPosition(1, end.Position);
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.gameObject.SetActive(start.IsActive && end.IsActive);
        
        }
    }
}
