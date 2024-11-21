using OmniBot.Common.Audio;
using System.Diagnostics;

public class ContinuousAudioStream : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;

    public override long Length => bufferQueue.Sum(b => b.Data.Length);
    public override long Position
    {
        get => currentPosition;
        set => throw new NotSupportedException();
    }

    private readonly IAudioSource _audioSource;

    private Queue<AudioBuffer> bufferQueue = new();

    private AudioBuffer currentBuffer = null;
    private int currentPosition = 0;

    public ContinuousAudioStream(IAudioSource audioSource)
    {
        _audioSource = audioSource;
        _audioSource.OnAudioBufferReceived += AudioSource_OnAudioBufferReceived;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (currentBuffer == null)
        {
            long timeout = Environment.TickCount + count / 32;

            SpinWait spinWait = new SpinWait();
            while (bufferQueue.Count == 0)
            {
                if (Environment.TickCount > timeout)
                {
                    Array.Clear(buffer, offset, count);
                    return count;
                }

                spinWait.SpinOnce();
            }

            currentBuffer = bufferQueue.Dequeue();
            currentPosition = 0;
        }

        int read = Math.Min(currentBuffer.Data.Length - currentPosition, count);

        Buffer.BlockCopy(currentBuffer.Data, currentPosition, buffer, offset, read);
        currentPosition += read;

        if (currentPosition == currentBuffer.Data.Length)
            currentBuffer = null;

        //if (Debugger.IsAttached)
            //Console.WriteLine($"ContinuousAudioInputStream.Read() => {read}");

        return read;
    }
    public void Reset()
    {
        currentBuffer = null;
        bufferQueue.Clear();
        currentPosition = 0;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        _audioSource.OnAudioBufferReceived -= AudioSource_OnAudioBufferReceived;
    }

    private void AudioSource_OnAudioBufferReceived(AudioBuffer audioBuffer)
    {
        bufferQueue.Enqueue(audioBuffer);
    }
}