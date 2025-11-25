using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;

namespace Configs
{
    public class PersonDetectorConfig
    {
        public ModelAsset Model;
        public TextAsset Classes;
        public RawImage RawImage;
        public Font Font;
        public Texture2D BorderTexture;
    }
}