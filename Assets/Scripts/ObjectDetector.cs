using UnityEngine;

public class ObjectDetector : MonoBehaviour
{
    public Transform[] ObjectsToDetect;
    public Keypoint[] Hands;

    // Update is called once per frame
    void Update()
    {
        foreach (var Object in ObjectsToDetect)
        {
            float distanceOne = Vector3.Distance(Object.position, Hands[0].Position);
            float distanceTwo = Vector3.Distance(Object.position, Hands[1].Position);
            Debug.Log(Hands[0].name + " is currently " + distanceOne.ToString() + " units from " + Object.name);
            Debug.Log(Hands[1].name + " is currently " + distanceTwo.ToString() + " units from " + Object.name);

        }
    }
}
