using System.Collections.Generic;
using Configs;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;
using VisionModels.Input;
using VisionModels.Utilities;

namespace VisionModels.ModelRunners
{
    public class PersonDetector : MonoBehaviour
    {
        // nick stuff
        public Vector3 boxPosition;
    
        // yolo stuff
        private ModelAsset _modelAsset;
        private TextAsset _classesAsset;
        private RawImage _displayImage;
        private Texture2D _borderTexture;
        private Font _font;
        
        private bool _useWebcam;
        private string _webcamDeviceName;
        private string _videoFilename;
        
        private bool _preferFrontCamera;
        
        private bool _mirrorHorizontally; // set true for selfie view

        private const BackendType Backend = BackendType.GPUCompute;

        private Transform _displayLocation;
        private Worker _worker;
        private string[] _labels;
        private RenderTexture _targetRT;
        private Sprite _borderSprite;

        // Model input size
        private const int ImageWidth = 640;
        private const int ImageHeight = 640;

        // Inputs
        private Texture _video;
        private Texture2D _still;
        private WebCamTexture _cam;

        private bool _isModelReady;

        private List<GameObject> _boxPool = new();
    
        private readonly BoundingBox _personBox = new BoundingBox(); // persistent box for person

        [Tooltip("Intersection over union threshold used for non-maximum suppression")]
        [SerializeField, Range(0, 1)] private float iouThreshold = 0.5f;

        [Tooltip("Confidence score threshold used for non-maximum suppression")]
        [SerializeField, Range(0, 1)] private float scoreThreshold = 0.5f;

        private Tensor<float> _centersToCorners;

        // Simple test log
        private bool _lastHasPerson;
        
        private void OnEnable()
        {
            AppEvents.WebcamReady += OnWebcamReady;
            AppEvents.VideoReady += OnVideoReady;
            AppEvents.StillReady += OnStillReady;
            AppEvents.ConfigurePersonDetector += OnConfigurePersonDetector;
        }
        
        private void OnDisable()
        {
            AppEvents.WebcamReady -= OnWebcamReady;
            AppEvents.VideoReady -= OnVideoReady;
            AppEvents.StillReady -= OnStillReady;
            AppEvents.ConfigurePersonDetector -= OnConfigurePersonDetector;
        }
        
        private void OnVideoReady(Texture video)
        {
            _video = video;
            _useWebcam = false;
        }
        
        private void OnWebcamReady(WebCamTexture cam)
        {
            _cam = cam;
            _useWebcam = true;
        }
        
        private void OnStillReady(Texture2D still)
        {
            _still = still;
            _useWebcam = false;
        }
        
        private void OnConfigurePersonDetector(PersonDetectorConfig config)
        {
            _modelAsset = config.Model;
            _classesAsset = config.Classes;
            _displayImage = config.RawImage;
            _font = config.Font;
            _borderTexture = config.BorderTexture;
            
            StartModel();
        }

        private void StartModel()
        {
            _labels = _classesAsset.text.Split('\n');
            LoadModel();

            _targetRT = new RenderTexture(ImageWidth, ImageHeight, 0);
            _displayLocation = _displayImage.transform;

            _borderSprite = Sprite.Create(
                _borderTexture,
                new Rect(0, 0, _borderTexture.width, _borderTexture.height),
                new Vector2(_borderTexture.width / 2f, _borderTexture.height / 2f)
            );
        
            Debug.Log($"{this} is ready");
            _isModelReady = true;
        }

