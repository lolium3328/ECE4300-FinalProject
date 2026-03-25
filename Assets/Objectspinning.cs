using UnityEngine;

public class Objectspinning : MonoBehaviour
{
    public Vector3 rotationAxis = Vector3.forward;
    public float rotationSpeed = 90f;
    public Space rotationSpace = Space.Self;

    void Update()
    {
        transform.Rotate(rotationAxis.normalized, rotationSpeed * Time.deltaTime, rotationSpace);
    }
}
