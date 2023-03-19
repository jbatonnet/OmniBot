using System.Diagnostics;

namespace OmniBot.Common.Audio;

public class BufferedAudioSource : IAudioSource
{
    private class BufferCountOverride : IDisposable
    {
        private readonly BufferedAudioSource _bufferedAudioSource;

        private int originalBufferCount;
        private int overridenBufferCount;

        public BufferCountOverride(BufferedAudioSource bufferedAudioSource, int bufferCount)
        {
            _bufferedAudioSource = bufferedAudioSource;

            originalBufferCount = bufferedAudioSource.bufferCount;
            overridenBufferCount = bufferCount;

            _bufferedAudioSource.bufferCount = overridenBufferCount;
        }

        public void Dispose()
        {
            _bufferedAudioSource.bufferCount = originalBufferCount;
        }
    }

    public event AudioBufferReceivedEventHandler OnAudioBufferReceived;

    public bool Listening
    {
        get => _audioSource.Listening;
        set => _audioSource.Listening = value;
    }
    public AudioFormat Format => _audioSource.Format;

    private readonly IAudioSource _audioSource;
    private readonly TimeSpan _bufferSize;

    private int bufferCount;
    private Queue<AudioBuffer> audioBuffers = new Queue<AudioBuffer>();
    private AudioBuffer currentAudioBuffer;
    private int bufferIndex;

    public BufferedAudioSource(IAudioSource audioSource) : this(audioSource, 20, TimeSpan.FromMilliseconds(100)) { }
    public BufferedAudioSource(IAudioSource audioSource, int bufferCount) : this(audioSource, bufferCount, TimeSpan.FromMilliseconds(100)) { }
    public BufferedAudioSource(IAudioSource audioSource, int bufferCount, TimeSpan bufferSize)
    {
        _audioSource = audioSource;
        this.bufferCount = bufferCount;
        _bufferSize = bufferSize;

        _audioSource.OnAudioBufferReceived += AudioSource_OnAudioBufferReceived;
    }

    public AudioBuffer GetBuffer(TimeSpan fromTimecode)
    {
        TimeSpan currentTimecode = currentAudioBuffer.Timecode + currentAudioBuffer.Format.GetDuration(bufferIndex);
        return GetBuffer(fromTimecode, currentTimecode);
    }
    public AudioBuffer GetBuffer(TimeSpan fromTimecode, TimeSpan toTimecode)
    {
        TimeSpan currentTimecode = currentAudioBuffer.Timecode + currentAudioBuffer.Format.GetDuration(bufferIndex);

        // Sanity check
        if (fromTimecode > toTimecode)
            return null;

        // No data outside of what is being buffered
        if (toTimecode > currentTimecode)
            toTimecode = currentTimecode;
        if (fromTimecode < currentAudioBuffer.Timecode && (audioBuffers.Count == 0 || fromTimecode < audioBuffers.First().Timecode))
            fromTimecode = audioBuffers.First().Timecode;

        // If we have no data
        if (fromTimecode == toTimecode)
            return null;

        int sampleRate = _audioSource.Format.SampleRate;
        int sampleSize = _audioSource.Format.GetSampleSize();
        int bufferLength = (int)(_bufferSize.TotalSeconds * sampleRate) * sampleSize;

        int totalLength = (int)((toTimecode - fromTimecode).TotalSeconds * sampleRate) * sampleSize;

        AudioBuffer audioBuffer = new AudioBuffer()
        {
            Format = _audioSource.Format,
            Timecode = fromTimecode,
            Data = new byte[totalLength]
        };

        var matchingBuffers = audioBuffers.Append(currentAudioBuffer)
            .SkipWhile(b => b.Timecode + _bufferSize <= fromTimecode)
            .TakeWhile(b => b.Timecode + _bufferSize <= toTimecode);

        if (Debugger.IsAttached)
            matchingBuffers = matchingBuffers.ToArray();

        int copyIndex = 0;

        foreach (AudioBuffer matchingBuffer in matchingBuffers)
        {
            int index = Math.Max(0, (int)((fromTimecode - matchingBuffer.Timecode).TotalSeconds * sampleRate) * sampleSize);
            int length = Math.Min(bufferLength - index, (int)((toTimecode - matchingBuffer.Timecode).TotalSeconds * sampleRate) * sampleSize);

            Array.Copy(matchingBuffer.Data, index, audioBuffer.Data, copyIndex, Math.Min(Math.Min(length, audioBuffer.Data.Length - copyIndex), matchingBuffer.Data.Length - index));

            copyIndex += length;
        }

        return audioBuffer;
    }

    public IDisposable OverrideBufferCount(int bufferCount = 1000)
    {
        return new BufferCountOverride(this, bufferCount);
    }

    private void AudioSource_OnAudioBufferReceived(AudioBuffer audioBuffer)
    {
        if (currentAudioBuffer == null)
        {
            currentAudioBuffer = new AudioBuffer()
            {
                Format = _audioSource.Format,
                Data = new byte[_audioSource.Format.GetSampleSize() * _audioSource.Format.SampleRate * (int)_bufferSize.TotalMilliseconds / 1000],
                Timecode = audioBuffer.Timecode
            };
        }

        int index = 0;

        while (true)
        {
            int count = Math.Min(currentAudioBuffer.Data.Length - bufferIndex, audioBuffer.Data.Length - index);
            Array.Copy(audioBuffer.Data, index, currentAudioBuffer.Data, bufferIndex, count);

            index += count;
            bufferIndex += count;

            if (bufferIndex == currentAudioBuffer.Data.Length)
            {
                audioBuffers.Enqueue(currentAudioBuffer);
                while (audioBuffers.Count > bufferCount)
                    audioBuffers.Dequeue();

                var lastBuffer = currentAudioBuffer;

                currentAudioBuffer = new AudioBuffer()
                {
                    Format = _audioSource.Format,
                    Data = new byte[_audioSource.Format.GetSampleSize() * _audioSource.Format.SampleRate * (int)_bufferSize.TotalMilliseconds / 1000],
                    Timecode = audioBuffer.Timecode + audioBuffer.Format.GetDuration(index)
                };

                bufferIndex = 0;

                OnAudioBufferReceived?.Invoke(lastBuffer);
            }

            if (index == audioBuffer.Data.Length)
                break;
        }
    }
}
