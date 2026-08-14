//	Copyright (c) 2012 Calvin Rien
//        http://the.darktable.com
//
//	This software is provided 'as-is', without any express or implied warranty. In
//	no event will the authors be held liable for any damages arising from the use
//	of this software.
//
//	Permission is granted to anyone to use this software for any purpose,
//	including commercial applications, and to alter it and redistribute it freely,
//	subject to the following restrictions:
//
//	1. The origin of this software must not be misrepresented; you must not claim
//	that you wrote the original software. If you use this software in a product,
//	an acknowledgment in the product documentation would be appreciated but is not
//	required.
//
//	2. Altered source versions must be plainly marked as such, and must not be
//	misrepresented as being the original software.
//
//	3. This notice may not be removed or altered from any source distribution.
//
//  =============================================================================
//
//  derived from Gregorio Zanon's script
//  http://forum.unity3d.com/threads/119295-Writing-AudioListener.GetOutputData-to-wav-problem?p=806734&viewfull=1#post806734
//
//  ALTERED SOURCE VERSION: rewritten as an in-memory encoder for the OVARP web
//  client. File-system output and the unused TrimSilence helpers were removed;
//  Unity Web has no usable persistent file system and the audio goes straight to
//  the WebSocket.

using System;
using UnityEngine;

public static class SavWav
{
    private const int HeaderSize = 44;
    private const int BitsPerSample = 16;
    private const float RescaleFactor = 32767f;

    public static byte[] ToWavBytes(AudioClip clip)
    {
        if (clip == null) throw new ArgumentNullException(nameof(clip));

        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);
        return ToWavBytes(samples, clip.channels, clip.frequency);
    }

    /// <summary>Encodes interleaved PCM float samples as a 16-bit mono/stereo WAV file.</summary>
    public static byte[] ToWavBytes(float[] samples, int channels, int frequency)
    {
        if (samples == null) throw new ArgumentNullException(nameof(samples));
        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
        if (frequency <= 0) throw new ArgumentOutOfRangeException(nameof(frequency));

        int dataSize = samples.Length * 2;
        byte[] wav = new byte[HeaderSize + dataSize];

        WriteHeader(wav, dataSize, channels, frequency);

        for (int i = 0; i < samples.Length; i++)
        {
            short value = (short)(Mathf.Clamp(samples[i], -1f, 1f) * RescaleFactor);
            int offset = HeaderSize + i * 2;
            wav[offset] = (byte)(value & 0xFF);
            wav[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        return wav;
    }

    // Canonical 44-byte RIFF/WAVE PCM header. OvarpServerConnector.DecodeWavPcm
    // reads channels at offset 22 and sample rate at offset 24, matching this layout.
    private static void WriteHeader(byte[] wav, int dataSize, int channels, int frequency)
    {
        WriteAscii(wav, 0, "RIFF");
        WriteInt32(wav, 4, HeaderSize - 8 + dataSize);
        WriteAscii(wav, 8, "WAVE");
        WriteAscii(wav, 12, "fmt ");
        WriteInt32(wav, 16, 16);
        WriteInt16(wav, 20, 1);
        WriteInt16(wav, 22, (short)channels);
        WriteInt32(wav, 24, frequency);
        WriteInt32(wav, 28, frequency * channels * BitsPerSample / 8);
        WriteInt16(wav, 32, (short)(channels * BitsPerSample / 8));
        WriteInt16(wav, 34, BitsPerSample);
        WriteAscii(wav, 36, "data");
        WriteInt32(wav, 40, dataSize);
    }

    private static void WriteAscii(byte[] buffer, int offset, string text)
    {
        for (int i = 0; i < text.Length; i++)
            buffer[offset + i] = (byte)text[i];
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteInt16(byte[] buffer, int offset, short value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }
}
