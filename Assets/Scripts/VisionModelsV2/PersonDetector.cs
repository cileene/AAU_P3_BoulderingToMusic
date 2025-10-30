using System.Collections.Generic;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;

// nick, messy but readable

//TODO: Clean up the img rotation logic

//TODO: In prototype make it select the widest angle non-selfie cam and remove mirroring


namespace VisionModelsV2
{
    public class PersonDetector : MonoBehaviour
    {
        // nick stuff
        public Vector3 boxPosition;
    
        // yolo stuff
        private ModelAsset modelAsset;
        private TextAsset classesAsset;
        private RawImage displayImage;
        private Texture2D borderTexture;
        private Font font;
        
        private bool useWebcam;
        private string webcamDeviceName;
        private string videoFilename;
        
        private bool preferFrontCamera;
        
        private bool mirrorHorizontally; // set true for selfie view

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
        private WebCamTexture _cam;

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
            AppEvents.ConfigurePersonDetector += OnConfigurePersonDetector;
        }
        
        private void OnDisable()
        {
            AppEvents.WebcamReady -= OnWebcamReady;
            AppEvents.VideoReady -= OnVideoReady;
            AppEvents.ConfigurePersonDetector -= OnConfigurePersonDetector;
        }
        
        private void OnVideoReady(Texture video)
        {
            _video = video;
            useWebcam = false;
        }
        
        private void OnWebcamReady(WebCamTexture cam)
        {
            _cam = cam;
            useWebcam = true;
        }
        
        private void OnConfigurePersonDetector(ModelAsset model, TextAsset classes, RawImage display, Font fnt, Texture2D borderTex)
        {
            modelAsset = model;
            classesAsset = classes;
            displayImage = display;
            font = fnt;
            borderTexture = borderTex;
            StartModel();
        }

        private void StartModel()
        {
            _labels = classesAsset.text.Split('\n');
            LoadModel();

            _targetRT = new RenderTexture(ImageWidth, ImageHeight, 0);
            _displayLocation = displayImage.transform;

            _borderSprite = Sprite.Create(
                borderTexture,
                new Rect(0, 0, borderTexture.width, borderTexture.height),
                new Vector2(borderTexture.width / 2f, borderTexture.height / 2f)
            );
        
            Debug.Log($"{this} is ready");
        }

        private void LoadModel() // here be dragons and math
        {
            var model1 = ModelLoader.Load(modelAsset);

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

            float displayWidth = displayImage.rectTransform.rect.width;
            float displayHeight = displayImage.rectTransform.rect.height;
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

            // nick test
            if (hasPerson != _lastHasPerson)
            {
                if (hasPerson)
                {
                    AppEvents.RaisePersonDetected(_personBox);
                }

                else
                {
                    AppEvents.RaisePersonLost();
                }
                _lastHasPerson = hasPerson;
            }
        }

        private bool HandleInput()
        {
            Texture sourceTex = null;
            int srcW = 0, srcH = 0;

            if (useWebcam && _cam != null && _cam.width > 16 && _cam.height > 16)
            {
                sourceTex = _cam;
                srcW = _cam.width; srcH = _cam.height;
            }
            else if (!useWebcam && _video && _video)
            {
                sourceTex = _video;
                srcW = (int)_video.width;
                srcH = (int)_video.height;
            }
            else
            {
                return true;
            }
            
            // Correct orientation and mirroring for webcam/video before inference
            int rot = 0;
            bool vflip = false;
            if (useWebcam && _cam != null)
            {
                rot = _cam.videoRotationAngle;                 // 0, 90, 180, 270 from platform
                vflip = _cam.videoVerticallyMirrored;          // front cameras often true
            }

            // Rotate the UI container so the feed and overlays stay aligned
            var eul = displayImage.rectTransform.localEulerAngles;
            displayImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -rot);

            // Letterbox to 640x640 while preserving aspect, then apply requested mirror and platform vertical flip
            float aspect = srcW * 1f / Mathf.Max(1, srcH);
            float sx = (mirrorHorizontally ? -1f : 1f) / aspect; // horizontal mirror for selfie view
            float sy = vflip ? -1f : 1f;                         // platform vertical flip
            Vector2 scale = new Vector2(sx, sy);
            Vector2 offset = new Vector2(mirrorHorizontally ? 1f : 0f, vflip ? 1f : 0f);

            Graphics.Blit(sourceTex, _targetRT, scale, offset);
            displayImage.texture = _targetRT;
            return false;
        }

        private void DrawBox(BoundingBox box, int id, float fontSize)
        {
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

            panel.transform.localPosition = new Vector3(box.CenterX, -box.CenterY);
            boxPosition = panel.transform.localPosition; // nick taking this for later use

            RectTransform rt = panel.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(box.Width, box.Height);

            var label = panel.GetComponentInChildren<Text>();
            label.text = box.Label;
            label.fontSize = (int)fontSize;
        }

        private GameObject CreateNewBox(Color color)
        {
            var panel = new GameObject("ObjectBox");
            panel.AddComponent<CanvasRenderer>();
            Image img = panel.AddComponent<Image>();
            img.color = color;
            img.sprite = _borderSprite;
            img.type = Image.Type.Sliced;
            panel.transform.SetParent(_displayLocation, false);

            var text = new GameObject("ObjectLabel");
            text.AddComponent<CanvasRenderer>();
            text.transform.SetParent(panel.transform, false);
            Text txt = text.AddComponent<Text>();
            txt.font = font;
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
            foreach (var box in _boxPool) box.SetActive(false);
        }

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