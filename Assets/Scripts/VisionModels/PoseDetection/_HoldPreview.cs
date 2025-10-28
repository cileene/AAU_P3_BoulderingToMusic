using UnityEngine;

namespace VisionModels.PoseDetection
{
    public class _HoldPreview : MonoBehaviour
    {
        public GameObject HoldPrefab;
        private _Hold[] _holds;

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    
        public void SetMesh(Vector3[] holdPoints, int[][] holdVertex)
        {
            _holds = new _Hold[_holds.Length];
            for (int i = 0; i < _holds.Length; i++)
            {
                GameObject holdGO = Instantiate(HoldPrefab, Vector3.zero, Quaternion.identity, transform);
            
            }
        }
    }
}
