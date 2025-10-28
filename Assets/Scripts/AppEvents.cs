using System;
using UnityEngine;
using Shared;
using VisionModels.HandholdDetection;
using VisionModels.PersonDetection;

/// <summary>
/// A system I've used in many projects to manage events.
/// Sort of a mishmash of the Observer pattern and an Event bus.
/// Any class can call an event method using e.g. AppEvents.Start()
/// Other classes can then subscribe to that event and take action.
/// Can be split into multiple event classes if it gets unwieldy. - Nick
/// <para>Example:</para>
/// <para>AppEvents.OnStart += OnStartHandler;</para>
/// <para>private void OnStartHandler() { ... }</para>
/// <para>It's best practice to subscribe in the OnEnable method and unsubscribe in the OnDisable method.</para>
/// </summary>

public static class AppEvents
{
    // EVENTS
    public static event Action<BoundingBox> PersonDetected;
    public static event Action PersonLost;
    public static event Action<UseWebcam.WebcamResolution, string> RequestUseWebcam;
    public static event Action<string> RequestUseVideo;
    public static event Action<string> RequestUseStill;
    public static event Action<WebCamTexture> WebcamReady;
    public static event Action<HandholdsDetector.ProblemColor> RequestProblemColor;
    public static event Action SegRunning;
    
    
    // EVENT METHODS
    public static void RaisePersonDetected(BoundingBox box)
    {
        PersonDetected?.Invoke(box);
        Debug.Log($"Event: PersonDetected at x:{box.CenterX} y {box.CenterY}");
    }

    public static void RaisePersonLost()
    {
        PersonLost?.Invoke();
        Debug.Log("Event: PersonLost");
    }
    
    public static void RaiseRequestUseWebcam(UseWebcam.WebcamResolution resolution, string deviceName)
    {
        RequestUseWebcam?.Invoke(resolution, deviceName);
        Debug.Log($"Event: ConfigureWebcam to use {deviceName} at {resolution}");
    }
    
    public static void RaiseRequestUseVideo(string path)
    {
        RequestUseVideo?.Invoke(path);
        Debug.Log($"Event: RequestUseVideo from {path}");
    }
    
    public static void RaiseRequestUseStill(string path)
    {
        RequestUseStill?.Invoke(path);
        Debug.Log($"Event: RequestUseStill from {path}");
    }
    
    public static void RaiseWebcamReady(WebCamTexture cam)
    {
        WebcamReady?.Invoke(cam);
        Debug.Log($"Event: WebcamReady using {cam.deviceName} at {cam.width}x{cam.height}");
    }
    
    public static void RaiseRequestProblemColor(HandholdsDetector.ProblemColor color)
    {
        RequestProblemColor?.Invoke(color);
        Debug.Log($"Event: RequestRouteColor to {color}");
    }
    
    public static void RaiseSegRunning()
    {
        SegRunning?.Invoke();
        Debug.Log("Event: SegRunning");
    }
}