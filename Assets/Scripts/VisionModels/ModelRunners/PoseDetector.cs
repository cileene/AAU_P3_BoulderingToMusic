using System.Collections.Generic;
using System.Linq;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;
using VisionModels.Input;
using VisionModels.Utilities;

/*
    01 Nose
    02 Left Eye
    03 Right Eye
    04 Left Ear
    05 Right Ear
    06 Left Shoulder
    07 Right Shoulder
    08 Left Elbow
    09 Right Elbow
    10 Left Wrist
    11 Right Wrist
    12 Left Hip
    13 Right Hip
    14 Left Knee
    15 Right Knee
    16 Left Ankle
    17 Right Ankle
*/


namespace VisionModels.ModelRunners
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
        private Texture2D _still;
        private WebCamTexture _cam;
        private bool _useWebcam = true;

        private const int imageWidth = 640;
        private const int imageHeight = 640;

        private readonly List<GameObject> objectPool = new();
        private float _scoreThreshold = 0.5f;

        private bool _isModelReady;
        
        private PoseData _currentPose;

        private void OnEnable()
        {
            AppEvents.WebcamReady += OnWebcamReady;
            AppEvents.VideoReady += OnVideoReady;
            AppEvents.StillReady += OnStillReady;
            AppEvents.ConfigurePoseDetector += OnConfigurePoseDetector;
        }

        private void OnDisable()
        {
            AppEvents.WebcamReady -= OnWebcamReady;
            AppEvents.VideoReady -= OnVideoReady;
            AppEvents.StillReady -= OnStillReady;
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

        private void OnStillReady(Texture2D still)
        {
            _still = still;
            _useWebcam = false;
        }

        // How does this work with webcamtextures? Texture2D and webcamtexture both inherit from Texture, but they shouldn't be usable interchangably like this, to my understanding
        // 
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

            _currentPose = new PoseData(); // Initialize once
            
            AppEvents.RaiseNewPoseDetected(_currentPose);

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

            if (HandleInput()) return;

            //Creates a tensor with values: number of images, color channels, height, width. These all have empty values, with set sizes
            using var inputTensor = new Tensor<float>(new TensorShape(1, 3, imageHeight, imageWidth));
            //ToTensor allows the tensor to contain the information of the input image, filling the tensor with values from the texture. 
            TextureConverter.ToTensor(_targetRT, inputTensor, default);
            //Schedule allows the model to be read by the worker.
            _worker.Schedule(inputTensor);

            //peeks the workers output and sets the value it has to being the output tensor<float>
            using var output = (_worker.PeekOutput() as Tensor<float>).ReadbackAndClone();

            int numDetections = output.shape[2];
            int numKeypoints = (output.shape[1] - 5) / 3;

            float displayWidth = _displayImage.rectTransform.rect.width;
            float displayHeight = _displayImage.rectTransform.rect.height;

            List<PoseData> allDetections = new();

            for (int n = 0; n < numDetections; n++)
            {
                float conf = output[0, 4, n];
                if (conf < _scoreThreshold) continue;

                List<Vector2> keypoints = new();
                for (int k = 0; k < numKeypoints; k++)
                {
                    float x = output[0, 5 + k * 3, n] - imageWidth / 2f;
                    float y = output[0, 5 + k * 3 + 1, n] - imageHeight / 2f;
                    float c = output[0, 5 + k * 3 + 2, n];

                    if (c > 0.3f)
                    {
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

                _currentPose.UpdateKeypoints(keypoints, conf);
                DrawPose(_currentPose.Keypoints);
                break; // Only process first detection
                //Why not use keypoints[0] then? Or format it to be able to detect a custom amount of climbers 
            }
        }


        private bool HandleInput()
        {
            InputMode mode = _useWebcam ? InputMode.Webcam : (_video ? InputMode.Video : InputMode.Still);

            return InputProcessor.ProcessInput(mode, _cam, _video, _still, _targetRT, _displayImage,
                _mirrorHorizontally);
        }

        private void DrawPose(List<Vector2> keypoints)
        {
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
                }
                dot.transform.localPosition = new Vector3(keypoints[i].x, keypoints[i].y, 0);
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

        private void ClearAnnotations() => AnnotationManager.ClearAnnotations(objectPool);

        private void OnDestroy()
        {
            _worker?.Dispose();
        }
    }
}