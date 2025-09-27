using System;
using System.Collections.Generic;
using System.IO;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

public class RunYOLO : MonoBehaviour
{
    [Tooltip("Drag a YOLO model .onnx file here")]
    public ModelAsset modelAsset;

    [Tooltip("Drag the classes.txt here")]
    public TextAsset classesAsset;

    [Tooltip("Create a Raw Image in the scene and link it here")]
    public RawImage displayImage;

    [Tooltip("Drag a border box texture here")]
    public Texture2D borderTexture;

    [Tooltip("Select an appropriate font for the labels")]
    public Font font;

    [Header("Input")]
    public bool useWebcam = true;
    [Tooltip("Use empty to pick default camera")]
    public string webcamDeviceName = "";
    [Tooltip("Video file in Assets/StreamingAssets if not using webcam")]
    public string videoFilename = "giraffes.mp4";
    
    [Header("Camera selection")]
    public bool preferFrontCamera = true;

    [Header("Image options")]
    public bool mirrorHorizontally = true; // set true for selfie view
    
    [Header("Fun Debug")]
    [Tooltip("When we see a person")]
    [SerializeField] private TMP_Text _hiText;
    [Tooltip("When we dont see a person")]
    [SerializeField] private TMP_Text _whereText;

    const BackendType backend = BackendType.GPUCompute;

    private Transform displayLocation;
    private Worker worker;
    private string[] labels;
    private RenderTexture targetRT;
    private Sprite borderSprite;

    // Model input size
    private const int imageWidth = 640;
    private const int imageHeight = 640;

    // Inputs
    private VideoPlayer video;
    private WebCamTexture cam;

    List<GameObject> boxPool = new();

    [Tooltip("Intersection over union threshold used for non-maximum suppression")]
    [SerializeField, Range(0, 1)]
    float iouThreshold = 0.5f;

    [Tooltip("Confidence score threshold used for non-maximum suppression")]
    [SerializeField, Range(0, 1)]
    float scoreThreshold = 0.5f;

    Tensor<float> centersToCorners;

    // Simple state log
    bool lastHasPerson;

    public struct BoundingBox
    {
        public float centerX;
        public float centerY;
        public float width;
        public float height;
        public string label;
    }

    void Start()
    {
        Application.targetFrameRate = 60;
        //Screen.orientation = ScreenOrientation.Portrait;

        labels = classesAsset.text.Split('\n');
        LoadModel();

        targetRT = new RenderTexture(imageWidth, imageHeight, 0);
        displayLocation = displayImage.transform;

        SetupInput();

        borderSprite = Sprite.Create(
            borderTexture,
            new Rect(0, 0, borderTexture.width, borderTexture.height),
            new Vector2(borderTexture.width / 2f, borderTexture.height / 2f)
        );
    }

