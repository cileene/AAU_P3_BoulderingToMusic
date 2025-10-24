using System.Collections.Generic;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;

namespace YoloSegmentation
{
    public class IEMasker
    {
        [SerializeField] private Transform _displayLocation;
        private const int YOLO11_MASK_HEIGHT = 160;
        private const int YOLO11_MASK_WIDTH = 160;

        private readonly List<RawImage> _maskImages = new();
        private readonly Dictionary<string, Color> _classColorMap = new()
        {
            { "black", new Color(0f, 0f, 0f, 0.75f) },
            { "blue", new Color(0f, 0f, 1f, 0.75f) },
            { "Gray", new Color(0.5f, 0.5f, 0.5f, 0.75f) },
            { "green", new Color(0f, 1f, 0f, 0.75f) },
            { "orange", new Color(1f, 0.65f, 0f, 0.75f) },
            { "purple", new Color(0.5f, 0f, 0.5f, 0.75f) },
            { "red", new Color(1f, 0f, 0f, 0.75f) },
            { "Turquise", new Color(0.25f, 0.88f, 0.82f, 0.75f) },
            { "volume", new Color(1f, 1f, 1f, 0.75f) }, 
            { "white", new Color(1f, 1f, 1f, 0.75f) },
            { "yellow", new Color(1f, 1f, 0f, 0.75f) }
        };

        private float _confidenceThreshold = 0.5f;

        public IEMasker(Transform displayLocation, float confidenceThreshold)
        {
            _displayLocation = displayLocation;
            _confidenceThreshold = confidenceThreshold;
        }

        public void DrawMask(List<BoundingBox> boundBoxes, Tensor<float> mask, int imageWidth, int imageHeight)
        {
            int numObjects = mask.shape[0];
            if (numObjects <= 0 || mask.shape[1] != YOLO11_MASK_HEIGHT || mask.shape[2] != YOLO11_MASK_WIDTH)
            {
                Debug.LogWarning("No objects found or mask shape is invalid.");
                return;
            }

            Color32[] pixelArray = new Color32[YOLO11_MASK_HEIGHT * YOLO11_MASK_WIDTH];

            for (int i = 0; i < numObjects; i++)
            {
                Texture2D maskTexture = GetTexture(i, imageWidth, imageHeight);
                Color maskColor = GetColorForClass(boundBoxes[i].Label);
            
                for (int y = 0; y < YOLO11_MASK_HEIGHT; y++)
                {
                    for (int x = 0; x < YOLO11_MASK_WIDTH; x++)
                    {
                        float value = mask[i, y, x];
                        int posX = x;
                        int posY = YOLO11_MASK_HEIGHT - y - 1;

                        if (value > _confidenceThreshold && PixelInBoundingBox(boundBoxes[i], posX, posY, imageWidth, imageHeight))
                        {
                            pixelArray[posY * YOLO11_MASK_WIDTH + posX] = maskColor;
                        }
                        else
                        {
                            pixelArray[posY * YOLO11_MASK_WIDTH + posX] = Color.clear;
                        }
                    }
                }
                maskTexture.SetPixels32(pixelArray);
                maskTexture.Apply();
            }
            ClearMasks(numObjects);
        }

        private Color GetColorForClass(string className)
        {
            if (_classColorMap.TryGetValue(className, out Color color))
            {
                return color;
            }
        
            // Fallback to a random color if class name not found
            Debug.LogWarning($"Class '{className}' not found in color map. Using random color.");
            return new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, 0.75f);
        }

        private void ClearMasks(int lastBoxCount)
        {
            for (int i = lastBoxCount; i < _maskImages.Count; i++)
            {
                _maskImages[i].gameObject.SetActive(false);
            }
        }

        private bool PixelInBoundingBox(BoundingBox box, int x, int y, int imageWidth, int imageHeight)
        {
            float xScaleFactor = YOLO11_MASK_WIDTH / (float)imageWidth;
            float yScaleFactor = YOLO11_MASK_HEIGHT / (float)imageHeight;

            float centerX = (box.CenterX * xScaleFactor) + (YOLO11_MASK_WIDTH / 2);
            float centerY = (YOLO11_MASK_HEIGHT / 2) - (box.CenterY * yScaleFactor);

            float halfWidth = box.Width * xScaleFactor / 2;
            float halfHeight = box.Height * yScaleFactor / 2;

            return x >= (centerX - halfWidth) &&
                   x <= (centerX + halfWidth) &&
                   y >= (centerY - halfHeight) &&
                   y <= (centerY + halfHeight);
        }

        private Texture2D GetTexture(int segmentationId, int imageWidth, int imageHeight)
        {
            RawImage maskImage;
            if (segmentationId < _maskImages.Count)
            {
                maskImage = _maskImages[segmentationId];
            }
            else
            {
                maskImage = CreateRawImage(segmentationId, imageWidth, imageHeight);
                _maskImages.Add(maskImage);
            }
            maskImage.gameObject.SetActive(true);

            RectTransform rectTransform = maskImage.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(imageWidth, imageHeight);
            rectTransform.localPosition = Vector3.zero;

            return maskImage.texture as Texture2D;
        }

        private RawImage CreateRawImage(int segmentationId, int imageWidth, int imageHeight)
        {
            GameObject maskObject = new GameObject("MaskImage " + segmentationId);
            maskObject.transform.SetParent(_displayLocation, false);

            RawImage rawImage = maskObject.AddComponent<RawImage>();
            rawImage.color = Color.white;
            rawImage.texture = CreateTexture();

            return rawImage;
        }

        private Texture2D CreateTexture()
        {
            return new(YOLO11_MASK_WIDTH, YOLO11_MASK_HEIGHT, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }
    }
}
