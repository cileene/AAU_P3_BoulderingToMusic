using System.Collections.Generic;
using System.IO;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace VisionModelsV2
{
    public class PoseDetector : MonoBehaviour
    {
        [Tooltip("Drag your YOLO11n-pose .onnx model here")]
        public ModelAsset modelAsset;

        [Tooltip("Display target (Raw Image)")]
        public RawImage displayImage;

        [Tooltip("Border or dot texture")]
        public Texture2D borderTexture;

        [Tooltip("Font (optional, not used for pose)")]
        public Font font;

        [Tooltip("Video name inside StreamingAssets folder")]
        public string videoFilename = "sample.mp4";

        private const BackendType backend = BackendType.GPUCompute;

        private Worker worker;
        private RenderTexture targetRT;
        private Transform displayLocation;
        private Sprite borderSprite;
        private VideoPlayer video;

        private const int imageWidth = 640;
        private const int imageHeight = 640;

        private readonly List<GameObject> objectPool = new();

        [Tooltip("Confidence threshold for pose and keypoints")]
        [SerializeField, Range(0, 1)]
        private float scoreThreshold = 0.5f;

        private void Start()
        {
            LoadModel();

            targetRT = new RenderTexture(imageWidth, imageHeight, 0);
            displayLocation = displayImage.transform;

            SetupVideo();

            borderSprite = Sprite.Create(borderTexture,
                new Rect(0, 0, borderTexture.width, borderTexture.height),
                new Vector2(0.5f, 0.5f));
        }

        private void LoadModel()
        {
            var model = ModelLoader.Load(modelAsset);
            worker = new Worker(model, backend);
            Debug.Log("YOLO11n-Pose model loaded");
        }

        private void SetupVideo()
        {
            video = gameObject.AddComponent<VideoPlayer>();
            video.renderMode = VideoRenderMode.APIOnly;
            video.source = VideoSource.Url;
            video.url = Path.Join(Application.streamingAssetsPath, videoFilename);
            video.isLooping = true;
            video.Play();
        }

        private void Update()
        {
            ExecuteML();
        }

        private void ExecuteML()
        {
            // Disable all previous annotations (reuse pool)
            foreach (var obj in objectPool)
                obj.SetActive(false);

            if (!video || !video.texture) return;

            Graphics.Blit(video.texture, targetRT);
            displayImage.texture = targetRT;

            using var inputTensor = new Tensor<float>(new TensorShape(1, 3, imageHeight, imageWidth));
            TextureConverter.ToTensor(targetRT, inputTensor, default);
            worker.Schedule(inputTensor);

            using var output = (worker.PeekOutput() as Tensor<float>).ReadbackAndClone();

            // YOLO11n-pose output shape: [1, 56, N]
            int numDetections = output.shape[2];
            int numKeypoints = (output.shape[1] - 5) / 3; // (56 - 5) / 3 = 17 keypoints

            float displayWidth = displayImage.rectTransform.rect.width;
            float displayHeight = displayImage.rectTransform.rect.height;

            for (int n = 0; n < numDetections; n++)
            {
                float conf = output[0, 4, n];
                if (conf < scoreThreshold) continue;

                List<Vector2> keypoints = new();
                for (int k = 0; k < numKeypoints; k++)
                {
                    // YOLO11n-pose gives absolute coordinates in model space (0–640)
                    float x = output[0, 5 + k * 3, n] - imageWidth / 2f;
                    float y = output[0, 5 + k * 3 + 1, n] - imageHeight / 2f;
                    float c = output[0, 5 + k * 3 + 2, n];

                    if (c > 0.3f)
                    {
                        // Scale once to match display size
                        Vector2 scaled = new Vector2(
                            x * (displayWidth / imageWidth),
                            -y * (displayHeight / imageHeight)
                        );
                        keypoints.Add(scaled);
                    }
                    else
                    {
                        keypoints.Add(Vector2.zero);
                    }
                }

                DrawPose(keypoints);
            }
        }


        private void DrawPose(List<Vector2> keypoints)
        {
            // Draw points (reuse existing objects)
            for (int i = 0; i < keypoints.Count; i++)
            {
                if (keypoints[i] == Vector2.zero) continue;

                GameObject dot;
                if (i < objectPool.Count)
                {
                    dot = objectPool[i];
                    dot.SetActive(true);
                }
                else
                {
                    dot = CreateDot(Color.cyan);
                    objectPool.Add(dot);
                }

                dot.transform.localPosition = new Vector3(keypoints[i].x, keypoints[i].y, 0);
            }

            // COCO skeleton connections
            int[,] skeletonPairs =
            {
                {5,7}, {7,9}, {6,8}, {8,10}, // arms
                {11,13}, {13,15}, {12,14}, {14,16}, // legs
                {5,6}, {11,12}, {5,11}, {6,12}, // torso
                {0,5}, {0,6}, {0,11}, {0,12} // head connections
            };

            for (int i = 0; i < skeletonPairs.GetLength(0); i++)
            {
                int a = skeletonPairs[i, 0];
                int b = skeletonPairs[i, 1];

                if (a < keypoints.Count && b < keypoints.Count &&
                    keypoints[a] != Vector2.zero && keypoints[b] != Vector2.zero)
                {
                    ConnectKeypoints(keypoints[a], keypoints[b]);
                }
            }
        }


        private GameObject CreateDot(Color color)
        {
            var dot = new GameObject("Keypoint");
            var img = dot.AddComponent<Image>();
            img.color = color;
            dot.transform.SetParent(displayLocation, false);

            var rt = dot.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(8, 8);
            objectPool.Add(dot);
            return dot;
        }

        private void ConnectKeypoints(Vector2 a, Vector2 b)
        {
            var line = new GameObject("Limb");
            var img = line.AddComponent<Image>();
            img.color = Color.yellow;
            img.sprite = borderSprite;
            img.type = Image.Type.Sliced;
            line.transform.SetParent(displayLocation, false);

            var rt = line.GetComponent<RectTransform>();
            Vector2 dir = b - a;
            rt.sizeDelta = new Vector2(dir.magnitude, 2);
            rt.localPosition = a + dir / 2;
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            objectPool.Add(line);
        }

        private void ClearAnnotations()
        {
            foreach (var obj in objectPool)
                obj.SetActive(false);
            objectPool.Clear();
        }

        private void OnDestroy()
        {
            worker?.Dispose();
        }
    }
}
