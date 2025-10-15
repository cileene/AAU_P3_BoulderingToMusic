using UnityEngine;
using Unity.InferenceEngine;

public class ModelContainer : MonoBehaviour
{
    [Tooltip("ONNX model for pose-detection")]
    public ModelAsset modelAsset;
    
    Model runtimeModel;
    Worker worker;
    public float[] results;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    void OnDisable()
    {
        // Tell the GPU we're finished with the memory the engine used
        worker.Dispose();
    }
}
