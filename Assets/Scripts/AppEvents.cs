using System;
using UnityEngine;

/// <summary>
/// A system I've used in many projects to manage events.
/// Sort of a mishmash of the Observer pattern and an Event bus.
/// Any class can call an event method using eg. AppEvents.Start()
/// Other classes can then subscribe to that event and take action.
/// Example:
/// AppEvents.RaiseStart += OnStartHandler;
/// private void OnStartHandler() { ... }
/// It's best practice to subscribe in the OnEnable method and unsubscribe in the OnDisable method.
/// </summary>

public static class AppEvents
{
    public static event Action OnStart;
    public static event Action OnQuit;
    public static event Action OnPersonDetected;
    public static event Action OnPersonLost;

    public static void RaiseStart()
    {
        OnStart?.Invoke();
        Debug.Log("Event: Start");
    }
    
    public static void RaiseQuit()
    {
        OnQuit?.Invoke();
        Debug.Log("Event: Quit");
    }
    
    public static void RaisePersonDetected()
    {
        OnPersonDetected?.Invoke();
        Debug.Log("Event: PersonDetected");
    }

    public static void RaisePersonLost()
    {
        OnPersonLost?.Invoke();
        Debug.Log("Event: PersonLost");
    }
}