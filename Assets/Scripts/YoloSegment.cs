// YoloSegment.cs
// Minimal YOLOv11-seg runner for Unity Sentis that overlays alpha masks.
// Assumes a 640x640 input and nm=32 prototype channels (Ultralytics default).
// Requires: com.unity.sentis (UIE). Tested with Sentis 2.x API shapes.
// Hook up in Inspector: modelAsset (YOLOv11n-seg .onnx), sourceTexture (camera RT),
// displayImage (RawImage), maskImage (RawImage stacked above display).
// If your model uses a different input size or nm, change the serialized fields.

using UnityEngine;
using UnityEngine.UI;
using Unity.InferenceEngine;

public class YoloSegment : MonoBehaviour
{
    [Header("Model")]
    [SerializeField] private ModelAsset modelAsset;
    [SerializeField] private BackendType backend = BackendType.GPUCompute; // GPUCompute or CPU
    [Tooltip("Input side length. 640 for most YOLOv8/YOLOv11 exports.")]
    [SerializeField] private int inputSize = 640;
    [Tooltip("Number of classes in the model (nc).")]
    [SerializeField] private int numClasses = 1; // set this to your dataset
    [Tooltip("Number of mask coefficients (nm). Ultralytics default = 32.")]
    [SerializeField] private int numMaskCoeffs = 32;

    [Header("Proto head (usually input/4)")]
    [SerializeField] private int protoWidth = 160;
    [SerializeField] private int protoHeight = 160;
    [SerializeField] private int stride = 4; // inputSize / protoWidth

    [Header("Thresholds")]
    [Range(0, 1)] public float scoreThreshold = 0.25f;
    [Range(0, 1)] public float maskThreshold = 0.5f;
    [Range(0, 1)] public float iouThreshold = 0.5f;
    [Range(0, 1)] public float maskAlpha = 0.35f;
    [Tooltip("Max detections kept after NMS")]
    public int topK = 100;

    [Header("IO")]
    [SerializeField] private Texture sourceTexture;      // Your camera or game RT
    [SerializeField] private RawImage displayImage;      // Shows the RGB input
    [SerializeField] private RawImage maskImage;         // Shows the alpha mask overlay

    // Runtime
    private Model _model;
    private FunctionalGraph _graph;
    private Worker _worker;
    private Tensor<float> _centerToCorner; // 4x4 mapping
    private Texture2D _maskTexture;
    private Color32[] _maskPixels;
    private bool _initialized;

    // Reusable CPU buffers
    private float[] _protoFlat; // nm * ph * pw

    // Utility: build constant matrix for cxcywh -> x0y0x1y1 (corners)
    private static Tensor<float> BuildCenterToCorner()
    {
        // 4x4:
        // [ 1 0 1 0
        //   0 1 0 1
        //  -0.5 0 0.5 0
        //   0 -0.5 0 0.5 ]
        var data = new float[]
        {
            1, 0, 1, 0,
            0, 1, 0, 1,
            -0.5f, 0, 0.5f, 0,
            0, -0.5f, 0, 0.5f
        };
        var t = new Tensor<float>(new TensorShape(4, 4));
        t.Upload(data);
        return t;
    }

    private void OnEnable()
    {
        TryInit();
    }

    private void OnDisable()
    {
        _worker?.Dispose();
        _centerToCorner?.Dispose();
        _maskTexture = null;
        _graph = null;
        _model = null;
        _initialized = false;
    }

    private void TryInit()
    {
        if (_initialized) return;
        if (modelAsset == null || sourceTexture == null || displayImage == null || maskImage == null) return;

        _model = ModelLoader.Load(modelAsset);
        _graph = new FunctionalGraph();

        // Graph I/O
        var inputs = _graph.AddInputs(_model);
        var outputs = Functional.Forward(_model, inputs);
        // Expecting: outputs[0] = det tensor (1, 4+nc+nm, S)  S~8400 for 640 input
        //            outputs[1] = proto tensor (1, nm, ph, pw)
        var dets = outputs[0];
        var proto = outputs[1];

        // Slice detection head
        // (S,4)
        var boxCxCyWh = dets[0, 0..4, ..].Transpose(0, 1);
        // (nc,S)
        var classLogits = dets[0, 4..(4 + numClasses), ..];
        // (S,nm)
        var maskCoeffs = dets[0, (4 + numClasses)..(4 + numClasses + numMaskCoeffs), ..].Transpose(0, 1);

        // Score and class per anchor
        var scores = Functional.ReduceMax(classLogits, 0); // (S)
        var classIDs = Functional.ArgMax(classLogits, 0);  // (S)

        // Convert to corners (S,4)
        _centerToCorner = BuildCenterToCorner();
        var corners = Functional.MatMul(boxCxCyWh, Functional.Constant(_centerToCorner));

        // NMS
        var keep = Functional.NMS(corners, scores, iouThreshold, scoreThreshold);

        // Gather kept detections
        var coordsK = Functional.IndexSelect(boxCxCyWh, 0, keep);    // (K,4)
        var labelsK = Functional.IndexSelect(classIDs, 0, keep);     // (K)
        var coeffsK = Functional.IndexSelect(maskCoeffs, 0, keep);   // (K,nm)

        // Compile a worker with the tensors we will read + proto
        _worker = new Worker(_graph.Compile(coordsK, labelsK, coeffsK, proto), backend);

        // UI setup
        displayImage.texture = sourceTexture;
        _maskTexture = new Texture2D(inputSize, inputSize, TextureFormat.Alpha8, false);
        _maskPixels = new Color32[inputSize * inputSize];
        maskImage.texture = _maskTexture;

        _protoFlat = new float[numMaskCoeffs * protoHeight * protoWidth];

        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized) TryInit();
        if (!_initialized) return;

