using System.Collections.Generic;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;

namespace VisionModelsV2
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

        private ModelAsset _modelAsset;
        private TextAsset _classesAsset;
        private RawImage _displayImage;
        private Texture2D _borderTexture;
        private Font _font;
        private string _videoFilename;

        private const BackendType backend = BackendType.GPUCompute;

        private Transform _displayLocation;
        private Worker _worker;
        private string[] _labels;
        private RenderTexture _targetRT;
        private Sprite _borderSprite;

        //Image size for the model
        private const int imageWidth = 640;
        private const int imageHeight = 640;

        private Texture _video;
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

        private void OnEnable()
        {
            AppEvents.WebcamReady += OnWebcamReady;
            AppEvents.VideoReady += OnVideoReady;
            AppEvents.ConfigureHandholdsDetector += OnConfigureHandholdsDetector;
        }

        private void OnDisable()
        {
            AppEvents.WebcamReady -= OnWebcamReady;
            AppEvents.VideoReady -= OnVideoReady;
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

        private void OnConfigureHandholdsDetector(
            ModelAsset model,
            TextAsset classes,
            ProblemColor problemColor,
            RawImage rawImage,
            Font font,
            Texture2D borderTex)
        {
            _modelAsset = model;
            _classesAsset = classes;
            _selectedColor = problemColor;
            _displayImage = rawImage;
            _borderTexture = borderTex;
            _font = font;

            StartModel();
        }

        private void StartModel()
        {
            //Parse neural net labels
            _labels = _classesAsset.text.Split('\n');

            LoadModel();

            _targetRT = new RenderTexture(imageWidth, imageHeight, 0);

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
            _worker = new Worker(graph.Compile(coords, labelIDs), backend);
        }

        private bool ShouldDisplayLabel(string label, ProblemColor selectedColor)
        {
            if (selectedColor == ProblemColor.All)
                return true;

            string colorName = selectedColor.ToString().ToLower();
            return label.ToLower().Contains(colorName);
        }

        private void Update()
        {
            if (!_isModelReady) return;
            ExecuteML();
        }

        private void ExecuteML()
        {
            ClearAnnotations();

            if (HandleInput()) return;

            using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, imageHeight, imageWidth));
            TextureConverter.ToTensor(_targetRT, inputTensor, default);
            _worker.Schedule(inputTensor);

            using var output = (_worker.PeekOutput("output_0") as Tensor<float>).ReadbackAndClone();
            using var labelIDs = (_worker.PeekOutput("output_1") as Tensor<int>).ReadbackAndClone();

            float displayWidth = _displayImage.rectTransform.rect.width;
            float displayHeight = _displayImage.rectTransform.rect.height;

            float scaleX = displayWidth / imageWidth;
            float scaleY = displayHeight / imageHeight;

            int boxesFound = output.shape[0];
            Debug.Log($"Boxes found: {boxesFound}, Labels length: {_labels.Length}");

            for (int i = 0; i < Mathf.Min(boxesFound, 5); i++)
            {
                int id = labelIDs[i];
                Debug.Log($"Label ID[{i}] = {id}");
            }

            //Draw the bounding boxes
            int drawnBoxes = 0;
            for (int n = 0; n < Mathf.Min(boxesFound, 200); n++)
            {
                string label = _labels[labelIDs[n]];
    
                // Skip if color doesn't match filter
                if (!ShouldDisplayLabel(label, _selectedColor))
                    continue;

                var box = new BoundingBox
                {
                    CenterX = output[n, 0] * scaleX - displayWidth / 2,
                    CenterY = output[n, 1] * scaleY - displayHeight / 2,
                    Width = output[n, 2] * scaleX,
                    Height = output[n, 3] * scaleY,
                    Label = label,
                };
                DrawBox(box, drawnBoxes++, displayHeight * 0.05f);
            }
        }

        private bool HandleInput()
        {
            return InputProcessor.ProcessInput(
                _useWebcam,
                _cam,
                _video,
                _targetRT,
                _displayImage,
                _mirrorHorizontally
            );
        }

        private void DrawBox(BoundingBox box, int id, float fontSize)
        {
            var panel = AnnotationManager.GetOrCreateBox(_boxPool, id, _displayLocation, _borderSprite, _font,
                Color.yellow);
            panel.transform.localPosition = new Vector3(box.CenterX, -box.CenterY);

            var rt = panel.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(box.Width, box.Height);

            var label = panel.GetComponentInChildren<Text>();
            label.text = box.Label;
            label.fontSize = (int)fontSize;
        }

        private void ClearAnnotations()
        {
            AnnotationManager.ClearAnnotations(_boxPool);
        }
        
        private void OnDestroy()
        {
            centersToCorners?.Dispose();
            _worker?.Dispose();
        }
    }
}