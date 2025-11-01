using System.IO;
using UnityEngine;

namespace VisionModelsV2.Input
{
    public class UseStill : MonoBehaviour
    {
        private void OnEnable() => AppEvents.RequestUseStill += OnRequestUseStill;

        private void OnDisable() => AppEvents.RequestUseStill -= OnRequestUseStill;

        private void OnRequestUseStill(string fileName)
        {
            string filePath = Path.Combine(Application.streamingAssetsPath, fileName);
            
            if (!File.Exists(filePath))
            {
                Debug.LogError($"Failed to load still image from StreamingAssets/{fileName}");
                return;
            }

            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2);
            
            if (!texture.LoadImage(fileData))
            {
                Debug.LogError($"Failed to decode image data from {fileName}");
                Destroy(texture);
                return;
            }

            AppEvents.RaiseStillReady(texture);
        }
    }
}