using System;
using UnityEngine;

/// <summary>
/// A system I've used in many projects to manage events.
/// Sort of a mishmash of the Observer pattern and an Event bus.
/// Any class can call an event method using eg. AppEvents.Start()
/// Other classes can then subscribe to that event and take action.
/// Example:
/// AppEvents.OnStart += OnStartHandler;
/// private void OnStartHandler() { ... }
/// It's best practice to subscribe in the OnEnable method and unsubscribe in the OnDisable method.
/// </summary>

public static class AppEvents
{
    public static event Action<BoundingBox> OnPersonDetected;
    public static event Action OnPersonLost;
    
    // Pass the bounding box of the detected person
    public static void RaisePersonDetected(BoundingBox box)
    {
        OnPersonDetected?.Invoke(box);
        Debug.Log($"Event: PersonDetected at x:{box.CenterX} y {box.CenterY}");
    }

    public static void RaisePersonLost()
    {
        OnPersonLost?.Invoke();
        Debug.Log("Event: PersonLost");
    }
}