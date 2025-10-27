using System;
using Unity.InferenceEngine;
using UnityEngine;

public class HoldPreviewLive : MonoBehaviour
{
    
    [Header("Scene References")]
    public HoldPreview holdPreview;
    public ImagePreview imagePreview;
    public CameraCapture cameraCapture;

    [Header("Model Asset")] public ModelAsset holdAsset;
    
    [Header("Settings")]
    [Range(0f, 1f)]
    public float scoreThreshold = 0.75f;

    private const int detectorInputSize = 1;
    private Worker m_HoldDetectorWorker;
    private Tensor<float> m_DetectorInput;
    
    float m_TextureWidth;
    float m_TextureHeight;
    
    [Header("Testing"),Tooltip("For testing purposes, add images here as textures")]
    [SerializeField] Texture presetTexture;
    [SerializeField] bool isPresetTexture = true;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
        if (holdPreview == null)
        {
            Debug.LogError("HoldDetectionLive: holdPreview is not assigned. This can be done in the inspector");
        }
        if (imagePreview == null)
        {
            Debug.LogError("PoseDetectionLive: imagePreview is not assigned. Please assign it in the Inspector.");
            enabled = false;
            return;
        }
        if (cameraCapture == null)
        {
            Debug.LogError("PoseDetectionLive: cameraCapture is not assigned. Please assign it in the Inspector.");
            enabled = false;
            return;
        }

        if (holdAsset == null)
        {
            Debug.LogError("PoseDetectionLive: holdAsset is not assigned. Please assign it in the Inspector.");
            enabled = false;
            return;
        }
        
        
        var holdDetectorModel = ModelLoader.Load(holdAsset);
        if (holdAsset == null)
        {
            Debug.LogError("PoseDetectionLive: Failed to load holdAsset model via ModelLoader.Load(poseDetector). Check the ModelAsset and package setup.");
            enabled = false;
            return;
        }

        var graph = new FunctionalGraph();
        var input = graph.AddInput(holdDetectorModel, 0);
        var output = Functional.Forward(holdDetectorModel, input);
        var boxes = output[0];
        var scores = output[1];
        var idx_scores_boxes = BlazeUtils.ArgMaxFiltering(boxes, scores);
        holdDetectorModel = graph.Compile(idx_scores_boxes.Item1, idx_scores_boxes.Item2, idx_scores_boxes.Item3);

        // Initialize workers: try GPUCompute then fallback to CPU
        try
        {
            m_HoldDetectorWorker = new Worker(holdDetectorModel, BackendType.GPUCompute);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"PoseDetectionLive: GPU backend failed for pose detector, falling back to CPU. Exception: {e.Message}");
            try { m_HoldDetectorWorker = new Worker(holdDetectorModel, BackendType.CPU); }
            catch (Exception e2) { Debug.LogError($"PoseDetectionLive: Failed to create pose detector Worker: {e2}"); enabled = false; return; }
        }
        
        // Allocate tensors for model input
        m_DetectorInput = new Tensor<float>(new TensorShape(1, detectorInputSize, detectorInputSize, 3));

        // Wait for camera initialization with timeout to avoid hanging the Editor/Play mode
        const float kCameraTimeoutSeconds = 5.0f;
        float startTime = Time.realtimeSinceStartup;
        while (cameraCapture.WebCamTex == null || !cameraCapture.WebCamTex.didUpdateThisFrame)
        {
            if (Time.realtimeSinceStartup - startTime > kCameraTimeoutSeconds)
            {
                Debug.LogError($"PoseDetectionLive: Camera did not initialize within {kCameraTimeoutSeconds} seconds. Aborting live detection.");
                enabled = false;
                return;
            }
            await Awaitable.NextFrameAsync();
        }

        Debug.Log("Camera ready. Starting live hold detection...");
        
        
        // Main loop: continuously process frames from webcam
        while (true)
        {
            try
            {
                var tex = cameraCapture.WebCamTex;
                if (tex != null && tex.didUpdateThisFrame && !isPresetTexture)
                    await Detect(tex);
                else if (tex != null && isPresetTexture)
                {
                    await Detect(presetTexture);
                }
                else
                {
                    await Awaitable.NextFrameAsync();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // Cleanup
        m_HoldDetectorWorker.Dispose();
        m_DetectorInput.Dispose();
    }

    async Awaitable Detect(Texture texture)
    {
            m_TextureWidth = texture.width;
        m_TextureHeight = texture.height;

        // Update preview texture for visualization
        imagePreview.SetTexture(texture);

        // === Stage 1: Pose detection ===
        var size = Mathf.Max(texture.width, texture.height);
        var scale = size / (float)detectorInputSize;

        // Compute transformation matrix for sampling
        var M = BlazeUtils.mul(
            BlazeUtils.TranslationMatrix(0.5f * (new Vector2(texture.width, texture.height) + new Vector2(-size, size))),
            BlazeUtils.ScaleMatrix(new Vector2(scale, -scale))
        );

        // Sample input texture into tensor space
        BlazeUtils.SampleImageAffine(texture, m_DetectorInput, M);
        if (m_HoldDetectorWorker == null)
        {
            Debug.LogError("PoseDetectionLive: m_PoseDetectorWorker is null before scheduling. Aborting Detect.");
            return;
        }
        try
        {
            m_HoldDetectorWorker.Schedule(m_DetectorInput);
        }
        catch (Exception e)
        {
            Debug.LogError($"PoseDetectionLive: Exception while scheduling pose detector worker: {e}\nWorker: {m_HoldDetectorWorker}\nTensor: {m_DetectorInput}");
            return;
        }

        // Retrieve inference results
        var outputIdxAwaitable = (m_HoldDetectorWorker.PeekOutput(0) as Tensor<int>).ReadbackAndCloneAsync();
        var outputScoreAwaitable = (m_HoldDetectorWorker.PeekOutput(1) as Tensor<float>).ReadbackAndCloneAsync();
        var outputBoxAwaitable = (m_HoldDetectorWorker.PeekOutput(2) as Tensor<float>).ReadbackAndCloneAsync();

        using var outputIdx = await outputIdxAwaitable;
        using var outputScore = await outputScoreAwaitable;
        using var outputBox = await outputBoxAwaitable;

        bool scorePassesThreshold = outputScore[0] >= scoreThreshold;
        holdPreview.SetActive(scorePassesThreshold);

        if (!scorePassesThreshold)
            return;
    }
}
