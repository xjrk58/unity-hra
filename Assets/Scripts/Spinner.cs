using UnityEngine;

/// <summary>Spins an object about the world's vertical through its own pivot (the goal's extractor fan).</summary>
public class Spinner : MonoBehaviour
{
    public float degreesPerSecond = 300f;

    void Update() => transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.World);
}
