using UnityEngine;
using System.Collections;

public class ListWebcams : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log(devices[i].name);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
