using UnityEngine;

namespace PoseDetection
{
    public class Hold : MonoBehaviour
    {
        private Vector3[] _vertices;
        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;

        public Hold(int verticesCount, Vector3[] vertices)
        {
            _vertices = vertices;
            _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _meshFilter = gameObject.AddComponent<MeshFilter>();
            _meshRenderer.material = setMaterial();
            _meshFilter.mesh = new Mesh();
            _meshFilter.mesh.vertices = _vertices;
        }

        //Unfinished method, here to fill out if we wanna create different materials for the meshes
        private Material setMaterial()
        {
            Material material = new Material(Shader.Find("Standard"));
            return material;
        }
    }
}
