using System;
using System.IO;
using UnityEngine;

public static class WavUtility
{
    public static byte[] FromAudioClip(AudioClip clip)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            WriteWavHeader(clip, stream);

            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            byte[] byteArray = ConvertSamplesToByteArray(samples);
            stream.Write(byteArray, 0, byteArray.Length);

            return stream.ToArray();
        }
    }

    public static AudioClip ToAudioClip(byte[] wavData)
    {
        if (wavData.Length < 44)
        {
            Debug.LogError("Invalid WAV file: too short to contain a valid header.");
            return null;
        }

        // Extract WAV header info
        int channels = BitConverter.ToInt16(wavData, 22);
        int sampleRate = BitConverter.ToInt32(wavData, 24);
        int bitsPerSample = BitConverter.ToInt16(wavData, 34);
        int bytePerSample = bitsPerSample / 8;

        Debug.Log($"WAV Info - Channels: {channels}, Sample Rate: {sampleRate}, Bits Per Sample: {bitsPerSample}");

        // Find the start of the data chunk
        int dataStartIndex = 44; // Default
        string chunkID = System.Text.Encoding.ASCII.GetString(wavData, 36, 4);
        if (chunkID != "data")
        {
            Debug.LogWarning("Unexpected chunk ID, searching for 'data' chunk...");
            for (int i = 36; i < wavData.Length - 4; i++)
            {
                if (System.Text.Encoding.ASCII.GetString(wavData, i, 4) == "data")
                {
                    dataStartIndex = i + 4;
                    break;
                }
            }
        }

        if (dataStartIndex >= wavData.Length)
        {
            Debug.LogError("Could not find the 'data' chunk in the WAV file.");
            return null;
        }

        // Extract audio data
        int dataLength = wavData.Length - dataStartIndex;
        int sampleCount = dataLength / bytePerSample;
        float[] audioData = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(wavData, dataStartIndex + i * bytePerSample);
            audioData[i] = Mathf.Clamp(sample / 32768.0f, -1.0f, 1.0f);
        }

        // Create an AudioClip
        AudioClip audioClip = AudioClip.Create("AudioClipFromWav", sampleCount / channels, channels, sampleRate, false);
        audioClip.SetData(audioData, 0);

        return audioClip;
    }

    private static void WriteWavHeader(AudioClip clip, MemoryStream stream)
    {
        int sampleRate = clip.frequency;
        int channels = clip.channels;
        int sampleCount = clip.samples * channels;
        int byteRate = sampleRate * channels * 2; // 16-bit PCM

        byte[] header = new byte[44];

        // RIFF header
        System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(header, 0);
        BitConverter.GetBytes(36 + sampleCount * 2).CopyTo(header, 4); // ChunkSize
        System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(header, 8);

        // fmt sub-chunk
        System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(header, 12);
        BitConverter.GetBytes(16).CopyTo(header, 16); // SubChunk1Size
        BitConverter.GetBytes((short)1).CopyTo(header, 20); // AudioFormat (PCM)
        BitConverter.GetBytes((short)channels).CopyTo(header, 22);
        BitConverter.GetBytes(sampleRate).CopyTo(header, 24);
        BitConverter.GetBytes(byteRate).CopyTo(header, 28);
        BitConverter.GetBytes((short)(channels * 2)).CopyTo(header, 32); // BlockAlign
        BitConverter.GetBytes((short)16).CopyTo(header, 34); // BitsPerSample

        // data sub-chunk
        System.Text.Encoding.ASCII.GetBytes("data").CopyTo(header, 36);
        BitConverter.GetBytes(sampleCount * 2).CopyTo(header, 40); // DataSize

        stream.Write(header, 0, header.Length);
    }

    private static byte[] ConvertSamplesToByteArray(float[] samples)
    {
        byte[] byteArray = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short sample = (short)(Mathf.Clamp(samples[i], -1.0f, 1.0f) * 32767);
            byteArray[i * 2] = (byte)(sample & 0xff);
            byteArray[i * 2 + 1] = (byte)((sample >> 8) & 0xff);
        }
        return byteArray;
    }
}
