using UnityEngine;

public class Keypoint : MonoBehaviour
{
    public LineRenderer outerCircle;
    public LineRenderer innerCircle;
    bool m_IsActive;
    Vector3 m_Position;

    public bool IsActive => m_IsActive;
    public Vector3 Position => m_Position;

    public Color outerColor;
    public Color innerColor;
    public float outerWidth;
    public float innerWidth;

    public void Update()
    {
        outerCircle.startColor = outerColor;
        outerCircle.endColor = outerColor;
        outerCircle.startWidth = outerWidth;
        outerCircle.endWidth = outerWidth;
        innerCircle.startColor = innerColor;
        innerCircle.endColor = innerColor;
        innerCircle.startWidth = innerWidth;
        innerCircle.endWidth = innerWidth;
    }

    public void Set(bool active, Vector3 position)
    {
        m_IsActive = active;
        m_Position = position;
        if (position == Vector3.zero)
        {
            Debug.Log(gameObject.name + ": position is zero");
        }
        gameObject.SetActive(active);
        outerCircle.SetPosition(0, position);
        outerCircle.SetPosition(1, position);
        innerCircle.SetPosition(0, position);
        innerCircle.SetPosition(1, position);
    }
}
