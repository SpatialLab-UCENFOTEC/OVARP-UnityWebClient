using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LipSync : MonoBehaviour {
    
    [System.Serializable]
    public class LipSyncKeyframe
    {
        public float time; // Time in seconds
        public float blendShapeValue; // Blendshape value at this time
    }


    public AudioSource audioSource;
    public SkinnedMeshRenderer bodyMesh;
    public string blendshapeName;
    public float openValue = 100f;
    public float closedValue = 0f;
    public List<LipSyncKeyframe> lipSyncKeyframes;


    private int blendshapeIndex;
    private float maxVolume = 0f;


    // private float[] samples = new float[2048];

	// Use this for initialization
	void Start () {
        blendshapeIndex = bodyMesh.sharedMesh.GetBlendShapeIndex(blendshapeName);
        bodyMesh.SetBlendShapeWeight(blendshapeIndex, 0);
        //blendshapeIndex = 39;
        // AnalyzeAudioClip(audioSource.clip);
    }
	
	// Update is called once per frame
	void Update () {
        if (audioSource.isPlaying)
        {
            for(int i = 0; i<lipSyncKeyframes.Count - 1;i++)
            {
                if (audioSource.time >= lipSyncKeyframes[i].time && audioSource.time <= lipSyncKeyframes[i + 1].time)
                {
                    float temp = lipSyncKeyframes[i].blendShapeValue;
                    bodyMesh.SetBlendShapeWeight(blendshapeIndex, temp);
                    break; // Assuming keyframes are sorted by time
                }
            }
        }
	}

    public IEnumerator AnalyzeAudioClip(AudioClip clip)
    {
        while (clip.loadState != AudioDataLoadState.Loaded)
        {
            yield return null;
        }

        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        int sampleRate = clip.frequency;
        int stepSize = sampleRate / 20;  // Analyze 10 times per second
        lipSyncKeyframes = new List<LipSyncKeyframe>();

        for (int i = 0; i < samples.Length; i += stepSize)
        {
            float sumOfSquares = 0f;
            for (int j = 0; j < stepSize && i + j < samples.Length; j++)
            {
                sumOfSquares += samples[i + j] * samples[i + j];
            }
            float rms = Mathf.Sqrt(sumOfSquares / stepSize);
            float volume = rms * 100;  // Scale volume as needed
            maxVolume = Mathf.Max(maxVolume, volume);

            float blendShapeValue = Mathf.Lerp(closedValue, openValue, volume / maxVolume);
            float time = (float)i / sampleRate;
            // Debug.Log(time);
            // Debug.Log("Value: " + blendShapeValue);
            lipSyncKeyframes.Add(new LipSyncKeyframe { time = time, blendShapeValue = blendShapeValue*2 });
        }
        audioSource.Play();
    }
}
