using System.Collections.Generic;
using System.IO;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// nick, messy but readable

//TODO: Clean up the img rotation logic

//TODO: In prototype make it select the widest angle non-selfie cam and remove mirroring


namespace YoloDetection
{
    public class RunYOLO : MonoBehaviour
    {
        // nick stuff
        public Vector3 boxPosition;
    
        // yolo stuff
        [Tooltip("Drag a YOLO model .onnx file here")]
        [SerializeField] private ModelAsset modelAsset;

        [Tooltip("Drag the classes.txt here")]
        [SerializeField] private TextAsset classesAsset;

        [Tooltip("Create a Raw Image in the scene and link it here")]
        [SerializeField] private RawImage displayImage;

        [Tooltip("Drag a border box texture here")]
        [SerializeField] private Texture2D borderTexture;

        [Tooltip("Select an appropriate font for the labels")]
        [SerializeField] private Font font;

        [Header("Input")]
        [SerializeField] private bool useWebcam = true;
        [Tooltip("Use empty to pick default camera")]
        [SerializeField] private string webcamDeviceName = "";
        [Tooltip("Video file in Assets/StreamingAssets if not using webcam")]
        [SerializeField] private string videoFilename = "giraffes.mp4";
    
        [Header("Camera selection")]
        [SerializeField] private bool preferFrontCamera = true;

        [Header("Image options")]
        [SerializeField] private bool mirrorHorizontally = true; // set true for selfie view

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
        private VideoPlayer _video;
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
            AppEvents.OnWebcamReady += HandleWebcamReady;
        }
        
        private void OnDisable()
        {
            AppEvents.OnWebcamReady -= HandleWebcamReady;
        }
        
        private void HandleWebcamReady(WebCamTexture cam)
        {
            _cam = cam;
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            //Screen.orientation = ScreenOrientation.Portrait; // this is evil

            _labels = classesAsset.text.Split('\n');
            LoadModel();

            _targetRT = new RenderTexture(ImageWidth, ImageHeight, 0);
            _displayLocation = displayImage.transform;

            SetupInput();

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

        private void SetupInput() //TODO: the non webcam part could/should be removed
        {
            if (useWebcam)
            {
                _cam.Play();
            }
            else
            {
                _video = gameObject.AddComponent<VideoPlayer>();
                _video.renderMode = VideoRenderMode.APIOnly;
                _video.source = VideoSource.Url;
                _video.url = Path.Join(Application.streamingAssetsPath, videoFilename);
                _video.isLooping = true;
                _video.Play();
            }
        }

        private void Update()
        {
            ExecuteML();

            if (Input.GetKeyDown(KeyCode.Escape))
                Application.Quit();
        
            //Debug.Log(boxPosition);
        }

        private void ExecuteML() // Mighty Messy Method (should be split up)
        {
            ClearAnnotations();

            Texture sourceTex = null;
            int srcW = 0, srcH = 0;

            if (useWebcam && _cam != null && _cam.width > 16 && _cam.height > 16)
            {
                sourceTex = _cam;
                srcW = _cam.width; srcH = _cam.height;
            }
            else if (!useWebcam && _video && _video.texture)
            {
                sourceTex = _video.texture;
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