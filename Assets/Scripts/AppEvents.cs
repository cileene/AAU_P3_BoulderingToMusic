using System;
using UnityEngine;
using YoloDetection;
//using BoundingBox = PoseDetection.BoundingBox;

/// <summary>
/// A system I've used in many projects to manage events.
/// Sort of a mishmash of the Observer pattern and an Event bus.
/// Any class can call an event method using eg. AppEvents.Start()
/// Other classes can then subscribe to that event and take action. - nick
/// <para>Example:</para>
/// <para>AppEvents.OnStart += OnStartHandler;</para>
/// <para>private void OnStartHandler() { ... }</para>
/// <para>It's best practice to subscribe in the OnEnable method and unsubscribe in the OnDisable method.</para>
/// </summary>

public static class AppEvents
{
    // EVENTS
    public static event Action<BoundingBox> OnPersonDetected;
    public static event Action OnPersonLost;
    public static event Action<WebCamTexture> OnWebcamReady;
    public static event Action OnSegRunning;
    
    
    // EVENT METHODS
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
    
    public static void RaiseWebcamReady(WebCamTexture cam)
    {
        OnWebcamReady?.Invoke(cam);
        Debug.Log("Event: WebcamReady");
    }
    
    public static void RaiseSegRunning()
    {
        OnSegRunning?.Invoke();
        Debug.Log("Event: SegRunning");
    }
}