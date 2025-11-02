using TMPro;
using UnityEngine;

namespace VisionModels.Utilities
{
    public class PersonPosition : MonoBehaviour
    {
        [SerializeField] private TMP_Text hiText;
    
        private BoundingBox _lastBox;
    
        private void OnEnable()
        {
            AppEvents.PersonDetected += HandlePersonDetected; 
            AppEvents.PersonLost += HandlePersonLost;
        }

        private void OnDisable()
        {
            AppEvents.PersonDetected -= HandlePersonDetected;
            AppEvents.PersonLost -= HandlePersonLost;
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