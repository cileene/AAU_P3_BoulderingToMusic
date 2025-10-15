using TMPro;
using UnityEngine;

public class PersonPosition : MonoBehaviour
{
    [Header("Fun Debug")]
    [Tooltip("When we see a person")]
    [SerializeField] private TMP_Text hiText;
    [Tooltip("When we dont see a person")]
    [SerializeField] private TMP_Text whereText;
    
    private BoundingBox lastBox;
    
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
        if (lastBox != null)
        {
            Debug.Log($"Last detected person at x:{lastBox.CenterX} y {lastBox.CenterY}");
        }
    }

    private void HandlePersonDetected(BoundingBox box)
    {
        lastBox = box;
        hiText.enabled = true;
        whereText.enabled = false;
    }    
    
    private void HandlePersonLost()
    {
        lastBox = null;
        hiText.enabled = false;
        whereText.enabled = true;
    }
}