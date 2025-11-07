using System;
using Unity.InferenceEngine;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace PoseDetection
{
    /// <summary>
    /// Modified PoseDetection class that runs the Blaze Pose pipeline using a live webcam feed
    /// instead of a static image. It continuously performs detection on each frame.
    /// </summary>
    public class PoseDetectionLive : MonoBehaviour
    {
        [Header("Scene References")]
        public PosePreview posePreview;
        public ImagePreview imagePreview;
        public CameraCapture cameraCapture;

        [Header("Model Assets")]
        public ModelAsset poseDetector;
        public ModelAsset poseLandmarker;
        public TextAsset anchorsCSV;

        [Header("Settings")]
        [Range(0.0f, 1.0f)]
        public float scoreThreshold = 0.75f;

        private const int k_NumAnchors = 2254;
        private const int k_NumKeypoints = 33;
        private const int detectorInputSize = 224;
        private const int landmarkerInputSize = 256;

        private float[,] m_Anchors;
        private Worker m_PoseDetectorWorker;
        private Worker m_PoseLandmarkerWorker;
        private Tensor<float> m_DetectorInput;
        private Tensor<float> m_LandmarkerInput;

        private float m_TextureWidth;
        private float m_TextureHeight;

        [Header("Testing"),Tooltip("For testing purposes, add images here as textures")]
        [SerializeField]
        private Texture presetTexture;
        [SerializeField] private bool isPresetTexture = true;
        public float poseDetectionScale = 300f;
    
        private async void Start()
        {
            // Validate required TextAssets and other references
            if (anchorsCSV == null)
            {
#if UNITY_EDITOR
                // Try to auto-assign the anchors CSV when running in the Editor
                var autoAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/anchors.csv");
                if (autoAsset != null)
                {
                    anchorsCSV = autoAsset;
                    Debug.Log("PoseDetectionLive: auto-assigned anchorsCSV from Assets/Data/anchors.csv");
                }
                else
                {
                    Debug.LogError("PoseDetectionLive: anchorsCSV is not assigned. Please assign Assets/Data/anchors.csv in the Inspector.");
                    enabled = false;
                    return;
                }
#else
            Debug.LogError("PoseDetectionLive: anchorsCSV is not assigned. Please assign Assets/Data/anchors.csv in the Inspector.");
            enabled = false;
            return;
#endif
            }

            // Load anchor data for the BlazePose model
            m_Anchors = BlazeUtils.LoadAnchors(anchorsCSV.text, k_NumAnchors);

            // Validate scene references early to avoid allocating models/tensors when required refs are missing
            if (posePreview == null)
            {
                Debug.LogError("PoseDetectionLive: posePreview is not assigned. Please assign it in the Inspector.");
                enabled = false;
                return;
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

            // Validate model assets
            if (poseDetector == null)
            {
                Debug.LogError("PoseDetectionLive: poseDetector ModelAsset is not assigned. Please assign it in the Inspector.");
                enabled = false;
                return;
            }
            if (poseLandmarker == null)
            {
                Debug.LogError("PoseDetectionLive: poseLandmarker ModelAsset is not assigned. Please assign it in the Inspector.");
                enabled = false;
                return;
            }

            // Load both detector and landmarker models
            var poseDetectorModel = ModelLoader.Load(poseDetector);
            if (poseDetectorModel == null)
            {
                Debug.LogError("PoseDetectionLive: Failed to load poseDetector model via ModelLoader.Load(poseDetector). Check the ModelAsset and package setup.");
                enabled = false;
                return;
            }

            // Modify model graph for post-processing (filtering + argmax)
            var graph = new FunctionalGraph();
            var input = graph.AddInput(poseDetectorModel, 0);
            var outputs = Functional.Forward(poseDetectorModel, input);
            var boxes = outputs[0];
            var scores = outputs[1];
            var idx_scores_boxes = BlazeUtils.ArgMaxFiltering(boxes, scores);
            poseDetectorModel = graph.Compile(idx_scores_boxes.Item1, idx_scores_boxes.Item2, idx_scores_boxes.Item3);

            // Initialize workers: try GPUCompute then fallback to CPU
            try
            {
                m_PoseDetectorWorker = new Worker(poseDetectorModel, BackendType.GPUCompute);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"PoseDetectionLive: GPU backend failed for pose detector, falling back to CPU. Exception: {e.Message}");
                try { m_PoseDetectorWorker = new Worker(poseDetectorModel, BackendType.CPU); }
                catch (Exception e2) { Debug.LogError($"PoseDetectionLive: Failed to create pose detector Worker: {e2}"); enabled = false; return; }
            }

            var landmarkerModel = ModelLoader.Load(poseLandmarker);
            if (landmarkerModel == null)
            {
                Debug.LogError("PoseDetectionLive: Failed to load poseLandmarker model via ModelLoader.Load(poseLandmarker). Check the ModelAsset and package setup.");
                enabled = false;
                return;
            }

            try
            {
                m_PoseLandmarkerWorker = new Worker(landmarkerModel, BackendType.GPUCompute);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"PoseDetectionLive: GPU backend failed for landmarker, falling back to CPU. Exception: {e.Message}");
                try { m_PoseLandmarkerWorker = new Worker(landmarkerModel, BackendType.CPU); }
                catch (Exception e2) { Debug.LogError($"PoseDetectionLive: Failed to create landmarker Worker: {e2}"); enabled = false; return; }
            }

            // Allocate tensors for model input
            m_DetectorInput = new Tensor<float>(new TensorShape(1, detectorInputSize, detectorInputSize, 3));
            m_LandmarkerInput = new Tensor<float>(new TensorShape(1, landmarkerInputSize, landmarkerInputSize, 3));

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

            Debug.Log("Camera ready. Starting live pose detection...");

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
            m_PoseDetectorWorker.Dispose();
            m_PoseLandmarkerWorker.Dispose();
            m_DetectorInput.Dispose();
            m_LandmarkerInput.Dispose();
        }

        private void OnDisable()
        {
            try
            {
                m_PoseDetectorWorker?.Dispose();
            }
            catch { }
            try
            {
                m_PoseLandmarkerWorker?.Dispose();
            }
            catch { }
            try
            {
                m_DetectorInput?.Dispose();
            }
            catch { }
            try
            {
                m_LandmarkerInput?.Dispose();
            }
            catch { }
        }

        private void OnDestroy()
        {
            OnDisable();
        }

        private Vector3 ImageToWorld(Vector2 position)
        {
            return (position - 0.5f * new Vector2(m_TextureWidth, m_TextureHeight)) / m_TextureHeight;
        }

        private async Awaitable Detect(Texture texture)
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
            if (m_PoseDetectorWorker == null)
            {
                Debug.LogError("PoseDetectionLive: m_PoseDetectorWorker is null before scheduling. Aborting Detect.");
                return;
            }
            try
            {
                m_PoseDetectorWorker.Schedule(m_DetectorInput);
            }
            catch (Exception e)
            {
                Debug.LogError($"PoseDetectionLive: Exception while scheduling pose detector worker: {e}\nWorker: {m_PoseDetectorWorker}\nTensor: {m_DetectorInput}");
                return;
            }

            // Retrieve inference results
            var outputIdxAwaitable = (m_PoseDetectorWorker.PeekOutput(0) as Tensor<int>).ReadbackAndCloneAsync();
            var outputScoreAwaitable = (m_PoseDetectorWorker.PeekOutput(1) as Tensor<float>).ReadbackAndCloneAsync();
            var outputBoxAwaitable = (m_PoseDetectorWorker.PeekOutput(2) as Tensor<float>).ReadbackAndCloneAsync();

            using var outputIdx = await outputIdxAwaitable;
            using var outputScore = await outputScoreAwaitable;
            using var outputBox = await outputBoxAwaitable;

            bool scorePassesThreshold = outputScore[0] >= scoreThreshold;
            posePreview?.SetActive(scorePassesThreshold);

            if (!scorePassesThreshold)
                return;

            // === Stage 2: Keypoint refinement ===
            var idx = outputIdx[0];
            var anchorPosition = detectorInputSize * new float2(m_Anchors[idx, 0], m_Anchors[idx, 1]);

            var face_ImageSpace = BlazeUtils.mul(M, anchorPosition + new float2(outputBox[0, 0, 0], outputBox[0, 0, 1]));
            var faceTopRight_ImageSpace = BlazeUtils.mul(M, anchorPosition + new float2(outputBox[0, 0, 0] + 0.5f * outputBox[0, 0, 2], outputBox[0, 0, 1] + 0.5f * outputBox[0, 0, 3]));

            var kp1_ImageSpace = BlazeUtils.mul(M, anchorPosition + new float2(outputBox[0, 0, 4], outputBox[0, 0, 5]));
            var kp2_ImageSpace = BlazeUtils.mul(M, anchorPosition + new float2(outputBox[0, 0, 6], outputBox[0, 0, 7]));

            var delta_ImageSpace = kp2_ImageSpace - kp1_ImageSpace;
            var dscale = 1.25f;
            var radius = dscale * math.length(delta_ImageSpace);
            var theta = math.atan2(delta_ImageSpace.y, delta_ImageSpace.x);

            var origin2 = new float2(0.5f * landmarkerInputSize, 0.5f * landmarkerInputSize);
            var scale2 = radius / (0.5f * landmarkerInputSize);

            var M2 = BlazeUtils.mul(
                BlazeUtils.mul(
                    BlazeUtils.mul(BlazeUtils.TranslationMatrix(kp1_ImageSpace),
                        BlazeUtils.ScaleMatrix(new float2(scale2, -scale2))),
                    BlazeUtils.RotationMatrix(0.5f * Mathf.PI - theta)),
                BlazeUtils.TranslationMatrix(-origin2)
            );

            BlazeUtils.SampleImageAffine(texture, m_LandmarkerInput, M2);

            var boxSize = 2f * (faceTopRight_ImageSpace - face_ImageSpace);
            posePreview.SetBoundingBox(true, ImageToWorld(face_ImageSpace), boxSize / m_TextureHeight);
            posePreview.SetBoundingCircle(true, ImageToWorld(kp1_ImageSpace), radius / m_TextureHeight);

            m_PoseLandmarkerWorker.Schedule(m_LandmarkerInput);

            // Wait for landmark results
            var landmarksAwaitable = (m_PoseLandmarkerWorker.PeekOutput("Identity") as Tensor<float>).ReadbackAndCloneAsync();
            using var landmarks = await landmarksAwaitable;

            for (int i = 0; i < k_NumKeypoints; i++)
            {
                var position_ImageSpace = BlazeUtils.mul(M2, new float2(landmarks[5 * i + 0], landmarks[5 * i + 1]));
                var visibility = landmarks[5 * i + 3];
                var presence = landmarks[5 * i + 4];

                Vector3 position_WorldSpace = ImageToWorld(position_ImageSpace) + new Vector3(0, 0, landmarks[5 * i + 2] / m_TextureHeight);
                posePreview.SetKeypoint(i, visibility > 0.5f && presence > 0.5f, position_WorldSpace * poseDetectionScale);
            }
        }
    }
}
