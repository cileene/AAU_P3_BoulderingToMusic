using TMPro;
using UnityEngine;

namespace YoloDetection
{
    public class PersonPosition : MonoBehaviour
    {
        [SerializeField] private TMP_Text hiText;
    
        private BoundingBox _lastBox;
    
        private void OnEnable()
        {
            AppEvents.OnPersonDetected += HandlePersonDetected; 
            AppEvents.OnPersonLost += HandlePersonLost;
        }

        private void OnDisable()
        {
            AppEvents.OnPersonDetected -= HandlePersonDetected;
            AppEvents.OnPersonLost -= HandlePersonLost;
        }

        private void Update()
        {
            if (_lastBox != null)
            {
                Debug.Log($"Last detected person at x:{_lastBox.CenterX} y {_lastBox.CenterY}");
            }
        }

        private void HandlePersonDetected(BoundingBox box)
        {
            _lastBox = box;
            hiText.text = "Hi human!";
        }    
    
        private void HandlePersonLost()
        {
            _lastBox = null;
            hiText.text = "Where are you human?";
        }
    }
}