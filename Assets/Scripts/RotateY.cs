using UnityEngine;

public class RotateY : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // rotate y axis
        transform.Rotate(0, 200 * Time.deltaTime, 0);
    }
}