        private void LoadModel() // here be dragons and math
        {
            var model1 = ModelLoader.Load(_modelAsset);

            _centersToCorners = new Tensor<float>(new TensorShape(4, 4),
                new float[]
                {
                    1,      0,      1,      0,
                    0,      1,      0,      1,
                    -0.5f,   0,     0.5f,   0,
                    0,     -0.5f,  0,      0.5f
                });

            var graph = new FunctionalGraph();
            var inputs = graph.AddInputs(model1);
            var modelOutput = Functional.Forward(model1, inputs)[0];                         // (1,84,8400)
            var boxCoords  = modelOutput[0, 0..4, ..].Transpose(0, 1);                       // (8400,4)
            var allScores  = modelOutput[0, 4.., ..];                                        // (80,8400)
            var scores     = Functional.ReduceMax(allScores, 0);                              // (8400)
            var classIDs   = Functional.ArgMax(allScores, 0);                                 // (8400)
            var boxCorners = Functional.MatMul(boxCoords, Functional.Constant(_centersToCorners)); // (8400,4)
            var indices    = Functional.NMS(boxCorners, scores, iouThreshold, scoreThreshold);// (N)
            var coords     = Functional.IndexSelect(boxCoords, 0, indices);                   // (N,4)
            var labelIDs   = Functional.IndexSelect(classIDs, 0, indices);                    // (N)

            _worker = new Worker(graph.Compile(coords, labelIDs), Backend);
        }

        private void Update()
        {
            if (!_isModelReady) return;
            ExecuteML();
        }

        private void ExecuteML() // Mighty Messy Method (should be split up)
        {
            ClearAnnotations();

            if (HandleInput()) return;

            using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, ImageHeight, ImageWidth));
            TextureConverter.ToTensor(_targetRT, inputTensor, default);
            _worker.Schedule(inputTensor);

            using var coords = (_worker.PeekOutput("output_0") as Tensor<float>).ReadbackAndClone(); // (N,4) centers
            using var labelIDs = (_worker.PeekOutput("output_1") as Tensor<int>).ReadbackAndClone(); // (N)

            float displayWidth = _displayImage.rectTransform.rect.width;
            float displayHeight = _displayImage.rectTransform.rect.height;
            float scaleX = displayWidth / ImageWidth;
            float scaleY = displayHeight / ImageHeight;

            bool hasPerson = false;

            int boxesFound = coords.shape[0];
        
            BoundingBox firstPerson = null; // reference to first person box found for event
        
            for (int n = 0; n < Mathf.Min(boxesFound, 200); n++)
            {
                int cls = labelIDs[n];
                var box = new BoundingBox
                {
                    CenterX = coords[n, 0] * scaleX - displayWidth / 2f,
                    CenterY = coords[n, 1] * scaleY - displayHeight / 2f,
                    Width   = coords[n, 2] * scaleX,
                    Height  = coords[n, 3] * scaleY,
                    Label   = _labels[cls],
                };

                if (cls == 0) // COCO class 0 = person
                {
                    hasPerson = true; 
                
                    // Update the persistent reference type so subscribers see live changes
                    _personBox.CenterX = box.CenterX;
                    _personBox.CenterY = box.CenterY;
                    _personBox.Width   = box.Width;
                    _personBox.Height  = box.Height;
                    _personBox.Label   = box.Label;

                    if (firstPerson == null)
                        firstPerson = _personBox; // pass the persistent instance on first sighting
                }

                DrawBox(box, n, displayHeight * 0.05f);
            }
        }

        private bool HandleInput()
        {
            InputMode mode = _useWebcam ? InputMode.Webcam :
                (_video ? InputMode.Video : InputMode.Still);

            return InputProcessor.ProcessInput(mode, _cam, _video, _still, _targetRT, _displayImage, _mirrorHorizontally);
        }

        private void DrawBox(BoundingBox box, int id, float fontSize)
        {
            var panel = AnnotationManager.GetOrCreateBox(_boxPool, id, _displayLocation, _borderSprite, _font, Color.yellow);
            panel.transform.localPosition = new Vector3(box.CenterX, -box.CenterY);
            boxPosition = panel.transform.localPosition;

            var rt = panel.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(box.Width, box.Height);

            var label = panel.GetComponentInChildren<Text>();
            label.text = box.Label;
            label.fontSize = (int)fontSize;
        }

        private void ClearAnnotations() => AnnotationManager.ClearAnnotations(_boxPool);

        private void OnDestroy()
        {
            _centersToCorners?.Dispose();
            _worker?.Dispose();

            if (_cam != null)
            {
                if (_cam.isPlaying) _cam.Stop();
                _cam = null;
            }
        }
    }
}