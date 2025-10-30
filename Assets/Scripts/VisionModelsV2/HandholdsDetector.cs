using System.Collections.Generic;
using System.IO;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

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

        private void Start()
        {
            //Parse neural net labels
            _labels = _classesAsset.text.Split('\n');

            LoadModel();

            _targetRT = new RenderTexture(imageWidth, imageHeight, 0);

            //Create image to display video
            _displayLocation = _displayImage.transform;

            SetupInput();

            _borderSprite = Sprite.Create(_borderTexture, new Rect(0, 0, _borderTexture.width, _borderTexture.height), new Vector2(_borderTexture.width / 2, _borderTexture.height / 2));
        }

        private void LoadModel()
        {
            //Load model
            var model1 = ModelLoader.Load(_modelAsset);

            centersToCorners = new Tensor<float>(new TensorShape(4, 4),
                new float[]
                {
                    1,      0,      1,      0,
                    0,      1,      0,      1,
                    -0.5f,  0,      0.5f,   0,
                    0,      -0.5f,  0,      0.5f
                });

            //Here we transform the output of the model1 by feeding it through a Non-Max-Suppression layer.
            var graph = new FunctionalGraph();
            var inputs = graph.AddInputs(model1);
            int numClasses = 11; // ← set this to your class count

            var modelOutput = Functional.Forward(model1, inputs)[0];
            var boxCoords = modelOutput[0, 0..4, ..].Transpose(0, 1);
            var allScores = modelOutput[0, 4..(4 + numClasses), ..];
            var scores = Functional.ReduceMax(allScores, 0);                                //shape=(8400)
            var classIDs = Functional.ArgMax(allScores, 0);                                 //shape=(8400)
            var boxCorners = Functional.MatMul(boxCoords, Functional.Constant(centersToCorners));   //shape=(8400,4)
            var indices = Functional.NMS(boxCorners, scores, _iouThreshold, _scoreThreshold); //shape=(N)
            var coords = Functional.IndexSelect(boxCoords, 0, indices);                     //shape=(N,4)
            var labelIDs = Functional.IndexSelect(classIDs, 0, indices);                    //shape=(N)

            //Create worker to run model
            _worker = new Worker(graph.Compile(coords, labelIDs), backend);
        }

        private void Update()
        {
            if (!_isModelReady) return;
            ExecuteML();
        }

        private void ExecuteML()
        {
            ClearAnnotations();

            if (_video && _video.texture)
            {
                float aspect = _video.width * 1f / _video.height;
                Graphics.Blit(_video.texture, _targetRT, new Vector2(1f / aspect, 1), new Vector2(0, 0));
                _displayImage.texture = _targetRT;
            }
            else return;

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
            for (int n = 0; n < Mathf.Min(boxesFound, 200); n++)
            {
                var box = new BoundingBox
                {
                    centerX = output[n, 0] * scaleX - displayWidth / 2,
                    centerY = output[n, 1] * scaleY - displayHeight / 2,
                    width = output[n, 2] * scaleX,
                    height = output[n, 3] * scaleY,
                    label = _labels[labelIDs[n]],
                };
                DrawBox(box, n, displayHeight * 0.05f);
            }
        }

        private void DrawBox(BoundingBox box, int id, float fontSize)
        {
            //Create the bounding box graphic or get from pool
            GameObject panel;
            if (id < _boxPool.Count)
            {
                panel = _boxPool[id];
                panel.SetActive(true);
            }
            else
            {
                panel = CreateNewBox(Color.yellow);
            }
            //Set box position
            panel.transform.localPosition = new Vector3(box.centerX, -box.centerY);

            //Set box size
            RectTransform rt = panel.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(box.width, box.height);

            //Set label text
            var label = panel.GetComponentInChildren<Text>();
            label.text = box.label;
            label.fontSize = (int)fontSize;
        }

        private GameObject CreateNewBox(Color color)
        {
            //Create the box and set image

            var panel = new GameObject("ObjectBox");
            panel.AddComponent<CanvasRenderer>();
            Image img = panel.AddComponent<Image>();
            img.color = color;
            img.sprite = _borderSprite;
            img.type = Image.Type.Sliced;
            panel.transform.SetParent(_displayLocation, false);

            //Create the label

            var text = new GameObject("ObjectLabel");
            text.AddComponent<CanvasRenderer>();
            text.transform.SetParent(panel.transform, false);
            Text txt = text.AddComponent<Text>();
            txt.font = _font;
            txt.color = color;
            txt.fontSize = 40;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;

            RectTransform rt2 = text.GetComponent<RectTransform>();
            rt2.offsetMin = new Vector2(20, rt2.offsetMin.y);
            rt2.offsetMax = new Vector2(0, rt2.offsetMax.y);
            rt2.offsetMin = new Vector2(rt2.offsetMin.x, 0);
            rt2.offsetMax = new Vector2(rt2.offsetMax.x, 30);
            rt2.anchorMin = new Vector2(0, 0);
            rt2.anchorMax = new Vector2(1, 1);

            _boxPool.Add(panel);
            return panel;
        }

        private void ClearAnnotations()
        {
            foreach (var box in _boxPool)
            {
                box.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            centersToCorners?.Dispose();
            _worker?.Dispose();
        }
    }
}
