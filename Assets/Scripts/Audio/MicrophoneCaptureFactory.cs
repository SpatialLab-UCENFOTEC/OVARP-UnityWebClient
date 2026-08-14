public static class MicrophoneCaptureFactory
{
    public static IMicrophoneCapture Create()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return new WebMicrophoneCapture();
#else
        return new UnityMicrophoneCapture();
#endif
    }
}
