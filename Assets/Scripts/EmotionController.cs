using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Drives facial emotions via blendshapes on the CC3 SkinnedMeshRenderer.
// If expressions look weak, enable "Log Blendshape Names" in the Inspector to
// see all available names in the Console, then fix mismatches in the arrays below.
public class EmotionController : MonoBehaviour
{
    [SerializeField] SkinnedMeshRenderer faceRenderer;
    [SerializeField] float transitionDuration = 0.5f;
    // Weight applied to all active emotion shapes (0–100). Push above 100 only if mesh supports it.
    [SerializeField, Range(0f, 100f)] float intensity = 100f;
    // Prints every blendshape name on the mesh to the Console at startup — use to fix name mismatches
    [SerializeField] bool logBlendshapeNames = false;

    // ── Blendshape name groups per emotion (Inspector-tunable) ────────────────

    [Header("Happy")]
    [SerializeField] string[] happyShapes = {
        "Morpher_CC_Base_Body.Mouth_Smile_L",
        "Morpher_CC_Base_Body.Mouth_Smile_R",
        "Morpher_CC_Base_Body.Cheek_Raise_L",
        "Morpher_CC_Base_Body.Cheek_Raise_R",
        "Morpher_CC_Base_Body.Eye_Squint_L",
        "Morpher_CC_Base_Body.Eye_Squint_R",
        "Morpher_CC_Base_Body.Brow_Raise_Inner_L",
        "Morpher_CC_Base_Body.Brow_Raise_Inner_R",
    };

    [Header("Sad")]
    [SerializeField] string[] sadShapes = {
        "Morpher_CC_Base_Body.Brow_Drop_L",
        "Morpher_CC_Base_Body.Brow_Drop_R",
        // Inner brows raised + outer dropped = classic sad brow shape
        "Morpher_CC_Base_Body.Brow_Raise_Inner_L",
        "Morpher_CC_Base_Body.Brow_Raise_Inner_R",
        "Morpher_CC_Base_Body.Mouth_Frown",
        "Morpher_CC_Base_Body.Mouth_Frown_L",
        "Morpher_CC_Base_Body.Mouth_Frown_R",
        "Morpher_CC_Base_Body.Eye_Squint_L",
        "Morpher_CC_Base_Body.Eye_Squint_R",
    };

    [Header("Angry")]
    [SerializeField] string[] angryShapes = {
        "Morpher_CC_Base_Body.Brow_Drop_L",
        "Morpher_CC_Base_Body.Brow_Drop_R",
        "Morpher_CC_Base_Body.Brow_Compress_L",
        "Morpher_CC_Base_Body.Brow_Compress_R",
        "Morpher_CC_Base_Body.Nose_Scrunch_L",
        "Morpher_CC_Base_Body.Nose_Scrunch_R",
    };

    [Header("Surprised")]
    [SerializeField] string[] surprisedShapes = {
        "Morpher_CC_Base_Body.Eye_Wide_L",
        "Morpher_CC_Base_Body.Eye_Wide_R",
        "Morpher_CC_Base_Body.Brow_Raise_Inner_L",
        "Morpher_CC_Base_Body.Brow_Raise_Inner_R",
        "Morpher_CC_Base_Body.Brow_Raise_Outer_L",
        "Morpher_CC_Base_Body.Brow_Raise_Outer_R",
        "Morpher_CC_Base_Body.Mouth_Lips_Open",
    };

    // All shapes across all emotions — used to ensure neutral clears everything
    string[] AllEmotionShapes => happyShapes
        .Concat(sadShapes)
        .Concat(angryShapes)
        .Concat(surprisedShapes)
        .Distinct()
        .ToArray();

    readonly Dictionary<string, float> _current = new();
    Coroutine _transition;

    void Start()
    {
        if (faceRenderer == null)
            faceRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetEmotion(string emotion)
    {
        string[] targetShapes = emotion switch
        {
            "happy"     => happyShapes,
            "sad"       => sadShapes,
            "angry"     => angryShapes,
            "surprised" => surprisedShapes,
            "neutral"   => System.Array.Empty<string>(),
            _           => null
        };

        if (targetShapes == null)
        {
            Debug.LogWarning($"[EmotionController] Unknown emotion: {emotion}");
            return;
        }

        if (_transition != null) StopCoroutine(_transition);
        _transition = StartCoroutine(TransitionTo(targetShapes));
    }

    // ── Transition ────────────────────────────────────────────────────────────

    IEnumerator TransitionTo(string[] incomingShapes)
    {
        var targets = new Dictionary<string, float>();
        foreach (var name in incomingShapes)
            targets[name] = intensity;

        // Neutral: explicitly zero every emotion shape, not just the active ones
        foreach (var name in AllEmotionShapes)
            if (!targets.ContainsKey(name)) targets[name] = 0f;

        var startWeights = new Dictionary<string, float>();
        foreach (var name in targets.Keys)
            startWeights[name] = GetCurrentWeight(name);

        for (float t = 0; t < transitionDuration; t += Time.deltaTime)
        {
            float alpha = t / transitionDuration;
            foreach (var (name, targetWeight) in targets)
            {
                float w = Mathf.Lerp(startWeights[name], targetWeight, alpha);
                SetWeight(name, w);
                _current[name] = w;
            }
            yield return null;
        }

        foreach (var (name, targetWeight) in targets)
        {
            SetWeight(name, targetWeight);
            if (targetWeight == 0f) _current.Remove(name);
            else _current[name] = targetWeight;
        }
        _transition = null;
    }

    float GetCurrentWeight(string name)
    {
        if (_current.TryGetValue(name, out float w)) return w;
        int idx = faceRenderer.sharedMesh.GetBlendShapeIndex(name);
        return idx >= 0 ? faceRenderer.GetBlendShapeWeight(idx) : 0f;
    }

    void SetWeight(string name, float weight)
    {
        int idx = faceRenderer.sharedMesh.GetBlendShapeIndex(name);
        if (idx >= 0) faceRenderer.SetBlendShapeWeight(idx, weight);
    }
}
