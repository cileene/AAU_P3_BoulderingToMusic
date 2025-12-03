using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;
using VisionModels.ModelRunners;

namespace Configs
{
    public class HandholdsDetectorConfig
    {
        public ModelAsset Model;
        public TextAsset Classes;
        public HandholdsDetector.ProblemColor ProblemColor;
        public RawImage RawImage;
        public Font Font;
        public Texture2D BorderTexture;
        public int KeepHandholdsFrames;
        public bool ContinuousHandholdDetection;
    }
}