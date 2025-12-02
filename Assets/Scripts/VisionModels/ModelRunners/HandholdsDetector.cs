using System.Collections.Generic;
using System.Diagnostics;
using Configs;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;
using VisionModels.Input;
using VisionModels.Utilities;


namespace VisionModels.ModelRunners
{
    public class HandholdsDetector : MonoBehaviour
    {
        public enum ProblemColor
        {
            Gray,
            orange,
            black,
            blue,
            green,
            purple,
            red,
            Turquise,
            volume,
            white,
            yellow,
            All
        }

        private List<DetectedHandhold> _persistentHandholds = new();
        private int _nextHandholdId = 0;
        private bool _debugMode;

        private ModelAsset _modelAsset;
        private TextAsset _classesAsset;
        private RawImage _displayImage;
        private Texture2D _borderTexture;
        private Font _font;
        private string _videoFilename;

        private const BackendType Backend = BackendType.GPUCompute;

        private Transform _displayLocation;
        private Worker _worker;
        private string[] _labels;
        private RenderTexture _targetRT;
        private Sprite _borderSprite;

        //Image size for the model
        private const int ImageWidth = 960;
        private const int ImageHeight = 960;

        private Texture _video;
        private Texture2D _still;
        private WebCamTexture _webcamTexture;

        private bool _isModelReady;

        private List<GameObject> _boxPool = new();
        private float _iouThreshold = 0.5f;
        private float _scoreThreshold = 0.5f;

        private Tensor<float> centersToCorners;

        private bool _mirrorHorizontally;
        private WebCamTexture _cam;
        private bool _useWebcam = true;
        private ProblemColor _selectedColor;
        
        private Button _detectionToggleButton;
        private bool _isButtonPressed;

        private void OnEnable()
        {
            AppEvents.WebcamReady += OnWebcamReady;
            AppEvents.VideoReady += OnVideoReady;
            AppEvents.StillReady += OnStillReady;
            AppEvents.ConfigureHandholdsDetector += OnConfigureHandholdsDetector;
        }

        private void OnDisable()
        {
            AppEvents.WebcamReady -= OnWebcamReady;
            AppEvents.VideoReady -= OnVideoReady;
            AppEvents.StillReady -= OnStillReady;
            AppEvents.ConfigureHandholdsDetector -= OnConfigureHandholdsDetector;
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

        private void OnConfigureHandholdsDetector(HandholdsDetectorConfig config)
        {
            _modelAsset = config.Model;
            _classesAsset = config.Classes;
            _selectedColor = config.ProblemColor;
            _displayImage = config.RawImage;
            _borderTexture = config.BorderTexture;
            _font = config.Font;
            _detectionToggleButton = config.HandholdDetectButton;

            StartModel();
            SetupButtonEventTriggers();
        }

        private void SetupButtonEventTriggers()
        {
            _detectionToggleButton.onClick.AddListener(() =>
            {
                if (_worker == null) LoadModel();
                ExecuteML();
                _worker?.Dispose();
                _worker = null;
            });
        }
        
        private void StartModel()
        {
            //Parse neural net labels
            _labels = _classesAsset.text.Split('\n');

            LoadModel();

            _targetRT = new RenderTexture(ImageWidth, ImageHeight, 0);

            //Create image to display video
            _displayLocation = _displayImage.transform;

            _borderSprite = Sprite.Create(_borderTexture, new Rect(0, 0, _borderTexture.width, _borderTexture.height),
                new Vector2(_borderTexture.width / 2, _borderTexture.height / 2));
            _isModelReady = true;
        }

        private void LoadModel()
        {
            //Load model
            var model1 = ModelLoader.Load(_modelAsset);

            centersToCorners = new Tensor<float>(new TensorShape(4, 4),
                new float[]
                {
                    1, 0, 1, 0,
                    0, 1, 0, 1,
                    -0.5f, 0, 0.5f, 0,
                    0, -0.5f, 0, 0.5f
                });

            //Here we transform the output of the model1 by feeding it through a Non-Max-Suppression layer.
            var graph = new FunctionalGraph();
            var inputs = graph.AddInputs(model1);
            int numClasses = 11; // ← set this to your class count

            var modelOutput = Functional.Forward(model1, inputs)[0];
            var boxCoords = modelOutput[0, 0..4, ..].Transpose(0, 1);
            var allScores = modelOutput[0, 4..(4 + numClasses), ..];
            var scores = Functional.ReduceMax(allScores, 0); //shape=(8400)
            var classIDs = Functional.ArgMax(allScores, 0); //shape=(8400)
            var boxCorners = Functional.MatMul(boxCoords, Functional.Constant(centersToCorners)); //shape=(8400,4)
            var indices = Functional.NMS(boxCorners, scores, _iouThreshold, _scoreThreshold); //shape=(N)
            var coords = Functional.IndexSelect(boxCoords, 0, indices); //shape=(N,4)
            var labelIDs = Functional.IndexSelect(classIDs, 0, indices); //shape=(N)

            //Create worker to run model
            _worker = new Worker(graph.Compile(coords, labelIDs), Backend);
        }

        private bool ShouldDisplayLabel(string label, ProblemColor selectedColor)
        {
            if (selectedColor == ProblemColor.All)
                return true;

            string colorName = selectedColor.ToString().ToLower();
            return label.ToLower().Contains(colorName);
        }

        private void ExecuteML()
        {
            if (HandleInput()) return;
            var stopwatch = Stopwatch.StartNew(); // debug timing

            using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, ImageHeight, ImageWidth));
            TextureConverter.ToTensor(_targetRT, inputTensor, default);
            _worker.Schedule(inputTensor);

