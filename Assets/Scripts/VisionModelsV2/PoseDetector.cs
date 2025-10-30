using System.Collections.Generic;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;

namespace VisionModelsV2
{
    public class PoseDetector : MonoBehaviour
    {
        private ModelAsset _modelAsset;
        private RawImage _displayImage;
        private Texture2D _borderTexture;
        private Font _font;
        private string _videoFilename;
        private bool _mirrorHorizontally;

        private const BackendType backend = BackendType.GPUCompute;

        private Worker _worker;
        private RenderTexture _targetRT;
        private Transform _displayLocation;
        private Sprite _borderSprite;
        private Texture _video;
        private WebCamTexture _cam;
        private bool _useWebcam = true;

        private const int imageWidth = 640;
        private const int imageHeight = 640;

        private readonly List<GameObject> objectPool = new();
        private float _scoreThreshold = 0.5f;

        private bool _isModelReady;
        
        private void OnEnable()
        {
            AppEvents.WebcamReady += OnWebcamReady;
            AppEvents.VideoReady += OnVideoReady;
            AppEvents.ConfigurePoseDetector += OnConfigurePoseDetector;
        }
        
        private void OnDisable()
        {
            AppEvents.WebcamReady -= OnWebcamReady;
            AppEvents.VideoReady -= OnVideoReady;
            AppEvents.ConfigurePoseDetector -= OnConfigurePoseDetector;
        }
        
        private void OnWebcamReady(WebCamTexture cam)
        {
            _cam = cam;
            _useWebcam = true;
        }
        
        private void OnVideoReady(Texture videoTexture)
        {
            _video = videoTexture;
            _useWebcam = false;
        }
        
        private void OnConfigurePoseDetector(
            ModelAsset model,
            RawImage rawImage,
            Texture2D borderTex)
        {
            _modelAsset = model;
            _displayImage = rawImage;
            _borderTexture = borderTex;

            StartModel();
        }

        private void StartModel()
        {
            LoadModel();

            _targetRT = new RenderTexture(imageWidth, imageHeight, 0);
            _displayLocation = _displayImage.transform;

            _borderSprite = Sprite.Create(_borderTexture,
                new Rect(0, 0, _borderTexture.width, _borderTexture.height),
                new Vector2(0.5f, 0.5f));
            
            _isModelReady = true;
        }

        private void LoadModel()
        {
            var model = ModelLoader.Load(_modelAsset);
            _worker = new Worker(model, backend);
            Debug.Log("YOLO11n-Pose model loaded");
        }

        private void Update()
        {
            if (!_isModelReady) return;
            ExecuteML();
        }

        private void ExecuteML()
        {
            // Disable all previous annotations (reuse pool)
            foreach (var obj in objectPool)
                obj.SetActive(false);

            ClearAnnotations();

            Texture sourceTex = null;
            int srcW = 0, srcH = 0;

            if (_useWebcam && _cam != null && _cam.width > 16 && _cam.height > 16)
            {
                sourceTex = _cam;
                srcW = _cam.width; srcH = _cam.height;
            }
            else if (!_useWebcam && _video && _video)
            {
                sourceTex = _video;
                srcW = (int)_video.width;
                srcH = (int)_video.height;
            }
            else
            {
                return;
            }

        
            // Correct orientation and mirroring for webcam/video before inference
            int rot = 0;
            bool vflip = false;
            if (_useWebcam && _cam != null)
            {
                rot = _cam.videoRotationAngle;                 // 0, 90, 180, 270 from platform
                vflip = _cam.videoVerticallyMirrored;          // front cameras often true
            }

            // Rotate the UI container so the feed and overlays stay aligned
            var eul = _displayImage.rectTransform.localEulerAngles;
            _displayImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -rot);

            // Letterbox to 640x640 while preserving aspect, then apply requested mirror and platform vertical flip
            float aspect = srcW * 1f / Mathf.Max(1, srcH);
            float sx = (_mirrorHorizontally ? -1f : 1f) / aspect; // horizontal mirror for selfie view
            float sy = vflip ? -1f : 1f;                         // platform vertical flip
            Vector2 scale = new Vector2(sx, sy);
            Vector2 offset = new Vector2(_mirrorHorizontally ? 1f : 0f, vflip ? 1f : 0f);

            Graphics.Blit(sourceTex, _targetRT, scale, offset);
            _displayImage.texture = _targetRT;

            using var inputTensor = new Tensor<float>(new TensorShape(1, 3, imageHeight, imageWidth));
            TextureConverter.ToTensor(_targetRT, inputTensor, default);
            _worker.Schedule(inputTensor);

            using var output = (_worker.PeekOutput() as Tensor<float>).ReadbackAndClone();

            // YOLO11n-pose output shape: [1, 56, N]
            int numDetections = output.shape[2];
            int numKeypoints = (output.shape[1] - 5) / 3; // (56 - 5) / 3 = 17 keypoints

            float displayWidth = _displayImage.rectTransform.rect.width;
            float displayHeight = _displayImage.rectTransform.rect.height;

            for (int n = 0; n < numDetections; n++)
            {
                float conf = output[0, 4, n];
                if (conf < _scoreThreshold) continue;

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
            dot.transform.SetParent(_displayLocation, false);

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
            img.sprite = _borderSprite;
            img.type = Image.Type.Sliced;
            line.transform.SetParent(_displayLocation, false);

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
            _worker?.Dispose();
        }
    }
}
