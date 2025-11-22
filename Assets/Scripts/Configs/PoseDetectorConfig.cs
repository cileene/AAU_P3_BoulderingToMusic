using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;

namespace Configs
{
    public class PoseDetectorConfig
    {
        public ModelAsset Model;
        public RawImage RawImage;
        public Texture2D BorderTexture;
    }
}