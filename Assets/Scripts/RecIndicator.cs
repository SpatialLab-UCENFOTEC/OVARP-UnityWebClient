using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RecIndicator : MonoBehaviour
{

    private bool blink = false;
    private float elapasedTime = 0.0f;
    private Renderer myRenderer;
    // Start is called before the first frame update
    void Start()
    {
        myRenderer = gameObject.GetComponent<Renderer>();
        myRenderer.material.SetColor("_BaseColor", Color.gray);
    }

    // Update is called once per frame
    void Update()
    {
        if(blink)
        {
            if(elapasedTime < 0.75f)
            {
                myRenderer.material.SetColor("_BaseColor", Color.red);
            }
            else if(elapasedTime > 0.75f)
            {
                myRenderer.material.SetColor("_BaseColor", Color.gray);
            }
            if(elapasedTime > 1.5f)
            {
                elapasedTime = 0.0f;
                myRenderer.material.SetColor("_BaseColor", Color.red);
            }

            elapasedTime += Time.deltaTime;
        }
        
    }

    public void StartBlinking()
    {
        blink = true;
    }

    public void StopBlinking()
    {
        blink = false;
        myRenderer.material.SetColor("_BaseColor", Color.gray);
        elapasedTime = 0.0f;
    }
}
