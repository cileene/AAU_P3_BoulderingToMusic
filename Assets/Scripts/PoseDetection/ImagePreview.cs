using System;
using UnityEngine;

namespace PoseDetection
{
    public class ImagePreview : MonoBehaviour
    {
        public GameObject imageQuad;

        void Awake()
        {
            imageQuad = this.gameObject;
        }
        public void SetTexture(Texture texture)
        {
            imageQuad.GetComponent<MeshRenderer>().material.mainTexture = texture;
            var aspectRatio = texture.width / (float)texture.height;
            imageQuad.transform.localScale = new Vector3(aspectRatio, 1f, 1f);
        }

        private void OnDisable()
        {
            imageQuad = null;
        }
    }
}
