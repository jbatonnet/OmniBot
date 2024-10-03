using System.Collections.Concurrent;

using OmniBot.Common.Audio;

namespace OmniBot.Common;

public class DiscreteRecordingSource : IAudioSource
{
    public event AudioBufferReceivedEventHandler OnAudioBufferReceived;

    public AudioFormat Format { get; init; }
    public bool Listening
    {
        get => listening;
        set
        {
            listening = value;

            if (listening)
                sendAudioTimer.Change(0, sendAudioPeriodMilliseconds);
            else
                sendAudioTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    private readonly int sendAudioPeriodMilliseconds;
    private readonly int sendAudioSamplesCount;
    private readonly int sendAudioBufferSize;

    private bool listening = true;
    private byte[] encodedSilence;
    private TimeSpan timecode = TimeSpan.Zero;

    private Timer sendAudioTimer;
    private ConcurrentQueue<byte[]> sendAudioQueue = new();
    //private Mutex sendAudioMutex = new();
    private Semaphore sendAudioSemaphore = new(1, 1);

    public DiscreteRecordingSource(AudioFormat audioFormat) : this(audioFormat, TimeSpan.FromMilliseconds(20)) { }
    public DiscreteRecordingSource(AudioFormat audioFormat, TimeSpan sendAudioPeriod)
    {
        Format = audioFormat;

        sendAudioPeriodMilliseconds = (int)sendAudioPeriod.TotalMilliseconds;
        sendAudioSamplesCount = Format.SampleRate * sendAudioPeriodMilliseconds / 1000;
        sendAudioBufferSize = Format.BitsPerSample / 8 * sendAudioSamplesCount;

        encodedSilence = new byte[sendAudioBufferSize];
        Array.Clear(encodedSilence);

        sendAudioTimer = new Timer(SendAudioTimer_OnTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public async Task PlayAsync(AudioBuffer audioBuffer, CancellationToken cancellationToken = default)
    {
        // Convert to selected audio format
        audioBuffer = audioBuffer.ConvertTo(Format);

        //sendAudioSemaphore.WaitOne();
        await Task.Run(sendAudioSemaphore.WaitOne);

        // Add to send queue
        sendAudioQueue.Clear();

        for (int i = 0; i < audioBuffer.Data.Length; i += sendAudioBufferSize)
        {
            int length = Math.Min(i + sendAudioBufferSize, audioBuffer.Data.Length) - i;

            byte[] sendBuffer = new byte[sendAudioBufferSize];
            Array.Clear(sendBuffer);
            Array.Copy(audioBuffer.Data, i, sendBuffer, 0, length);

            sendAudioQueue.Enqueue(sendBuffer);
        }

        // Wait for the duration of the sound, or clear the queue if needed
        // FIXME: Make sure data is indeed consumed
        try
        {
            while (sendAudioQueue.Count > 0)
                await Task.Delay(sendAudioPeriodMilliseconds);
        }
        catch
        {
            sendAudioQueue.Clear();
            throw;
        }
        finally
        {
            sendAudioSemaphore.Release();
        }
    }

    private void SendAudioTimer_OnTick(object? state)
    {
        // Dequeue buffer or generate silence
        if (!sendAudioQueue.TryDequeue(out byte[] buffer))
            buffer = encodedSilence;

        if (Listening)
        {
            AudioBuffer audioBuffer = new AudioBuffer()
            {
                Format = Format,
                Data = buffer,
                Timecode = timecode
            };

            timecode += audioBuffer.GetDuration();

            OnAudioBufferReceived?.Invoke(audioBuffer);
        }
    }
}

public static class AudioSinkExtensions
{
    public static async Task PlayAsync(this IAudioSink audioSink, AudioBuffer audioBuffer, CancellationToken cancellationToken = default)
    {
        DiscreteRecordingSource discreteRecordingSource = new DiscreteRecordingSource(audioBuffer.Format);
        discreteRecordingSource.Listening = true;

        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        _ = audioSink.PlayAsync(discreteRecordingSource, cancellationTokenSource.Token);

        await discreteRecordingSource.PlayAsync(audioBuffer, cancellationToken);
        //await audioSink.PlayAsync(null);

        //discreteRecordingSource.Listening = false;
        //cancellationTokenSource.Cancel();
    }
}
