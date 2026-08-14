using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlinkEyes : MonoBehaviour
{
    public SkinnedMeshRenderer agentRenderer;

    [Space(10)]

    public string leftEyeBlendshape = "Morpher_CC_Base_Body.Eye_Blink_L";
    public string rightEyeBlendshape = "Morpher_CC_Base_Body.Eye_Blink_R";
    
    private int leftBlendshapeIndex;
    private int rightBlendshapeIndex;

    public float attackTime = 0.05f;
    public float releaseTime = 0.15f;
    public Vector2 blendshapeRange = new Vector2(0, 100);
    public Vector2 blinkDelay = new Vector2(4, 5);

    [Space(10)]

    public float curBlinkIntensity;
    public float curBlinkBlendshapeVal;
    public float curBlinkTime;
    public float nextBlinkTime;

    // Start is called before the first frame update
    void Start()
    {
        if (agentRenderer == null) agentRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

        leftBlendshapeIndex = agentRenderer.sharedMesh.GetBlendShapeIndex(leftEyeBlendshape);
        rightBlendshapeIndex = agentRenderer.sharedMesh.GetBlendShapeIndex(rightEyeBlendshape);
    }

    // Update is called once per frame
    void Update()
    {
        // Schedule next blink
        if (curBlinkTime > nextBlinkTime)
        {
            curBlinkTime = 0;
            nextBlinkTime = Random.Range(blinkDelay.x, blinkDelay.y);
        }
        else
        {
            curBlinkTime += Time.deltaTime;
        }

        // Set blink blendshapes
        float blinkElapsed = curBlinkTime;
        if (blinkElapsed > attackTime)
        {
            blinkElapsed = Mathf.Min(curBlinkTime - attackTime, releaseTime);
            curBlinkIntensity = 1 - blinkElapsed / releaseTime;
        }
        else
        {
            curBlinkIntensity = blinkElapsed / attackTime;
        }

        curBlinkBlendshapeVal = Mathf.Lerp(blendshapeRange.x, blendshapeRange.y, curBlinkIntensity);

        agentRenderer.SetBlendShapeWeight(leftBlendshapeIndex, curBlinkBlendshapeVal);
        agentRenderer.SetBlendShapeWeight(rightBlendshapeIndex, curBlinkBlendshapeVal);
    }
}
