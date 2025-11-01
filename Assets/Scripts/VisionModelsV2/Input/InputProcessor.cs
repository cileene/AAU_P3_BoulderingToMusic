using UnityEngine;
using UnityEngine.UI;

namespace VisionModelsV2.Input
{
    public enum InputMode
    {
        Webcam,
        Video,
        Still
    }

    public static class InputProcessor
    {
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

            int rot = 0;
            bool vflip = false;
            if (mode == InputMode.Webcam && cam != null)
            {
                rot = cam.videoRotationAngle;
                vflip = cam.videoVerticallyMirrored;
            }

            displayImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -rot);

            float aspect = srcW * 1f / Mathf.Max(1, srcH);
            float sx = (mirrorHorizontally ? -1f : 1f) / aspect;
            float sy = vflip ? -1f : 1f;
            Vector2 scale = new Vector2(sx, sy);
            Vector2 offset = new Vector2(mirrorHorizontally ? 1f : 0f, vflip ? 1f : 0f);

            Graphics.Blit(sourceTex, targetRT, scale, offset);
            displayImage.texture = targetRT;
            return false; // Valid input processed
        }
    }
}
