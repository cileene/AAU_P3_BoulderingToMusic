// ForcePortrait.cs
using UnityEngine;

public static class ForcePortrait
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    static void LockOrientation()
    {
        // If you want a hard lock:
        Screen.orientation = ScreenOrientation.Portrait;

        // If you prefer AutoRotation but only portrait:
        // Screen.orientation = ScreenOrientation.AutoRotation;
        // Screen.autorotateToPortrait = true;
        // Screen.autorotateToPortraitUpsideDown = false; // set true if desired
        // Screen.autorotateToLandscapeLeft = false;
        // Screen.autorotateToLandscapeRight = false;
    }
}