#if !UNITY_WEBGL || UNITY_EDITOR
using UnityEngine;

/// <summary>Editor and standalone capture using Unity's built-in Microphone class.</summary>
public class UnityMicrophoneCapture : IMicrophoneCapture
{
    private const int MaxRecordingSeconds = 30;

    private AudioClip _recording;
    private bool _requested;

    // Polled rather than latched from AsyncOperation.completed: the editor resolves the
    // request synchronously, and a callback attached to an already-finished operation
    // would never run, pinning the state at Pending.
    public MicrophonePermission Permission
    {
        get
        {
            if (Application.HasUserAuthorization(UserAuthorization.Microphone))
                return MicrophonePermission.Granted;

            return _requested ? MicrophonePermission.Pending : MicrophonePermission.Idle;
        }
    }

    public void RequestPermission()
    {
        if (_requested) return;

        _requested = true;
        Application.RequestUserAuthorization(UserAuthorization.Microphone);
    }

    public void StartRecording()
    {
        if (Permission != MicrophonePermission.Granted) return;
        _recording = Microphone.Start(null, false, MaxRecordingSeconds, AudioSettings.outputSampleRate);
    }

    public AudioClip StopRecording()
    {
        if (_recording == null) return null;

        // Read the position before End() — it resets to 0 afterwards.
        int recordedSamples = Microphone.GetPosition(null);
        Microphone.End(null);

        AudioClip source = _recording;
        _recording = null;

        if (recordedSamples <= 0) return null;

        int channels = source.channels;
        float[] data = new float[recordedSamples * channels];
        source.GetData(data, 0);

        AudioClip trimmed = AudioClip.Create("mic", recordedSamples, channels, source.frequency, false);
        trimmed.SetData(data, 0);
        return trimmed;
    }
}
#endif
