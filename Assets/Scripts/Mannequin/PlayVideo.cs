using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class PlayVideo : MonoBehaviour
{
    private RawImage image;
    private VideoClip RecordedVideo { get; set; }
    private VideoPlayer videoPlayer;
    private GameObject videoObj;
    private string videoName {set; get;}

    public void OnEnable()
    {
        videoPlayer = gameObject.GetComponent<VideoPlayer>();
        videoPlayer.url = videoName;
    }

    public void OnDisable()
    {
        videoPlayer.Stop();
        enabled = false;
    }

    public void Play(VideoClip video)
    {
        this.RecordedVideo = video;
        videoPlayer.clip = video;
        videoPlayer.Play();
    }
}
