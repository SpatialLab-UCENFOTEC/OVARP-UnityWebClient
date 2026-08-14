using UnityEngine;

public enum MicrophonePermission
{
    Idle = 0,
    Pending = 1,
    Granted = 2,
    Denied = 3
}

/// <summary>
/// Microphone capture abstraction. Unity Web has no <c>Microphone</c> class, so the
/// browser implementation goes through a JS plugin while the editor keeps using Unity's.
/// </summary>
public interface IMicrophoneCapture
{
    MicrophonePermission Permission { get; }

    /// <summary>
    /// Must be called from a user gesture handler — browsers reject getUserMedia otherwise.
    /// Resolution is asynchronous; poll <see cref="Permission"/>.
    /// </summary>
    void RequestPermission();

    void StartRecording();

    /// <summary>Stops capture and returns the recorded clip, or null if nothing was captured.</summary>
    AudioClip StopRecording();
}
