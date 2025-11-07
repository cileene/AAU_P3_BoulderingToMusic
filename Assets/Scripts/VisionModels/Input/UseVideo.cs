using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace VisionModels.Input
{
    public class UseVideo : MonoBehaviour
    {
        private void OnEnable() => AppEvents.RequestUseVideo += OnRequestUseVideo;

        private void OnDisable() => AppEvents.RequestUseVideo -= OnRequestUseVideo;

        private void OnRequestUseVideo(string fileName)
        {
            var existingPlayer = GetComponent<VideoPlayer>();
            if (existingPlayer != null)
            {
                existingPlayer.prepareCompleted -= OnVideoPrepared;
                Destroy(existingPlayer);
            }
            var video = gameObject.AddComponent<VideoPlayer>();  // Add to same GameObject as UseVideo
            video.renderMode = VideoRenderMode.APIOnly;
            video.source = VideoSource.Url;
            video.url = Path.Join(Application.streamingAssetsPath, fileName);
            video.isLooping = true;
            video.prepareCompleted += OnVideoPrepared;
            video.Prepare();
        }
        
        private void OnVideoPrepared(VideoPlayer source)
        {
            source.prepareCompleted -= OnVideoPrepared;
            source.Play();
            AppEvents.RaiseVideoReady(source);
        }
    }
}