using System;
using UnityEngine;
using Unity.InferenceEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using VisionModels.Input;
using VisionModels.ModelRunners;
using VisionModels.Utilities;

/// <summary>
/// A system I've used in many projects to manage events.
/// Sort of a mishmash of the Observer pattern and an Event bus.
/// Any class can call an event method using e.g., AppEvents.Start()
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
    public static event Action<Texture> VideoReady;
    public static event Action<Texture2D> StillReady;
    public static event Action<ModelAsset, TextAsset, RawImage, Font, Texture2D> ConfigurePersonDetector;
    public static event Action<ModelAsset, RawImage, Texture2D> ConfigurePoseDetector;

    public static event
        Action<ModelAsset, TextAsset, HandholdsDetector.ProblemColor, RawImage, Font, Texture2D, Int32, Button, bool>
        ConfigureHandholdsDetector;

    public static event Action<DetectedHandhold> NewHandholdDetected;
    public static event Action<PoseData> NewPoseDetected;
    public static event Action PotentialHandholdContact;
    public static event Action PotentialHighestHandholdContact;


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

    public static void RaiseRequestUseVideo(string fileName)
    {
        RequestUseVideo?.Invoke(fileName);
        Debug.Log($"Event: RequestUseVideo from {fileName}");
    }

    public static void RaiseRequestUseStill(string fileName)
    {
        RequestUseStill?.Invoke(fileName);
        Debug.Log($"Event: RequestUseStill from {fileName}");
    }

    public static void RaiseWebcamReady(WebCamTexture cam)
    {
        WebcamReady?.Invoke(cam);
        Debug.Log($"Event: WebcamReady using {cam.deviceName} at {cam.width}x{cam.height}");
    }

    public static void RaiseVideoReady(VideoPlayer video)
    {
        VideoReady?.Invoke(video.texture);
        Debug.Log($"Event: VideoReady using {video.url} at {video.width}x{video.height}");
    }

    public static void RaiseStillReady(Texture2D texture)
    {
        StillReady?.Invoke(texture);
        Debug.Log($"Event: StillReady with texture size {texture.width}x{texture.height}");
    }

    public static void RaiseConfigurePersonDetector(
        ModelAsset model,
        TextAsset classes,
        RawImage rawImage,
        Font font,
        Texture2D borderTexture)
    {
        ConfigurePersonDetector?.Invoke(model, classes, rawImage, font, borderTexture);
        Debug.Log($"Event: ConfigurePersonDetector with model {model.name}");
    }

    public static void RaiseConfigurePoseDetector(
        ModelAsset model,
        RawImage rawImage,
        Texture2D borderTexture)
    {
        ConfigurePoseDetector?.Invoke(model, rawImage, borderTexture);
        Debug.Log($"Event: ConfigurePoseDetector with model {model.name}");
    }

    public static void RaiseConfigureHandholdsDetector(
        ModelAsset model,
        TextAsset classes,
        HandholdsDetector.ProblemColor problemColor,
        RawImage rawImage,
        Font font,
        Texture2D borderTexture,
        Int32 keepHandholdsFrames,
        Button handholdDetectButton,
        bool continuousHandholdDetection)
    {
        ConfigureHandholdsDetector?.Invoke(model, classes, problemColor, rawImage, font, borderTexture,
            keepHandholdsFrames, handholdDetectButton, continuousHandholdDetection);
        Debug.Log($"Event: ConfigureHandholdsDetector with model {model.name}");
    }

    public static void RaiseNewHandholdDetected(DetectedHandhold handhold)
    {
        NewHandholdDetected?.Invoke(handhold);
        //Debug.Log($"Event: NewHandholdDetected ID:{handhold.Id} Label:{handhold.Label} at ({handhold.Box.CenterX:F1}, {handhold.Box.CenterY:F1})");
    }

    public static void RaiseNewPoseDetected(PoseData pose)
    {
        NewPoseDetected?.Invoke(pose);
        //Debug.Log("Event: NewPoseDetected with " + pose.Keypoints.Count + " keypoints");
    }
    public static void RaisePotentialHandholdContact()
    {
        PotentialHandholdContact?.Invoke();
    }
    public static void RaisePotentialHighestHandholdContact()
    {
        PotentialHighestHandholdContact?.Invoke();
    }
}