    void LoadModel()
    {
        var model1 = ModelLoader.Load(modelAsset);

        centersToCorners = new Tensor<float>(new TensorShape(4, 4),
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
        var boxCorners = Functional.MatMul(boxCoords, Functional.Constant(centersToCorners)); // (8400,4)
        var indices    = Functional.NMS(boxCorners, scores, iouThreshold, scoreThreshold);// (N)
        var coords     = Functional.IndexSelect(boxCoords, 0, indices);                   // (N,4)
        var labelIDs   = Functional.IndexSelect(classIDs, 0, indices);                    // (N)

        worker = new Worker(graph.Compile(coords, labelIDs), backend);
    }

    void SetupInput()
    {
        if (useWebcam)
        {
            WebCamDevice? chosen = null;

            // explicit name wins
            if (!string.IsNullOrEmpty(webcamDeviceName))
            {
                foreach (var d in WebCamTexture.devices)
                    if (d.name == webcamDeviceName) { chosen = d; break; }
            }

            // otherwise prefer front camera on mobile
            if (!chosen.HasValue && preferFrontCamera)
            {
                foreach (var d in WebCamTexture.devices)
                    if (d.isFrontFacing) { chosen = d; break; }
            }

            // fallback: first available
            if (!chosen.HasValue && WebCamTexture.devices.Length > 0)
                chosen = WebCamTexture.devices[0];

            cam = chosen.HasValue
                ? new WebCamTexture(chosen.Value.name, 1280, 720, 30)
                : new WebCamTexture(1280, 720, 30);

            cam.Play();
        }
        else
        {
            video = gameObject.AddComponent<VideoPlayer>();
            video.renderMode = VideoRenderMode.APIOnly;
            video.source = VideoSource.Url;
            video.url = Path.Join(Application.streamingAssetsPath, videoFilename);
            video.isLooping = true;
            video.Play();
        }
    }

    void Update()
    {
        ExecuteML();

        if (Input.GetKeyDown(KeyCode.Escape))
            Application.Quit();
    }

    public void ExecuteML()
    {
        ClearAnnotations();

        Texture sourceTex = null;
        int srcW = 0, srcH = 0;

        if (useWebcam && cam != null && cam.width > 16 && cam.height > 16)
        {
            sourceTex = cam;
            srcW = cam.width; srcH = cam.height;
        }
        else if (!useWebcam && video && video.texture)
        {
            sourceTex = video.texture;
            srcW = (int)video.width;
            srcH = (int)video.height;
        }
        else
        {
            return;
        }

        
        //TODO: This is flipping
        
        // Letterbox to 640x640 while preserving aspect
        // Mirror horizontally by making the X scale negative
        float aspect = srcW * 1f / Mathf.Max(1, srcH);
        var scale  = mirrorHorizontally ? new Vector2(-1f / aspect, 1f) : new Vector2(1f / aspect, 1f);
        var offset = mirrorHorizontally ? new Vector2(1f, 0f) : Vector2.zero;

        Graphics.Blit(sourceTex, targetRT, scale, offset);
        displayImage.texture = targetRT;

        using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, imageHeight, imageWidth));
        TextureConverter.ToTensor(targetRT, inputTensor, default);
        worker.Schedule(inputTensor);

        using var coords = (worker.PeekOutput("output_0") as Tensor<float>).ReadbackAndClone(); // (N,4) centers
        using var labelIDs = (worker.PeekOutput("output_1") as Tensor<int>).ReadbackAndClone(); // (N)

        float displayWidth = displayImage.rectTransform.rect.width;
        float displayHeight = displayImage.rectTransform.rect.height;
        float scaleX = displayWidth / imageWidth;
        float scaleY = displayHeight / imageHeight;

        bool hasPerson = false;

        int boxesFound = coords.shape[0];
        for (int n = 0; n < Mathf.Min(boxesFound, 200); n++)
        {
            int cls = labelIDs[n];
            var box = new BoundingBox
            {
                centerX = coords[n, 0] * scaleX - displayWidth / 2f,
                centerY = coords[n, 1] * scaleY - displayHeight / 2f,
                width   = coords[n, 2] * scaleX,
                height  = coords[n, 3] * scaleY,
                label   = labels[cls],
            };

            if (cls == 0) hasPerson = true; // COCO class 0 = person
            DrawBox(box, n, displayHeight * 0.05f);
        }

        if (hasPerson != lastHasPerson)
        {
            if (hasPerson)
            {
                Debug.Log("Person detected");
                _hiText.enabled = true;
                _whereText.enabled = false;
            }

            else
            {
                Debug.Log("No person");
                _hiText.enabled = false;
                _whereText.enabled = true;
            }
            lastHasPerson = hasPerson;
        }
    }

    public void DrawBox(BoundingBox box, int id, float fontSize)
    {
        GameObject panel;
        if (id < boxPool.Count)
        {
            panel = boxPool[id];
            panel.SetActive(true);
        }
        else
        {
            panel = CreateNewBox(Color.yellow);
        }

        panel.transform.localPosition = new Vector3(box.centerX, -box.centerY);

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(box.width, box.height);

        var label = panel.GetComponentInChildren<Text>();
        label.text = box.label;
        label.fontSize = (int)fontSize;
    }

    public GameObject CreateNewBox(Color color)
    {
        var panel = new GameObject("ObjectBox");
        panel.AddComponent<CanvasRenderer>();
        Image img = panel.AddComponent<Image>();
        img.color = color;
        img.sprite = borderSprite;
        img.type = Image.Type.Sliced;
        panel.transform.SetParent(displayLocation, false);

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

        boxPool.Add(panel);
        return panel;
    }

    public void ClearAnnotations()
    {
        foreach (var box in boxPool) box.SetActive(false);
    }

    void OnDestroy()
    {
        centersToCorners?.Dispose();
        worker?.Dispose();

        if (cam != null)
        {
            if (cam.isPlaying) cam.Stop();
            cam = null;
        }
    }
}