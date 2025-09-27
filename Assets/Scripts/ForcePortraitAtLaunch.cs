using UnityEngine;

public class ForcePortraitAtLaunch : MonoBehaviour
{
    void Awake()
    {
        // If you use Auto Rotation, lock down which orientations are allowed
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;
        Screen.autorotateToPortrait = true;
        Screen.autorotateToPortraitUpsideDown = false;

        // Force portrait immediately
        Screen.orientation = ScreenOrientation.Portrait;
    }
}