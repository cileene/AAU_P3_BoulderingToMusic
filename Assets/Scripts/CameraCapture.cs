using UnityEngine;

/// <summary>
/// Simple webcam capture component that provides a live Texture feed
/// for the pose detection pipeline.
/// </summary>
public class CameraCapture : MonoBehaviour
{
    [Tooltip("Name of the webcam device (optional). Leave empty for default.")]
    public string deviceName = "";

    [Tooltip("Requested webcam resolution width.")]
    public int width = 640;

    [Tooltip("Requested webcam resolution height.")]
    public int height = 480;

    [Tooltip("Requested webcam frame rate.")]
    public int fps = 30;

    private WebCamTexture _webCamTexture;
    private bool _isPlaying = false;

    // Expose whether the webcam feed is playing
    public bool IsPlaying => _isPlaying;

    /// <summary>
    /// Returns the live webcam WebCamTexture. Can be used by other scripts (e.g., PoseDetection).
    /// </summary>
    public WebCamTexture WebCamTex => _webCamTexture;

    void Start()
    {
        StartCamera();
    }

    /// <summary>
    /// Initializes and starts the webcam feed.
    /// </summary>
    private void StartCamera()
    {
        if (WebCamTexture.devices.Length == 0)
        {
            Debug.LogError("No webcam device detected!");
            return;
        }

        WebCamDevice device;
        if (string.IsNullOrEmpty(deviceName))
            device = WebCamTexture.devices[0]; // Use first camera
        else
            device = System.Array.Find(WebCamTexture.devices, d => d.name == deviceName);

        try
        {
            _webCamTexture = new WebCamTexture(device.name, width, height, fps);
            _webCamTexture.Play();
            _isPlaying = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CameraCapture: Failed to start WebCamTexture for device '{device.name}': {e}");
            _webCamTexture = null;
            _isPlaying = false;
        }
    }

    void OnDisable()
    {
        if (_webCamTexture != null && _isPlaying)
            _webCamTexture.Stop();
    }

    void OnDestroy()
    {
        if (_webCamTexture != null)
        {
            _webCamTexture.Stop();
            Destroy(_webCamTexture);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Assign Pose Assets (auto)")]
    void AssignPoseAssetsContext()
    {
        // Try to find a PoseDetectionLive on this GameObject or its children
        var pd = GetComponentInChildren<PoseDetectionLive>(true);
        if (pd == null)
        {
            // Fallback: search the entire scene for any PoseDetectionLive (including inactive)
            var fallback = FindFirstInScene<PoseDetectionLive>();
            if (fallback != null)
            {
                pd = fallback;
                UnityEngine.Debug.Log("CameraCapture: No PoseDetectionLive on this GameObject; using first PoseDetectionLive found in scene: " + pd.gameObject.name);
            }
            else
            {
                UnityEngine.Debug.LogWarning("CameraCapture: No PoseDetectionLive found on this GameObject, children, or scene.");
                return;
            }
        }

        var so = new UnityEditor.SerializedObject(pd);

        // anchors
        var anchorsPath = "Assets/Data/anchors.csv";
        var anchorsAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(anchorsPath);
        if (anchorsAsset != null)
        {
            var prop = so.FindProperty("anchorsCSV");
            if (prop != null) { UnityEditor.Undo.RecordObject(pd, "Auto-assign anchorsCSV"); prop.objectReferenceValue = anchorsAsset; }
        }

        // pose detector
        var detectorPath = "Assets/Models/pose_detection.onnx";
        var detectorAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(detectorPath);
        if (detectorAsset != null)
        {
            var prop = so.FindProperty("poseDetector");
            if (prop != null) { UnityEditor.Undo.RecordObject(pd, "Auto-assign poseDetector"); prop.objectReferenceValue = detectorAsset; }
        }

        // pose landmarker
        string[] candidatePaths = new string[] {
            "Assets/Models/pose_landmarks_detector_full.onnx",
            "Assets/Models/pose_landmarks_detector_heavy.onnx",
            "Assets/Models/pose_landmarks_detector_lite.onnx"
        };
        UnityEngine.Object found = null;
        foreach (var p in candidatePaths)
        {
            var a = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p);
            if (a != null) { found = a; break; }
        }
        if (found != null)
        {
            var prop = so.FindProperty("poseLandmarker");
            if (prop != null) { UnityEditor.Undo.RecordObject(pd, "Auto-assign poseLandmarker"); prop.objectReferenceValue = found; }
        }

        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(pd);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEngine.Debug.Log("CameraCapture: Assigned available pose assets to PoseDetectionLive (if found)");
    }
#endif
    // Editor-only helper to find first component of type T in the active scene (includes inactive)
    static T FindFirstInScene<T>() where T : UnityEngine.Component
    {
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            var comps = root.GetComponentsInChildren<T>(true);
            if (comps != null && comps.Length > 0)
                return comps[0];
        }
        return null;
    }
}
