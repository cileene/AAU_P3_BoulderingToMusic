using UnityEngine;
using UnityEngine.UI;

namespace VisionModels.Input
{
    public enum InputMode
    {
        Webcam,
        Video,
        Still
    }

    public static class InputProcessor
    {
        /// <summary>
        /// Processes input from various sources and renders it to a target RenderTexture.
        /// Uses letterboxing to preserve aspect ratio - adds black bars as needed instead
        /// of stretching/squashing the image.
        /// </summary>
        public static bool ProcessInput(
            InputMode mode,
            WebCamTexture cam,
            Texture video,
            Texture2D still,
            RenderTexture targetRT,
            RawImage displayImage,
            bool mirrorHorizontally)
        {
            Texture sourceTex = null;
            int srcW = 0, srcH = 0;

            // Determine source texture based on input mode
            switch (mode)
            {
                case InputMode.Webcam:
                    if (cam != null && cam.width > 16 && cam.height > 16)
                    {
                        sourceTex = cam;
                        srcW = cam.width;
                        srcH = cam.height;
                    }
                    break;

                case InputMode.Video:
                    if (video != null)
                    {
                        sourceTex = video;
                        srcW = video.width;
                        srcH = video.height;
                    }
                    break;

                case InputMode.Still:
                    if (still != null)
                    {
                        sourceTex = still;
                        srcW = still.width;
                        srcH = still.height;
                    }
                    break;
            }

            if (sourceTex == null)
            {
                return true; // No valid input
            }

            // Handle webcam rotation and mirroring
            int rot = 0;
            bool vflip = false;
            if (mode == InputMode.Webcam && cam != null)
            {
                rot = cam.videoRotationAngle;
                vflip = cam.videoVerticallyMirrored;
            }

            displayImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -rot);

            // Calculate aspect ratios
            float srcAspect = srcW * 1f / Mathf.Max(1, srcH);
            float targetAspect = targetRT.width * 1f / Mathf.Max(1, targetRT.height);

            float scaleX, scaleY;
            if (srcAspect > targetAspect)
            {
                // Source is wider - fit to width, letterbox top/bottom
                scaleX = 1f;
                scaleY = targetAspect / srcAspect;
            }
            else
            {
                // Source is taller - fit to height, letterbox left/right
                scaleX = srcAspect / targetAspect;
                scaleY = 1f;
            }

            // Apply mirroring/flipping to the calculated scale
            float sx = (mirrorHorizontally ? -scaleX : scaleX);
            float sy = (vflip ? -scaleY : scaleY);
            Vector2 scale = new Vector2(sx, sy);

            // Center the image and account for mirroring/flipping
            float offsetX = (1f - Mathf.Abs(scaleX)) * 0.5f + (mirrorHorizontally ? scaleX : 0f);
            float offsetY = (1f - Mathf.Abs(scaleY)) * 0.5f + (vflip ? scaleY : 0f);
            Vector2 offset = new Vector2(offsetX, offsetY);

            // Clear target to black for letterbox bars
            RenderTexture.active = targetRT;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = null;

            // Blit with letterboxing - preserves aspect ratio
            Graphics.Blit(sourceTex, targetRT, scale, offset);
            displayImage.texture = targetRT;
            return false; // Valid input processed
        }
    }
}
