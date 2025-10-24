using Unity.InferenceEngine;
using UnityEngine;

namespace YoloSegmentation
{
    public class IEModelConverter : MonoBehaviour
    {
        public ModelAsset _onnxModel;
        [SerializeField, Range(0, 1)] private float _iouThreshold = 0.6f;
        [SerializeField, Range(0, 1)] private float _scoreThreshold = 0.23f;
        [SerializeField] private string filepath = "Assets/Resources/Model/yolo11n-seg-sentis.sentis";
        [SerializeField] private int classCount = 11;
    }
}