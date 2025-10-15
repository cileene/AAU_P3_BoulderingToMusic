using TMPro;
using UnityEngine;

public class PersonPosition : MonoBehaviour
{
    [SerializeField] private TMP_Text hiText;
    [SerializeField] private GameObject fmodEmitter;
    
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
        fmodEmitter.SetActive(true);
        hiText.text = "Hi human!";
    }    
    
    private void HandlePersonLost()
    {
        _lastBox = null;
        fmodEmitter.SetActive(false);
        hiText.text = "Where are you human?";
    }
}