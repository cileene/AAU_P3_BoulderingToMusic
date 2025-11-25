using TMPro;
using UnityEngine;
using VisionModels.Utilities;

/// <summary>
/// In charge of detecting the position of the relevant climber, and controls the fmod emitter/music controller.
/// </summary>
public class PersonPosition : MonoBehaviour
{
    [SerializeField] private TMP_Text hiText;
    [SerializeField] private GameObject fmodEmitter;
    
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

    //Given that the system seems to "blink" the person being detected, I worry this is gonna give us issues with the music cutting in and out, to a notable degree
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