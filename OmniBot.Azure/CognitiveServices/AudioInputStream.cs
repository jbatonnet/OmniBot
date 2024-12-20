﻿using System.Diagnostics;

using Microsoft.CognitiveServices.Speech.Audio;

using OmniBot.Common.Audio;

namespace OmniBot.Azure.CognitiveServices;

public class DiscreteAudioInputStream : PullAudioInputStreamCallback
{
    private readonly AudioBuffer _audioBuffer;

    private int position = 0;

    public DiscreteAudioInputStream(AudioBuffer audioBuffer)
    {
        _audioBuffer = audioBuffer;
    }

    public override int Read(byte[] dataBuffer, uint size)
    {
        int read = Math.Min(_audioBuffer.Data.Length - position, (int)size);

        Buffer.BlockCopy(_audioBuffer.Data, position, dataBuffer, 0, read);
        position += read;

        return read;
    }
}

public class ContinuousAudioInputStream : PullAudioInputStreamCallback
{
    private readonly IAudioSource _audioSource;

    private Queue<AudioBuffer> bufferQueue = new();

    private AudioBuffer currentBuffer = null;
    private int currentPosition = 0;

    public ContinuousAudioInputStream(IAudioSource audioSource)
    {
        _audioSource = audioSource;
        _audioSource.OnAudioBufferReceived += b => bufferQueue.Enqueue(b);
    }

    public override int Read(byte[] dataBuffer, uint size)
    {
        if (currentBuffer == null)
        {
            long timeout = Environment.TickCount + size / 32;

            SpinWait spinWait = new SpinWait();
            while (bufferQueue.Count == 0)
            {
                if (Environment.TickCount > timeout)
                {
                    Array.Clear(dataBuffer, 0, (int)size);

                    if (Debugger.IsAttached)
                        Console.WriteLine($"ContinuousAudioInputStream.Read() => Fake");

                    return (int)size;
                }

                spinWait.SpinOnce();
            }

            currentBuffer = bufferQueue.Dequeue();
            currentPosition = 0;
        }

        int read = Math.Min(currentBuffer.Data.Length - currentPosition, (int)size);

        Buffer.BlockCopy(currentBuffer.Data, currentPosition, dataBuffer, 0, read);
        currentPosition += read;

        if (currentPosition == currentBuffer.Data.Length)
            currentBuffer = null;

        if (Debugger.IsAttached)
            Console.WriteLine($"ContinuousAudioInputStream.Read() => {read}");

        return read;
    }
    public void Reset()
    {
        currentBuffer = null;
        bufferQueue.Clear();
        currentPosition = 0;
    }
}