            using var output = (_worker.PeekOutput("output_0") as Tensor<float>).ReadbackAndClone();
            using var labelIDs = (_worker.PeekOutput("output_1") as Tensor<int>).ReadbackAndClone();

            float displayWidth = _displayImage.rectTransform.rect.width;
            float displayHeight = _displayImage.rectTransform.rect.height;
            float scaleX = displayWidth / ImageWidth;
            float scaleY = displayHeight / ImageHeight;

            int boxesFound = output.shape[0];

            // Mark all existing handholds as not seen this frame
            foreach (var handhold in _persistentHandholds)
            {
                handhold.FramesSinceLastSeen++;
            }

            // Update or add new detections
            for (int n = 0; n < Mathf.Min(boxesFound, 200); n++)
            {
                string label = _labels[labelIDs[n]];
                if (!ShouldDisplayLabel(label, _selectedColor))
                    continue;

                var box = new BoundingBox
                {
                    CenterX = output[n, 0] * scaleX - displayWidth / 2,
                    CenterY = -(output[n, 1] * scaleY - displayHeight / 2), // important -
                    Width = output[n, 2] * scaleX,
                    Height = output[n, 3] * scaleY,
                    Label = label,
                };

                UpdateOrAddHandhold(box, label);
                stopwatch.Stop(); // debug timing
                UnityEngine.Debug.Log($"ExecuteML took {stopwatch.Elapsed.TotalMilliseconds:F2} ms"); // debug timing
            }

            // Draw persistent handholds
            ClearAnnotations();
            for (int i = 0; i < _persistentHandholds.Count; i++)
            {
                DrawBox(_persistentHandholds[i].Box, i, displayHeight * 0.05f);
            }
        }

        private void UpdateOrAddHandhold(BoundingBox box, string label)
        {
            // Find matching handhold based on proximity
            DetectedHandhold match = null;
            float minDistance = float.MaxValue;
            float distanceThreshold = 15f; // pixels - adjust based on your needs

            foreach (var handhold in _persistentHandholds)
            {
                float dx = handhold.Box.CenterX - box.CenterX;
                float dy = handhold.Box.CenterY - box.CenterY;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                if (distance < distanceThreshold && distance < minDistance)
                {
                    match = handhold;
                    minDistance = distance;
                }
            }

            if (match != null)
            {
                // Update existing handhold
                match.Box = box;
                match.Label = label;
                match.FramesSinceLastSeen = 0;
            }
            else
            {
                // Add new handhold
                var newHandhold = new DetectedHandhold
                {
                    Box = box,
                    Label = label,
                    FramesSinceLastSeen = 0,
                    Id = _nextHandholdId++
                };
    
                _persistentHandholds.Add(newHandhold);
                AppEvents.RaiseNewHandholdDetected(newHandhold);
            }
        }

        private bool HandleInput()
        {
            InputMode mode = _useWebcam ? InputMode.Webcam : (_video ? InputMode.Video : InputMode.Still);

            return InputProcessor.ProcessInput(mode, _cam, _video, _still, _targetRT, _displayImage, _mirrorHorizontally);
        }

        private void DrawBox(BoundingBox box, int id, float fontSize)
        {
            var panel = AnnotationManager.GetOrCreateBox(
                _boxPool, id, 
                _displayLocation, 
                _borderSprite, 
                _font,
                Color.red);
            
            panel.transform.localPosition = new Vector3(box.CenterX, box.CenterY);

            var rt = panel.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(box.Width, box.Height);

            var label = panel.GetComponentInChildren<Text>();
            label.text = box.Label;
            label.fontSize = (int)fontSize;
        }

        private void ClearAnnotations() => AnnotationManager.ClearAnnotations(_boxPool);

        private void OnDestroy()
        {
            centersToCorners?.Dispose();
            _worker?.Dispose();
        }
    }
}