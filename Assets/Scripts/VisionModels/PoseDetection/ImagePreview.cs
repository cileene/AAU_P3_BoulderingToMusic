using UnityEngine;

namespace VisionModels.PoseDetection
{
    //TODO: We should be using a rawimage in ui NOT a quad in 3D space for image preview
    public class ImagePreview : MonoBehaviour 
    {
        public GameObject imageQuad;

        public void SetTexture(Texture texture)
        { 
            imageQuad.GetComponent<MeshRenderer>().material.mainTexture = texture; 
            var aspectRatio = texture.width / (float)texture.height; 
            imageQuad.transform.localScale = new Vector3(aspectRatio, 1f, 1f);
        }
    }
}