        RunOnce();
    }

    private void RunOnce()
    {
        // 1) Preprocess input to TensorFloat [1,3,H,W], 0..1
        using var input = TextureConverter.ToTensor(sourceTexture, inputSize, inputSize, 3);


        // 2) Execute
        _worker.Schedule(input);

        // 3) Read outputs
        using var coords = _worker.PeekOutput(0) as Tensor<float>; // (K,4) cxcywh
        using var labels = _worker.PeekOutput(1) as Tensor<int>;   // (K)
        using var coeffs = _worker.PeekOutput(2) as Tensor<float>; // (K,nm)
        using var proto  = _worker.PeekOutput(3) as Tensor<float>; // (1,nm,ph,pw)


        // Flatten proto -> (nm, ph*pw)
        int nm = numMaskCoeffs;
        int ph = protoHeight;
        int pw = protoWidth;
        int pixelsProto = ph * pw;
        {
            int idx = 0;
            for (int m = 0; m < nm; m++)
            {
                for (int y = 0; y < ph; y++)
                {
                    for (int x = 0; x < pw; x++)
                    {
                        _protoFlat[idx++] = proto[0, m, y, x];
                    }
                }
            }
        }

        // Clear previous mask buffer
        for (int i = 0; i < _maskPixels.Length; i++) _maskPixels[i] = new Color32(0, 0, 0, 0);

        int K = coords.shape[0];
        byte aByte = (byte)(maskAlpha * 255);

        // For each kept detection build a mask and write into alpha texture
        for (int k = 0; k < K; k++)
        {
            float cx = coords[k, 0];
            float cy = coords[k, 1];
            float w  = coords[k, 2];
            float h  = coords[k, 3];

            float x0 = cx - 0.5f * w;
            float y0 = cy - 0.5f * h;
            float x1 = cx + 0.5f * w;
            float y1 = cy + 0.5f * h;

            // 1) mask160 = sigmoid( proto^T * coeffs )
            float[] maskSmall = new float[pixelsProto];

            for (int i = 0; i < pixelsProto; i++)
            {
                float s = 0f;
                for (int m = 0; m < nm; m++)
                {
                    s += _protoFlat[m * pixelsProto + i] * coeffs[k, m];
                }
                maskSmall[i] = 1f / (1f + Mathf.Exp(-s));
            }

            // 2) Upsample and place into 640x640, clipped to the box
            int xmin = Mathf.Clamp(Mathf.FloorToInt(x0), 0, inputSize - 1);
            int ymin = Mathf.Clamp(Mathf.FloorToInt(y0), 0, inputSize - 1);
            int xmax = Mathf.Clamp(Mathf.CeilToInt(x1),  0, inputSize - 1);
            int ymax = Mathf.Clamp(Mathf.CeilToInt(y1),  0, inputSize - 1);

            for (int yy = ymin; yy <= ymax; yy++)
            {
                // Map output pixel to proto space
                float fy = (yy + 0.5f) / stride - 0.5f;
                int yb = Mathf.Clamp(Mathf.FloorToInt(fy), 0, ph - 1);
                int yt = Mathf.Clamp(yb + 1, 0, ph - 1);
                float ty = Mathf.Clamp01(fy - yb);

                for (int xx = xmin; xx <= xmax; xx++)
                {
                    float fx = (xx + 0.5f) / stride - 0.5f;
                    int xl = Mathf.Clamp(Mathf.FloorToInt(fx), 0, pw - 1);
                    int xr = Mathf.Clamp(xl + 1, 0, pw - 1);
                    float tx = Mathf.Clamp01(fx - xl);

                    int i00 = yb * pw + xl;
                    int i01 = yb * pw + xr;
                    int i10 = yt * pw + xl;
                    int i11 = yt * pw + xr;

                    float v0 = Mathf.Lerp(maskSmall[i00], maskSmall[i01], tx);
                    float v1 = Mathf.Lerp(maskSmall[i10], maskSmall[i11], tx);
                    float v  = Mathf.Lerp(v0, v1, ty);

                    if (v >= maskThreshold)
                    {
                        int di = yy * inputSize + xx;
                        // accumulate alpha; keep max on overlap
                        if (_maskPixels[di].a < aByte) _maskPixels[di].a = aByte;
                    }
                }
            }
        }

        // 4) Upload alpha texture
        _maskTexture.SetPixelData(_maskPixels, 0);
        _maskTexture.Apply(false, false);
    }

    // Optional helper to set textures from script
    public void SetSource(Texture tex)
    {
        sourceTexture = tex;
        if (displayImage) displayImage.texture = tex;
        _initialized = false; // re-init next frame
    }
}