#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>Browser microphone capture backed by Assets/Plugins/WebGL/OvarpMic.jslib.</summary>
public class WebMicrophoneCapture : IMicrophoneCapture
{
    private const int MaxRecordingSeconds = 30;

    [DllImport("__Internal")] private static extern void OvarpMic_RequestPermission();
    [DllImport("__Internal")] private static extern int OvarpMic_GetPermissionState();
    [DllImport("__Internal")] private static extern void OvarpMic_Start();
    [DllImport("__Internal")] private static extern void OvarpMic_Stop();
    [DllImport("__Internal")] private static extern int OvarpMic_GetSampleCount();
    [DllImport("__Internal")] private static extern int OvarpMic_GetSampleRate();
    [DllImport("__Internal")] private static extern int OvarpMic_ReadSamples(float[] buffer, int maxSamples);

    public MicrophonePermission Permission => (MicrophonePermission)OvarpMic_GetPermissionState();

    public void RequestPermission() => OvarpMic_RequestPermission();

    public void StartRecording() => OvarpMic_Start();

    public AudioClip StopRecording()
    {
        OvarpMic_Stop();

        int sampleRate = OvarpMic_GetSampleRate();
        int available = OvarpMic_GetSampleCount();
        if (sampleRate <= 0 || available <= 0) return null;

        int capacity = Mathf.Min(available, sampleRate * MaxRecordingSeconds);
        float[] samples = new float[capacity];
        int written = OvarpMic_ReadSamples(samples, capacity);
        if (written <= 0) return null;

        if (written < capacity)
            System.Array.Resize(ref samples, written);

        AudioClip clip = AudioClip.Create("mic", written, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
#endif
